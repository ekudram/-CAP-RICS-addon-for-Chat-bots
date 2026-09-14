// BotMapHandler.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt

using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    internal static class BotMapHandler
    {
        public static string Build()
        {
            try
            {
                Map map = BotMapHelper.GetFocusMap();
                if (map == null)
                    return BotMapHelper.ErrorNoMapJson();

                var tickManager = Find.TickManager;
                var worldGrid = Find.WorldGrid;
                var tile = map.Tile;
                long absTicks = GenDate.TickGameToAbs(tickManager.TicksGame);
                var longLat = worldGrid.LongLatOf(tile);

                var wm = map.weatherManager;
                var cur = wm?.curWeather;

                var payload = new BotMapPayload
                {
                    status = "ok",
                    command = "botmap",
                    name = map.Parent?.Label ?? "Unknown",
                    sizeX = map.Size.x,
                    sizeZ = map.Size.z,
                    biome = map.Biome?.label ?? "Unknown",
                    biomeDef = map.Biome?.defName,
                    outdoorTempC = map.mapTemperature != null
                        ? Math.Round(map.mapTemperature.OutdoorTemp, 1)
                        : 0,
                    season = GenDate.Season(absTicks, longLat).ToString(),
                    hour = GenDate.HourOfDay(tickManager.TicksGame, longLat.x),
                    day = GenDate.DaysPassed,
                    weather = cur?.label ?? "Unknown",
                    weatherDef = cur?.defName,
                    lastWeather = wm?.lastWeather?.label,
                    uniqueId = map.uniqueID,
                    isPlayerHome = map.IsPlayerHome,
                    gravship = BotMapHelper.MapWasGravship(map),
                    isCurrent = Find.CurrentMap != null && map.uniqueID == Find.CurrentMap.uniqueID,
                    mapCount = Find.Maps?.Count ?? 0,
                    maps = BotMapHelper.BuildMapsRoster()
                };

                return BotJson.Serialize(payload);
            }
            catch (Exception ex)
            {
                return BotMapHelper.ErrorExceptionJson(ex.Message);
            }
        }
    }

    internal class BotMapPayload
    {
        public string status;
        public string command;
        public string name;
        public int sizeX;
        public int sizeZ;
        public string biome;
        public string biomeDef;
        public double outdoorTempC;
        public string season;
        public int hour;
        public int day;
        public string weather;
        public string weatherDef;
        public string lastWeather;
        public int uniqueId;
        public bool isPlayerHome;
        public bool gravship;
        public bool isCurrent;
        public int mapCount;
        public List<BotMapRosterEntry> maps;
    }
}
