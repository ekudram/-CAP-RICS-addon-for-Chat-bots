// BotOwnedHandler.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt
//
// Bot-only RICS ownership inventory (weapons/apparel the assigned pawn owns anywhere).

using System;
using System.Collections.Generic;
using CAP_ChatInteractive;
using CAP_ChatInteractive.Ownership;
using RimWorld;
using Verse;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    internal static class BotOwnedHandler
    {
        public const string Command = "botowned";

        public static string Build(ChatMessageWrapper user)
        {
            try
            {
                if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
                    return BotJson.Serialize(new BotOwnedOffPayload { status = "ownership_off", command = Command, ownershipActive = false });

                var mgr = CAPChatInteractiveMod.GetPawnAssignmentManager();
                Pawn pawn = mgr?.GetAssignedPawn(user);
                if (pawn == null || pawn.Destroyed)
                    return BotJson.Serialize(new BotOwnedNoPawnPayload { status = "no_pawn", command = Command, ownershipActive = true });

                var all = RICS_OwnedItemsCollector.CollectForPawn(pawn);
                var weapons = new List<BotOwnedItemDto>();
                foreach (var item in RICS_OwnedItemsCollector.WeaponsSorted(all))
                {
                    var dto = ToDto(item);
                    if (dto != null)
                        weapons.Add(dto);
                }

                var apparel = new List<BotOwnedItemDto>();
                foreach (var item in RICS_OwnedItemsCollector.ApparelSorted(all))
                {
                    var dto = ToDto(item);
                    if (dto != null)
                        apparel.Add(dto);
                }

                return BotJson.Serialize(new BotOwnedPayload
                {
                    status = "ok",
                    command = Command,
                    ownershipActive = true,
                    pawn = pawn.LabelShort ?? pawn.Name?.ToStringShort ?? "unknown",
                    count = weapons.Count + apparel.Count,
                    weapons = weapons,
                    apparel = apparel
                });
            }
            catch (Exception ex)
            {
                return BotMapHelper.ErrorExceptionJson(ex.Message);
            }
        }

        internal static BotOwnedItemDto ToDto(RICS_OwnedItem item)
        {
            Thing t = item?.Thing;
            if (t == null || t.Destroyed)
                return null;

            var dto = new BotOwnedItemDto
            {
                id = t.thingIDNumber,
                label = t.LabelNoCount ?? t.def?.label,
                defName = t.def?.defName,
                stuff = t.Stuff?.label ?? t.Stuff?.defName,
                quality = QualityOf(t) ?? (item.QualityLabel == "-" ? null : item.QualityLabel),
                hpPct = HpPct(t),
                kind = item.IsWeapon ? "weapon" : (item.IsApparel ? "apparel" : "item"),
                where = item.Where ?? "Unknown"
            };

            if (item.IsApparel)
            {
                dto.armorSharp = Math.Round(item.ArmorSharp, 3);
                dto.armorBlunt = Math.Round(item.ArmorBlunt, 3);
                dto.armorHeat = Math.Round(item.ArmorHeat, 3);
            }

            return dto;
        }

        internal static string QualityOf(Thing t)
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

        internal static double HpPct(Thing t)
        {
            try
            {
                if (t == null || t.MaxHitPoints <= 0)
                    return 1.0;
                return Math.Round((double)t.HitPoints / t.MaxHitPoints, 3);
            }
            catch
            {
                return 1.0;
            }
        }
    }

    internal class BotOwnedNoPawnPayload
    {
        public string status;
        public string command;
        public bool ownershipActive;
    }

    internal class BotOwnedOffPayload
    {
        public string status;
        public string command;
        public bool ownershipActive;
    }

    internal class BotOwnedPayload
    {
        public string status;
        public string command;
        public bool ownershipActive;
        public string pawn;
        public int count;
        public List<BotOwnedItemDto> weapons;
        public List<BotOwnedItemDto> apparel;
    }

    internal class BotOwnedItemDto
    {
        public int id;
        public string label;
        public string defName;
        public string stuff;
        public string quality;
        public double hpPct;
        public string kind;
        public string where;
        public double? armorSharp;
        public double? armorBlunt;
        public double? armorHeat;
    }
}
