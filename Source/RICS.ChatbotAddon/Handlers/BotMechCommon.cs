// BotMechCommon.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt
//
// Shared lookup for the mechanitor's own mechs. Used by the group and scrap commands.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CAP_ChatInteractive;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    internal sealed class BotMechGate
    {
        public Pawn pawn;
        public string status;
        public string reason;
        public bool ok;
    }

    internal sealed class BotMechNameReason
    {
        public string label;
        public string reason;
    }

    internal static class BotMechCommon
    {
        public static BotMechGate Open(ChatMessageWrapper user)
        {
            var gate = new BotMechGate();
            try
            {
                var mgr = CAPChatInteractiveMod.GetPawnAssignmentManager();
                gate.pawn = mgr?.GetAssignedPawn(user);
            }
            catch
            {
                gate.pawn = null;
            }

            if (gate.pawn == null || gate.pawn.Destroyed)
            {
                gate.status = "no_pawn";
                gate.reason = "No pawn is assigned to this bot.";
                return gate;
            }

            if (!IsMechanitor(gate.pawn))
            {
                gate.status = "not_mechanitor";
                gate.reason = BotCareCommon.Pronoun(gate.pawn) + " does not have a mechlink.";
                return gate;
            }

            gate.ok = true;
            return gate;
        }

        public static bool IsMechanitor(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive || pawn == null)
                return false;

            try
            {
                var mechLinkDef = DefDatabase<HediffDef>.GetNamedSilentFail("MechlinkImplant");
                bool hasMechLink = mechLinkDef != null && pawn.health?.hediffSet?.HasHediff(mechLinkDef) == true;
                return hasMechLink && pawn.mechanitor != null;
            }
            catch
            {
                return false;
            }
        }

        public static List<Pawn> ControlledMechs(Pawn pawn)
        {
            var seen = new HashSet<Pawn>();
            var mechs = new List<Pawn>();
            if (pawn == null)
                return mechs;

            var tracker = pawn.mechanitor;

            void AddMany(IEnumerable<Pawn> src)
            {
                if (src == null) return;
                foreach (var mech in src)
                {
                    if (mech == null || mech.Destroyed || mech.Dead) continue;
                    if (mech.RaceProps == null || !mech.RaceProps.IsMechanoid) continue;
                    if (seen.Add(mech))
                        mechs.Add(mech);
                }
            }

            try
            {
                var assignedOrder = new List<Pawn>();
                MechanitorUtility.GetMechsInAssignedOrder(pawn, ref assignedOrder);
                AddMany(assignedOrder);
            }
            catch { /* ignore */ }

            try { AddMany(tracker?.ControlledPawns); }
            catch { /* ignore */ }

            try { AddMany(tracker?.OverseenPawns); }
            catch { /* ignore */ }

            try
            {
                if (tracker?.controlGroups != null)
                {
                    foreach (var group in tracker.controlGroups)
                        AddMany(group?.AssignedMechs == null ? null : AssignedPawns(group));
                }
            }
            catch { /* ignore */ }

            try
            {
                if (Current.Game != null)
                {
                    foreach (var map in Find.Maps)
                    {
                        if (map?.mapPawns == null) continue;
                        IEnumerable<Pawn> spawned = map.mapPawns.AllPawnsSpawned ?? new List<Pawn>();
                        IEnumerable<Pawn> unspawned = map.mapPawns.AllPawnsUnspawned ?? new List<Pawn>();
                        foreach (var other in spawned)
                            ConsiderMapPawn(other, pawn, seen, mechs);
                        foreach (var other in unspawned)
                            ConsiderMapPawn(other, pawn, seen, mechs);
                    }
                }
            }
            catch { /* ignore */ }

            for (int i = mechs.Count - 1; i >= 0; i--)
            {
                Pawn mech = mechs[i];
                try
                {
                    if (mech == null || mech.GetOverseer() != pawn)
                        mechs.RemoveAt(i);
                }
                catch
                {
                    mechs.RemoveAt(i);
                }
            }

            return mechs;
        }

        public static List<Pawn> MatchKind(List<Pawn> mechs, string token)
        {
            var byDef = new List<Pawn>();
            var byLabel = new List<Pawn>();
            if (mechs == null || string.IsNullOrWhiteSpace(token))
                return byDef;

            foreach (var mech in mechs)
            {
                if (mech == null) continue;
                if (string.Equals(KindDefName(mech), token, StringComparison.OrdinalIgnoreCase))
                    byDef.Add(mech);
                else if (string.Equals(KindLabel(mech), token, StringComparison.OrdinalIgnoreCase))
                    byLabel.Add(mech);
            }

            return byDef.Count > 0 ? byDef : byLabel;
        }

        public static string KindDefName(Pawn mech)
        {
            if (!string.IsNullOrEmpty(mech?.kindDef?.defName))
                return mech.kindDef.defName;
            if (!string.IsNullOrEmpty(mech?.def?.defName))
                return mech.def.defName;
            return "unknown";
        }

        public static string KindLabel(Pawn mech)
        {
            string kind = mech?.kindDef?.label;
            string race = mech?.def?.label;
            string raw = !string.IsNullOrEmpty(kind) ? kind : race;
            if (string.IsNullOrEmpty(raw))
                raw = KindDefName(mech);
            raw = BotJson.WithoutColor(raw);
            return string.IsNullOrEmpty(raw) ? "mech" : raw;
        }

        public static string PawnLabel(Pawn pawn)
        {
            string raw = null;
            try { raw = pawn?.LabelShort; }
            catch { /* ignore */ }
            if (string.IsNullOrEmpty(raw))
                raw = KindLabel(pawn);
            raw = BotJson.WithoutColor(raw);
            return string.IsNullOrEmpty(raw) ? "mech" : raw;
        }

        public static string Who(Pawn pawn)
        {
            return BotCareCommon.Pronoun(pawn);
        }

        public static string WhoHas(Pawn pawn)
        {
            string who = Who(pawn);
            return who + (who == "They" ? " have" : " has");
        }

        public static string Plural(string label, int count)
        {
            string name = string.IsNullOrEmpty(label) ? "mech" : label;
            if (count == 1)
                return name;
            if (name.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                return name;
            return name + "s";
        }

        public static bool IsProtectedKind(Pawn sample)
        {
            if (sample == null)
                return true;
            if (IsNamedProtected(sample))
                return true;

            var raceDef = sample.kindDef?.race ?? sample.def;
            var works = raceDef?.race?.mechEnabledWorkTypes;
            bool work = works != null && works.Count > 0;
            return !work || Bandwidth(sample, raceDef) != 1;
        }

        public static int AvailableGroups(Pawn pawn)
        {
            try
            {
                if (pawn?.mechanitor == null)
                    return 0;
                return pawn.mechanitor.TotalAvailableControlGroups;
            }
            catch
            {
                return pawn?.mechanitor?.controlGroups?.Count ?? 0;
            }
        }

        // Grows the list up to the requested 1-based slot. Does not remove groups.
        public static void GrowGroups(Pawn_MechanitorTracker tracker, int groupNumber)
        {
            if (tracker?.controlGroups == null || groupNumber < 1)
                return;

            int before = tracker.controlGroups.Count;
            while (tracker.controlGroups.Count < groupNumber)
                tracker.controlGroups.Add(new MechanitorControlGroup(tracker));

            if (tracker.controlGroups.Count == before)
                return;

            try
            {
                var field = typeof(Pawn_MechanitorTracker).GetField("controlGroupGizmos", BindingFlags.Instance | BindingFlags.NonPublic);
                if (!(field?.GetValue(tracker) is IList list))
                    return;
                list.Clear();
                foreach (var group in tracker.controlGroups)
                {
                    if (group != null)
                        list.Add(new MechanitorControlGroupGizmo(group));
                }
            }
            catch { /* the groups still accept mechs if the gizmo list cannot be refreshed */ }
        }

        public static string GroupSkipReason(Pawn mech)
        {
            try
            {
                if (mech == null)
                    return "Could not be moved.";
                if (mech.Downed)
                    return "Downed.";
                if (mech.CarriedBy != null)
                    return "Being carried.";
                if (mech.IsCaravanMember())
                    return "In a caravan.";
                if (mech.IsGestating())
                    return "Gestating.";
            }
            catch
            {
                return "Could not be moved.";
            }

            return null;
        }

        public static string RemoveBlockReason(Pawn mech)
        {
            try
            {
                if (mech == null || mech.Dead || mech.Destroyed)
                    return "Already gone.";
                if (mech.CarriedBy != null)
                    return "Being carried.";
                if (mech.IsCaravanMember())
                    return "In a caravan.";
                if (mech.IsGestating())
                    return "Gestating.";
            }
            catch
            {
                return "Could not be disassembled.";
            }

            return null;
        }

        public static string AreaName(Pawn pawn)
        {
            try
            {
                var area = pawn?.playerSettings?.AreaRestrictionInPawnCurrentMap;
                if (area == null)
                    return "unrestricted";
                string label = BotJson.WithoutColor(area.Label);
                return string.IsNullOrEmpty(label) ? "unrestricted" : label;
            }
            catch
            {
                return "unrestricted";
            }
        }

        public static Area FindPlayerArea(Map map, string name)
        {
            if (map?.areaManager?.AllAreas == null || string.IsNullOrWhiteSpace(name))
                return null;

            foreach (var area in map.areaManager.AllAreas)
            {
                if (area == null || !area.AssignableAsAllowed())
                    continue;
                string label = BotJson.WithoutColor(area.Label);
                if (string.Equals(label, name, StringComparison.OrdinalIgnoreCase))
                    return area;
            }

            return null;
        }

        public static int TickAssigned(Pawn mechanitor, Pawn mech)
        {
            try
            {
                var groups = mechanitor?.mechanitor?.controlGroups;
                if (groups == null || mech == null)
                    return 0;
                foreach (var group in groups)
                {
                    if (group?.AssignedMechs == null)
                        continue;
                    foreach (var entry in group.AssignedMechs)
                    {
                        if (entry?.pawn == mech)
                            return entry.tickAssigned;
                    }
                }
            }
            catch { /* ignore */ }

            return 0;
        }

        public static float HealthKey(Pawn mech)
        {
            try
            {
                if (mech?.health?.summaryHealth == null)
                    return 101f;
                return mech.health.summaryHealth.SummaryHealthPercent;
            }
            catch
            {
                return 101f;
            }
        }

        public static float EnergyKey(Pawn mech)
        {
            try
            {
                if (mech?.needs?.energy != null)
                    return mech.needs.energy.CurLevelPercentage;
                var cell = mech?.TryGetComp<CompMechPowerCell>();
                if (cell != null)
                    return cell.PercentFull;
            }
            catch { /* ignore */ }

            return 101f;
        }

        public static int DownRank(Pawn mech)
        {
            try
            {
                return mech != null && mech.Downed ? 1 : 0;
            }
            catch
            {
                return 0;
            }
        }

        public static Map CurrentMap(Pawn pawn)
        {
            if (pawn == null)
                return null;
            return pawn.MapHeld ?? pawn.Map;
        }

        private static bool IsNamedProtected(Pawn mech)
        {
            string def = mech?.kindDef?.defName ?? mech?.def?.defName ?? "";
            string label = mech?.kindDef?.label ?? mech?.def?.label ?? "";
            string blob = (def + " " + label).ToLowerInvariant();
            return blob.Contains("warqueen")
                || blob.Contains("war queen")
                || blob.Contains("centurion");
        }

        private static int Bandwidth(Pawn mech, ThingDef raceDef)
        {
            try
            {
                if (mech != null)
                    return UnityEngine.Mathf.RoundToInt(mech.GetStatValue(StatDefOf.BandwidthCost));
                if (raceDef != null)
                    return UnityEngine.Mathf.RoundToInt(raceDef.GetStatValueAbstract(StatDefOf.BandwidthCost));
            }
            catch { /* ignore */ }

            return 0;
        }

        private static void ConsiderMapPawn(Pawn other, Pawn overseer, HashSet<Pawn> seen, List<Pawn> mechs)
        {
            if (other == null || other.Destroyed || other.Dead) return;
            if (other.RaceProps == null || !other.RaceProps.IsMechanoid) return;
            try
            {
                if (other.GetOverseer() != overseer) return;
            }
            catch
            {
                return;
            }

            if (seen.Add(other))
                mechs.Add(other);
        }

        private static IEnumerable<Pawn> AssignedPawns(MechanitorControlGroup group)
        {
            if (group?.AssignedMechs == null)
                yield break;
            foreach (var entry in group.AssignedMechs)
            {
                if (entry?.pawn != null)
                    yield return entry.pawn;
            }
        }
    }
}
