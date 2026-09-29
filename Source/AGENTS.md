# RICS Chatbot Addon — Grok Build notes (RimWorld 1.6)

Optional **RICS addon** (`Captolamia.RICS.ChatbotAddon`). Not the main RICS tree (`[CAP] Chat Interactive`). Do not edit RICS core from this folder.

Moderator-only commands return **JSON gamestate** for an external AI bot (Masie) on the RICS AI file/HTTP path — not public viewer chat.

## Trees

- Mod root: `Mods\[CAP] RICS addon for Chat bots\`
- Namespace / assembly: `CAP_RICS_ChatbotAddon`
- GitHub: `ekudram/cap-RICS-Chatbot-Addon`
- DLL: `1.6/Assemblies/CAP_RICS_ChatbotAddon.dll`
- Load **after** `Captolamia.RICS` or `Captolamia.RICS.Beta`
- HintPath compiles against DEV RICS DLL. **Private=false** — do not copy Harmony, Newtonsoft, or RICS into this addon’s Assemblies (PostBuild deletes extras).

## Version (ask first)

**Current work: 1.0.0.** There is **no** `VersionHistory.cs`.

| File | Role |
|------|------|
| `About/About.xml` `<modVersion>` | RimWorld / Workshop version (source of truth) |
| `changelog.txt` (mod root) | Steam Workshop paste — **commit this** |
| `Source/VERSION_HISTORY.md` | Local agent notes — **gitignored** |

Ask before bumping. Same version → append both history files. New version → bump About, new heading in both files. In-progress date line: **Pre-Release**.

## Commands

Defs: `1.6/Defs/Commands/BotCommands.xml`. Classes: `Commands/BotCommands.cs` + `Handlers/`. `permissionLevel` moderator, `excludeFromPricelist` true.

`!botstate` `!botpawns` `!botpawncheck` `!botthreats` `!botmap` `!botwealth` `!botresources` `!botgear` `!botfaction` `!botowned` `!botdisown`

JSON via `BotJson.Serialize` only — **no `dynamic`**. Changing JSON fields or command names needs a **rics-handoff** for Masie.

## Logging

`Log.Message` with `[RICS Chatbot Addon]` (see `ChatbotAddonMod`). Players should not see `[CAP]` on addon lines.

## Translations

Keyed XML is not `string.Format`. `{0}` only — never `{0:N0}`. Keys under `1.6/Languages/` if added.

## Code

- Prefer RICS types already referenced (`ChatCommand`, `ChatMessageWrapper`). Do not duplicate RICS command registration.
- Harmony is not required in this addon unless a future patch needs it.
- Read vanilla 1.6 at `...\RimWorld\Source\RIMWORLD 1.6\Assembly-CSharp\` when checking pawn/map/wealth APIs.
- Null-check; do not crash the game.
