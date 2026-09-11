// BotDisownHandler.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt
//
// Bot-only unclaim of RICS-owned gear (same ClearOwner path as !mypawn disown).

using System;
using System.Collections.Generic;
using System.Linq;
using CAP_ChatInteractive;
using CAP_ChatInteractive.Ownership;
using Verse;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    internal static class BotDisownHandler
    {
        public const string Command = "botdisown";

        public static string Build(ChatMessageWrapper user, string[] args)
        {
            try
            {
                if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
                    return BotJson.Serialize(new BotDisownSimplePayload { status = "ownership_off", command = Command, ownershipActive = false });

                var mgr = CAPChatInteractiveMod.GetPawnAssignmentManager();
                Pawn pawn = mgr?.GetAssignedPawn(user);
                if (pawn == null || pawn.Destroyed)
                    return BotJson.Serialize(new BotDisownSimplePayload { status = "no_pawn", command = Command, ownershipActive = true });

                string query = args != null && args.Length > 0
                    ? string.Join(" ", args).Trim()
                    : "";
                if (string.IsNullOrWhiteSpace(query))
                    return BotJson.Serialize(new BotDisownSimplePayload
                    {
                        status = "usage",
                        command = Command,
                        ownershipActive = true,
                        message = "Usage: !botdisown <thingId|label>"
                    });

                var all = RICS_OwnedItemsCollector.CollectForPawn(pawn);
                if (all.Count == 0)
                    return BotJson.Serialize(new BotDisownSimplePayload { status = "none_owned", command = Command, ownershipActive = true });

                RICS_OwnedItem match = null;

                if (int.TryParse(query, out int thingId) && thingId > 0)
                {
                    match = all.FirstOrDefault(i => i.Thing != null && i.Thing.thingIDNumber == thingId);
                    if (match?.Thing == null || match.Thing.Destroyed)
                        return BotJson.Serialize(new BotDisownSimplePayload { status = "gone", command = Command, ownershipActive = true });
                }
                else
                {
                    var matches = all.Where(i => i.Thing != null && !i.Thing.Destroyed
                        && ((i.Thing.LabelNoCount ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                            || (i.Thing.def?.label ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                            || (i.Thing.def?.defName ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                        .ToList();

                    if (matches.Count == 0)
                        return BotJson.Serialize(new BotDisownSimplePayload { status = "not_found", command = Command, ownershipActive = true, message = query });

                    if (matches.Count > 1)
                    {
                        var dtos = new List<BotOwnedItemDto>();
                        foreach (var m in matches)
                        {
                            var dto = BotOwnedHandler.ToDto(m);
                            if (dto != null)
                                dtos.Add(dto);
                        }
                        return BotJson.Serialize(new BotDisownAmbiguousPayload
                        {
                            status = "ambiguous",
                            command = Command,
                            ownershipActive = true,
                            matchCount = dtos.Count,
                            matches = dtos
                        });
                    }

                    match = matches[0];
                }

                Thing target = match.Thing;
                var owner = RICS_OwnershipUtility.GetOwner(target);
                if (owner != pawn)
                    return BotJson.Serialize(new BotDisownSimplePayload { status = "not_yours", command = Command, ownershipActive = true });

                var snapshot = BotOwnedHandler.ToDto(match);
                if (!RICS_OwnershipUtility.ClearOwner(target, "bot disown"))
                    return BotJson.Serialize(new BotDisownFailedPayload
                    {
                        status = "failed",
                        command = Command,
                        ownershipActive = true,
                        item = snapshot
                    });

                return BotJson.Serialize(new BotDisownOkPayload
                {
                    status = "ok",
                    command = Command,
                    ownershipActive = true,
                    pawn = pawn.LabelShort ?? pawn.Name?.ToStringShort ?? "unknown",
                    cleared = snapshot
                });
            }
            catch (Exception ex)
            {
                return BotMapHelper.ErrorExceptionJson(ex.Message);
            }
        }
    }

    internal class BotDisownSimplePayload
    {
        public string status;
        public string command;
        public bool ownershipActive;
        public string message;
    }

    internal class BotDisownAmbiguousPayload
    {
        public string status;
        public string command;
        public bool ownershipActive;
        public int matchCount;
        public List<BotOwnedItemDto> matches;
    }

    internal class BotDisownFailedPayload
    {
        public string status;
        public string command;
        public bool ownershipActive;
        public BotOwnedItemDto item;
    }

    internal class BotDisownOkPayload
    {
        public string status;
        public string command;
        public bool ownershipActive;
        public string pawn;
        public BotOwnedItemDto cleared;
    }
}
