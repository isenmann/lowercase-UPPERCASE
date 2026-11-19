#if UNITY_EDITOR
using TMPro;
using UnityEngine;

namespace LowercaseUppercase.Levels
{
    /// <summary>
    /// The menu's "How to play" popup: pages taken from the design canvas's "The two forms" and "Level vocabulary"
    /// boards, rewritten to match what the game does now, plus a page listing every enemy and one on the level editor. Positions are CSS pixels from the top left of a 1280x720 board.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        static readonly Color GuideCard = new Color32(0x18, 0x1B, 0x22, 0xFF);
        static readonly Color GuideBody = new Color32(0xD8, 0xD6, 0xD0, 0xFF);
        static readonly Color GuideRule = new Color32(0xB8, 0xBC, 0xC6, 0xFF);
        static readonly Color GuideOrange = new Color32(0xFF, 0x8A, 0x3D, 0xFF);

        struct GuideTile
        {
            public string glyph, tag, name, rule;
            public Color color;

            public GuideTile(string glyph, Color color, string tag, string name, string rule)
            {
                this.glyph = glyph;
                this.color = color;
                this.tag = tag;
                this.name = name;
                this.rule = rule;
            }
        }

        static GuideTile[] VocabularyTiles() => new[]
        {
            new GuideTile(".", Interactive, "A", "Period",
                "A pressure plate. Only UPPERCASE is heavy enough to hold it down and open the door."),
            new GuideTile("!", Interactive, "both", "Exclamation",
                "A spring. Launches anyone; lowercase flies twice as high."),
            new GuideTile(",", Interactive, "a", "Comma",
                "A hook to hang from. Jump to let go. Breaks under a capital."),
            new GuideTile("a-b", Interactive, "a", "Hyphen bridge",
                "Joins two words over a gap. Snaps under UPPERCASE."),
            new GuideTile("ice", Interactive, "both", "Literal words",
                "Words do what they say: ice slides, up lifts, sticky holds you to walls, hot burns."),
            new GuideTile("brick", Breakable, "A", "Cracked word",
                "A wall or floor made of a word. Run into it or slam down on it as a capital."),
            new GuideTile("g a p", Paper, "a", "Kerning gap",
                "Loosely spaced letters leave holes only a small letter fits through."),
            new GuideTile("LOCK", GuideOrange, "both", "Caps Lock zone",
                "Shift is inverted inside: you are A unless you hold it. Find the key to switch it off."),
            new GuideTile(";", Muted, "both", "Checkpoint",
                "Pass it, and dying brings you back here instead of to the start."),
            new GuideTile("[<color=#FFD23F>*</color>]", Interactive, "both", "Power block",
                "Hit it from below to get asterisks, then Ctrl shoots them. Dying loses them."),
            new GuideTile("#", Player, "both", "Hash",
                "Worth 50 points. Each one can be taken once per level."),
        };

