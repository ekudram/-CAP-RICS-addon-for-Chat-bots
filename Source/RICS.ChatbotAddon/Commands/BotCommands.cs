// BotCommands.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt
//
// ChatCommand subclasses for bot-only gamestate queries.
// Registered via 1.6/Defs/Commands/BotCommands.xml (ChatCommandDef).

using CAP_ChatInteractive;
using CAP_RICS_ChatbotAddon.Handlers;

namespace CAP_RICS_ChatbotAddon.Commands
{
    public class BotStateCommand : ChatCommand
    {
        public override string Name => "botstate";
        public override string Description => "AI bot colony gamestate snapshot (JSON).";
        public override string Execute(ChatMessageWrapper user, string[] args) => BotStateHandler.Build();
    }

    public class BotPawnsCommand : ChatCommand
    {
        public override string Name => "botpawns";
        public override string Description => "AI bot free colonists list (JSON).";
        public override string Execute(ChatMessageWrapper user, string[] args) => BotPawnsHandler.Build();
    }

    public class BotThreatsCommand : ChatCommand
    {
        public override string Name => "botthreats";
        public override string Description => "AI bot hostiles / threats summary (JSON).";
        public override string Execute(ChatMessageWrapper user, string[] args) => BotThreatsHandler.Build();
    }

    public class BotMapCommand : ChatCommand
    {
        public override string Name => "botmap";
        public override string Description => "AI bot map info (JSON).";
        public override string Execute(ChatMessageWrapper user, string[] args) => BotMapHandler.Build();
    }

    public class BotWealthCommand : ChatCommand
    {
        public override string Name => "botwealth";
        public override string Description => "AI bot wealth breakdown (JSON).";
        public override string Execute(ChatMessageWrapper user, string[] args) => BotWealthHandler.Build();
    }

    public class BotResourcesCommand : ChatCommand
    {
        public override string Name => "botresources";
        public override string Description => "AI bot stockpile / resources (JSON).";
        public override string Execute(ChatMessageWrapper user, string[] args) => BotResourcesHandler.Build();
    }
}
