// BotThreatsHandler.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    internal static class BotThreatsHandler
    {
        public static string Build()
        {
            try
            {
                Map map = BotMapHelper.GetPlayerMap();
                if (map == null)
                    return BotMapHelper.ErrorNoMapJson();

                var hostiles = new List<BotHostileEntry>();
                int manhunterCount = 0;
                int fireCount = 0;

                // Prefer hostile-to-colony cache; fall back to faction hostility scan
                try
                {
                    HashSet<IAttackTarget> danger = map.attackTargetsCache?.TargetsHostileToColony;
                    if (danger != null)
                    {
                        foreach (IAttackTarget t in danger)
                        {
                            Thing thing = t?.Thing;
                            if (thing == null || thing.Destroyed) continue;
                            if (!(thing is Pawn p) || p.Dead) continue;
                            AddHostile(p, hostiles, ref manhunterCount);
                        }
                    }
                }
                catch
                {
                    // ignore cache failures
                }

                if (hostiles.Count == 0)
                {
                    try
                    {
                        foreach (var p in map.mapPawns?.AllPawnsSpawned ?? Enumerable.Empty<Pawn>())
                        {
                            if (p == null || p.Dead) continue;

                            bool manhunter = IsManhunter(p);
                            bool factionHostile = p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer);
                            if (!manhunter && !factionHostile) continue;

                            AddHostile(p, hostiles, ref manhunterCount);
                        }
                    }
                    catch { /* ignore */ }
                }

                try
                {
                    fireCount = map.listerThings?.ThingsOfDef(ThingDefOf.Fire)?.Count ?? 0;
                }
                catch { /* ignore */ }

                var payload = new BotThreatsPayload
                {
                    status = "ok",
                    command = "botthreats",
                    threatPoints = StorytellerUtility.DefaultThreatPointsNow(map),
                    hostileCount = hostiles.Count,
                    manhunterCount = manhunterCount,
                    fireCount = fireCount,
                    hostiles = hostiles
                };

                return BotJson.Serialize(payload);
            }
            catch (Exception ex)
            {
                return BotMapHelper.ErrorExceptionJson(ex.Message);
            }
        }

        private static void AddHostile(Pawn p, List<BotHostileEntry> hostiles, ref int manhunterCount)
        {
            bool manhunter = IsManhunter(p);
            if (manhunter)
                manhunterCount++;

            hostiles.Add(new BotHostileEntry
            {
                name = p.LabelShortCap,
                kind = p.kindDef?.defName,
                faction = p.Faction?.Name,
                manhunter = manhunter,
                downed = p.Downed,
                healthPct = p.health?.summaryHealth != null
                    ? Math.Round(p.health.summaryHealth.SummaryHealthPercent * 100.0, 1)
                    : 100.0
            });
        }

        private static bool IsManhunter(Pawn p)
        {
            if (p?.MentalStateDef == null) return false;
            string defName = p.MentalStateDef.defName ?? "";
            return p.MentalStateDef == MentalStateDefOf.Manhunter
                   || defName.IndexOf("Manhunter", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    internal class BotThreatsPayload
    {
        public string status;
        public string command;
        public float threatPoints;
        public int hostileCount;
        public int manhunterCount;
        public int fireCount;
        public List<BotHostileEntry> hostiles;
    }

    internal class BotHostileEntry
    {
        public string name;
        public string kind;
        public string faction;
        public bool manhunter;
        public bool downed;
        public double healthPct;
    }
}