        // Tags say what stops each enemy: A lands on it, * shoots it, avoid means neither works.
        static GuideTile[] EnemyTiles() => new[]
        {
            new GuideTile("&", Danger, "A *", "Walker",
                "Paces along its word. The first enemy you meet."),
            new GuideTile("\u00a7", Danger, "A", "Armour",
                "Asterisks bounce off it. Only a capital landing on it gets through."),
            new GuideTile("^", Danger, "*", "Spike",
                "Walks like the others, but landing on it hurts. A slam beside it knocks it out."),
            new GuideTile("!", Danger, "A *", "Hopper",
                "Hops on a beat. Pass under it while it is up."),
            new GuideTile("~", Danger, "A *", "Flier",
                "Bobs and drifts through the air over gaps and bridges."),
            new GuideTile("\u00ab <", Danger, "A *", "Arrow",
                "Fired from a launcher. Flies over a and hits A."),
            new GuideTile("\u00a1", Danger, "*", "Riser",
                "Rises out of a hole in a spaced-out word. Stays down while you stand by it."),
            new GuideTile("'", Danger, "*", "Drip",
                "Hangs under a ceiling and falls when you pass below. It grows back."),
            new GuideTile("\u00b0", Hot, "avoid", "Spark",
                "Leaps out of a hot word and dives back in. Time your way past."),
            new GuideTile("o\u00b7\u00b7\u00b7\u00b7", Hot, "avoid", "Fire bar",
                "A bar of fire turning around a hot o. a slips under it."),
            new GuideTile("@", Danger, "A *", "Curl",
                "Land on it as A and it curls into an o. Touch the o to kick it into enemies."),
            new GuideTile("\u00f6", Danger, "*", "Eye",
                "Wakes when you come near and drifts after you, even through words."),
            new GuideTile("\u2195", Danger, "A *", "Case swapper",
                "Touch it and Shift works the other way until you touch another."),
            new GuideTile("b", Danger, "A *", "Mimic",
                "Copies your case a moment late. Turn A just before you land on the small b."),
            new GuideTile("|", Danger, "avoid", "Cursor",
                "Chases you and erases the words behind you. Keep moving."),
        };

        /// <summary>
        /// A full-screen panel that covers the title: one page per tab, and the tabs and Back in a row at the bottom.
        /// Selecting a tab shows its page, so arrow keys and gamepads flip through the pages.
        /// </summary>
        static RectTransform BuildGuide(Transform ui, Sprite filled, Sprite outline, Sprite keycap, Sprite shiftArrow,
            TMP_FontAsset regular, TMP_FontAsset medium, TMP_FontAsset bold,
            out UnityEngine.UI.Button[] tabs, out TextMeshProUGUI[] tabLabels, out GameObject[] pages,
            out UnityEngine.UI.Button back)
        {
            UnityEngine.UI.Image cover = UiImage(ui, "GuidePanel", white, Background);
            cover.raycastTarget = true; // Keeps clicks off the hidden menu behind it.
            RectTransform panel = cover.rectTransform;
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;

            // The pages keep the board's size and stay centred on wider screens.
            var boardObject = new GameObject("Board", typeof(RectTransform));
            boardObject.transform.SetParent(panel, false);
            var board = (RectTransform)boardObject.transform;
            Place(board, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1280f, 720f));

            pages = new[]
            {
                FormsPage(board, filled, outline, keycap, shiftArrow, regular, bold).gameObject,
                VocabularyPage(board, filled, outline, keycap, regular, bold).gameObject,
                EnemiesPage(board, filled, outline, keycap, regular, bold).gameObject,
                EditorPage(board, filled, outline, keycap, regular, bold).gameObject
            };

            var rowObject = new GameObject("GuideTabs", typeof(RectTransform));
            rowObject.transform.SetParent(board, false);
            var row = (RectTransform)rowObject.transform;
            Place(row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(5 * 210f + 4 * 12f, 44f));
            var layout = rowObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            tabs = new UnityEngine.UI.Button[4];
            tabLabels = new TextMeshProUGUI[4];
            tabs[0] = OutlineButton(row, "FormsTab", "The two forms", outline, medium, out tabLabels[0]);
            tabs[1] = OutlineButton(row, "VocabularyTab", "Level vocabulary", outline, medium, out tabLabels[1]);
            tabs[2] = OutlineButton(row, "EnemiesTab", "Enemies", outline, medium, out tabLabels[2]);
            tabs[3] = OutlineButton(row, "EditorTab", "Level editor", outline, medium, out tabLabels[3]);
            back = OutlineButton(row, "BackButton", "Back", outline, medium, out _);

