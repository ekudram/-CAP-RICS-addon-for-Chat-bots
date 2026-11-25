// BotSleepHandler.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt
//
// Send the assigned pawn to bed. Pass check to preview, floor to allow the ground.

using System;
using CAP_ChatInteractive;
using RimWorld;
using Verse;
using Verse.AI;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    internal static class BotSleepHandler
    {
        private const float DefaultThreshold = 30f;

        public static string Build(ChatMessageWrapper user, string[] args)
        {
            try
            {
                BotCareArgs parsed = BotCareCommon.ParseArgs(args);
                float threshold = parsed.percent ?? DefaultThreshold;
                Pawn pawn = BotCareCommon.AssignedPawn(user);
                BotCareState state = BotCareCommon.BuildState(pawn);
                BotCareReply blocked = BotCareCommon.Blocked("botsleep", pawn, state);
                if (blocked != null)
                    return Finish(blocked, parsed.check);

                float? rest = state.rest;
                if (!rest.HasValue)
                {
                    return Finish(new BotCareReply
                    {
                        command = "botsleep",
                        status = "busy_unavailable",
                        reason = BotCareCommon.Pronoun(pawn) + " has no rest need.",
                        state = state
                    }, parsed.check);
                }

                if (rest.Value >= threshold)
                {
                    return Finish(new BotCareReply
                    {
                        command = "botsleep",
                        status = "not_tired",
                        reason = "Rest is " + rest.Value.ToString("0.#") + "%, which is not below " + threshold.ToString("0.#") + "%.",
                        state = state
                    }, parsed.check);
                }

                if (state.hostiles)
                {
                    return Finish(new BotCareReply
                    {
                        command = "botsleep",
                        status = "unsafe",
                        reason = "Hostiles are active on the map.",
                        state = state
                    }, parsed.check);
                }

                if (RestUtility.DisturbancePreventsLyingDown(pawn) || !RestUtility.CanFallAsleep(pawn))
                {
                    return Finish(new BotCareReply
                    {
                        command = "botsleep",
                        status = "busy_unavailable",
                        reason = BotCareCommon.Pronoun(pawn) + " cannot fall asleep right now.",
                        state = state
                    }, parsed.check);
                }

                Building_Bed bed = null;
                try
                {
                    bed = RestUtility.FindBedFor(pawn);
                }
                catch { /* ignore */ }

                if (bed != null)
                {
                    string bedLabel = BotCareCommon.ThingLabel(bed) ?? "bed";
                    string room = BotCareCommon.PlaceLabel(pawn, bed.Position);
                    var reply = new BotCareReply
                    {
                        command = "botsleep",
                        status = "sleeping",
                        reason = BotCareCommon.Pronoun(pawn) + " will lie down in the " + bedLabel + " (" + room + ").",
                        state = state,
                        bed = bedLabel,
                        room = room
                    };
                    if (!parsed.check)
                    {
                        try
                        {
                            BotCareCommon.StartJob(pawn, JobMaker.MakeJob(JobDefOf.LayDown, bed));
                        }
                        catch (Exception ex)
                        {
                            reply.status = "busy_unavailable";
                            reply.reason = "The bed could not be reserved.";
                            reply.bed = null;
                            reply.room = null;
                            if (!string.IsNullOrEmpty(ex.Message))
                                reply.reason = "The bed could not be reserved. " + ex.Message;
                        }
                    }
                    return Finish(reply, parsed.check);
                }

                if (!parsed.floor)
                {
                    return Finish(new BotCareReply
                    {
                        command = "botsleep",
                        status = "no_bed",
                        reason = "No free bed " + BotCareCommon.Pronoun(pawn).ToLowerInvariant() + " can use. Pass floor to sleep on the ground.",
                        state = state
                    }, parsed.check);
                }

                if (!TryFindGroundCell(pawn, out IntVec3 cell))
                {
                    return Finish(new BotCareReply
                    {
                        command = "botsleep",
                        status = "no_bed",
                        reason = "No free bed or ground spot in the allowed area.",
                        state = state
                    }, parsed.check);
                }

                string groundRoom = BotCareCommon.PlaceLabel(pawn, cell);
                var ground = new BotCareReply
                {
                    command = "botsleep",
                    status = "sleeping",
                    reason = BotCareCommon.Pronoun(pawn) + " will sleep on the ground (" + groundRoom + ").",
                    state = state,
                    bed = "ground",
                    room = groundRoom
                };
                if (!parsed.check)
                {
                    try
                    {
                        BotCareCommon.StartJob(pawn, JobMaker.MakeJob(JobDefOf.LayDown, cell));
                    }
                    catch (Exception ex)
                    {
                        ground.status = "busy_unavailable";
                        ground.reason = "The ground spot could not be reserved.";
                        ground.bed = null;
                        ground.room = null;
                        if (!string.IsNullOrEmpty(ex.Message))
                            ground.reason = ground.reason + " " + ex.Message;
                    }
                }
                return Finish(ground, parsed.check);
            }
            catch (Exception ex)
            {
                return BotMapHelper.ErrorExceptionJson(ex.Message);
            }
        }

        private static string Finish(BotCareReply reply, bool check)
        {
            if (check)
            {
                reply.check = true;
                if (!string.IsNullOrEmpty(reply.reason) && reply.reason.IndexOf("Dry run.", StringComparison.Ordinal) < 0)
                    reply.reason = "Dry run. " + reply.reason;
            }
            return BotJson.Serialize(reply);
        }

        private static bool TryFindGroundCell(Pawn pawn, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (pawn?.Map == null) return false;
            if (IsValidSleepCell(pawn, pawn.Position))
            {
                cell = pawn.Position;
                return true;
            }

            float best = float.MaxValue;
            bool found = false;
            try
            {
                foreach (IntVec3 candidate in GenRadial.RadialCellsAround(pawn.Position, 12f, true))
                {
                    if (!IsValidSleepCell(pawn, candidate)) continue;
                    float dist = pawn.Position.DistanceToSquared(candidate);
                    if (dist >= best) continue;
                    best = dist;
                    cell = candidate;
                    found = true;
                }
            }
            catch
            {
                return false;
            }
            return found;
        }

        private static bool IsValidSleepCell(Pawn pawn, IntVec3 cell)
        {
            try
            {
                Map map = pawn.Map;
                if (map == null || !cell.IsValid || !cell.InBounds(map) || !cell.Standable(map))
                    return false;
                if (cell.IsForbidden(pawn))
                    return false;
                TerrainDef terrain = cell.GetTerrain(map);
                if (terrain == null || terrain.avoidWander)
                    return false;
                return pawn.CanReserve(cell);
            }
            catch
            {
                return false;
            }
        }
    }
}
