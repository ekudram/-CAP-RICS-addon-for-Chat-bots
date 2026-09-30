// Patch_AiEventRoom.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt
//
// Adds the room name onto text RICS is about to write for Masie.
// Does not edit RICS source. Outdoors and missing cells are left unchanged.

using System;
using CAP_ChatInteractive;
using CAP_ChatInteractive.AI;
using CAP_RICS_ChatbotAddon.Handlers;
using HarmonyLib;
using Verse;

namespace CAP_RICS_ChatbotAddon.Harmony
{
    [HarmonyPatch(typeof(AIChatBotService), nameof(AIChatBotService.NotifyColonyEvent))]
    public static class Patch_NotifyColonyEvent_Room
    {
        public static void Prefix(ref string message, AiMapLocation location)
        {
            try
            {
                message = BotRoomHelper.AppendRoom(message, location);
            }
            catch (Exception ex)
            {
                Log.Warning("[RICS Chatbot Addon] Room tag on colony event failed: " + ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(AIChatBotService), nameof(AIChatBotService.NotifyColonyMessage))]
    public static class Patch_NotifyColonyMessage_Room
    {
        public static void Prefix(ref string addressedMessage, ref string rawText, AiMapLocation location)
        {
            try
            {
                addressedMessage = BotRoomHelper.AppendRoom(addressedMessage, location);
                rawText = BotRoomHelper.AppendRoom(rawText, location);
            }
            catch (Exception ex)
            {
                Log.Warning("[RICS Chatbot Addon] Room tag on colony message failed: " + ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(CAPChatInteractive_GameComponent), nameof(CAPChatInteractive_GameComponent.RecordDeath))]
    public static class Patch_RecordDeath_Room
    {
        public static void Prefix(ref string deathMessage, AiMapLocation location)
        {
            try
            {
                deathMessage = BotRoomHelper.AppendRoom(deathMessage, location);
            }
            catch (Exception ex)
            {
                Log.Warning("[RICS Chatbot Addon] Room tag on death line failed: " + ex.Message);
            }
        }
    }
}
