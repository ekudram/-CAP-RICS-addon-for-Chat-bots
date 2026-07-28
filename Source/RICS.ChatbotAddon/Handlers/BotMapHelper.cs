// BotMapHelper.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt

using System.Linq;
using RimWorld;
using Verse;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    /// <summary>Shared map resolution for bot gamestate queries (prefer player home).</summary>
    internal static class BotMapHelper
    {
        public static Map GetPlayerMap()
        {
            if (Current.Game == null)
                return null;

            Map home = Current.Game.Maps?.FirstOrDefault(m => m != null && m.IsPlayerHome && !m.Disposed)
                       ?? Find.AnyPlayerHomeMap
                       ?? Find.CurrentMap;

            if (home != null)
                return home;

            foreach (var map in Find.Maps)
            {
                if (map != null && map.ParentFaction == Faction.OfPlayer)
                    return map;
            }

            return null;
        }

        public static string ErrorNoMapJson()
        {
            return BotJson.Serialize(new BotErrorPayload { status = "error", message = "no_map" });
        }

        public static string ErrorExceptionJson(string message)
        {
            return BotJson.Serialize(new BotErrorPayload { status = "error", message = message ?? "unknown" });
        }
    }

    internal class BotErrorPayload
    {
        public string status;
        public string message;
    }
}
