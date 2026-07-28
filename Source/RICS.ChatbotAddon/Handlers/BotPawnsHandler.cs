// BotPawnsHandler.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    internal static class BotPawnsHandler
    {
        public static string Build()
        {
            try
            {
                Map map = BotMapHelper.GetPlayerMap();
                if (map == null)
                    return BotMapHelper.ErrorNoMapJson();

                var pawns = map.mapPawns?.FreeColonistsSpawned?.ToList() ?? new List<Pawn>();
                var list = new List<BotPawnEntry>(pawns.Count);

                foreach (var pawn in pawns)
                {
                    if (pawn == null || pawn.Dead) continue;
                    list.Add(BuildPawn(pawn));
                }

                var payload = new BotPawnsPayload
                {
                    status = "ok",
                    command = "botpawns",
                    count = list.Count,
                    pawns = list
                };

                return BotJson.Serialize(payload);
            }
            catch (Exception ex)
            {
                return BotMapHelper.ErrorExceptionJson(ex.Message);
            }
        }

        private static BotPawnEntry BuildPawn(Pawn pawn)
        {
            string job = null;
            try
            {
                job = pawn.jobs?.curJob?.def?.label ?? pawn.jobs?.curDriver?.GetReport();
            }
            catch { /* ignore */ }

            float? mood = null;
            try
            {
                if (pawn.needs?.mood != null)
                    mood = (float)Math.Round(pawn.needs.mood.CurLevelPercentage * 100f, 1);
            }
            catch { /* ignore */ }

            float healthPct = 1f;
            try
            {
                if (pawn.health?.summaryHealth != null)
                    healthPct = pawn.health.summaryHealth.SummaryHealthPercent;
            }
            catch { /* ignore */ }

            var skills = new List<BotSkillEntry>();
            try
            {
                if (pawn.skills?.skills != null)
                {
                    foreach (var s in pawn.skills.skills)
                    {
                        if (s?.def == null) continue;
                        skills.Add(new BotSkillEntry
                        {
                            skill = s.def.defName,
                            level = s.Level,
                            passion = s.passion.ToString()
                        });
                    }
                }
            }
            catch { /* ignore */ }

            return new BotPawnEntry
            {
                name = pawn.Name?.ToStringFull ?? pawn.LabelShort,
                shortName = pawn.LabelShort,
                gender = pawn.gender.ToString(),
                age = pawn.ageTracker != null ? (int)pawn.ageTracker.AgeBiologicalYears : 0,
                healthPct = Math.Round(healthPct * 100.0, 1),
                moodPct = mood,
                downed = pawn.Downed,
                draftable = pawn.drafter != null,
                drafted = pawn.Drafted,
                job = job,
                skills = skills
            };
        }
    }

    internal class BotPawnsPayload
    {
        public string status;
        public string command;
        public int count;
        public List<BotPawnEntry> pawns;
    }

    internal class BotPawnEntry
    {
        public string name;
        public string shortName;
        public string gender;
        public int age;
        public double healthPct;
        public float? moodPct;
        public bool downed;
        public bool draftable;
        public bool drafted;
        public string job;
        public List<BotSkillEntry> skills;
    }

    internal class BotSkillEntry
    {
        public string skill;
        public int level;
        public string passion;
    }
}
