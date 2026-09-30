// BotRoomHelper.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt
//
// Room name for AI event text, and a safe map phrase for player map pings.
// Words that Masie treats as raids, traders, deaths, and similar letters are left out.

using System;
using System.Text.RegularExpressions;
using CAP_ChatInteractive.AI;
using RimWorld;
using Verse;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    internal static class BotRoomHelper
    {
        public const string RoomToken = "[Room:";

        private static readonly string[] HotFragments =
        {
            "raid", "siege", "trader", "visitor", "quest", "crash", "fire",
            "manhunter", "died", "killed", "death", "euthan", "berserk",
            "ancient danger", "flashstorm", "eclipse", "heat wave", "cold snap",
            "toxic fallout", "volcanic", "meteorite", "cargo pod", "transport pod",
            "psychic", "prison break", "wedding", "marriage", "gave birth", "pregnant",
            "under attack", "gone mad"
        };

        /// <summary>
        /// Append " [Room: label]" when the location cell is an enclosed room.
        /// Returns the original text when there is no room, or the tag is already present.
        /// </summary>
        public static string AppendRoom(string message, AiMapLocation location)
        {
            if (string.IsNullOrEmpty(message) || location == null)
                return message;
            if (message.IndexOf(RoomToken, StringComparison.OrdinalIgnoreCase) >= 0)
                return message;

            Map map = FindMap(location.mapId);
            var cell = new IntVec3(location.x, location.y, location.z);
            if (!TryGetRoomLabel(map, cell, out string label))
                return message;

            return message + " [Room: " + label + "]";
        }

        /// <summary>
        /// Player-facing room name for a proper enclosed room. False for outdoors and role None.
        /// </summary>
        public static bool TryGetRoomLabel(Map map, IntVec3 cell, out string label)
        {
            label = null;
            try
            {
                if (map == null || map.Disposed || !cell.IsValid || !cell.InBounds(map))
                    return false;

                Room room = cell.GetRoom(map);
                if (room == null || !room.ProperRoom || room.PsychologicallyOutdoors)
                    return false;
                if (room.Role == null || room.Role == RoomRoleDefOf.None)
                    return false;

                string raw = room.GetRoomRoleLabel();
                string clean = SafeFragment(raw);
                label = string.IsNullOrEmpty(clean) ? "enclosed room" : clean;
                return true;
            }
            catch
            {
                label = null;
                return false;
            }
        }

        /// <summary>
        /// Map phrase for a ping. Avoids site names that would classify the event as a letter type.
        /// </summary>
        public static string SafeMapPhrase(Map map)
        {
            try
            {
                if (map == null)
                    return "an unknown location";

                bool grav = false;
                try { grav = map.wasSpawnedViaGravShipLanding; }
                catch { /* API variance */ }

                if (map.IsPlayerHome && grav)
                    return "the colony gravship (home map)";
                if (map.IsPlayerHome)
                    return "the home colony map";
                if (grav)
                    return "the colony gravship (remote map)";

                string parent = null;
                try { parent = map.Parent?.Label; }
                catch { /* ignore */ }

                string clean = SafeFragment(parent);
                if (string.IsNullOrEmpty(clean) || clean == "Map")
                    return "a remote map";
                return clean;
            }
            catch
            {
                return "an unknown location";
            }
        }

        public static string SafeFragment(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            string clean = StripMarkup(text).Trim();
            if (clean.Length == 0 || ContainsHotWord(clean))
                return null;
            return clean;
        }

        public static bool ContainsHotWord(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            string lower = text.ToLowerInvariant();
            for (int i = 0; i < HotFragments.Length; i++)
            {
                if (lower.IndexOf(HotFragments[i], StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        public static Map FindMap(int mapId)
        {
            try
            {
                if (Find.Maps == null)
                    return null;
                foreach (var map in Find.Maps)
                {
                    if (map != null && !map.Disposed && map.uniqueID == mapId)
                        return map;
                }
            }
            catch { /* ignore */ }
            return null;
        }

        private static string StripMarkup(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            return Regex.Replace(text, "<[^>]+>", "");
        }
    }
}
