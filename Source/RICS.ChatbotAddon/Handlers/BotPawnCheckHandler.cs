// BotPawnCheckHandler.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt
//
// Compact JSON health for the AI bot: hediff flags + rest vs healer-serum advice.

using System;
using System.Collections.Generic;
using System.Linq;
using CAP_ChatInteractive;
using RimWorld;
using Verse;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    internal static class BotPawnCheckHandler
    {
        private const int MaxHediffs = 16;

        public static string Build(ChatMessageWrapper user, string[] args)
        {
            try
            {
                Pawn pawn = ResolvePawn(user, args);
                if (pawn == null || pawn.Destroyed)
                    return BotJson.Serialize(new BotPawnCheckPayload
                    {
                        status = "no_pawn",
                        command = "botpawncheck"
                    });

                if (pawn.Dead)
                    return BotJson.Serialize(new BotPawnCheckPayload
                    {
                        status = "dead",
                        command = "botpawncheck",
                        name = pawn.LabelShortCap,
                        downed = true,
                        recommendation = "none"
                    });

                var problems = new List<BotHediffEntry>();
                int bleedN = 0, restN = 0, healN = 0, richN = 0;
                try
                {
                    var list = pawn.health?.hediffSet?.hediffs;
                    if (list != null)
                    {
                        foreach (var h in list)
                        {
                            if (h == null || h.def == null) continue;
                            var entry = Classify(pawn, h);
                            if (entry == null) continue;
                            if (entry.advice == "ignore") continue;
                            problems.Add(entry);
                            if (entry.bleeding) bleedN++;
                            if (entry.advice == "rest") restN++;
                            if (entry.advice == "healpawn") healN++;
                            if (entry.advice == "healpawn_if_rich") richN++;
                        }
                    }
                }
                catch { /* partial */ }

                problems = problems
                    .OrderBy(e => AdviceRank(e.advice))
                    .ThenByDescending(e => e.severity)
                    .Take(MaxHediffs)
                    .ToList();

                string rec = "none";
                if (healN > 0) rec = "healpawn";
                else if (richN > 0) rec = "healpawn_if_rich";
                else if (restN > 0) rec = "rest";

                float healthPct = 100f;
                try
                {
                    if (pawn.health?.summaryHealth != null)
                        healthPct = (float)Math.Round(pawn.health.summaryHealth.SummaryHealthPercent * 100.0, 1);
                }
                catch { }

                float bleedRate = 0f;
                try { bleedRate = pawn.health?.hediffSet?.BleedRateTotal ?? 0f; }
                catch { }

                bool inBed = false;
                try { inBed = pawn.InBed(); }
                catch { }

                return BotJson.Serialize(new BotPawnCheckPayload
                {
                    status = "ok",
                    command = "botpawncheck",
                    name = pawn.LabelShortCap,
                    downed = pawn.Downed,
                    inBed = inBed,
                    healthPct = healthPct,
                    bleedRate = Math.Round(bleedRate, 3),
                    bleedingCount = bleedN,
                    restCount = restN,
                    healCount = healN,
                    healIfRichCount = richN,
                    recommendation = rec,
                    hediffs = problems
                });
            }
            catch (Exception ex)
            {
                return BotMapHelper.ErrorExceptionJson(ex.Message);
            }
        }

        private static int AdviceRank(string advice)
        {
            if (advice == "healpawn") return 0;
            if (advice == "healpawn_if_rich") return 1;
            if (advice == "rest") return 2;
            return 3;
        }

        private static Pawn ResolvePawn(ChatMessageWrapper user, string[] args)
        {
            string query = args != null ? string.Join(" ", args.Where(a => !string.IsNullOrWhiteSpace(a))).Trim() : "";
            if (query.StartsWith("@"))
                query = query.Substring(1).Trim();

            var mgr = CAPChatInteractiveMod.GetPawnAssignmentManager();
            if (string.IsNullOrWhiteSpace(query))
                return mgr?.GetAssignedPawn(user);

            Pawn assigned = mgr?.GetAssignedPawn(query);
            if (assigned != null) return assigned;

            foreach (var map in Find.Maps)
            {
                var list = map?.mapPawns?.FreeColonistsSpawned;
                if (list == null) continue;
                foreach (var p in list)
                {
                    if (p == null) continue;
                    if (string.Equals(p.LabelShort, query, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(p.LabelShortCap, query, StringComparison.OrdinalIgnoreCase)
                        || (p.Name != null && p.Name.ToStringFull.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                        return p;
                }
            }
            return mgr?.GetAssignedPawn(user);
        }

        private static BotHediffEntry Classify(Pawn pawn, Hediff h)
        {
            // Hidden hediffs (bionic leftover "Removed" femur, covered missing parts)
            // are not on the Health tab — do not send them to the bot.
            try
            {
                if (!h.Visible)
                    return null;
            }
            catch { }

            bool isBad = h.def.isBad;
            bool isImplant = h.def.countsAsAddedPartOrImplant
                             || (h.def.addedPartProps != null);
            bool isInjury = h is Hediff_Injury;
            bool isMissing = h is Hediff_MissingPart;
            if (isMissing && MissingPartReplacedByProsthetic(pawn, h))
                return null;
            bool isScar = false;
            try
            {
                if (isInjury && h.IsPermanent())
                    isScar = true;
            }
            catch { }
            if (!isScar)
            {
                string lb = (h.Label ?? h.def.label ?? "").ToLowerInvariant();
                if (lb.IndexOf("scar", StringComparison.OrdinalIgnoreCase) >= 0)
                    isScar = true;
            }

            bool bleeding = false;
            try { bleeding = h.Bleeding; }
            catch { }

            bool tended = false;
            try { tended = HediffUtility.IsTended(h); }
            catch { }

            bool tendableNow = false;
            try { tendableNow = h.TendableNow(); }
            catch { }

            bool isInfection = false;
            bool isDisease = false;
            try
            {
                if (h.def.makesSickThought)
                    isDisease = true;
                string dn = h.def.defName ?? "";
                string lab = h.def.label ?? "";
                if (dn.IndexOf("Infect", StringComparison.OrdinalIgnoreCase) >= 0
                    || lab.IndexOf("infect", StringComparison.OrdinalIgnoreCase) >= 0
                    || dn.IndexOf("WoundInfect", StringComparison.OrdinalIgnoreCase) >= 0)
                    isInfection = true;
                if (h is HediffWithComps hwc && hwc.TryGetComp<HediffComp_Immunizable>() != null)
                    isDisease = true;
            }
            catch { }

            bool isBrain = false;
            try
            {
                var part = h.Part;
                if (part?.def != null &&
                    string.Equals(part.def.defName, "Brain", StringComparison.OrdinalIgnoreCase))
                    isBrain = true;
            }
            catch { }

            bool beneficial = !isBad && !bleeding && !isMissing && !isInfection && !isDisease;
            if (isImplant && !isBad)
                beneficial = true;

            // Skip buffs / implants / genes
            if (beneficial)
                return null;
            if (!isBad && !isInjury && !isMissing && !bleeding && !tendableNow && !isInfection && !isDisease)
                return null;

            string advice = "ignore";
            if (bleeding || tendableNow || isInfection || isDisease || isMissing || isBrain)
                advice = "healpawn";
            else if (isScar || (isInjury && h.IsPermanent()))
                advice = "healpawn_if_rich";
            else if (isInjury && (tended || !tendableNow) && !bleeding)
                advice = "rest";
            else if (isInjury)
                advice = "healpawn";

            float pain = 0f;
            try { pain = h.PainOffset; }
            catch { }

            return new BotHediffEntry
            {
                label = h.LabelCap ?? h.def.label,
                part = h.Part?.LabelCap,
                isBad = isBad,
                bleeding = bleeding,
                tended = tended,
                tendableNow = tendableNow,
                isInjury = isInjury,
                isScar = isScar,
                isMissingPart = isMissing,
                isInfection = isInfection,
                isDisease = isDisease,
                isImplant = isImplant,
                isBeneficial = beneficial,
                severity = Math.Round(h.Severity, 3),
                pain = Math.Round(pain, 3),
                advice = advice
            };
        }

        /// <summary>
        /// True when a missing-part hediff is leftover under a bionic / added part
        /// (Health tab hides these; Label often "Removed").
        /// </summary>
        private static bool MissingPartReplacedByProsthetic(Pawn pawn, Hediff h)
        {
            if (!(h is Hediff_MissingPart) || pawn?.health?.hediffSet?.hediffs == null)
                return false;

            try
            {
                if (!h.Bleeding && !h.TendableNow())
                {
                    string lab = h.Label ?? h.def?.label ?? "";
                    if (lab.IndexOf("Removed", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            catch { }

            var missingPart = h.Part;
            if (missingPart == null)
                return false;

            foreach (var other in pawn.health.hediffSet.hediffs)
            {
                if (other == null || other.def == null)
                    continue;
                bool implant = other.def.countsAsAddedPartOrImplant
                               || other.def.addedPartProps != null
                               || other is Hediff_AddedPart
                               || other is Hediff_Implant;
                if (!implant)
                    continue;
                var ip = other.Part;
                if (ip == null)
                    continue;
                if (PartIsOrAncestorOf(ip, missingPart) || PartIsOrAncestorOf(missingPart, ip))
                    return true;
            }
            return false;
        }

        private static bool PartIsOrAncestorOf(BodyPartRecord ancestor, BodyPartRecord part)
        {
            var p = part;
            while (p != null)
            {
                if (p == ancestor)
                    return true;
                p = p.parent;
            }
            return false;
        }
    }

    internal class BotPawnCheckPayload
    {
        public string status;
        public string command;
        public string name;
        public bool downed;
        public bool inBed;
        public double healthPct;
        public double bleedRate;
        public int bleedingCount;
        public int restCount;
        public int healCount;
        public int healIfRichCount;
        public string recommendation;
        public List<BotHediffEntry> hediffs;
    }

    internal class BotHediffEntry
    {
        public string label;
        public string part;
        public bool isBad;
        public bool bleeding;
        public bool tended;
        public bool tendableNow;
        public bool isInjury;
        public bool isScar;
        public bool isMissingPart;
        public bool isInfection;
        public bool isDisease;
        public bool isImplant;
        public bool isBeneficial;
        public double severity;
        public double pain;
        public string advice;
    }
}
