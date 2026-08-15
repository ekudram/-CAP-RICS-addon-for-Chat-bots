# [CAP] RICS addon for Chat bots

Optional **RimWorld 1.6** addon for **[CAP] RICS** (Rimworld Interactive Chat Service).

Adds **moderator-level** commands that return **structured JSON gamestate** for an external AI chatbot (e.g. Masie). Commands are meant for the RICS **AI command path** (file bridge / `ProcessAICommand`), not public viewer chat — so responses are **not** truncated for Twitch/YouTube limits.

## Requirements

- RimWorld 1.6
- **[CAP] RICS** loaded first
  - Release `packageId`: `Captolamia.RICS`
  - DEV/Beta `packageId`: `Captolamia.RICS.Beta`

This addon does **not** ship Harmony, Newtonsoft, or RICS DLLs. RICS provides shared dependencies at runtime.

## Load order

1. Harmony (if required by RICS)
2. RICS (release or beta)
3. **This addon**

## Bot commands (v1)

| Command | Description |
|---------|-------------|
| `!botstate` | Colony snapshot (day, colonists, threat, food/med status, weather, storyteller) |
| `!botpawns` | Free colonists (name, gender, age, skills, health/mood, job) |
| `!botthreats` | Live threat check: hostile faction members (not prisoners/slaves), manhunters, scaria animals (one pawn, stacked flags), fires |
| `!botmap` | Map name, biome, temp, weather, season |
| `!botwealth` | Wealth total + items/buildings/pawns |
| `!botresources` | Stockpile counts (meals, meds, materials, components) |
| `!botgear` | Assigned pawn full loadout (equipment, sidearms, **all** inventory, apparel) plus a generic `weapons[]` list. Bot-only — not `!mypawn gear`. |

All use `permissionLevel` = **moderator** and `excludeFromPricelist` = **true** (hidden from the public RICS Pricelist site when RICS exports settings with that flag).

## Building (Visual Studio)

1. Open `Source/RICS.ChatbotAddon.sln` in Visual Studio.
2. Ensure RICS DEV is present at  
   `Mods\[CAP] Chat Interactive\1.6\Assemblies\[CAP] Chat Interactive.dll`  
   (or release `Mods\[CAP] RICS\1.6\Assemblies\...` — update HintPath in the csproj if needed).
3. Build **Release** or **Debug**. Output goes to `1.6/Assemblies/`.

## Git

Local repo lives in this mod folder. Use **GitHub Desktop** to create/publish the remote (suggested: `ekudram/cap-RICS-Chatbot-Addon`) and push/pull.

## License

AGPLv3 — see `LICENSE.txt`.
