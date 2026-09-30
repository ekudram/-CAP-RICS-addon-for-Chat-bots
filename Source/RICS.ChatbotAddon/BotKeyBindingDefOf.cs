// BotKeyBindingDefOf.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt

using System.Collections.Generic;
using RimWorld;
using Verse;

namespace CAP_RICS_ChatbotAddon
{
    [DefOf]
    public static class BotKeyBindingDefOf
    {
        public static KeyBindingDef RICSChatbot_MapPing;

        static BotKeyBindingDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BotKeyBindingDefOf));
        }
    }

    /// <summary>
    /// Options > Controls only marks a conflict for categories listed on this binding.
    /// Architect tabs and other mods are created after XML load, so add them here.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BotKeyConflictSetup
    {
        static BotKeyConflictSetup()
        {
            try
            {
                KeyBindingCategoryDef cat = DefDatabase<KeyBindingCategoryDef>.GetNamedSilentFail("RICSChatbot");
                if (cat == null)
                    return;
                if (cat.checkForConflicts == null)
                    cat.checkForConflicts = new List<KeyBindingCategoryDef>();

                List<KeyBindingCategoryDef> all = DefDatabase<KeyBindingCategoryDef>.AllDefsListForReading;
                if (all == null)
                    return;
                for (int i = 0; i < all.Count; i++)
                {
                    KeyBindingCategoryDef other = all[i];
                    if (other == null || other == cat)
                        continue;
                    if (!cat.checkForConflicts.Contains(other))
                        cat.checkForConflicts.Add(other);
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning("[RICS Chatbot Addon] Key binding conflict list failed: " + ex.Message);
            }
        }
    }
}
