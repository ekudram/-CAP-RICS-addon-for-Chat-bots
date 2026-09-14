// BotWealthHandler.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt

using System;
using RimWorld;
using Verse;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    internal static class BotWealthHandler
    {
        public static string Build()
        {
            try
            {
                Map map = BotMapHelper.GetFocusMap();
                if (map == null)
                    return BotMapHelper.ErrorNoMapJson();

                var ww = map.wealthWatcher;
                float total = ww?.WealthTotal ?? 0f;
                float items = ww?.WealthItems ?? 0f;
                float buildings = ww?.WealthBuildings ?? 0f;
                float pawns = ww?.WealthPawns ?? 0f;

                var payload = new BotWealthPayload
                {
                    status = "ok",
                    command = "botwealth",
                    total = Math.Round(total, 0),
                    items = Math.Round(items, 0),
                    buildings = Math.Round(buildings, 0),
                    pawns = Math.Round(pawns, 0),
                    threatPoints = StorytellerUtility.DefaultThreatPointsNow(map)
                };

                return BotJson.Serialize(payload);
            }
            catch (Exception ex)
            {
                return BotMapHelper.ErrorExceptionJson(ex.Message);
            }
        }
    }

    internal class BotWealthPayload
    {
        public string status;
        public string command;
        public double total;
        public double items;
        public double buildings;
        public double pawns;
        public float threatPoints;
    }
}
