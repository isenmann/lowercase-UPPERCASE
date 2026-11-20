# Shared levels page

A public page where players upload lowercase / UPPERCASE level codes and browse everyone's levels. It runs on
Cloudflare Workers with a D1 database, on the free plan. Only the code is stored, plus a key that spots the same level
uploaded twice. The browser works out names and previews from the code when the page opens. It follows the Shift
Register sharing page.

Live at https://lowercase-uppercase-levels.devplayrepeat.workers.dev.

| Path | What it is |
| --- | --- |
| `public/` | The page, served as it is. `sharecode.js` is shared with the Worker. |
| `src/worker.js` | `GET /api/levels` gives a page of codes (`?before=` takes the `next` of the previous page), `POST /api/levels` adds one (the body is the code). The high score calls go to `src/scores.js`. |
| `src/scores.js`, `src/names.js` | Online high scores: runs, level reports, the world list, the fastest finish per level, the name filter. |
| `src/run-limits.json` | What each of the 15 levels can pay, written by the game (see High scores). |
| `src/run-floors.json` | The fewest seconds each level can be finished in, kept by hand. |
| `schema.sql` | The database table. |
| `scripts/fonts.js` | Copies the game's fonts from `Assets/Fonts` into `public/fonts`. |
| `test/` | Tests for the code reading and the Worker. `vectors.json` holds what the game makes of a set of codes and levels. |

The game's C# code is the reference for level codes: `Assets/Scripts/LevelShareCode.cs` and
`Assets/Scripts/CustomLevelData.cs`. `test/vectors.json` holds its verdicts on a set of codes, and what every piece
of a few levels looks like once built. After changing level codes, the level format or how pieces are built, rerun
lowercase UPPERCASE > Shared Levels > Write Test Vectors in Unity, then `npm test`.

## Fonts

The page uses the game's fonts, which are third-party (SIL Open Font License) and kept out of git like `Assets/Fonts`.
`npm run dev` and `npm run deploy` copy them into `public/fonts` first, so `Assets/Fonts` must hold them. To copy them
by hand, run `npm run fonts`.

## Run it

Tested with Node 26. Nothing to install: `npx` fetches Wrangler, Cloudflare's tool, when needed.

```sh
npm test       # no network or account needed
npm run dev    # local copy with a local database, at http://localhost:8787
```

## Put it online

1. In `public/index.html`, replace `ITCH_URL` with the game's itch.io address.
2. Sign in and create the database, then copy the `database_id` it prints into `wrangler.jsonc`:

   ```sh
   npx wrangler@4 login
   npx wrangler@4 d1 create lowercase-uppercase-levels
   ```

3. Create the table and publish. The last command prints the page's address:

   ```sh
   npx wrangler@4 d1 execute lowercase-uppercase-levels --remote --file=schema.sql
   npm run deploy
   ```

The database id is not a secret, so it can be committed.

## Look after it

Limits: 5 uploads a minute per visitor (`wrangler.jsonc`), codes up to 64 KB (`MAX_BODY_LENGTH` in `src/limits.js`)
and 20000 levels in total (`MAX_LEVELS`). The list loads 50 levels at a time as people scroll (`PAGE_SIZE`). The total
counts arrivals: removing older levels frees no room, so after a flood empty the table or remove the newest rows.

Remove a level, for example one with a rude name or rude words in it:

```sh
npx wrangler@4 d1 execute lowercase-uppercase-levels --remote --command "DELETE FROM levels WHERE code = 'luc1:...'"
```

To empty the page after a flood of uploads, use `DELETE FROM levels`.

## High scores

Runs through the game's 15 levels go to a world list, besides the player's own top 10. The game starts a run as the
first level starts, reports each level as it is finished and finishes the run with a name. The server checks every
report and adds the points up itself; see the comments in `src/scores.js`.

| Call | What it does |
| --- | --- |
| `POST /api/runs` | `{hash, platform, version}` → `{id}`. A build whose `hash` differs from `run-limits.json` gets `OldVersion`. |
| `POST /api/runs/{id}/level` | `{level, points, centisLeft}`: one finished level, its points with the time bonus, the clock left in 1/100 s. |
| `POST /api/runs/{id}/finish` | `{name, partialPoints}` → `{rank, count, total, name, nameRefused}`. |
| `GET /api/scores` | `?offset=&limit=&run=` → `{count, offset, scores, you}`. `you` is the given run's own row. |
| `GET /api/fastest` | The fastest finish of each level. |

The run `id` is random and secret: it is all it takes to report for a run, so it is never listed.

The POST calls carry `X-Signature`, an HMAC-SHA256 in hex of `<METHOD> <path>\n<body>` under the key in the secret
`SCORE_KEY`. Without that secret, runs are refused (`NotReady`). The game is built with the same key. Anyone who takes
the game apart has it, so it only stops casual tampering:

```sh
npx wrangler@4 secret put SCORE_KEY
```

The game reads its copy from `Assets/Scripts/OnlineScores.Key.cs`, which is kept out of git. A checkout without it
builds and plays but sends nothing online. To make one, put the same key in:

```csharp
namespace LowercaseUppercase
{
    public static partial class OnlineScores
    {
        static partial void ReadBuildKey(ref string key) => key = "<the key>";
    }
}
```

The game never sends from the editor (`OnlineScores.AllowInEditor`), so trying levels there leaves the live list alone.

**After changing a level's enemies, # or clock**, run lowercase UPPERCASE > High Scores > Write Run Limits in Unity,
then `npm test`, and deploy together with the new builds: runs from older builds are turned away from then on.

**Floor times.** A level reported faster than its floor in `src/run-floors.json` is refused. Placeholders until the
levels are measured; `npm run deploy` refuses to run while `"placeholder"` is `true`.

**Marked runs** reach 95 % of every enemy and # (plus the time bonus they got) in the levels they finished, or finish a level within 10 % of its floor.
The page shows them so they are easy to look at. Unfinished runs are removed by a daily cron (`triggers` in
`wrangler.jsonc`).

To find a run, then rename or remove it:

```sh
npx wrangler@4 d1 execute lowercase-uppercase-levels --remote --command "SELECT id, name, total, levels_done, marked FROM runs WHERE finished_at IS NOT NULL ORDER BY total DESC LIMIT 20"
npx wrangler@4 d1 execute lowercase-uppercase-levels --remote --command "UPDATE runs SET name = '???' WHERE id = '<id>'; UPDATE fastest SET name = '???' WHERE run_id = '<id>'"
npx wrangler@4 d1 execute lowercase-uppercase-levels --remote --command "DELETE FROM run_levels WHERE run_id = '<id>'; DELETE FROM runs WHERE id = '<id>'"
```

After removing runs, recount them and rebuild the fastest finishes (the marks on those are not rebuilt):

```sh
npx wrangler@4 d1 execute lowercase-uppercase-levels --remote --file=scripts/rebuild-scores.sql
```
