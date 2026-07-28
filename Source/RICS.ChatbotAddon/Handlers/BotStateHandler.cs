// BotStateHandler.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    internal static class BotStateHandler
    {
        public static string Build()
        {
            try
            {
                Map map = BotMapHelper.GetPlayerMap();
                if (map == null)
                    return BotMapHelper.ErrorNoMapJson();

                var tickManager = Find.TickManager;
                var worldGrid = Find.WorldGrid;
                var tile = map.Tile;
                int colonistCount = map.mapPawns?.FreeColonistsSpawnedCount ?? 0;
                if (colonistCount < 1) colonistCount = 1;

                int meals = 0, medicine = 0;
                CountFoodAndMedicine(map, out meals, out medicine);

                float mealsPer = (float)meals / colonistCount;
                float medsPer = (float)medicine / colonistCount;
                string foodStatus = mealsPer > 18 ? "Abundant" : mealsPer > 9 ? "Good" : "Low";
                string medStatus = medsPer > 12 ? "Good" : medsPer > 4 ? "Okay" : "Low";

                long absTicks = GenDate.TickGameToAbs(tickManager.TicksGame);
                var longLat = worldGrid.LongLatOf(tile);

                var payload = new BotStatePayload
                {
                    status = "ok",
                    command = "botstate",
                    day = GenDate.DaysPassed,
                    hour = GenDate.HourOfDay(tickManager.TicksGame, longLat.x),
                    colonists = map.mapPawns?.FreeColonistsSpawnedCount ?? 0,
                    threatPoints = StorytellerUtility.DefaultThreatPointsNow(map),
                    season = GenDate.Season(absTicks, longLat).ToString(),
                    storyteller = Find.Storyteller?.def?.label ?? "Unknown",
                    colony = new BotColonyBlock
                    {
                        name = map.Parent?.Label ?? "Unknown Colony",
                        biome = map.Biome?.label ?? "Unknown",
                        outdoorTempC = map.mapTemperature != null
                            ? Math.Round(map.mapTemperature.OutdoorTemp, 1)
                            : 0
                    },
                    food = new BotStatusCount { total = meals, status = foodStatus },
                    medicine = new BotStatusCount { total = medicine, status = medStatus },
                    weather = BuildWeather(map)
                };

                return BotJson.Serialize(payload);
            }
            catch (Exception ex)
            {
                return BotMapHelper.ErrorExceptionJson(ex.Message);
            }
        }

        private static void CountFoodAndMedicine(Map map, out int meals, out int medicine)
        {
            meals = 0;
            medicine = 0;
            try
            {
                var counter = map.resourceCounter;
                if (counter == null) return;

                foreach (var kvp in counter.AllCountedAmounts)
                {
                    var def = kvp.Key;
                    if (def == null) continue;
                    int count = kvp.Value;

                    if (def.IsMedicine)
                        medicine += count;

                    if (def.ingestible != null)
                    {
                        var cats = def.thingCategories ?? new List<ThingCategoryDef>();
                        bool isMeal = cats.Any(c => c.defName.Equals("Meals", StringComparison.OrdinalIgnoreCase))
                                      || (def.ingestible.foodType & FoodTypeFlags.Meal) != 0
                                      || (def.ingestible.foodType & FoodTypeFlags.Processed) != 0;
                        if (isMeal)
                            meals += count;
                    }
                }
            }
            catch
            {
                // partial ok
            }
        }

        private static BotWeatherBlock BuildWeather(Map map)
        {
            try
            {
                var wm = map?.weatherManager;
                var cur = wm?.curWeather;
                return new BotWeatherBlock
                {
                    current = cur?.label ?? "Unknown",
                    currentDef = cur?.defName,
                    last = wm?.lastWeather?.label
                };
            }
            catch
            {
                return new BotWeatherBlock { current = "Unknown" };
            }
        }
    }

    internal class BotStatePayload
    {
        public string status;
        public string command;
        public int day;
        public int hour;
        public int colonists;
        public float threatPoints;
        public string season;
        public string storyteller;
        public BotColonyBlock colony;
        public BotStatusCount food;
        public BotStatusCount medicine;
        public BotWeatherBlock weather;
    }

    internal class BotColonyBlock
    {
        public string name;
        public string biome;
        public double outdoorTempC;
    }

    internal class BotStatusCount
    {
        public int total;
        public string status;
    }

    internal class BotWeatherBlock
    {
        public string current;
        public string currentDef;
        public string last;
    }
}
