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

        /// <summary>
        /// Prefer the map where hostiles are (current map, then any non-home fight, then home).
        /// Used by !botthreats so mission fights are not reported as the idle home map.
        /// </summary>
        public static Map GetCombatMap()
        {
            Map current = Find.CurrentMap;
            if (current != null && !current.Disposed && MapHasFieldHostiles(current))
                return current;

            Map firstHostile = null;
            if (Find.Maps != null)
            {
                foreach (var map in Find.Maps)
                {
                    if (map == null || map.Disposed) continue;
                    if (!MapHasFieldHostiles(map)) continue;
                    if (!map.IsPlayerHome)
                        return map;
                    if (firstHostile == null)
                        firstHostile = map;
                }
            }

            return firstHostile ?? GetPlayerMap();
        }

        public static bool MapHasFieldHostiles(Map map)
        {
            try
            {
                var spawned = map?.mapPawns?.AllPawnsSpawned;
                if (spawned == null) return false;
                foreach (var p in spawned)
                {
                    if (p == null || p.Dead || p.Destroyed) continue;
                    if (p.IsPrisonerOfColony || p.IsSlaveOfColony) continue;
                    if (p.Faction != null && p.Faction.IsPlayer) continue;
                    if (p.HostileTo(Faction.OfPlayer))
                        return true;
                    if (p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer))
                        return true;
                }
            }
            catch { }
            return false;
        }

        public static bool PlayerIsAggressorOn(Map map)
        {
            if (map == null) return false;
            if (map.IsPlayerHome) return false;
            return true;
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
