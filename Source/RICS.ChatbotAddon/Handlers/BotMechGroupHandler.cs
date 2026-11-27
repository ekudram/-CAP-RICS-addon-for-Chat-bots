// BotMechGroupHandler.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt
//
// !botmechgroup moves the mechanitor's mechs of one kind into a control group.
// Pass check to read the current groups without changing them.

using System;
using System.Collections.Generic;
using CAP_ChatInteractive;
using RimWorld;
using Verse;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    internal static class BotMechGroupHandler
    {
        public static string Build(ChatMessageWrapper user, string[] args)
        {
            try
            {
                BotMechGate gate = BotMechCommon.Open(user);
                if (!gate.ok)
                    return Status(gate.status, gate.reason, null);

                if (IsCheck(args))
                    return Check(gate.pawn);

                Parse(args, out string kindToken, out int? groupNumber, out string areaName);

                if (string.IsNullOrEmpty(kindToken))
                {
                    return Status(
                        "no_mechs_of_kind",
                        "Say which mech and which control group.",
                        new BotMechGroupMoveDetails());
                }

                var matches = BotMechCommon.MatchKind(BotMechCommon.ControlledMechs(gate.pawn), kindToken);
                if (matches.Count == 0)
                {
                    return Status(
                        "no_mechs_of_kind",
                        BotMechCommon.WhoHas(gate.pawn) + " no mechs of that kind.",
                        new BotMechGroupMoveDetails { kind = kindToken });
                }

                int available = BotMechCommon.AvailableGroups(gate.pawn);
                if (!groupNumber.HasValue || groupNumber.Value < 1 || groupNumber.Value > available)
                {
                    string groupReason = groupNumber.HasValue
                        ? BotMechCommon.Who(gate.pawn) + " only has " + available + " control groups."
                        : "Say the control group number. " + BotMechCommon.Who(gate.pawn) + " has " + available + ".";
                    return Status("no_such_group", groupReason, new BotMechGroupMoveDetails
                    {
                        groups = available,
                        asked = groupNumber
                    });
                }

                bool unrestricted = string.Equals(areaName, "unrestricted", StringComparison.OrdinalIgnoreCase);
                bool hasArea = !string.IsNullOrEmpty(areaName);
                Area area = null;
                if (hasArea && !unrestricted)
                {
                    area = BotMechCommon.FindPlayerArea(BotMechCommon.CurrentMap(gate.pawn), areaName);
                    if (area == null)
                    {
                        return Status(
                            "no_such_area",
                            "There is no player area named " + areaName + ".",
                            new BotMechGroupMoveDetails { area = areaName });
                    }
                }

                var tracker = gate.pawn.mechanitor;
                BotMechCommon.GrowGroups(tracker, groupNumber.Value);
                if (tracker?.controlGroups == null || tracker.controlGroups.Count < groupNumber.Value)
                {
                    return Status(
                        "no_such_group",
                        BotMechCommon.Who(gate.pawn) + " only has " + available + " control groups.",
                        new BotMechGroupMoveDetails { groups = available, asked = groupNumber });
                }

                MechanitorControlGroup target = tracker.controlGroups[groupNumber.Value - 1];
                if (target == null)
                {
                    return Status(
                        "no_such_group",
                        BotMechCommon.Who(gate.pawn) + " only has " + available + " control groups.",
                        new BotMechGroupMoveDetails { groups = available, asked = groupNumber });
                }

                string sampleLabel = BotMechCommon.KindLabel(matches[0]);
                string sampleDef = BotMechCommon.KindDefName(matches[0]);
                int moved = 0;
                int already = 0;
                var skipped = new List<BotMechNameReason>();
                var areaMissed = new List<BotMechNameReason>();

                foreach (var mech in matches)
                {
                    if (mech == null) continue;
                    string skip = BotMechCommon.GroupSkipReason(mech);
                    if (skip != null)
                    {
                        skipped.Add(new BotMechNameReason { label = BotMechCommon.PawnLabel(mech), reason = skip });
                        continue;
                    }

                    int current = 0;
                    try { current = mech.GetMechControlGroup()?.Index ?? 0; }
                    catch { current = 0; }

                    bool needGroup = current != groupNumber.Value;
                    bool needArea = false;
                    if (hasArea)
                    {
                        string now = BotMechCommon.AreaName(mech);
                        string wanted = unrestricted ? "unrestricted" : BotJson.WithoutColor(area.Label);
                        needArea = !string.Equals(now, wanted, StringComparison.OrdinalIgnoreCase);
                    }

                    if (!needGroup && !needArea)
                    {
                        already++;
                        continue;
                    }

                    bool grouped = false;
                    if (needGroup)
                    {
                        try
                        {
                            target.Assign(mech);
                            grouped = true;
                        }
                        catch
                        {
                            skipped.Add(new BotMechNameReason
                            {
                                label = BotMechCommon.PawnLabel(mech),
                                reason = "Could not change control group."
                            });
                            continue;
                        }
                    }

                    if (needArea && !TrySetArea(mech, unrestricted, area, areaName, out string why))
                    {
                        if (!grouped)
                        {
                            skipped.Add(new BotMechNameReason { label = BotMechCommon.PawnLabel(mech), reason = why });
                            continue;
                        }

                        areaMissed.Add(new BotMechNameReason { label = BotMechCommon.PawnLabel(mech), reason = why });
                    }

                    moved++;
                }

                string status;
                string reason;
                if (moved > 0)
                {
                    status = "moved";
                    reason = "Moved " + moved + " " + BotMechCommon.Plural(sampleLabel, moved) + " into control group " + groupNumber.Value + ".";
                    if (hasArea)
                        reason += unrestricted
                            ? " Their allowed area is unrestricted."
                            : " Their allowed area is " + areaName + ".";
                    if (skipped.Count > 0)
                        reason += " Some were skipped.";
                }
                else if (already > 0)
                {
                    status = "already_there";
                    reason = already == 1
                        ? "That " + sampleLabel + " is already in control group " + groupNumber.Value + "."
                        : "Those " + BotMechCommon.Plural(sampleLabel, already) + " are already in control group " + groupNumber.Value + ".";
                    if (hasArea)
                        reason += unrestricted
                            ? " The allowed area is already unrestricted."
                            : " The allowed area is already " + areaName + ".";
                }
                else
                {
                    status = "moved";
                    reason = "None of those " + BotMechCommon.Plural(sampleLabel, matches.Count) + " could be moved.";
                }

                return Status(status, reason, new BotMechGroupMoveDetails
                {
                    kind = sampleDef,
                    label = sampleLabel,
                    group = groupNumber,
                    area = hasArea ? (unrestricted ? "unrestricted" : areaName) : null,
                    moved = moved,
                    alreadyThere = already,
                    skipped = skipped.Count > 0 ? skipped : null,
                    areaMissed = areaMissed.Count > 0 ? areaMissed : null
                });
            }
            catch (Exception ex)
            {
                return BotMapHelper.ErrorExceptionJson(ex.Message);
            }
        }

        private static string Check(Pawn pawn)
        {
            int available = BotMechCommon.AvailableGroups(pawn);
            var groups = new List<BotMechGroupInfo>();
            try
            {
                var tracker = pawn.mechanitor;
                if (tracker?.controlGroups != null)
                {
                    foreach (var group in tracker.controlGroups)
                    {
                        if (group == null) continue;
                        groups.Add(Describe(group));
                    }
                }
            }
            catch { /* ignore */ }

            groups.Sort((a, b) => a.number.CompareTo(b.number));
            string who = BotMechCommon.Who(pawn);
            string whose = who == "They" ? "their" : who == "He" ? "his" : "her";
            return CheckStatus("ok", "These are " + whose + " current control groups.", new BotMechGroupCheckDetails
            {
                pawn = BotMechCommon.PawnLabel(pawn),
                availableGroups = available,
                groups = groups
            });
        }

        private static BotMechGroupInfo Describe(MechanitorControlGroup group)
        {
            var info = new BotMechGroupInfo
            {
                number = group.Index,
                workMode = group.WorkMode?.defName,
                mechs = new List<BotMechGroupKindInfo>()
            };

            var buckets = new Dictionary<string, BotMechGroupKindInfo>(StringComparer.OrdinalIgnoreCase);
            if (group.AssignedMechs == null)
                return info;

            foreach (var entry in group.AssignedMechs)
            {
                Pawn mech = entry?.pawn;
                if (mech == null || mech.Destroyed || mech.Dead) continue;
                string def = BotMechCommon.KindDefName(mech);
                if (!buckets.TryGetValue(def, out var kind))
                {
                    kind = new BotMechGroupKindInfo
                    {
                        defName = def,
                        label = BotMechCommon.KindLabel(mech),
                        areas = new List<BotMechAreaCount>()
                    };
                    buckets[def] = kind;
                    info.mechs.Add(kind);
                }

                kind.count++;
                string area = BotMechCommon.AreaName(mech);
                BotMechAreaCount slot = null;
                foreach (var existing in kind.areas)
                {
                    if (string.Equals(existing.name, area, StringComparison.OrdinalIgnoreCase))
                    {
                        slot = existing;
                        break;
                    }
                }

                if (slot == null)
                {
                    slot = new BotMechAreaCount { name = area, count = 0 };
                    kind.areas.Add(slot);
                }

                slot.count++;
            }

            info.mechs.Sort((a, b) => string.Compare(a.defName, b.defName, StringComparison.Ordinal));
            return info;
        }

        private static bool TrySetArea(Pawn mech, bool unrestricted, Area area, string areaName, out string why)
        {
            why = null;
            try
            {
                if (mech?.playerSettings == null)
                {
                    why = "No area controls.";
                    return false;
                }

                Map map = mech.MapHeld;
                if (map == null)
                {
                    why = "That mech is not on a map.";
                    return false;
                }

                if (unrestricted)
                {
                    mech.playerSettings.AreaRestrictionInPawnCurrentMap = null;
                    return true;
                }

                Area use = area;
                if (use == null || use.Map != map)
                    use = BotMechCommon.FindPlayerArea(map, areaName);
                if (use == null)
                {
                    why = "That area is not on the mech's map.";
                    return false;
                }

                mech.playerSettings.AreaRestrictionInPawnCurrentMap = use;
                return true;
            }
            catch
            {
                why = "Could not set the allowed area.";
                return false;
            }
        }

        private static bool IsCheck(string[] args)
        {
            if (args == null || args.Length == 0)
                return false;
            bool any = false;
            foreach (var raw in args)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;
                any = true;
                if (!string.Equals(raw, "check", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return any;
        }

        private static void Parse(string[] args, out string kind, out int? groupNumber, out string areaName)
        {
            kind = null;
            groupNumber = null;
            areaName = null;
            if (args == null)
                return;

            var kindParts = new List<string>();
            var areaParts = new List<string>();
            foreach (var raw in args)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;
                if (!groupNumber.HasValue && int.TryParse(raw, out int number))
                {
                    groupNumber = number;
                    continue;
                }

                if (!groupNumber.HasValue)
                    kindParts.Add(raw);
                else
                    areaParts.Add(raw);
            }

            if (kindParts.Count > 0)
                kind = string.Join(" ", kindParts);
            if (areaParts.Count > 0)
                areaName = string.Join(" ", areaParts);
        }

        private static string Status(string status, string reason, BotMechGroupMoveDetails details)
        {
            return BotJson.Serialize(new BotMechGroupReply
            {
                command = "botmechgroup",
                status = status,
                reason = reason,
                details = details
            });
        }

        private static string CheckStatus(string status, string reason, BotMechGroupCheckDetails details)
        {
            return BotJson.Serialize(new BotMechGroupCheckReply
            {
                command = "botmechgroup",
                status = status,
                reason = reason,
                details = details
            });
        }
    }

    internal class BotMechGroupReply
    {
        public string command;
        public string status;
        public string reason;
        public BotMechGroupMoveDetails details;
    }

    internal class BotMechGroupCheckReply
    {
        public string command;
        public string status;
        public string reason;
        public BotMechGroupCheckDetails details;
    }

    internal class BotMechGroupMoveDetails
    {
        public string kind;
        public string label;
        public int? group;
        public int? asked;
        public int? groups;
        public string area;
        public int? moved;
        public int? alreadyThere;
        public List<BotMechNameReason> skipped;
        public List<BotMechNameReason> areaMissed;
    }

    internal class BotMechGroupCheckDetails
    {
        public string pawn;
        public int availableGroups;
        public List<BotMechGroupInfo> groups;
    }

    internal class BotMechGroupInfo
    {
        public int number;
        public string workMode;
        public List<BotMechGroupKindInfo> mechs;
    }

    internal class BotMechGroupKindInfo
    {
        public string defName;
        public string label;
        public int count;
        public List<BotMechAreaCount> areas;
    }

    internal class BotMechAreaCount
    {
        public string name;
        public int count;
    }
}
