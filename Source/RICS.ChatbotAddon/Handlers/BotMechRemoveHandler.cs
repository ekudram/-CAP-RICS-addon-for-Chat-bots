// BotMechRemoveHandler.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt
//
// !botmechremove scraps extra light work mechs.
// The scrap step is the finish of vanilla JobDriver_DisassembleMech:
// drop IngredientsFromDisassembly, kill with no death letter, then destroy the corpse.
// It does not queue the 300-tick walk, and it does not Destroy() the living pawn.

using System;
using System.Collections.Generic;
using CAP_ChatInteractive;
using RimWorld;
using Verse;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    internal static class BotMechRemoveHandler
    {
        private const int DefaultTarget = 5;

        public static string Build(ChatMessageWrapper user, string[] args)
        {
            try
            {
                BotMechGate gate = BotMechCommon.Open(user);
                if (!gate.ok)
                    return Finish(gate.status, gate.reason, null, false);

                Parse(args, out bool check, out string kindToken, out int? count, out int? excess, out bool badExcess);
                if (badExcess)
                {
                    return Finish(
                        "below_threshold",
                        "excess has to be a whole number, such as excess=5.",
                        null,
                        check);
                }

                if (string.IsNullOrEmpty(kindToken))
                {
                    return Finish(
                        "no_mechs_of_kind",
                        "Say which mech to remove.",
                        null,
                        check);
                }

                var matches = BotMechCommon.MatchKind(BotMechCommon.ControlledMechs(gate.pawn), kindToken);
                if (matches.Count == 0)
                {
                    return Finish(
                        "no_mechs_of_kind",
                        BotMechCommon.WhoHas(gate.pawn) + " no mechs of that kind.",
                        new BotMechRemoveDetails { kind = kindToken },
                        check);
                }

                string label = BotMechCommon.KindLabel(matches[0]);
                string defName = SharedDef(matches, kindToken);
                if (AllProtected(matches))
                {
                    return Finish(
                        "protected",
                        "Those mechs were left alone. Only light work mechs can be scrapped.",
                        new BotMechRemoveDetails { kind = defName, label = label },
                        check);
                }

                int target = excess ?? DefaultTarget;
                int total = matches.Count;
                if (total <= target)
                {
                    return Finish(
                        "below_threshold",
                        BotMechCommon.WhoHas(gate.pawn) + " " + total + " " + BotMechCommon.Plural(label, total)
                            + ". That is not above the target of " + target + ", so none were removed.",
                        new BotMechRemoveDetails
                        {
                            kind = defName,
                            label = label,
                            kept = total,
                            target = target,
                            count = total
                        },
                        check);
                }

                int excessCount = total - target;
                int toRemove = count.HasValue ? Math.Min(Math.Max(0, count.Value), excessCount) : excessCount;
                bool clamped = count.HasValue && count.Value > excessCount;

                var pool = new List<Pawn>();
                var blocked = new List<BotMechNameReason>();
                foreach (var mech in matches)
                {
                    if (mech == null) continue;
                    if (BotMechCommon.IsProtectedKind(mech))
                    {
                        blocked.Add(new BotMechNameReason
                        {
                            label = BotMechCommon.PawnLabel(mech),
                            reason = "Left alone."
                        });
                        continue;
                    }

                    string block = BotMechCommon.RemoveBlockReason(mech);
                    if (block != null)
                    {
                        blocked.Add(new BotMechNameReason { label = BotMechCommon.PawnLabel(mech), reason = block });
                        continue;
                    }

                    pool.Add(mech);
                }

                pool.Sort((a, b) =>
                {
                    int down = BotMechCommon.DownRank(b).CompareTo(BotMechCommon.DownRank(a));
                    if (down != 0) return down;
                    int health = BotMechCommon.HealthKey(a).CompareTo(BotMechCommon.HealthKey(b));
                    if (health != 0) return health;
                    int energy = BotMechCommon.EnergyKey(a).CompareTo(BotMechCommon.EnergyKey(b));
                    if (energy != 0) return energy;
                    return BotMechCommon.TickAssigned(gate.pawn, b).CompareTo(BotMechCommon.TickAssigned(gate.pawn, a));
                });

                int take = Math.Min(toRemove, pool.Count);
                var resources = new List<BotMechResource>();
                var names = new List<string>();
                var left = new List<BotMechNameReason>();
                int removed = 0;

                for (int i = 0; i < take; i++)
                {
                    Pawn mech = pool[i];
                    if (check)
                    {
                        AddResources(resources, CopyIngredients(mech));
                        names.Add(BotMechCommon.PawnLabel(mech));
                        removed++;
                        continue;
                    }

                    if (TryScrap(mech, gate.pawn, resources, out string why))
                    {
                        names.Add(BotMechCommon.PawnLabel(mech));
                        removed++;
                    }
                    else
                    {
                        left.Add(new BotMechNameReason
                        {
                            label = BotMechCommon.PawnLabel(mech),
                            reason = why ?? "Could not be disassembled."
                        });
                    }
                }

                if (removed < toRemove)
                {
                    foreach (var item in blocked)
                        left.Add(item);
                }

                int kept = total - removed;
                string reason = BuildReason(gate.pawn, label, removed, kept, target, clamped, count, resources, left.Count > 0, check);
                return Finish("removed", reason, new BotMechRemoveDetails
                {
                    kind = defName,
                    label = label,
                    removed = removed,
                    kept = kept,
                    target = target,
                    requested = count,
                    clamped = clamped ? (bool?)true : null,
                    resources = resources.Count > 0 ? resources : null,
                    names = names.Count > 0 ? names : null,
                    left = left.Count > 0 ? left : null
                }, check);
            }
            catch (Exception ex)
            {
                return BotMapHelper.ErrorExceptionJson(ex.Message);
            }
        }

        private static string BuildReason(
            Pawn pawn,
            string label,
            int removed,
            int kept,
            int target,
            bool clamped,
            int? requested,
            List<BotMechResource> resources,
            bool anyLeft,
            bool check)
        {
            string who = BotMechCommon.Who(pawn);
            string reason;
            if (removed > 0)
            {
                string verb = check ? "Would scrap " : "Scrapped ";
                reason = verb + removed + " " + BotMechCommon.Plural(label, removed) + ". " + who + (check ? " would still have " : " still has ") + kept + ".";
            }
            else
            {
                reason = "No " + BotMechCommon.Plural(label, 2) + " were scrapped. " + who + " still has " + kept + ".";
            }

            if (clamped && requested.HasValue)
                reason += " The request was " + requested.Value + ", and it stopped at the target of " + target + ".";

            string got = ResourceSentence(resources);
            if (!string.IsNullOrEmpty(got))
            {
                if (check)
                    got = got.Replace("Got back", "Would get back");
                reason += " " + got;
            }

            if (anyLeft)
                reason += " Some were left in place.";

            return reason;
        }

        private static string ResourceSentence(List<BotMechResource> resources)
        {
            if (resources == null || resources.Count == 0)
                return null;

            var parts = new List<string>();
            foreach (var item in resources)
            {
                if (item == null || item.count <= 0) continue;
                string name = string.IsNullOrEmpty(item.label) ? item.defName : item.label;
                parts.Add(item.count + " " + name);
            }

            if (parts.Count == 0)
                return null;
            if (parts.Count == 1)
                return "Got back " + parts[0] + ".";
            if (parts.Count == 2)
                return "Got back " + parts[0] + " and " + parts[1] + ".";

            string last = parts[parts.Count - 1];
            parts.RemoveAt(parts.Count - 1);
            return "Got back " + string.Join(", ", parts) + ", and " + last + ".";
        }

        private static bool TryScrap(Pawn mech, Pawn mechanitor, List<BotMechResource> resources, out string reason)
        {
            reason = null;
            var made = new List<Thing>();
            try
            {
                if (mech == null || mech.Destroyed || mech.Dead)
                {
                    reason = "Already gone.";
                    return false;
                }

                if (!TryDropSpot(mech, mechanitor, out Map map, out IntVec3 cell))
                {
                    reason = "No place to drop the scrap.";
                    return false;
                }

                var planned = CopyIngredients(mech);
                foreach (var item in planned)
                {
                    if (item?.thingDef == null || item.count <= 0)
                        continue;
                    Thing thing = ThingMaker.MakeThing(item.thingDef);
                    if (thing == null)
                    {
                        Cleanup(made);
                        reason = "Could not make the scrap.";
                        return false;
                    }

                    thing.stackCount = item.count;
                    if (!GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near))
                    {
                        if (!thing.Destroyed)
                            thing.Destroy();
                        Cleanup(made);
                        reason = "No place to drop the scrap.";
                        return false;
                    }

                    made.Add(thing);
                }

                try { mechanitor?.mechanitor?.UnassignPawnFromAnyControlGroup(mech); }
                catch { /* group cleanup is best-effort */ }

                mech.forceNoDeathNotification = true;
                mech.Kill(null, null);
                mech.forceNoDeathNotification = false;

                if (!mech.Dead)
                {
                    Cleanup(made);
                    reason = "The mech could not be disassembled.";
                    return false;
                }

                try
                {
                    if (mech.Corpse != null && !mech.Corpse.Destroyed)
                        mech.Corpse.Destroy();
                }
                catch { /* the mech is already dead and the refund is on the ground */ }

                AddResources(resources, planned);
                return true;
            }
            catch
            {
                try
                {
                    if (mech != null)
                        mech.forceNoDeathNotification = false;
                }
                catch { /* ignore */ }

                Cleanup(made);
                reason = "The mech could not be disassembled.";
                return false;
            }
        }

        private static bool TryDropSpot(Pawn mech, Pawn mechanitor, out Map map, out IntVec3 cell)
        {
            map = null;
            cell = IntVec3.Invalid;
            try
            {
                map = mech.MapHeld ?? mech.Map;
                if (map != null)
                {
                    if (mech.Spawned && mech.Position.InBounds(map))
                    {
                        cell = mech.Position;
                        return true;
                    }

                    if (mechanitor != null && mechanitor.Spawned && mechanitor.Map == map && mechanitor.Position.InBounds(map))
                    {
                        cell = mechanitor.Position;
                        return true;
                    }

                    if (map.Center.InBounds(map))
                    {
                        cell = map.Center;
                        return true;
                    }
                }

                if (mechanitor != null && mechanitor.Spawned && mechanitor.Map != null)
                {
                    map = mechanitor.Map;
                    cell = mechanitor.Position;
                    return cell.InBounds(map);
                }
            }
            catch
            {
                map = null;
                cell = IntVec3.Invalid;
            }

            return false;
        }

        // IngredientsFromDisassembly returns a shared list. Copy it before anything else calls that method.
        private static List<ThingDefCountClass> CopyIngredients(Pawn mech)
        {
            var copy = new List<ThingDefCountClass>();
            try
            {
                if (mech?.def == null)
                    return copy;
                var src = MechanitorUtility.IngredientsFromDisassembly(mech.def);
                if (src == null)
                    return copy;
                foreach (var item in src)
                {
                    if (item?.thingDef == null || item.count <= 0)
                        continue;
                    copy.Add(new ThingDefCountClass(item.thingDef, item.count));
                }
            }
            catch { /* unknown refund */ }

            return copy;
        }

        private static void AddResources(List<BotMechResource> totals, List<ThingDefCountClass> planned)
        {
            if (totals == null || planned == null)
                return;
            foreach (var item in planned)
            {
                if (item?.thingDef == null || item.count <= 0)
                    continue;
                BotMechResource found = null;
                foreach (var existing in totals)
                {
                    if (existing != null && existing.defName == item.thingDef.defName)
                    {
                        found = existing;
                        break;
                    }
                }

                if (found == null)
                {
                    string label = BotJson.WithoutColor(item.thingDef.label);
                    found = new BotMechResource
                    {
                        defName = item.thingDef.defName,
                        label = string.IsNullOrEmpty(label) ? item.thingDef.defName : label,
                        count = 0
                    };
                    totals.Add(found);
                }

                found.count += item.count;
            }
        }

        private static void Cleanup(List<Thing> made)
        {
            if (made == null)
                return;
            foreach (var thing in made)
            {
                try
                {
                    if (thing != null && !thing.Destroyed)
                        thing.Destroy();
                }
                catch { /* ignore */ }
            }

            made.Clear();
        }

        private static bool AllProtected(List<Pawn> matches)
        {
            bool any = false;
            foreach (var mech in matches)
            {
                if (mech == null) continue;
                any = true;
                if (!BotMechCommon.IsProtectedKind(mech))
                    return false;
            }

            return any;
        }

        private static string SharedDef(List<Pawn> matches, string fallback)
        {
            string def = null;
            foreach (var mech in matches)
            {
                if (mech == null) continue;
                string next = BotMechCommon.KindDefName(mech);
                if (def == null)
                    def = next;
                else if (!string.Equals(def, next, StringComparison.OrdinalIgnoreCase))
                    return fallback;
            }

            return def ?? fallback;
        }

        private static void Parse(string[] args, out bool check, out string kind, out int? count, out int? excess, out bool badExcess)
        {
            check = false;
            kind = null;
            count = null;
            excess = null;
            badExcess = false;
            if (args == null)
                return;

            var kindParts = new List<string>();
            foreach (var raw in args)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;
                if (string.Equals(raw, "check", StringComparison.OrdinalIgnoreCase))
                {
                    check = true;
                    continue;
                }

                if (raw.StartsWith("excess=", StringComparison.OrdinalIgnoreCase))
                {
                    string number = raw.Substring("excess=".Length);
                    if (int.TryParse(number, out int parsed) && parsed >= 0)
                        excess = parsed;
                    else
                        badExcess = true;
                    continue;
                }

                if (kindParts.Count > 0 && !count.HasValue && int.TryParse(raw, out int amount) && amount >= 0)
                {
                    count = amount;
                    continue;
                }

                kindParts.Add(raw);
            }

            if (kindParts.Count > 0)
                kind = string.Join(" ", kindParts);
        }

        private static string Finish(string status, string reason, BotMechRemoveDetails details, bool check)
        {
            if (check)
            {
                if (!string.IsNullOrEmpty(reason) && reason.IndexOf("Dry run.", StringComparison.Ordinal) < 0)
                    reason = "Dry run. " + reason;
                if (details == null)
                    details = new BotMechRemoveDetails();
                details.check = true;
            }

            return BotJson.Serialize(new BotMechRemoveReply
            {
                command = "botmechremove",
                status = status,
                reason = reason,
                details = details
            });
        }
    }

    internal class BotMechRemoveReply
    {
        public string command;
        public string status;
        public string reason;
        public BotMechRemoveDetails details;
    }

    internal class BotMechRemoveDetails
    {
        public bool? check;
        public string kind;
        public string label;
        public int? removed;
        public int? kept;
        public int? target;
        public int? count;
        public int? requested;
        public bool? clamped;
        public List<BotMechResource> resources;
        public List<string> names;
        public List<BotMechNameReason> left;
    }

    internal class BotMechResource
    {
        public string defName;
        public string label;
        public int count;
    }
}
