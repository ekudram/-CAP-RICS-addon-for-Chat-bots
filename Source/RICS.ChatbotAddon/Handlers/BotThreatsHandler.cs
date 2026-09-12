// BotThreatsHandler.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt
//
// Live threat check for the AI bot: hostile faction members (not prisoners/slaves),
// manhunter animals, and scaria animals. One pawn once — flags may stack.

using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    internal static class BotThreatsHandler
    {
        public static string Build()
        {
            try
            {
                Map map = BotMapHelper.GetCombatMap();
                if (map == null)
                    return BotMapHelper.ErrorNoMapJson();

                var threats = new List<BotHostileEntry>();
                var seen = new HashSet<int>();
                var kindCounts = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
                int hostileFactionCount = 0;
                int manhunterCount = 0;
                int scariaCount = 0;
                int fireCount = 0;
                int downedHostileCount = 0;

                try
                {
                    var spawned = map.mapPawns?.AllPawnsSpawned;
                    if (spawned != null)
                    {
                        foreach (var p in spawned)
                        {
                            if (p == null || p.Dead || p.Destroyed) continue;
                            if (!seen.Add(p.thingIDNumber)) continue;

                            // Captured / enslaved by the colony are not an active field threat
                            if (p.IsPrisonerOfColony || p.IsSlaveOfColony) continue;
                            if (p.Faction != null && p.Faction.IsPlayer) continue;

                            bool hostileFaction = IsHostileFactionMember(p);
                            bool manhunter = IsManhunter(p);
                            bool scaria = HasScaria(p);

                            if (!hostileFaction && !manhunter && !scaria)
                                continue;

                            if (hostileFaction) hostileFactionCount++;
                            if (manhunter) manhunterCount++;
                            if (scaria) scariaCount++;
                            if (p.Downed) downedHostileCount++;

                            string kindLabel = p.kindDef?.label ?? p.kindDef?.defName ?? p.def?.label ?? "hostile";
                            if (!kindCounts.ContainsKey(kindLabel))
                                kindCounts[kindLabel] = 0;
                            kindCounts[kindLabel]++;

                            threats.Add(BuildEntry(p, hostileFaction, manhunter, scaria));
                        }
                    }
                }
                catch { /* ignore scan errors */ }

                try
                {
                    fireCount = map.listerThings?.ThingsOfDef(ThingDefOf.Fire)?.Count ?? 0;
                }
                catch { /* ignore */ }

                const int hostilesCap = 12;
                List<BotHostileEntry> hostilesOut = threats;
                if (threats.Count > 20)
                {
                    hostilesOut = new List<BotHostileEntry>(hostilesCap);
                    foreach (var e in threats)
                    {
                        if (e == null || e.downed) continue;
                        hostilesOut.Add(e);
                        if (hostilesOut.Count >= hostilesCap) break;
                    }
                }
                else if (threats.Count > hostilesCap)
                {
                    hostilesOut = threats.GetRange(0, hostilesCap);
                }

                var payload = new BotThreatsPayload
                {
                    status = "ok",
                    command = "botthreats",
                    threatActive = threats.Count > 0,
                    threatPoints = StorytellerUtility.DefaultThreatPointsNow(map),
                    hostileCount = threats.Count,
                    hostileFactionCount = hostileFactionCount,
                    manhunterCount = manhunterCount,
                    scariaCount = scariaCount,
                    fireCount = fireCount,
                    downedHostileCount = downedHostileCount,
                    mapName = map.Parent?.Label ?? map.ToString(),
                    isPlayerHome = map.IsPlayerHome,
                    playerIsAggressor = BotMapHelper.PlayerIsAggressorOn(map),
                    kindCounts = kindCounts,
                    hostiles = hostilesOut
                };

                return BotJson.Serialize(payload);
            }
            catch (Exception ex)
            {
                return BotMapHelper.ErrorExceptionJson(ex.Message);
            }
        }

        private static BotHostileEntry BuildEntry(Pawn p, bool hostileFaction, bool manhunter, bool scaria)
        {
            return new BotHostileEntry
            {
                name = p.LabelShortCap,
                kind = p.kindDef?.label ?? p.kindDef?.defName ?? p.def?.label,
                defName = p.def?.defName,
                faction = p.Faction?.Name,
                hostileFaction = hostileFaction,
                manhunter = manhunter,
                scaria = scaria,
                downed = p.Downed,
                animal = p.RaceProps?.Animal == true,
                mechanoid = p.RaceProps?.IsMechanoid == true,
                healthPct = p.health?.summaryHealth != null
                    ? Math.Round(p.health.summaryHealth.SummaryHealthPercent * 100.0, 1)
                    : 100.0
            };
        }

        private static bool IsHostileFactionMember(Pawn p)
        {
            try
            {
                if (p.Faction == null || p.Faction.IsPlayer)
                    return false;
                if (p.IsPrisoner || p.IsSlave)
                    return false;
                return p.Faction.HostileTo(Faction.OfPlayer) || p.HostileTo(Faction.OfPlayer);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Mental state and optional manhunter hediff. Scaria is a separate flag.</summary>
        private static bool IsManhunter(Pawn p)
        {
            try
            {
                if (p.InMentalState && p.MentalStateDef != null)
                {
                    if (p.MentalStateDef == MentalStateDefOf.Manhunter)
                        return true;
                    if (p.MentalStateDef == MentalStateDefOf.ManhunterPermanent)
                        return true;
                    string n = p.MentalStateDef.defName ?? "";
                    if (n.IndexOf("Manhunter", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }

                return HasHediffNamed(p, "Manhunter");
            }
            catch
            {
                return false;
            }
        }

        private static bool HasScaria(Pawn p)
        {
            try
            {
                var hs = p.health?.hediffSet;
                if (hs == null) return false;

                if (HediffDefOf.Scaria != null && hs.HasHediff(HediffDefOf.Scaria))
                    return true;
                if (HediffDefOf.ScariaInfection != null && hs.HasHediff(HediffDefOf.ScariaInfection))
                    return true;

                return HasHediffNamed(p, "Scaria");
            }
            catch
            {
                return false;
            }
        }

        private static bool HasHediffNamed(Pawn p, string token)
        {
            try
            {
                var list = p.health?.hediffSet?.hediffs;
                if (list == null) return false;
                foreach (var h in list)
                {
                    if (h?.def == null) continue;
                    string dn = h.def.defName ?? "";
                    string lb = h.def.label ?? "";
                    if (dn.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                    if (lb.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            catch { }
            return false;
        }
    }

    internal class BotThreatsPayload
    {
        public string status;
        public string command;
        public bool threatActive;
        public float threatPoints;
        public int hostileCount;
        public int hostileFactionCount;
        public int manhunterCount;
        public int scariaCount;
        public int fireCount;
        public int downedHostileCount;
        public string mapName;
        public bool isPlayerHome;
        public bool playerIsAggressor;
        public Dictionary<string, int> kindCounts;
        public List<BotHostileEntry> hostiles;
    }

    internal class BotHostileEntry
    {
        public string name;
        public string kind;
        public string defName;
        public string faction;
        public bool hostileFaction;
        public bool manhunter;
        public bool scaria;
        public bool downed;
        public bool animal;
        public bool mechanoid;
        public double healthPct;
    }
}
