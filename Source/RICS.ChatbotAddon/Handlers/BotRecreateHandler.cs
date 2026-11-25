// BotRecreateHandler.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt
//
// Send the assigned pawn to a joy job the game itself considers valid.

using System;
using System.Collections.Generic;
using System.Text;
using CAP_ChatInteractive;
using RimWorld;
using Verse;
using Verse.AI;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    internal static class BotRecreateHandler
    {
        private const float DefaultThreshold = 35f;
        private const float TooTiredThreshold = 25f;

        public static string Build(ChatMessageWrapper user, string[] args)
        {
            try
            {
                BotCareArgs parsed = BotCareCommon.ParseArgs(args);
                float threshold = parsed.percent ?? DefaultThreshold;
                Pawn pawn = BotCareCommon.AssignedPawn(user);
                BotCareState state = BotCareCommon.BuildState(pawn);
                BotCareReply blocked = BotCareCommon.Blocked("botrecreate", pawn, state);
                if (blocked != null)
                    return Finish(blocked, parsed.check);

                if (!state.joy.HasValue)
                {
                    return Finish(new BotCareReply
                    {
                        command = "botrecreate",
                        status = "busy_unavailable",
                        reason = BotCareCommon.Pronoun(pawn) + " has no recreation need.",
                        state = state
                    }, parsed.check);
                }

                if (state.rest.HasValue && state.rest.Value < TooTiredThreshold)
                {
                    return Finish(new BotCareReply
                    {
                        command = "botrecreate",
                        status = "too_tired",
                        reason = "Rest is " + state.rest.Value.ToString("0.#") + "%. Use !botsleep.",
                        state = state
                    }, parsed.check);
                }

                if (state.joy.Value >= threshold)
                {
                    return Finish(new BotCareReply
                    {
                        command = "botrecreate",
                        status = "not_bored",
                        reason = "Recreation is " + state.joy.Value.ToString("0.#") + "%, which is not below " + threshold.ToString("0.#") + "%.",
                        state = state
                    }, parsed.check);
                }

                if (state.hostiles)
                {
                    return Finish(new BotCareReply
                    {
                        command = "botrecreate",
                        status = "unsafe",
                        reason = "Hostiles are active on the map.",
                        state = state
                    }, parsed.check);
                }

                JoyChoice best = PickJoy(pawn, out List<string> why);
                if (best == null)
                {
                    return Finish(new BotCareReply
                    {
                        command = "botrecreate",
                        status = "nothing_available",
                        reason = "No recreation job is available.",
                        state = state,
                        why = why
                    }, parsed.check);
                }

                string activity = string.IsNullOrEmpty(best.giver?.label) ? best.job?.def?.label : best.giver.label;
                string kind = best.giver?.joyKind?.label ?? best.giver?.joyKind?.defName;
                string target = TargetLabel(best.job);
                var reply = new BotCareReply
                {
                    command = "botrecreate",
                    status = "started",
                    reason = BotCareCommon.Pronoun(pawn) + " will " + (activity ?? "relax") + ".",
                    state = state,
                    activity = activity,
                    joyKind = kind,
                    target = target,
                    distance = (float)Math.Round(best.distance, 1)
                };
                if (!parsed.check)
                {
                    try
                    {
                        BotCareCommon.StartJob(pawn, best.job);
                    }
                    catch (Exception ex)
                    {
                        reply.status = "nothing_available";
                        reply.reason = "The recreation job could not be started.";
                        reply.activity = null;
                        reply.joyKind = null;
                        reply.target = null;
                        reply.distance = null;
                        reply.why = new List<string> { string.IsNullOrEmpty(ex.Message) ? "could not reserve the target" : ex.Message };
                    }
                }
                return Finish(reply, parsed.check);
            }
            catch (Exception ex)
            {
                return BotMapHelper.ErrorExceptionJson(ex.Message);
            }
        }

        private static JoyChoice PickJoy(Pawn pawn, out List<string> why)
        {
            why = new List<string>();
            var choices = new List<JoyChoice>();
            int bored = 0;
            int eligible = 0;
            int missingCapacity = 0;
            bool outdoorsBad = false;
            string outdoorsReason = null;

            try
            {
                var fail = new StringBuilder();
                if (!JoyUtility.EnjoyableOutsideNow(pawn, fail))
                {
                    outdoorsBad = true;
                    outdoorsReason = OutsideReason(pawn, fail.ToString());
                }
            }
            catch { /* ignore */ }

            List<JoyGiverDef> defs = null;
            try { defs = DefDatabase<JoyGiverDef>.AllDefsListForReading; }
            catch { /* ignore */ }
            if (defs == null)
            {
                why.Add("no recreation jobs are loaded");
                return null;
            }

            for (int i = 0; i < defs.Count; i++)
            {
                JoyGiverDef def = defs[i];
                if (def?.Worker == null || def.joyKind == null) continue;
                try
                {
                    if (pawn.needs?.joy?.tolerances != null && pawn.needs.joy.tolerances.BoredOf(def.joyKind))
                    {
                        bored++;
                        continue;
                    }
                    if (!PawnEverDoes(def, pawn))
                        continue;
                    if (def.Worker.MissingRequiredCapacity(pawn) != null)
                    {
                        missingCapacity++;
                        continue;
                    }
                    if (!def.Worker.CanBeGivenTo(pawn))
                        continue;
                    eligible++;
                    Job job = def.Worker.TryGiveJob(pawn);
                    if (job == null) continue;
                    choices.Add(new JoyChoice
                    {
                        giver = def,
                        job = job,
                        tolerance = pawn.needs.joy.tolerances[def.joyKind],
                        distance = JobDistance(pawn, job)
                    });
                }
                catch { /* one bad giver does not fail the command */ }
            }

            if (choices.Count > 0)
            {
                choices.Sort(CompareChoices);
                return choices[0];
            }

            if (!AnyJoyThingInArea(pawn, out bool anyFree))
                why.Add("no joy buildings in allowed area");
            else if (!anyFree)
                why.Add("all reserved");
            if (outdoorsBad && !string.IsNullOrEmpty(outdoorsReason))
                why.Add(outdoorsReason);
            if (missingCapacity > 0 && eligible == 0)
                why.Add("missing a required capacity");
            if (bored > 0 && eligible == 0)
                why.Add("bored of the available joy kinds");
            if (why.Count == 0)
                why.Add("no reachable recreation");
            return null;
        }

        private static int CompareChoices(JoyChoice a, JoyChoice b)
        {
            int tol = a.tolerance.CompareTo(b.tolerance);
            if (tol != 0) return tol;
            return a.distance.CompareTo(b.distance);
        }

        private static bool PawnEverDoes(JoyGiverDef def, Pawn pawn)
        {
            if (def == null || def.pctPawnsEverDo >= 1f)
                return true;
            Rand.PushState(pawn.thingIDNumber ^ 63216713);
            bool ok = Rand.Value < def.pctPawnsEverDo;
            Rand.PopState();
            return ok;
        }

        private static bool AnyJoyThingInArea(Pawn pawn, out bool anyFree)
        {
            anyFree = false;
            bool any = false;
            if (pawn?.Map == null) return false;
            List<JoyGiverDef> defs;
            try { defs = DefDatabase<JoyGiverDef>.AllDefsListForReading; }
            catch { return false; }
            if (defs == null) return false;

            for (int i = 0; i < defs.Count; i++)
            {
                List<ThingDef> things = defs[i]?.thingDefs;
                if (things == null) continue;
                for (int t = 0; t < things.Count; t++)
                {
                    ThingDef def = things[t];
                    if (def == null) continue;
                    List<Thing> found = pawn.Map.listerThings?.ThingsOfDef(def);
                    if (found == null) continue;
                    for (int n = 0; n < found.Count; n++)
                    {
                        Thing thing = found[n];
                        if (thing == null || thing.Destroyed) continue;
                        try
                        {
                            if (thing.IsForbidden(pawn)) continue;
                            if (!pawn.CanReach(thing, PathEndMode.Touch, Danger.Some)) continue;
                            any = true;
                            if (pawn.CanReserve(thing))
                                anyFree = true;
                        }
                        catch { /* ignore */ }
                    }
                }
            }
            return any;
        }

        private static string OutsideReason(Pawn pawn, string raw)
        {
            try
            {
                float temp = pawn.Map.mapTemperature.OutdoorTemp;
                FloatRange comfort = pawn.ComfortableTemperatureRange();
                if (!comfort.Includes(temp))
                    return temp < comfort.min ? "too cold outside" : "too hot outside";
            }
            catch { /* ignore */ }
            string clean = BotJson.WithoutColor(raw ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(clean))
                return "not enjoyable outside";
            return clean;
        }

        private static float JobDistance(Pawn pawn, Job job)
        {
            try
            {
                if (pawn == null || job == null || !job.targetA.IsValid)
                    return 0f;
                if (job.targetA.HasThing && job.targetA.Thing != null)
                    return pawn.Position.DistanceTo(job.targetA.Thing.Position);
                if (job.targetA.Cell.IsValid)
                    return pawn.Position.DistanceTo(job.targetA.Cell);
            }
            catch { /* ignore */ }
            return 0f;
        }

        private static string TargetLabel(Job job)
        {
            try
            {
                Thing thing = job?.targetA.Thing;
                if (thing != null)
                    return BotCareCommon.ThingLabel(thing) ?? "target";
                if (job != null && job.targetA.IsValid)
                    return "a spot";
            }
            catch { /* ignore */ }
            return null;
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

        private sealed class JoyChoice
        {
            public JoyGiverDef giver;
            public Job job;
            public float tolerance;
            public float distance;
        }
    }
}
