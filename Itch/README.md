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
| Screenshots | `screenshots/1-title.png` … `6-gate-door.png`, 1920×1080, in that order |

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
| `1-title.png` | Title screen |
| `2-crack-smash.png` | Crack: A smashing a `brick` wall under the `ceiling` |
| `3-chain-comma-swing.png` | Chain: a swinging from a comma hook over a pit |
| `4-flames-caps-lock.png` | Flames: Caps Lock zone, A breaking a brick, a `hot` word and the `key` |
| `5-space-spring.png` | Space: a launched by the `!` spring over a wall |
| `6-gate-door.png` | Gate: a running under a door held open by a period |

Taken in the editor's Game view (2053×1155) on 2026-11-08 and scaled to 1920×1080.