            panel.gameObject.SetActive(false);
            return panel;
        }

        // ---------------------------------------------------------------- the two forms

        static RectTransform FormsPage(RectTransform board, Sprite filled, Sprite outline, Sprite keycap, Sprite shiftArrow,
            TMP_FontAsset regular, TMP_FontAsset bold)
        {
            RectTransform page = GuidePage(board, "FormsPage", "The two forms",
                "One key. Every puzzle is a question of which form, and when.", regular, bold);

            const float cardWidth = (1168f - 24f) / 2f;
            FormCard(page, 56f, cardWidth, "a", monoBold, 120f, "lowercase", mono, "default · Shift released",
                new[] { 1, 1, 5, 5 },
                "Fast and light, with a high, floaty jump\n" +
                "Slips through the gaps in spaced-out words\n" +
                "Hangs from commas and crosses hyphens\n" +
                "Springs launch it twice as high\n" +
                "Too light to hold a period down or break a word",
                filled, outline, regular);
            FormCard(page, 56f + cardWidth + 24f, cardWidth, "A", black, 130f, "UPPERCASE", black, "while Shift is held",
                new[] { 5, 5, 2, 1 },
                "Heavy and slow, with a short hop\n" +
                "Smashes cracked words, presses periods\n" +
                "Squashes an enemy by landing on it\n" +
                "Grow in mid-air to slam: knocks out enemies beside you\n" +
                "Snaps hyphens, breaks commas, shoots bigger asterisks",
                filled, outline, regular);

            RectTransform note = Strip(page, "ShiftNote", 562f, 52f, outline, MenuOutline);
            UnityEngine.UI.Image key = UiImage(note, "ShiftKey", keycap, MenuOutline);
            key.type = UnityEngine.UI.Image.Type.Sliced;
            PlaceTopLeft(key.rectTransform, 20f, 10f, 92f, 32f);
            UnityEngine.UI.Image arrow = UiImage(key.rectTransform, "ShiftIcon", shiftArrow, Paper);
            Place(arrow.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(15f, 15f));
            TextMeshProUGUI keyLabel = UiText(key.rectTransform, "ShiftLabel", "Shift", mono, 13f, Paper, TextAlignmentOptions.Left);
            Place(keyLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(50f, 20f));

            TextMeshProUGUI text = UiText(note, "Text",
                "Hold to grow, release to shrink. Growing needs headroom: under a low ceiling, Shift waits until there is room.",
                regular, 16f, GuideBody, TextAlignmentOptions.Left);
            PlaceTopLeft(text.rectTransform, 128f, 0f, 1168f - 148f, 52f);
            return page;
        }

        static void FormCard(RectTransform page, float x, float width, string glyph, TMP_FontAsset glyphFont, float glyphPx,
            string name, TMP_FontAsset nameFont, string when, int[] stats, string traits, Sprite filled, Sprite outline,
            TMP_FontAsset regular)
        {
            RectTransform card = Card(page, $"Card {name}", x, 104f, width, 442f, filled, outline);

            TextMeshProUGUI big = UiText(card, "Glyph", glyph, glyphFont, glyphPx, Player, TextAlignmentOptions.BottomLeft);
            // Space Mono leaves room for descenders under the a, so it sits lower than the A to share a baseline.
            PlaceTopLeft(big.rectTransform, 32f, glyphFont == black ? 20f : 44f, 110f, 130f);

            TextMeshProUGUI title = UiText(card, "Name", name, nameFont, 26f, Paper, TextAlignmentOptions.BottomLeft);
            PlaceTopLeft(title.rectTransform, 162f, 84f, 300f, 36f);
            TextMeshProUGUI subtitle = UiText(card, "When", when, regular, 14f, Muted, TextAlignmentOptions.TopLeft);
            PlaceTopLeft(subtitle.rectTransform, 162f, 124f, 300f, 20f);

            string[] labels = { "Size", "Weight", "Speed", "Jump" };
            for (int i = 0; i < labels.Length; i++)
            {
                float y = 170f + i * 24f;
                TextMeshProUGUI label = UiText(card, labels[i], labels[i], regular, 14f, Muted, TextAlignmentOptions.Left);
                PlaceTopLeft(label.rectTransform, 32f, y, 72f, 14f);
                for (int s = 0; s < 5; s++)
                {
                    UnityEngine.UI.Image segment = UiImage(card, $"{labels[i]} {s + 1}", white, s < stats[i] ? Player : BarLine);
                    PlaceTopLeft(segment.rectTransform, 120f + s * 46f, y + 2f, 40f, 10f);
                }
            }

            TextMeshProUGUI list = UiText(card, "Traits", Bullets(traits), regular, 16f, GuideBody, TextAlignmentOptions.TopLeft);
            list.lineSpacing = 20f;
            PlaceTopLeft(list.rectTransform, 32f, 282f, width - 64f, 140f);
        }

        /// <summary>One bullet per line, with wrapped lines indented under the text.</summary>
        static string Bullets(string lines)
        {
            var result = new System.Text.StringBuilder();
            foreach (string line in lines.Split('\n'))
            {
                if (result.Length > 0) result.Append('\n');
                result.Append("•<indent=20>").Append(line).Append("</indent>");
            }
            return result.ToString();
        }

        // ---------------------------------------------------------------- level vocabulary

        static RectTransform VocabularyPage(RectTransform board, Sprite filled, Sprite outline, Sprite keycap,
            TMP_FontAsset regular, TMP_FontAsset bold)
        {
            RectTransform page = GuidePage(board, "VocabularyPage", "Level vocabulary",
                "The world is text. Punctuation is machinery, and words do what they say.", regular, bold);

            TileGrid(page, VocabularyTiles(), 4, filled, outline, keycap, regular, bold);

            RectTransform goal = Strip(page, "Goal", 576f, 52f, filled, GuideCard);
            UnityEngine.UI.Image goalLine = UiImage(goal, "Outline", outline, BarLine);
            goalLine.type = UnityEngine.UI.Image.Type.Sliced;
            Stretch(goalLine.rectTransform);

            TextMeshProUGUI c = UiText(goal, "C", "c", mono, 26f, Paper, TextAlignmentOptions.Center);
            PlaceTopLeft(c.rectTransform, 20f, 6f, 18f, 40f);
            UnityEngine.UI.Image blank = UiImage(goal, "Blank", keycap, Player);
            blank.type = UnityEngine.UI.Image.Type.Sliced;
            PlaceTopLeft(blank.rectTransform, 40f, 15f, 18f, 24f);
            TextMeshProUGUI t = UiText(goal, "T", "t", mono, 26f, Paper, TextAlignmentOptions.Center);
            PlaceTopLeft(t.rectTransform, 60f, 6f, 18f, 40f);

            TextMeshProUGUI text = UiText(goal, "Text",
                "Goal: every level is a sentence missing one letter: you. Reach the blank in the form it wants to slot in. " +
                "Some blanks want <color=#FFD23F>a</color>, some want <color=#FFD23F><b>A</b></color>.",
                regular, 16f, GuideBody, TextAlignmentOptions.Left);
            PlaceTopLeft(text.rectTransform, 100f, 0f, 1168f - 120f, 52f);
            return page;
        }

        /// <summary>Tiles in rows of the given width, from under the title down to the strip at the bottom.</summary>
        static void TileGrid(RectTransform page, GuideTile[] tiles, int columns, Sprite filled, Sprite outline,
            Sprite keycap, TMP_FontAsset regular, TMP_FontAsset bold)
        {
            const float gap = 12f;
            const float tileHeight = 146f;
            float tileWidth = (1168f - (columns - 1) * gap) / columns;
            for (int i = 0; i < tiles.Length; i++)
            {
                float x = 56f + (i % columns) * (tileWidth + gap);
                float y = 100f + (i / columns) * (tileHeight + gap);
                VocabularyTile(page, tiles[i], x, y, tileWidth, tileHeight, filled, outline, keycap, regular, bold);
            }
        }

        // ---------------------------------------------------------------- enemies

        static RectTransform EnemiesPage(RectTransform board, Sprite filled, Sprite outline, Sprite keycap,
            TMP_FontAsset regular, TMP_FontAsset bold)
        {
            RectTransform page = GuidePage(board, "EnemiesPage", "Enemies",
                "Anything red or orange bites. Touching one costs a life.", regular, bold);

            TileGrid(page, EnemyTiles(), 5, filled, outline, keycap, regular, bold);

            RectTransform key = Strip(page, "Key", 576f, 52f, filled, GuideCard);
            UnityEngine.UI.Image keyLine = UiImage(key, "Outline", outline, BarLine);
            keyLine.type = UnityEngine.UI.Image.Type.Sliced;
            Stretch(keyLine.rectTransform);

            TextMeshProUGUI text = UiText(key, "Text",
                "<color=#FFD23F><b>A</b></color>  land on it as UPPERCASE      " +
                "<color=#FFD23F>*</color>  shoot it with Ctrl      " +
                "<color=#9AA0AE>avoid</color>  nothing stops it: get past",
                regular, 16f, GuideBody, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            return page;
        }

        // ---------------------------------------------------------------- level editor

        // Tags say how each is done: the mouse, a key, or the My levels menu.
        static GuideTile[] EditorTiles() => new[]
        {
            new GuideTile("abc", Paper, "mouse", "Place",
                "Pick a piece in the bar at the bottom, then click the page. Words start typing; Enter places them."),
            new GuideTile("[ ]", Player, "mouse", "Pick and move",
                "Click a piece to pick it and drag to move it. Double-click a word to retype it. Del removes."),
            new GuideTile("up", Interactive, "mouse", "Stretch",
                "Lifts, hooks, Caps Lock, fliers and cursors have a yellow square. Drag it to size them."),
            new GuideTile("< >", Paper, "mouse", "Settings",
                "The panel changes the picked piece. settings, top right, sets the name, sentence, time and ceiling."),
            new GuideTile("play", Player, "P H", "Test play",
                "P plays from the start, H from the mouse, and Esc comes back. Reach the exit from the start to finish it."),
            new GuideTile("!", Danger, "mouse", "Warnings",
                "Red notes under the top bar point at likely mistakes. Click them to find the piece."),
            new GuideTile("Ctrl Z", Paper, "keys", "Undo",
                "Ctrl Z undoes and Ctrl Y redoes. Every change is saved as you go; there is no save button."),
            new GuideTile("luc1:", Interactive, "menu", "Share",
                "Share in My levels copies a level as a code. Import there turns a code into a new level."),
        };

        static RectTransform EditorPage(RectTransform board, Sprite filled, Sprite outline, Sprite keycap,
            TMP_FontAsset regular, TMP_FontAsset bold)
        {
            RectTransform page = GuidePage(board, "EditorPage", "Level editor",
                "Editor in the menu opens My levels. Build a sentence of your own.", regular, bold);

            TileGrid(page, EditorTiles(), 4, filled, outline, keycap, regular, bold);

            RectTransform keys = Strip(page, "Keys", 576f, 52f, filled, GuideCard);
            UnityEngine.UI.Image keysLine = UiImage(keys, "Outline", outline, BarLine);
            keysLine.type = UnityEngine.UI.Image.Type.Sliced;
            Stretch(keysLine.rectTransform);

            TextMeshProUGUI text = UiText(keys, "Text",
                "Arrows, wheel or right drag  pan      G  grid      1 2 3  palette pages      Esc  back to My levels",
                regular, 16f, GuideBody, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            return page;
        }

        static void VocabularyTile(RectTransform page, GuideTile tile, float x, float y, float width, float height,
            Sprite filled, Sprite outline, Sprite keycap, TMP_FontAsset regular, TMP_FontAsset bold)
        {
            RectTransform card = Card(page, $"Tile {tile.name}", x, y, width, height, filled, outline);

            TextMeshProUGUI glyph = UiText(card, "Glyph", tile.glyph, monoBold, 34f, tile.color, TextAlignmentOptions.TopLeft);
            PlaceTopLeft(glyph.rectTransform, 16f, 10f, width - 90f, 44f);

            // Several tags sit side by side, ending at the right edge.
            string[] tags = tile.tag.Split(' ');
            float right = width - 16f;
            for (int i = tags.Length - 1; i >= 0; i--)
            {
                Color tagColor = tags[i].Length > 1 ? Muted : Player;
                float tagWidth = tags[i].Length > 1 ? 16f + 8f * tags[i].Length : 28f;
                right -= tagWidth;
                UnityEngine.UI.Image pill = UiImage(card, tags.Length > 1 ? $"Tag {tags[i]}" : "Tag", keycap, tagColor);
                pill.type = UnityEngine.UI.Image.Type.Sliced;
                PlaceTopLeft(pill.rectTransform, right, 16f, tagWidth, 22f);
                TextMeshProUGUI tag = UiText(pill.rectTransform, "Label", tags[i], mono, 12f, tagColor,
                    TextAlignmentOptions.Center);
                Stretch(tag.rectTransform);
                right -= 6f;
            }

            TextMeshProUGUI name = UiText(card, "Name", tile.name, bold, 17f, Paper, TextAlignmentOptions.TopLeft);
            PlaceTopLeft(name.rectTransform, 16f, 60f, width - 32f, 22f);

            TextMeshProUGUI rule = UiText(card, "Rule", tile.rule, regular, 13f, GuideRule, TextAlignmentOptions.TopLeft);
            rule.textWrappingMode = TextWrappingModes.Normal;
            rule.lineSpacing = 4f;
            PlaceTopLeft(rule.rectTransform, 16f, 86f, width - 32f, height - 96f);
        }

        // ---------------------------------------------------------------- pieces

        static RectTransform GuidePage(RectTransform board, string name, string title, string subtitle,
            TMP_FontAsset regular, TMP_FontAsset bold)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(board, false);
            var page = (RectTransform)go.transform;
            Stretch(page);

            TextMeshProUGUI heading = UiText(page, "Title", title, bold, 36f, Paper, TextAlignmentOptions.BottomLeft);
            heading.characterSpacing = -1.5f;
            PlaceTopLeft(heading.rectTransform, 56f, 36f, 500f, 46f);
            TextMeshProUGUI line = UiText(page, "Subtitle", subtitle, regular, 16f, Muted, TextAlignmentOptions.BottomRight);
            PlaceTopLeft(line.rectTransform, 1224f - 700f, 36f, 700f, 40f);
            return page;
        }

        static RectTransform Card(RectTransform parent, string name, float x, float y, float width, float height,
            Sprite filled, Sprite outline)
        {
            UnityEngine.UI.Image fill = UiImage(parent, name, filled, GuideCard);
            fill.type = UnityEngine.UI.Image.Type.Sliced;
            PlaceTopLeft(fill.rectTransform, x, y, width, height);
            UnityEngine.UI.Image line = UiImage(fill.rectTransform, "Outline", outline, BarLine);
            line.type = UnityEngine.UI.Image.Type.Sliced;
            Stretch(line.rectTransform);
            return fill.rectTransform;
        }

        /// <summary>A full-width band under the page's main content.</summary>
        static RectTransform Strip(RectTransform page, string name, float y, float height, Sprite sprite, Color color)
        {
            UnityEngine.UI.Image strip = UiImage(page, name, sprite, color);
            strip.type = UnityEngine.UI.Image.Type.Sliced;
            PlaceTopLeft(strip.rectTransform, 56f, y, 1168f, height);
            return strip.rectTransform;
        }

        static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            var topLeft = new Vector2(0f, 1f);
            Place(rect, topLeft, topLeft, new Vector2(x, -y), new Vector2(width, height));
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
#endif
