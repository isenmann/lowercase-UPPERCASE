# itch.io page

Everything needed to fill in the itch.io "Create a new project" form for lowercase / UPPERCASE.

## Fields

| Field | Value |
| --- | --- |
| Title | lowercase / UPPERCASE |
| Project URL | `lowercase-uppercase` |
| Short description or tagline | A one-letter platformer: hold Shift to become the big letter, let go to be the small one. |
| Classification | Games |
| Kind of project | HTML (web build), plus downloadable Windows and Linux builds |
| Release status | Released |
| Pricing | No payments (or "$0 or donate") |
| Genre | Platformer |
| Tags | platformer, puzzle-platformer, 2d, typography, minimalist, score-attack, short, singleplayer, game-off, gamejam |
| AI generation disclosure | Answer to match how the game was made (code and sounds were produced with Claude) |
| Inputs | Keyboard, Xbox controller, Playstation controller |
| Accessibility | Configurable graphics settings |
| Description | Paste from `description.html` (see below) |
| Cover image | `cover.png` (630×500); `cover@2x.png` is the same at 1260×1000 |
| Banner | `banner.png` (960×250); `banner@2x.png` at 1920×500 |
| Screenshots | `screenshots/01-title.png` … `15-shared-levels-page.png`, 1920×1080, in that order |

## Web build embed

- Viewport: 1280×720, "Mobile friendly" off (keyboard or gamepad required).
- Turn on the fullscreen button.
- Upload the zipped `Builds/Web` folder and tick "This file will be played in the browser".

## Pasting the description

itch.io's description editor is rich text, not Markdown. Open `description.html` in a browser, select all, and paste: headings, bold, lists and code spans carry over. Or switch the editor to its HTML view and paste the file's source.

## Theme (Edit theme on the page)

| Setting | Value |
| --- | --- |
| Background | `#111318` |
| Inner column | `#16191F` |
| Text | `#F2F0EA` |
| Links | `#FFD23F` |
| Buttons | `#FFD23F`, button text `#111318` |
| Header font | Space Grotesk |
| Body font | Space Grotesk |

Banner: upload `banner.png` (960×250, the width of the page column) under "Banner"; `banner@2x.png` is the same at 1920×500 for sharper screens. When the banner shows the title, turn off "Show title" in the theme editor so the name doesn't appear twice.

## Screenshots

| File | Shows |
| --- | --- |
| `01-title.png` | Title screen |
| `02-crack-armour.png` | Crack: A dropping onto a `§` armour, which only dies when A lands on it |
| `03-chain-comma-swing.png` | Chain: a swinging from a comma hook over a pit |
| `04-gate-arrows.png` | Gate: an arrow from the `«` launcher flying over a, next to the period and its `door` |
| `05-space-spring.png` | Space: a launched by the `!` spring over a wall |
| `06-skate-curl.png` | Skate: A has kicked a curled `@` and the `o` rolls away across the `slide` ice |
| `07-flames-sparks.png` | Flames: a hopping over `hot` words while a `°` spark leaps out, with a `!` hopper ahead |
| `08-flames-fire-bar.png` | Flames: a walking under a fire bar turning around a hot `o` |
| `09-tar-mimic.png` | Tar: A above a mimic that has copied it and turned into `B` |
| `10-skyline-eye.png` | Skyline: an `ö` eye drifting after a, with a `&` walker on the word |
| `11-final-cursor.png` | Final: the backspace cursor `\|` chasing a and erasing the words behind it |
| `12-editor-words.png` | Level editor: an ice word picked, with its settings in the panel and the words page of the palette |
| `13-editor-things.png` | Level editor: a flier picked with its travel handle, the plate's link to its door, and the things page |
| `14-my-levels.png` | My levels: the list with finished marks and the picture of the level |
| `15-shared-levels-page.png` | The shared levels page with three levels, the first one open with its preview |

`12`–`14` were taken on 2026-11-09 in Play mode (Game view at supersize 2, scaled to 1920×1080), with showcase levels in a scratch level folder. `15` is the page itself in headless Chromium at 1440×810 CSS pixels and 4/3 density, served locally with those levels. `01`, `03` and `05` were taken on 2026-11-08 in the editor's Game view (2053×1155). The others were taken on 2026-11-09 at 3310×1862 (Game view 1655×931, supersize 2), staged in Play mode, and all are scaled to 1920×1080.
