# Shared levels page

A public page where players upload lowercase / UPPERCASE level codes and browse everyone's levels. It runs on
Cloudflare Workers with a D1 database, on the free plan. Only the code is stored, plus a key that spots the same level
uploaded twice. The browser works out names and previews from the code when the page opens. It follows the Shift
Register sharing page.

| Path | What it is |
| --- | --- |
| `public/` | The page, served as it is. `sharecode.js` is shared with the Worker. |
| `src/worker.js` | `GET /api/levels` gives a page of codes (`?before=` takes the `next` of the previous page), `POST /api/levels` adds one (the body is the code). |
| `schema.sql` | The database table. |
| `scripts/fonts.js` | Copies the game's fonts from `Assets/Fonts` into `public/fonts`. |
| `test/` | Tests for the code reading and the Worker. |

The game's C# code is the reference for level codes: `Assets/Scripts/LevelShareCode.cs` and
`Assets/Scripts/CustomLevelData.cs`. After changing either, check `public/sharecode.js` and run `npm test`.

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
