// BotFactionHandler.cs
// Copyright (c) Captolamia — AGPLv3
//
// Bot-only faction lookup for Masie: name, race, relation, vanilla type, Ideology memes.

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    internal static class BotFactionHandler
    {
        public static string Build(string[] args)
        {
            try
            {
                if (Current.Game?.World?.factionManager == null)
                    return BotMapHelper.ErrorExceptionJson("no_world");

                bool ideologyActive = false;
                try { ideologyActive = ModsConfig.IdeologyActive; }
                catch { ideologyActive = false; }

                string query = args != null && args.Length > 0
                    ? string.Join(" ", args).Trim()
                    : null;

                var all = Current.Game.World.factionManager.AllFactionsVisible
                    .Where(f => f != null && !f.IsPlayer && !f.defeated)
                    .OrderByDescending(f => f.PlayerGoodwill)
                    .ToList();

                if (string.IsNullOrEmpty(query))
                {
                    var list = new BotFactionListPayload
                    {
                        status = "ok",
                        command = "botfaction",
                        ideologyActive = ideologyActive,
                        count = all.Count,
                        factions = all.Select(ToSummary).ToList()
                    };
                    return BotJson.Serialize(list);
                }

                Faction match = FindFaction(all, query);
                if (match == null)
                {
                    return BotJson.Serialize(new BotFactionMissPayload
                    {
                        status = "not_found",
                        command = "botfaction",
                        query = query,
                        ideologyActive = ideologyActive
                    });
                }

                var detail = new BotFactionDetailPayload
                {
                    status = "ok",
                    command = "botfaction",
                    ideologyActive = ideologyActive,
                    faction = ToDetail(match, ideologyActive)
                };
                return BotJson.Serialize(detail);
            }
            catch (Exception ex)
            {
                return BotMapHelper.ErrorExceptionJson(ex.Message);
            }
        }

        private static Faction FindFaction(List<Faction> all, string query)
        {
            string q = query.Trim();
            string ql = q.ToLowerInvariant();

            Faction exact = all.FirstOrDefault(f =>
                string.Equals(f.Name, q, StringComparison.OrdinalIgnoreCase)
                || string.Equals(f.def?.defName, q, StringComparison.OrdinalIgnoreCase)
                || string.Equals(f.def?.label, q, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact;

            return all.FirstOrDefault(f =>
                (f.Name != null && f.Name.IndexOf(ql, StringComparison.OrdinalIgnoreCase) >= 0)
                || (f.def?.defName != null && f.def.defName.IndexOf(ql, StringComparison.OrdinalIgnoreCase) >= 0)
                || (f.def?.label != null && f.def.label.IndexOf(ql, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private static BotFactionSummary ToSummary(Faction f)
        {
            return new BotFactionSummary
            {
                name = f.Name,
                defName = f.def?.defName,
                vanillaType = VanillaType(f),
                commonRace = CommonRace(f),
                relation = Relation(f),
                goodwill = f.PlayerGoodwill,
                hostileToPlayer = Hostile(f)
            };
        }

        private static BotFactionDetail ToDetail(Faction f, bool ideologyActive)
        {
            var d = new BotFactionDetail
            {
                name = f.Name,
                defName = f.def?.defName,
                vanillaType = VanillaType(f),
                commonRace = CommonRace(f),
                techLevel = f.def?.techLevel.ToString() ?? "Undefined",
                humanlike = f.def?.humanlikeFaction ?? false,
                permanentEnemy = f.def?.permanentEnemy ?? false,
                hidden = f.def?.hidden ?? false,
                relation = Relation(f),
                goodwill = f.PlayerGoodwill,
                hostileToPlayer = Hostile(f),
                temporary = f.temporary,
                ideology = BuildIdeology(f, ideologyActive)
            };
            return d;
        }

        private static BotIdeologyInfo BuildIdeology(Faction f, bool ideologyActive)
        {
            var info = new BotIdeologyInfo { present = false };
            if (!ideologyActive)
                return info;

            try
            {
                var ideo = f.ideos?.PrimaryIdeo;
                if (ideo == null)
                    return info;

                info.present = true;
                info.name = ideo.name;
                try { info.culture = ideo.culture?.label ?? ideo.culture?.defName; }
                catch { info.culture = null; }

                var major = new List<string>();
                string structure = null;
                if (ideo.memes != null)
                {
                    foreach (var meme in ideo.memes)
                    {
                        if (meme == null)
                            continue;
                        string label = meme.LabelCap.ToString();
                        if (string.IsNullOrEmpty(label))
                            label = meme.label ?? meme.defName;
                        try
                        {
                            if (meme.category == MemeCategory.Structure)
                            {
                                structure = label;
                                continue;
                            }
                        }
                        catch { /* category missing */ }
                        major.Add(label);
                    }
                }
                info.structureMeme = structure;
                info.majorMemes = major;
            }
            catch
            {
                info.present = false;
            }
            return info;
        }

        private static string Relation(Faction f)
        {
            try
            {
                switch (f.PlayerRelationKind)
                {
                    case FactionRelationKind.Ally: return "Ally";
                    case FactionRelationKind.Neutral: return "Neutral";
                    case FactionRelationKind.Hostile: return "Hostile";
                    default: return f.PlayerRelationKind.ToString();
                }
            }
            catch
            {
                return Hostile(f) ? "Hostile" : "Neutral";
            }
        }

        private static bool Hostile(Faction f)
        {
            try
            {
                return Faction.OfPlayer != null && f.HostileTo(Faction.OfPlayer);
            }
            catch
            {
                return f.def?.permanentEnemy == true;
            }
        }

        private static string CommonRace(Faction f)
        {
            try
            {
                var kind = f.def?.basicMemberKind;
                var race = kind?.race;
                if (race != null)
                    return race.label ?? race.defName;

                var kinds = f.def?.pawnGroupMakers;
                if (kinds != null)
                {
                    foreach (var pgm in kinds)
                    {
                        var opt = pgm?.options?.FirstOrDefault(o => o?.kind?.race != null);
                        if (opt?.kind?.race != null)
                            return opt.kind.race.label ?? opt.kind.race.defName;
                    }
                }
            }
            catch { }
            return null;
        }

        private static string VanillaType(Faction f)
        {
            string def = f.def?.defName ?? "";
            string label = f.def?.label ?? "";
            bool perm = f.def?.permanentEnemy ?? false;
            bool human = f.def?.humanlikeFaction ?? false;

            if (def.IndexOf("TwitchRaid", StringComparison.OrdinalIgnoreCase) >= 0
                || def.StartsWith("RICS_", StringComparison.OrdinalIgnoreCase))
                return "Twitch raid (temporary)";

            if (def.IndexOf("Pirate", StringComparison.OrdinalIgnoreCase) >= 0
                || label.IndexOf("pirate", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Pirate / raiders";

            if (def.IndexOf("Empire", StringComparison.OrdinalIgnoreCase) >= 0
                || def.Equals("StellarisEmpire", StringComparison.OrdinalIgnoreCase))
                return "Fallen Empire";

            if (def.IndexOf("Mechanoid", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Mechanoid";

            if (def.IndexOf("Insect", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Insect hive";

            if (def.IndexOf("Tribe", StringComparison.OrdinalIgnoreCase) >= 0
                || def.IndexOf("Tribal", StringComparison.OrdinalIgnoreCase) >= 0
                || def.IndexOf("Savage", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Tribe";

            if (def.IndexOf("Outlander", StringComparison.OrdinalIgnoreCase) >= 0
                || def.IndexOf("Civil", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Outlander";

            if (def.IndexOf("Ancient", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Ancients";

            if (perm && human)
                return "Raiders (permanent enemy)";

            if (perm && !human)
                return "Hostile non-human";

            if (!string.IsNullOrEmpty(label))
                return label;

            return def.Length > 0 ? def : "Unknown";
        }
    }

    internal class BotFactionListPayload
    {
        public string status;
        public string command;
        public bool ideologyActive;
        public int count;
        public List<BotFactionSummary> factions;
    }

    internal class BotFactionDetailPayload
    {
        public string status;
        public string command;
        public bool ideologyActive;
        public BotFactionDetail faction;
    }

    internal class BotFactionMissPayload
    {
        public string status;
        public string command;
        public string query;
        public bool ideologyActive;
    }

    internal class BotFactionSummary
    {
        public string name;
        public string defName;
        public string vanillaType;
        public string commonRace;
        public string relation;
        public int goodwill;
        public bool hostileToPlayer;
    }

    internal class BotFactionDetail
    {
        public string name;
        public string defName;
        public string vanillaType;
        public string commonRace;
        public string techLevel;
        public bool humanlike;
        public bool permanentEnemy;
        public bool hidden;
        public string relation;
        public int goodwill;
        public bool hostileToPlayer;
        public bool temporary;
        public BotIdeologyInfo ideology;
    }

    internal class BotIdeologyInfo
    {
        public bool present;
        public string name;
        public string culture;
        public string structureMeme;
        public List<string> majorMemes;
    }
}
