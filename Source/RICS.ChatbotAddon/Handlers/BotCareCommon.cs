// BotCareCommon.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt
//
// Shared checks for !botsleep and !botrecreate.

using System;
using System.Collections.Generic;
using System.Globalization;
using CAP_ChatInteractive;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    internal static class BotCareCommon
    {
        public static BotCareArgs ParseArgs(string[] args)
        {
            var parsed = new BotCareArgs();
            if (args == null) return parsed;
            foreach (var raw in args)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                string token = raw.Trim();
                if (token.Equals("check", StringComparison.OrdinalIgnoreCase))
                {
                    parsed.check = true;
                    continue;
                }
                if (token.Equals("floor", StringComparison.OrdinalIgnoreCase))
                {
                    parsed.floor = true;
                    continue;
                }

                string number = token.EndsWith("%", StringComparison.Ordinal) ? token.Substring(0, token.Length - 1) : token;
                if (!float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                    continue;
                if (value <= 1f)
                    value *= 100f;
                if (value < 0f) value = 0f;
                if (value > 100f) value = 100f;
                parsed.percent = value;
            }
            return parsed;
        }

        public static Pawn AssignedPawn(ChatMessageWrapper user)
        {
            try
            {
                var mgr = CAPChatInteractiveMod.GetPawnAssignmentManager();
                return mgr?.GetAssignedPawn(user);
            }
            catch
            {
                return null;
            }
        }

        public static string Pronoun(Pawn pawn)
        {
            if (pawn == null) return "They";
            if (pawn.gender == Gender.Female) return "She";
            if (pawn.gender == Gender.Male) return "He";
            return "They";
        }

        public static float? NeedPercent(Need need)
        {
            try
            {
                if (need == null) return null;
                return (float)Math.Round(need.CurLevelPercentage * 100f, 1);
            }
            catch
            {
                return null;
            }
        }

        public static bool HostilesOn(Map map)
        {
            try
            {
                if (map == null) return false;
                return GenHostility.AnyHostileActiveThreatToPlayer(map);
            }
            catch
            {
                return false;
            }
        }

        public static BotCareState BuildState(Pawn pawn)
        {
            var state = new BotCareState
            {
                job = "idle",
                hostiles = false,
                location = "unknown"
            };
            if (pawn == null)
            {
                state.job = "none";
                state.location = "no assigned pawn";
                return state;
            }

            state.job = JobLabel(pawn);
            state.rest = NeedPercent(pawn.needs?.rest);
            state.joy = NeedPercent(pawn.needs?.joy);
            state.mood = NeedPercent(pawn.needs?.mood);
            state.hostiles = HostilesOn(pawn.Map);
            state.location = LocationLabel(pawn);

            string who = Pronoun(pawn);
            if (pawn.Dead || pawn.Destroyed)
                state.dead = who + " is dead.";
            if (!pawn.Spawned || pawn.Map == null)
                state.onMap = who + " is not on a map.";
            if (pawn.Drafted)
                state.drafted = "Drafted means " + who.ToLowerInvariant() + " is under direct combat control by the player and cannot be given care orders.";
            if (pawn.Downed)
                state.downed = who + " is downed.";
            if (pawn.InMentalState)
                state.mentalState = who + " is in a mental state (" + MentalLabel(pawn) + ").";
            if (IsPrisoner(pawn))
                state.imprisoned = who + " is imprisoned.";
            if (BeingTendedOrFedInBed(pawn))
                state.bedCare = who + " is in bed being tended or fed.";
            Lord lord = SafeLord(pawn);
            if (lord != null)
                state.lord = who + " is busy with " + LordLabel(lord) + ".";
            return state;
        }

        /// <summary>
        /// Shared refuse rules. Null when the pawn can be given a care job.
        /// </summary>
        public static BotCareReply Blocked(string command, Pawn pawn, BotCareState state)
        {
            if (pawn == null)
            {
                return new BotCareReply
                {
                    command = command,
                    status = "no_pawn",
                    reason = "No pawn is assigned.",
                    state = state ?? BuildState(null)
                };
            }

            string who = Pronoun(pawn);
            if (pawn.Dead || pawn.Destroyed)
                return Busy(command, who + " is dead.", state);
            if (!pawn.Spawned || pawn.Map == null)
                return Busy(command, who + " is not on a map.", state);
            if (pawn.Drafted)
            {
                return new BotCareReply
                {
                    command = command,
                    status = "drafted",
                    reason = state?.drafted ?? "Drafted means she is under direct combat control by the player and cannot be given care orders.",
                    state = state
                };
            }
            if (pawn.Downed)
                return Busy(command, who + " is downed.", state);
            if (pawn.InMentalState)
                return Busy(command, who + " is in a mental state (" + MentalLabel(pawn) + ").", state);
            if (BeingTendedOrFedInBed(pawn))
                return Busy(command, who + " is in bed being tended or fed.", state);
            if (IsPrisoner(pawn))
                return Busy(command, who + " is imprisoned.", state);
            if (SafeLord(pawn) != null)
                return Busy(command, who + " is in a group activity and cannot take a care order.", state);
            return null;
        }

        public static void StartJob(Pawn pawn, Job job)
        {
            if (pawn?.jobs == null)
                throw new InvalidOperationException("Pawn has no job tracker.");
            if (job == null)
                throw new InvalidOperationException("No job to start.");
            if (pawn.CurJob != null)
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
        }

        public static string PlaceLabel(Pawn pawn, IntVec3 cell)
        {
            try
            {
                if (pawn?.Map != null && BotRoomHelper.TryGetRoomLabel(pawn.Map, cell, out string room))
                    return room;
                if (pawn?.Map != null && cell.IsValid && cell.InBounds(pawn.Map))
                {
                    Room found = cell.GetRoom(pawn.Map);
                    if (found != null && found.PsychologicallyOutdoors)
                        return "outdoors";
                }
            }
            catch { /* ignore */ }
            return "unenclosed";
        }

        public static string ThingLabel(Thing thing)
        {
            if (thing == null) return null;
            if (!string.IsNullOrEmpty(thing.def?.label))
                return thing.def.label;
            return thing.LabelNoCount;
        }

        private static BotCareReply Busy(string command, string reason, BotCareState state)
        {
            return new BotCareReply
            {
                command = command,
                status = "busy_unavailable",
                reason = reason,
                state = state
            };
        }

        private static bool IsPrisoner(Pawn pawn)
        {
            try
            {
                return pawn.IsPrisoner;
            }
            catch
            {
                return false;
            }
        }

        private static bool BeingTendedOrFedInBed(Pawn pawn)
        {
            try
            {
                if (pawn == null || !pawn.InBed() || pawn.Map == null)
                    return false;
                var pawns = pawn.Map.mapPawns?.AllPawnsSpawned;
                if (pawns == null) return false;
                foreach (var other in pawns)
                {
                    if (other == null || other == pawn) continue;
                    Job job = other.CurJob;
                    if (job?.def == null) continue;
                    if (job.def != JobDefOf.TendPatient && job.def != JobDefOf.FeedPatient && job.def != JobDefOf.TakeToBedToOperate)
                        continue;
                    if (job.targetA.Thing == pawn || job.targetB.Thing == pawn)
                        return true;
                }
            }
            catch { /* ignore */ }
            return false;
        }

        private static Lord SafeLord(Pawn pawn)
        {
            try
            {
                return pawn?.GetLord();
            }
            catch
            {
                return null;
            }
        }

        private static string LordLabel(Lord lord)
        {
            try
            {
                string name = lord?.LordJob?.GetType().Name;
                if (!string.IsNullOrEmpty(name))
                {
                    if (name.StartsWith("LordJob_", StringComparison.Ordinal))
                        name = name.Substring("LordJob_".Length);
                    return name;
                }
            }
            catch { /* ignore */ }
            return "a group activity";
        }

        private static string MentalLabel(Pawn pawn)
        {
            try
            {
                string label = pawn.MentalState?.def?.label;
                if (!string.IsNullOrEmpty(label))
                    return label;
            }
            catch { /* ignore */ }
            return "a break";
        }

        private static string JobLabel(Pawn pawn)
        {
            try
            {
                if (pawn.CurJob == null)
                    return "idle";
                string report = pawn.CurJob.GetReport(pawn);
                if (!string.IsNullOrEmpty(report))
                    return BotJson.WithoutColor(report);
                return pawn.CurJob.def?.label ?? "working";
            }
            catch
            {
                return pawn?.CurJob?.def?.label ?? "working";
            }
        }

        private static string LocationLabel(Pawn pawn)
        {
            try
            {
                if (!pawn.Spawned || pawn.Map == null || !pawn.Position.IsValid)
                    return "not on a map";
                return PlaceLabel(pawn, pawn.Position);
            }
            catch
            {
                return "unknown";
            }
        }
    }

    internal class BotCareArgs
    {
        public bool check;
        public bool floor;
        public float? percent;
    }

    internal class BotCareState
    {
        public string job;
        public float? rest;
        public float? joy;
        public float? mood;
        public bool hostiles;
        public string location;
        public string drafted;
        public string dead;
        public string downed;
        public string mentalState;
        public string bedCare;
        public string imprisoned;
        public string lord;
        public string onMap;
    }

    internal class BotCareReply
    {
        public string command;
        public string status;
        public string reason;
        public BotCareState state;
        public bool? check;
        public string bed;
        public string room;
        public string activity;
        public string joyKind;
        public string target;
        public float? distance;
        public List<string> why;
    }
}
