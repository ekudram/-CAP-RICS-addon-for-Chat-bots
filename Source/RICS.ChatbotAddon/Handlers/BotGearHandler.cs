// BotGearHandler.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt
//
// Bot-only full loadout for the AI user's assigned pawn.
// Viewer !mypawn gear stays short on purpose — do not change that path.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CAP_ChatInteractive;
using RimWorld;
using Verse;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    internal static class BotGearHandler
    {
        public static string Build(ChatMessageWrapper user)
        {
            try
            {
                var mgr = CAPChatInteractiveMod.GetPawnAssignmentManager();
                Pawn pawn = mgr?.GetAssignedPawn(user);
                if (pawn == null || pawn.Destroyed)
                    return BotJson.Serialize(new BotGearNoPawnPayload { status = "no_pawn", command = "botgear" });

                var equipment = new List<BotGearItem>();
                var weapons = new List<BotWeaponEntry>();
                var seenWeaponIds = new HashSet<int>();
                var primaryIds = new HashSet<int>();
                try
                {
                    var eqList = pawn.equipment?.AllEquipmentListForReading;
                    if (eqList != null)
                    {
                        foreach (var t in eqList)
                        {
                            if (t == null) continue;
                            primaryIds.Add(t.thingIDNumber);
                            equipment.Add(ToGearItem(t));
                            TryAddWeapon(t, "equipment", weapons, seenWeaponIds);
                        }
                    }
                }
                catch { /* ignore */ }

                var sidearms = new List<BotGearItem>();
                foreach (var t in CollectSidearms(pawn))
                {
                    if (t == null) continue;
                    if (primaryIds.Contains(t.thingIDNumber)) continue;
                    sidearms.Add(ToGearItem(t));
                    TryAddWeapon(t, "sidearm", weapons, seenWeaponIds);
                }

                var inventory = new List<BotInvItem>();
                try
                {
                    var bag = pawn.inventory?.innerContainer;
                    if (bag != null)
                    {
                        foreach (var t in bag)
                        {
                            if (t == null) continue;
                            inventory.Add(ToInvItem(t));
                            TryAddWeapon(t, "inventory", weapons, seenWeaponIds);
                        }
                    }
                }
                catch { /* ignore */ }

                var apparel = new List<BotApparelItem>();
                try
                {
                    var worn = pawn.apparel?.WornApparel;
                    if (worn != null)
                    {
                        foreach (var t in worn)
                        {
                            if (t == null) continue;
                            apparel.Add(ToApparelItem(t));
                        }
                    }
                }
                catch { /* ignore */ }

                var payload = new BotGearPayload
                {
                    status = "ok",
                    command = "botgear",
                    pawn = pawn.LabelShort ?? pawn.Name?.ToStringShort ?? "unknown",
                    equipment = equipment,
                    sidearms = sidearms,
                    inventory = inventory,
                    apparel = apparel,
                    hasWeapons = weapons.Count > 0,
                    weapons = weapons
                };

                return BotJson.Serialize(payload);
            }
            catch (Exception ex)
            {
                return BotMapHelper.ErrorExceptionJson(ex.Message);
            }
        }

        private static BotGearItem ToGearItem(Thing t)
        {
            return new BotGearItem
            {
                label = t.LabelNoCount ?? t.def?.label,
                defName = t.def?.defName,
                stuff = t.Stuff?.label ?? t.Stuff?.defName,
                quality = TryQuality(t),
                hpPct = HpPct(t),
                kind = WeaponKind(t)
            };
        }

        private static BotInvItem ToInvItem(Thing t)
        {
            return new BotInvItem
            {
                label = t.LabelNoCount ?? t.def?.label,
                defName = t.def?.defName,
                stuff = t.Stuff?.label ?? t.Stuff?.defName,
                stack = t.stackCount,
                quality = TryQuality(t)
            };
        }

        private static BotApparelItem ToApparelItem(Apparel t)
        {
            return new BotApparelItem
            {
                label = t.LabelNoCount ?? t.def?.label,
                defName = t.def?.defName,
                stuff = t.Stuff?.label ?? t.Stuff?.defName,
                quality = TryQuality(t),
                hpPct = HpPct(t)
            };
        }

        private static double HpPct(Thing t)
        {
            try
            {
                if (t.MaxHitPoints <= 0) return 1.0;
                return Math.Round((double)t.HitPoints / t.MaxHitPoints, 3);
            }
            catch
            {
                return 1.0;
            }
        }

        private static string TryQuality(Thing t)
        {
            try
            {
                var cq = t.TryGetComp<CompQuality>();
                if (cq != null)
                    return cq.Quality.GetLabel();
            }
            catch { /* ignore */ }
            return null;
        }

        private static string WeaponKind(Thing t)
        {
            var def = t?.def;
            if (def == null) return "other";
            if (def.IsRangedWeapon) return "ranged";
            if (def.IsMeleeWeapon) return "melee";
            return "other";
        }

        private static bool IsWeaponThing(Thing t)
        {
            var def = t?.def;
            if (def == null) return false;
            return def.IsWeapon || def.IsRangedWeapon || def.IsMeleeWeapon;
        }

        private static void TryAddWeapon(Thing t, string slot, List<BotWeaponEntry> weapons, HashSet<int> seen)
        {
            if (!IsWeaponThing(t)) return;
            if (!seen.Add(t.thingIDNumber)) return;
            var entry = new BotWeaponEntry
            {
                slot = slot,
                label = t.LabelNoCount ?? t.def?.label,
                defName = t.def?.defName,
                stuff = t.Stuff?.label ?? t.Stuff?.defName,
                quality = TryQuality(t),
                hpPct = HpPct(t),
                kind = WeaponKind(t)
            };
            FillWeaponCombatStats(t, entry);
            weapons.Add(entry);
        }

        /// <summary>
        /// Live inspect-card numbers (quality/stuff applied). Null fields are omitted in JSON.
        /// </summary>
        private static void FillWeaponCombatStats(Thing t, BotWeaponEntry e)
        {
            if (t?.def == null || e == null) return;

            try
            {
                float mass = t.GetStatValue(StatDefOf.Mass);
                if (mass > 0.001f)
                    e.mass = Math.Round(mass, 3);
            }
            catch { /* ignore */ }

            try
            {
                var traits = GetWeaponTraits(t);
                if (traits.Count > 0)
                    e.traits = traits;
            }
            catch { /* ignore */ }

            bool ranged = e.kind == "ranged" || t.def.IsRangedWeapon;
            bool melee = e.kind == "melee" || t.def.IsMeleeWeapon;
            if (ranged)
                FillRangedStats(t, e);
            if (melee && e.kind != "ranged")
                FillMeleeStats(t, e);
        }

        private static void FillRangedStats(Thing t, BotWeaponEntry e)
        {
            VerbProperties verb = null;
            try
            {
                verb = t.def.Verbs?.FirstOrDefault(v => v != null && !v.IsMeleeAttack && v.defaultProjectile != null);
            }
            catch { /* ignore */ }

            try
            {
                var projProps = verb?.defaultProjectile?.projectile;
                if (projProps != null)
                {
                    e.damage = projProps.GetDamageAmount(t);
                    float ap = projProps.GetArmorPenetration(t);
                    if (ap > 0.001f)
                        e.ap = Math.Round(ap, 3);
                }
            }
            catch { /* ignore */ }

            if (verb != null)
            {
                try
                {
                    if (verb.warmupTime > 0f)
                        e.warmup = Math.Round(verb.warmupTime, 3);
                }
                catch { /* ignore */ }

                try
                {
                    if (verb.range > 0f)
                        e.range = Math.Round(verb.range, 3);
                }
                catch { /* ignore */ }

                try
                {
                    int burst = verb.burstShotCount;
                    if (burst > 0)
                        e.burstCount = burst;
                    if (burst > 1 && verb.ticksBetweenBurstShots > 0)
                        e.ticksBetweenBurstShots = verb.ticksBetweenBurstShots;
                }
                catch { /* ignore */ }
            }

            try
            {
                float cd = t.GetStatValue(StatDefOf.RangedWeapon_Cooldown);
                if (cd > 0.001f)
                    e.cooldown = Math.Round(cd, 3);
            }
            catch { /* ignore */ }

            try
            {
                float acc = t.GetStatValue(StatDefOf.AccuracyTouch);
                if (acc > 0.001f)
                    e.accuracyTouch = Math.Round(acc, 3);
            }
            catch { /* ignore */ }
        }

        private static void FillMeleeStats(Thing t, BotWeaponEntry e)
        {
            try
            {
                float dps = t.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS);
                if (dps > 0.001f)
                    e.meleeDps = Math.Round(dps, 2);
            }
            catch { /* ignore */ }

            try
            {
                var apDef = DefDatabase<StatDef>.GetNamedSilentFail("MeleeWeapon_AverageArmorPenetration");
                if (apDef != null)
                {
                    float ap = t.GetStatValue(apDef);
                    if (ap > 0.001f)
                        e.meleeAp = Math.Round(ap, 3);
                }
            }
            catch { /* ignore */ }
        }

        private static List<string> GetWeaponTraits(Thing weapon)
        {
            var traits = new List<string>();
            if (weapon == null) return traits;

            try
            {
                var uniqueComp = weapon.TryGetComp<CompUniqueWeapon>();
                var uniqueList = uniqueComp?.TraitsListForReading;
                if (uniqueList != null)
                {
                    foreach (var trait in uniqueList)
                    {
                        if (trait == null) continue;
                        string label = trait.LabelCap;
                        if (!string.IsNullOrEmpty(label) && !traits.Contains(label))
                            traits.Add(label);
                    }
                }
            }
            catch { /* optional DLC/mod */ }

            try
            {
                var bladelink = weapon.TryGetComp<CompBladelinkWeapon>();
                var bladeList = bladelink?.TraitsListForReading;
                if (bladeList != null)
                {
                    foreach (var trait in bladeList)
                    {
                        if (trait == null) continue;
                        string label = trait.LabelCap;
                        if (!string.IsNullOrEmpty(label) && !traits.Contains(label))
                            traits.Add(label);
                    }
                }
            }
            catch { /* optional Royalty */ }

            return traits;
        }

        /// <summary>
        /// Several reflection paths — SimpleSidearms.GetSidearms alone often misses carried tools.
        /// </summary>
        private static List<Thing> CollectSidearms(Pawn pawn)
        {
            var found = new List<Thing>();
            var seen = new HashSet<int>();

            void AddRange(IEnumerable things)
            {
                if (things == null) return;
                foreach (var o in things)
                {
                    Thing t = o as Thing;
                    if (t == null && o is ThingDef)
                        continue;
                    if (t == null) continue;
                    if (seen.Add(t.thingIDNumber))
                        found.Add(t);
                }
            }

            try
            {
                AddRange(InvokeGetSidearms(pawn, "SimpleSidearms.SimpleSidearms, SimpleSidearms"));
                AddRange(InvokeGetSidearms(pawn, "SimpleSidearms.rimworld.CompSidearmMemory, SimpleSidearms"));
            }
            catch { /* optional mod */ }

            try
            {
                if (pawn.AllComps != null)
                {
                    foreach (var comp in pawn.AllComps)
                    {
                        if (comp == null) continue;
                        string tn = comp.GetType().Name ?? "";
                        string fn = comp.GetType().FullName ?? "";
                        if (tn.IndexOf("Sidearm", StringComparison.OrdinalIgnoreCase) < 0
                            && fn.IndexOf("Sidearm", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        AddRange(InvokeNamedEnumerable(comp, "GetSidearms"));
                        HarvestThingEnumerables(comp, AddRange);
                    }
                }
            }
            catch { /* ignore */ }

            return found;
        }

        private static IEnumerable<Thing> InvokeGetSidearms(Pawn pawn, string typeName)
        {
            var type = Type.GetType(typeName);
            if (type == null) return null;

            var method = type.GetMethod("GetSidearms", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null) return null;

            var result = method.Invoke(null, new object[] { pawn });
            return result as IEnumerable<Thing> ?? UnwrapThings(result);
        }

        private static IEnumerable<Thing> InvokeNamedEnumerable(object target, string methodName)
        {
            if (target == null) return null;
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null || method.GetParameters().Length != 0) return null;
            return UnwrapThings(method.Invoke(target, null));
        }

        private static void HarvestThingEnumerables(object obj, Action<IEnumerable> add)
        {
            if (obj == null) return;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var p in obj.GetType().GetProperties(flags))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                if (!typeof(IEnumerable).IsAssignableFrom(p.PropertyType)) continue;
                try { add(UnwrapThings(p.GetValue(obj))); } catch { /* skip */ }
            }
            foreach (var f in obj.GetType().GetFields(flags))
            {
                if (!typeof(IEnumerable).IsAssignableFrom(f.FieldType)) continue;
                try { add(UnwrapThings(f.GetValue(obj))); } catch { /* skip */ }
            }
        }

        private static IEnumerable<Thing> UnwrapThings(object result)
        {
            if (result == null) return null;
            if (result is IEnumerable<Thing> typed)
                return typed;
            if (result is IEnumerable raw)
            {
                var list = new List<Thing>();
                foreach (var o in raw)
                {
                    if (o is Thing t)
                        list.Add(t);
                }
                return list;
            }
            return null;
        }
    }

    internal class BotGearNoPawnPayload
    {
        public string status;
        public string command;
    }

    internal class BotGearPayload
    {
        public string status;
        public string command;
        public string pawn;
        public List<BotGearItem> equipment;
        public List<BotGearItem> sidearms;
        public List<BotInvItem> inventory;
        public List<BotApparelItem> apparel;
        public bool hasWeapons;
        public List<BotWeaponEntry> weapons;
    }

    internal class BotGearItem
    {
        public string label;
        public string defName;
        public string stuff;
        public string quality;
        public double hpPct;
        public string kind;
    }

    internal class BotInvItem
    {
        public string label;
        public string defName;
        public string stuff;
        public int stack;
        public string quality;
    }

    internal class BotApparelItem
    {
        public string label;
        public string defName;
        public string stuff;
        public string quality;
        public double hpPct;
    }

    internal class BotWeaponEntry
    {
        public string slot;
        public string label;
        public string defName;
        public string stuff;
        public string quality;
        public double hpPct;
        public string kind;
        public int? damage;
        public double? ap;
        public double? warmup;
        public double? cooldown;
        public double? range;
        public int? burstCount;
        public int? ticksBetweenBurstShots;
        public double? accuracyTouch;
        public double? meleeDps;
        public double? meleeAp;
        public double? mass;
        public List<string> traits;
    }
}
