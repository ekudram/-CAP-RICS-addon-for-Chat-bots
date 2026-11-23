// BotMechsHandler.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt
//
// Bot-only mechanitor roster for the AI user's assigned pawn.
// Viewer !mypawn mechs stays short on purpose — do not change that path.

using System;
using System.Collections.Generic;
using System.Linq;
using CAP_ChatInteractive;
using RimWorld;
using Verse;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    internal static class BotMechsHandler
    {
        public static string Build(ChatMessageWrapper user)
        {
            try
            {
                var mgr = CAPChatInteractiveMod.GetPawnAssignmentManager();
                Pawn pawn = mgr?.GetAssignedPawn(user);
                if (pawn == null || pawn.Destroyed)
                    return BotJson.Serialize(new BotMechsStatusPayload { status = "no_pawn", command = "botmechs" });

                if (!IsMechanitor(pawn))
                    return BotJson.Serialize(new BotMechsStatusPayload { status = "not_mechanitor", command = "botmechs" });

                var tracker = pawn.mechanitor;
                int used = 0;
                int total = 0;
                try
                {
                    used = tracker.UsedBandwidth;
                    total = tracker.TotalBandwidth;
                }
                catch { /* ignore */ }

                var groups = new List<BotMechGroupEntry>();
                try
                {
                    if (tracker.controlGroups != null)
                    {
                        foreach (var group in tracker.controlGroups)
                        {
                            if (group == null) continue;
                            groups.Add(new BotMechGroupEntry
                            {
                                index = group.Index,
                                workMode = group.WorkMode?.defName
                            });
                        }
                    }
                }
                catch { /* ignore */ }

                var mechs = CollectMechs(pawn, tracker);
                var types = BuildTypes(mechs);
                SplitCatalog(out var available, out var locked);

                var payload = new BotMechsPayload
                {
                    status = "ok",
                    command = "botmechs",
                    pawn = pawn.LabelShort ?? pawn.Name?.ToStringShort ?? "unknown",
                    bandwidth = new BotMechBandwidth
                    {
                        used = used,
                        total = total,
                        free = total - used
                    },
                    controlGroups = new BotMechControlGroups
                    {
                        count = groups.Count,
                        groups = groups
                    },
                    types = types,
                    available = available,
                    locked = locked
                };

                return BotJson.Serialize(payload);
            }
            catch (Exception ex)
            {
                return BotMapHelper.ErrorExceptionJson(ex.Message);
            }
        }

        private static bool IsMechanitor(Pawn pawn)
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

        // Same sources as RICS MyPawnCommandHandler.HandleMechsInfo, then deduped.
        private static List<Pawn> CollectMechs(Pawn pawn, Pawn_MechanitorTracker tracker)
        {
            var seen = new HashSet<Pawn>();
            var mechs = new List<Pawn>();

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
                        AddMany(group?.MechsForReading);
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
                        IEnumerable<Pawn> spawned = map.mapPawns.AllPawnsSpawned ?? Enumerable.Empty<Pawn>();
                        IEnumerable<Pawn> unspawned = map.mapPawns.AllPawnsUnspawned ?? Enumerable.Empty<Pawn>();
                        foreach (var other in spawned.Concat(unspawned))
                        {
                            if (other == null || other.Destroyed || other.Dead) continue;
                            if (other.RaceProps == null || !other.RaceProps.IsMechanoid) continue;
                            if (other.GetOverseer() != pawn) continue;
                            if (seen.Add(other))
                                mechs.Add(other);
                        }
                    }
                }
            }
            catch { /* ignore */ }

            return mechs;
        }

        private static List<BotMechTypeEntry> BuildTypes(List<Pawn> mechs)
        {
            var buckets = new Dictionary<string, TypeBucket>();
            foreach (var mech in mechs)
            {
                if (mech == null) continue;
                var kind = mech.kindDef;
                string defName = kind?.defName ?? mech.def?.defName ?? "unknown";
                if (!buckets.TryGetValue(defName, out var bucket))
                {
                    var raceDef = kind?.race ?? mech.def;
                    bucket = new TypeBucket
                    {
                        defName = defName,
                        label = PlainLabel(kind?.label, raceDef?.label, defName),
                        bandwidthCost = BandwidthCost(mech, raceDef),
                        role = RoleFor(raceDef),
                        workTypes = WorkTypeNames(raceDef)
                    };
                    buckets[defName] = bucket;
                }

                bucket.count++;
                float? health = HealthPct(mech);
                if (health.HasValue)
                {
                    bucket.healthSum += health.Value;
                    bucket.healthSamples++;
                }

                float? energy = EnergyPct(mech);
                if (energy.HasValue)
                {
                    bucket.energySum += energy.Value;
                    bucket.energySamples++;
                }

                if (IsCharging(mech)) bucket.charging++;
                if (IsDormant(mech)) bucket.dormant++;
                if (mech.Downed) bucket.downed++;
                try
                {
                    if (mech.Drafted) bucket.drafted++;
                }
                catch { /* ignore */ }
            }

            return buckets.Values
                .OrderByDescending(b => b.count)
                .ThenBy(b => b.defName, StringComparer.Ordinal)
                .Select(b => b.ToEntry())
                .ToList();
        }

        private static void SplitCatalog(out List<BotMechKindEntry> available, out List<BotMechLockedEntry> locked)
        {
            available = new List<BotMechKindEntry>();
            locked = new List<BotMechLockedEntry>();
            if (!ModsConfig.BiotechActive)
                return;

            var rows = new Dictionary<string, CatalogRow>();

            try
            {
                var recipes = MechanitorUtility.MechRecipes;
                if (recipes != null)
                {
                    foreach (var recipe in recipes)
                    {
                        var produced = recipe?.ProducedThingDef;
                        if (produced?.race == null || !produced.race.IsMechanoid) continue;
                        var kind = KindForRace(produced);
                        if (kind == null || string.IsNullOrEmpty(kind.defName)) continue;

                        var missing = new List<string>();
                        CollectMissingResearch(recipe, missing);
                        Touch(rows, kind, produced, missing.Count == 0, missing);
                    }
                }
            }
            catch { /* ignore */ }

            try
            {
                foreach (var kind in DefDatabase<PawnKindDef>.AllDefs)
                {
                    if (!CanBuy(kind)) continue;
                    if (kind.race?.race == null || !kind.race.race.IsMechanoid) continue;
                    if (string.IsNullOrEmpty(kind.defName) || rows.ContainsKey(kind.defName)) continue;
                    Touch(rows, kind, kind.race, true, null);
                }
            }
            catch { /* ignore */ }

            foreach (var row in rows.Values.OrderBy(r => r.defName, StringComparer.Ordinal))
            {
                if (row.unlocked)
                {
                    available.Add(new BotMechKindEntry
                    {
                        defName = row.defName,
                        label = row.label,
                        bandwidthCost = row.bandwidthCost,
                        role = row.role
                    });
                }
                else
                {
                    locked.Add(new BotMechLockedEntry
                    {
                        defName = row.defName,
                        label = row.label,
                        bandwidthCost = row.bandwidthCost,
                        role = row.role,
                        missingResearch = row.missing
                    });
                }
            }
        }

        private static void Touch(Dictionary<string, CatalogRow> rows, PawnKindDef kind, ThingDef raceDef, bool researchDone, List<string> missing)
        {
            if (!rows.TryGetValue(kind.defName, out var row))
            {
                row = new CatalogRow
                {
                    defName = kind.defName,
                    label = PlainLabel(kind.label, raceDef?.label, kind.defName),
                    bandwidthCost = BandwidthCost(null, raceDef),
                    role = RoleFor(raceDef),
                    missing = new List<string>()
                };
                rows[kind.defName] = row;
            }

            if (researchDone)
            {
                row.unlocked = true;
                return;
            }

            if (missing == null) return;
            foreach (var name in missing)
            {
                if (string.IsNullOrEmpty(name)) continue;
                if (!row.missing.Contains(name))
                    row.missing.Add(name);
            }
        }

        private static PawnKindDef KindForRace(ThingDef race)
        {
            if (race == null) return null;
            PawnKindDef fallback = null;
            foreach (var kind in DefDatabase<PawnKindDef>.AllDefs)
            {
                if (kind?.race != race) continue;
                if (kind.defName == race.defName)
                    return kind;
                if (fallback == null)
                    fallback = kind;
            }
            return fallback;
        }

        private static bool CanBuy(PawnKindDef kind)
        {
            var race = kind?.race;
            if (race == null) return false;
            if (race.tradeability != Tradeability.None) return true;
            return race.tradeTags != null && race.tradeTags.Count > 0;
        }

        private static void CollectMissingResearch(RecipeDef recipe, List<string> missing)
        {
            if (recipe == null || missing == null) return;
            if (recipe.researchPrerequisite != null && !recipe.researchPrerequisite.IsFinished)
                AddUnique(missing, recipe.researchPrerequisite.defName);
            if (recipe.researchPrerequisites == null) return;
            foreach (var project in recipe.researchPrerequisites)
            {
                if (project != null && !project.IsFinished)
                    AddUnique(missing, project.defName);
            }
        }

        private static void AddUnique(List<string> list, string value)
        {
            if (string.IsNullOrEmpty(value) || list.Contains(value)) return;
            list.Add(value);
        }

        private static string RoleFor(ThingDef raceDef)
        {
            var race = raceDef?.race;
            if (race != null && race.mechEnabledWorkTypes != null && race.mechEnabledWorkTypes.Count > 0)
                return "work";
            return "combat";
        }

        private static List<string> WorkTypeNames(ThingDef raceDef)
        {
            var names = new List<string>();
            var works = raceDef?.race?.mechEnabledWorkTypes;
            if (works == null) return names;
            foreach (var work in works)
            {
                if (work != null && !string.IsNullOrEmpty(work.defName))
                    names.Add(work.defName);
            }
            return names;
        }

        private static int BandwidthCost(Pawn mech, ThingDef raceDef)
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

        private static string PlainLabel(string kindLabel, string raceLabel, string fallback)
        {
            if (!string.IsNullOrEmpty(kindLabel)) return kindLabel;
            if (!string.IsNullOrEmpty(raceLabel)) return raceLabel;
            return fallback ?? "unknown";
        }

        private static float? HealthPct(Pawn pawn)
        {
            try
            {
                if (pawn?.health?.summaryHealth == null) return null;
                return pawn.health.summaryHealth.SummaryHealthPercent * 100f;
            }
            catch
            {
                return null;
            }
        }

        private static float? EnergyPct(Pawn pawn)
        {
            try
            {
                if (pawn?.needs?.energy != null)
                    return pawn.needs.energy.CurLevelPercentage * 100f;
                var cell = pawn?.TryGetComp<CompMechPowerCell>();
                if (cell != null)
                    return cell.PercentFull * 100f;
            }
            catch { /* ignore */ }
            return null;
        }

        private static bool IsCharging(Pawn pawn)
        {
            try
            {
                if (pawn?.needs?.energy?.currentCharger != null)
                    return true;
                return pawn?.CurJobDef == JobDefOf.MechCharge;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsDormant(Pawn pawn)
        {
            try
            {
                if (pawn?.needs?.energy != null && pawn.needs.energy.IsSelfShutdown)
                    return true;
                if (pawn?.CurJobDef == JobDefOf.SelfShutdown)
                    return true;
                return pawn?.health?.hediffSet?.HasHediff(HediffDefOf.SelfShutdown) == true;
            }
            catch
            {
                return false;
            }
        }

        private sealed class TypeBucket
        {
            public string defName;
            public string label;
            public int count;
            public int bandwidthCost;
            public string role;
            public List<string> workTypes;
            public float healthSum;
            public int healthSamples;
            public float energySum;
            public int energySamples;
            public int charging;
            public int dormant;
            public int downed;
            public int drafted;

            public BotMechTypeEntry ToEntry()
            {
                return new BotMechTypeEntry
                {
                    defName = defName,
                    label = label,
                    count = count,
                    bandwidthCost = bandwidthCost,
                    role = role,
                    workTypes = workTypes ?? new List<string>(),
                    avgHealthPct = healthSamples > 0 ? (float)Math.Round(healthSum / healthSamples, 1) : (float?)null,
                    avgEnergyPct = energySamples > 0 ? (float)Math.Round(energySum / energySamples, 1) : (float?)null,
                    charging = charging,
                    dormant = dormant,
                    downed = downed,
                    drafted = drafted
                };
            }
        }

        private sealed class CatalogRow
        {
            public string defName;
            public string label;
            public int bandwidthCost;
            public string role;
            public bool unlocked;
            public List<string> missing;
        }
    }

    internal class BotMechsStatusPayload
    {
        public string status;
        public string command;
    }

    internal class BotMechsPayload
    {
        public string status;
        public string command;
        public string pawn;
        public BotMechBandwidth bandwidth;
        public BotMechControlGroups controlGroups;
        public List<BotMechTypeEntry> types;
        public List<BotMechKindEntry> available;
        public List<BotMechLockedEntry> locked;
    }

    internal class BotMechBandwidth
    {
        public int used;
        public int total;
        public int free;
    }

    internal class BotMechControlGroups
    {
        public int count;
        public List<BotMechGroupEntry> groups;
    }

    internal class BotMechGroupEntry
    {
        public int index;
        public string workMode;
    }

    internal class BotMechTypeEntry
    {
        public string defName;
        public string label;
        public int count;
        public int bandwidthCost;
        public string role;
        public List<string> workTypes;
        public float? avgHealthPct;
        public float? avgEnergyPct;
        public int charging;
        public int dormant;
        public int downed;
        public int drafted;
    }

    internal class BotMechKindEntry
    {
        public string defName;
        public string label;
        public int bandwidthCost;
        public string role;
    }

    internal class BotMechLockedEntry
    {
        public string defName;
        public string label;
        public int bandwidthCost;
        public string role;
        public List<string> missingResearch;
    }
}
