// ChatbotAddonMod.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt

using System.Reflection;
using Verse;

namespace CAP_RICS_ChatbotAddon
{
    /// <summary>
    /// Optional entry point so RimWorld loads the assembly and logs that the addon is active.
    /// Commands register via RICS ChatCommandDef XML + GameComponent_CommandsInitializer.
    /// Harmony only tags room names onto RICS AI event text. The map ping is a GameComponent.
    /// </summary>
    public class ChatbotAddonMod : Mod
    {
        public ChatbotAddonMod(ModContentPack content) : base(content)
        {
            var harmony = new HarmonyLib.Harmony("Captolamia.RICS.ChatbotAddon");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.Message("[RICS Chatbot Addon] Loaded. Bot gamestate commands (!botstate, !botpawns, …) register via RICS. Room names tag AI events. Map ping is Options > Controls > RICS chatbot (default F8).");
        }
    }
}
