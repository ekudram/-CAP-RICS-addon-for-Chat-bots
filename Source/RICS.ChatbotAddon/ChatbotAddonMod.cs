// ChatbotAddonMod.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt

using Verse;

namespace CAP_RICS_ChatbotAddon
{
    /// <summary>
    /// Optional entry point so RimWorld loads the assembly and logs that the addon is active.
    /// Commands register via RICS ChatCommandDef XML + GameComponent_CommandsInitializer.
    /// </summary>
    public class ChatbotAddonMod : Mod
    {
        public ChatbotAddonMod(ModContentPack content) : base(content)
        {
            Log.Message("[RICS Chatbot Addon] Loaded. Bot gamestate commands (!botstate, !botpawns, …) register via RICS.");
        }
    }
}
