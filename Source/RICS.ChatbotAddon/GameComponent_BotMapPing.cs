// GameComponent_BotMapPing.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt
//
// Map ping is Options > Controls > RICS chatbot > map ping (default F8).
// A keyboard binding pings the cell under the cursor. A mouse binding pings on
// release only when the cursor barely moved, so middle-mouse pan still works if rebound.
// RimWorld instantiates every GameComponent subclass from Game.FillComponents.

using System;
using System.Collections.Generic;
using System.Globalization;
using CAP_ChatInteractive;
using CAP_ChatInteractive.AI;
using CAP_RICS_ChatbotAddon.Handlers;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace CAP_RICS_ChatbotAddon
{
    public class GameComponent_BotMapPing : GameComponent
    {
        private const float ClickSlopPx = 8f;
        private const float DebounceSeconds = 0.5f;

        private KeyCode _mouseTrack = KeyCode.None;
        private Vector2 _pressPos;
        private float _lastPingAt = -999f;

        public GameComponent_BotMapPing(Game game)
        {
        }

        public override void GameComponentOnGUI()
        {
            try
            {
                KeyBindingDef bind = BotKeyBindingDefOf.RICSChatbot_MapPing;
                if (bind == null || !bind.KeyDownEvent)
                    return;

                TryPing();
                if (Event.current != null && Event.current.type == EventType.KeyDown)
                    Event.current.Use();
            }
            catch (Exception ex)
            {
                Log.Warning("[RICS Chatbot Addon] Map ping input failed: " + ex.Message);
            }
        }

        public override void GameComponentUpdate()
        {
            try
            {
                KeyBindingDef bind = BotKeyBindingDefOf.RICSChatbot_MapPing;
                if (bind == null)
                    return;

                // KeyDownEvent never sees mouse buttons. JustPressed does.
                if (bind.JustPressed)
                {
                    KeyCode mouse = MouseButtonJustPressed();
                    if (mouse != KeyCode.None)
                    {
                        _mouseTrack = mouse;
                        _pressPos = (Vector2)Input.mousePosition;
                    }
                }

                if (_mouseTrack == KeyCode.None || !Input.GetKeyUp(_mouseTrack))
                    return;

                Vector2 delta = (Vector2)Input.mousePosition - _pressPos;
                _mouseTrack = KeyCode.None;
                if (delta.sqrMagnitude > ClickSlopPx * ClickSlopPx)
                    return;

                TryPing();
            }
            catch (Exception ex)
            {
                _mouseTrack = KeyCode.None;
                Log.Warning("[RICS Chatbot Addon] Map ping mouse input failed: " + ex.Message);
            }
        }

        private static KeyCode MouseButtonJustPressed()
        {
            for (KeyCode code = KeyCode.Mouse0; code <= KeyCode.Mouse6; code++)
            {
                if (Input.GetKeyDown(code))
                    return code;
            }
            return KeyCode.None;
        }

        private void TryPing()
        {
            if (Current.ProgramState != ProgramState.Playing)
                return;
            if (LongEventHandler.ShouldWaitForEvent)
                return;
            if (!WorldRendererUtility.DrawingMap)
                return;

            Map map = Find.CurrentMap;
            if (map == null || map.Disposed)
                return;

            try
            {
                if (Find.WindowStack != null &&
                    Find.WindowStack.GetWindowAt(UI.MousePositionOnUIInverted) != null)
                    return;
            }
            catch
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now - _lastPingAt < DebounceSeconds)
                return;

            IntVec3 cell = UI.MouseCell();
            if (!cell.IsValid || !cell.InBounds(map))
                return;

            _lastPingAt = now;

            bool isRoom = BotRoomHelper.TryGetRoomLabel(map, cell, out string roomLabel);
            string place = isRoom ? roomLabel : BuildAreaPhrase(map, cell);
            if (string.IsNullOrEmpty(place))
                place = "open ground";

            string toast = "[RICS Chatbot Addon] Ping: " + place;
            try
            {
                Messages.Message(toast, MessageTypeDefOf.NeutralEvent, false);
            }
            catch (Exception ex)
            {
                Log.Warning("[RICS Chatbot Addon] Map ping toast failed: " + ex.Message);
            }

            if (!TrySendEvent(map, cell, isRoom, roomLabel, place))
                Log.Message("[RICS Chatbot Addon] Map ping noted (AI chatbot off or unavailable): " + place);
        }

        private static bool TrySendEvent(Map map, IntVec3 cell, bool isRoom, string roomLabel, string place)
        {
            try
            {
                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (settings == null || !settings.AIChatBotActive)
                    return false;

                var gameComp = Current.Game?.GetComponent<CAPChatInteractive_GameComponent>();
                var ai = gameComp?._aiChatBotService;
                if (ai == null)
                    return false;

                string botName = string.IsNullOrWhiteSpace(settings.AIChatBotName)
                    ? "Masie"
                    : settings.AIChatBotName.Trim();

                string mapPhrase = BotRoomHelper.SafeMapPhrase(map);
                string climate = JoinSafe(SafeBiome(map), SafeWeather(map));
                string where = string.IsNullOrEmpty(climate) ? mapPhrase : mapPhrase + " (" + climate + ")";

                string detail = isRoom
                    ? "[Room: " + roomLabel + "]"
                    : "Place: " + place;

                string message = botName + ", map ping on " + where + ". " + detail;
                if (BotRoomHelper.ContainsHotWord(message))
                    message = botName + ", map ping on " + mapPhrase + ".";

                AiMapLocation location = AIChatBotService.TryCreateMapLocation(map, cell);
                if (location != null)
                    location.mapLabel = mapPhrase;

                AiMapSlicePayload slice = AiMapSliceBuilder.TryBuild(map, cell, AiMapSliceBuilder.SliceSizeLetter);

                ai.NotifyColonyEvent(message, location, slice);
                Log.Message("[RICS Chatbot Addon] Map ping sent: " + place);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning("[RICS Chatbot Addon] Map ping event failed: " + ex.Message);
                return false;
            }
        }

        private static string BuildAreaPhrase(Map map, IntVec3 cell)
        {
            var parts = new List<string>();
            try
            {
                RoofDef roof = cell.GetRoof(map);
                string roofText = roof == null ? "unroofed" : (roof.LabelCap.ToString() ?? "roof");
                Add(parts, roofText);

                TerrainDef terrain = cell.GetTerrain(map);
                Add(parts, terrain?.label);

                Zone zone = cell.GetZone(map);
                if (zone != null && !string.IsNullOrWhiteSpace(zone.label))
                    Add(parts, "zone " + zone.label);

                float temp = cell.GetTemperature(map);
                Add(parts, Math.Round(temp).ToString(CultureInfo.InvariantCulture) + "C");
            }
            catch { /* partial area is fine */ }

            try
            {
                AiMapSlicePayload slice = AiMapSliceBuilder.TryBuild(map, cell, AiMapSliceBuilder.SliceSizeToast);
                Add(parts, slice?.relativeToColony);
            }
            catch { /* direction is optional */ }

            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        private static string SafeBiome(Map map)
        {
            try { return BotRoomHelper.SafeFragment(map?.Biome?.label); }
            catch { return null; }
        }

        private static string SafeWeather(Map map)
        {
            try { return BotRoomHelper.SafeFragment(map?.weatherManager?.curWeather?.label); }
            catch { return null; }
        }

        private static string JoinSafe(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b;
            if (string.IsNullOrEmpty(b)) return a;
            return a + ", " + b;
        }

        private static void Add(List<string> parts, string text)
        {
            string clean = BotRoomHelper.SafeFragment(text);
            if (!string.IsNullOrEmpty(clean))
                parts.Add(clean);
        }
    }
}
