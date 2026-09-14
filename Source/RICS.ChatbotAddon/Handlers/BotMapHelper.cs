// BotMapHelper.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt

using System.Collections.Generic;
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
        /// Map Masie should describe for colony vibe (landing / camera), not the first home in Find.Maps.
        /// Combat still uses GetCombatMap.
        /// </summary>
        public static Map GetFocusMap()
        {
            Map current = Find.CurrentMap;
            if (current != null && !current.Disposed && MapIsPlayerFocus(current))
                return current;

            Map gravHome = null;
            Map bestHome = null;
            int bestColonists = -1;
            if (Find.Maps != null)
            {
                foreach (var map in Find.Maps)
                {
                    if (map == null || map.Disposed) continue;
                    if (!map.IsPlayerHome) continue;
                    if (MapWasGravship(map) && gravHome == null)
                        gravHome = map;
                    int n = 0;
                    try { n = map.mapPawns?.FreeColonistsSpawnedCount ?? 0; }
                    catch { }
                    if (n > bestColonists)
                    {
                        bestColonists = n;
                        bestHome = map;
                    }
                }
            }

            return gravHome ?? bestHome ?? GetPlayerMap();
        }

        public static bool MapWasGravship(Map map)
        {
            try
            {
                return map != null && map.wasSpawnedViaGravShipLanding;
            }
            catch
            {
                return false;
            }
        }

        public static bool MapIsPlayerFocus(Map map)
        {
            if (map == null) return false;
            if (map.IsPlayerHome) return true;
            if (MapWasGravship(map)) return true;
            if (MapHasFieldHostiles(map)) return true;
            try
            {
                if (map.ParentFaction == Faction.OfPlayer)
                    return true;
            }
            catch { }
            return false;
        }

        public static int StandingHostileCount(Map map)
        {
            int n = 0;
            try
            {
                var spawned = map?.mapPawns?.AllPawnsSpawned;
                if (spawned == null) return 0;
                foreach (var p in spawned)
                {
                    if (p == null || p.Dead || p.Destroyed || p.Downed) continue;
                    if (p.IsPrisonerOfColony || p.IsSlaveOfColony) continue;
                    if (p.Faction != null && p.Faction.IsPlayer) continue;
                    if (p.HostileTo(Faction.OfPlayer)
                        || (p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer)))
                        n++;
                }
            }
            catch { }
            return n;
        }

        public static List<BotMapRosterEntry> BuildMapsRoster()
        {
            var list = new List<BotMapRosterEntry>();
            Map current = Find.CurrentMap;
            if (Find.Maps == null)
                return list;
            foreach (var map in Find.Maps)
            {
                if (map == null || map.Disposed) continue;
                int colonists = 0;
                try { colonists = map.mapPawns?.FreeColonistsSpawnedCount ?? 0; }
                catch { }
                list.Add(new BotMapRosterEntry
                {
                    uniqueId = map.uniqueID,
                    name = map.Parent?.Label ?? map.ToString(),
                    isPlayerHome = map.IsPlayerHome,
                    gravship = MapWasGravship(map),
                    isCurrent = current != null && map.uniqueID == current.uniqueID,
                    colonists = colonists,
                    hostilesStanding = StandingHostileCount(map)
                });
            }
            return list;
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

    internal class BotMapRosterEntry
    {
        public int uniqueId;
        public string name;
        public bool isPlayerHome;
        public bool gravship;
        public bool isCurrent;
        public int colonists;
        public int hostilesStanding;
    }
}
