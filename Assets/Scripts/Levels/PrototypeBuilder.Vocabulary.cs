using TMPro;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;

namespace LowercaseUppercase.Levels
{
    /// <summary>
    /// A scrolling level with one labelled station for each tile of the "Level vocabulary" mockup.
    /// Stations are laid out with a cursor that moves right, so a word can be retyped without
    /// recalculating everything after it.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const float Ground = 520f;
        const float WordGap = 22f; // Narrower than a, so it can never drop through between words.
        const float BreakRespawnSeconds = 3f;
        static readonly Color Hot = new Color32(0xFF, 0x8A, 0x3D, 0xFF);

#if UNITY_EDITOR
        /// <summary>Builds only this level, so edits made to the other scenes are left alone.</summary>
        [MenuItem("lowercase UPPERCASE/Rebuild Vocabulary Level")]
        public static void RebuildVocabulary()
        {
            PrepareAssets(out FormSettings lower, out FormSettings upper);
            BuildVocabulary(lower, upper);
            SetBuildScenes();
        }
#endif

#if UNITY_EDITOR
        static void BuildVocabulary(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(VocabularyScene, "Vocabulary");
            float x = 40f;

            Label(level, x, "Move", "Walk right. Each station is one word of the vocabulary.", "both");
            Floor(level, "start here", ref x);

            x = PeriodStation(level, x);
            x = CrackedStation(level, x);
            x = HyphenStation(level, x);
            x = CommaStation(level, x);
            x = KerningAndSpringStation(level, x);
            x = LiteralStation(level, x);
            x = LockStation(level, x);
            x = BlanksStation(level, x);

            FinishScene(VocabularyScene, level, "Vocabulary · 1 ch · 2 play · 3 vocab", lower, upper,
                new Vector2(110f, Ground), "every word does what it ", "says", "", scrollEndPx: x + 200f);
            EditorSceneManager.OpenScene(VocabularyScene);
        }
#endif

        // ---------------------------------------------------------------- stations

        static float PeriodStation(Transform level, float x)
        {
            Label(level, x, "Period", "Pressure plate. Only UPPERCASE is heavy enough to hold it down and open things.", "A");
            WordBlock plate = Plate(level, x + 30f);
            x += 80f;
            Floor(level, "then run", ref x);

            WordBlock door = Column(level, "d\no\no\nr\nd\no\no\nr", x - 80f, Ground, 72f, WordKind.Door, Hot);
            door.gameObject.name = "Word door";
            plate.linkedDoor = door;
            return x;
        }

        static float CrackedStation(Transform level, float x)
        {
            Label(level, x, "Cracked word", "A wall made of a word. Smash through it as a capital.", "A");
            float left = x;
            Floor(level, "smash this", ref x);
            WordBlock brick = Column(level, "b\nr\ni\nc\nk\nb\nr\ni\nc\nk", left + 300f, Ground, 46f, WordKind.Cracked, Breakable);
            brick.gameObject.name = "Word brick";
            brick.respawnSeconds = BreakRespawnSeconds;
            return x;
        }

        static float HyphenStation(Transform level, float x)
        {
            Label(level, x, "Hyphen bridge", "Joins two words over a gap. Snaps under UPPERCASE.", "a");
            WordBlock bridge = Word(level, "--------", x, Ground, 72f, WordKind.Hyphen, Interactive);
            bridge.impactFont = black;
            bridge.impactText = "SNAP";
            bridge.respawnSeconds = BreakRespawnSeconds;
            x = RightEdge(bridge) + WordGap;
            Floor(level, "bridge", ref x);
            return x;
        }

        static float CommaStation(Transform level, float x)
        {
            Label(level, x, "Comma", "A hook to hang and swing from. Jump to let go. Too fragile for capitals.", "a");
            Floor(level, "hook", ref x);
            Hook(level, "Hook 1", x + 170f, 270f);
            Hook(level, "Hook 2", x + 410f, 270f);
            return x + 620f;
        }

        static float KerningAndSpringStation(Transform level, float x)
        {
            Label(level, x, "Kerning gap", "Loosely spaced words have holes only a small letter fits through.", "a");
            float left = x;

            var kerning = new GameObject("Kerning Gap").transform;
            kerning.SetParent(level, false);
            foreach (char letter in "gapgap")
            {
                Word(kerning, letter.ToString(), x, Ground, 72f);
                x += 82f;
            }

            WordBlock lower = Word(level, "only a small letter", left, 600f, 44f);
            lower.gameObject.name = "Word under gap";
            float springCentre = RightEdge(lower) + 45f;
            Spring(level, springCentre, 600f, padWidthPx: 80f, fontPx: 60f);
            Label(level, springCentre - 40f, "Exclamation", "Spring. Launches anyone; lowercase flies twice as high.", "both", 210f);
            return springCentre + 50f;
        }

        static float LiteralStation(Transform level, float x)
        {
            Label(level, x, "Literal words", "Words act out their meaning: ice slides, hot burns, up lifts, sticky holds.", "both");
            PlaceCheckpoint(level, x + 30f, Ground);
            WordBlock ice = Word(level, "ice ice", x, Ground, 72f, WordKind.Ice, Interactive, italic: true);
            ice.grip = 0.12f;
            x = RightEdge(ice) + WordGap;

            WordBlock hot = Word(level, "hot", x, Ground, 72f, WordKind.Hot, Hot);
            x = RightEdge(hot) + WordGap;

            Floor(level, "cool", ref x);

            // Up: a lift over a gap, with the next word too high to reach by jumping.
            WordBlock up = Word(level, "up", x + 20f, Ground, 72f, WordKind.Up, Interactive);
            up.liftHeight = Size(350f);
            up.FitCollider();
            SetHeight(up, Y(585f));
            for (float rowY = 500f; rowY > 240f; rowY -= 80f)
                Text(level, "Lift Mark", "up", x + 20f, rowY, 72f, mono, new Color(Interactive.r, Interactive.g, Interactive.b, 0.18f));
            x += 150f;

            WordBlock above = Word(level, "above", x, 300f, 72f);
            x = RightEdge(above);

            // Sticky: slow to cross, and a wall that can be climbed by jumping and pressing into it.
            Label(level, x, "sticky", "Slows you down. Press into the wall and jump to climb it.", "both");
            WordBlock floor = Word(level, "sticky", x, Ground, 72f, WordKind.Sticky, Interactive);
            SetSticky(floor);
            x = RightEdge(floor);

            WordBlock wall = Column(level, "s\nt\ni\nc\nk\ny", x, Ground, 72f, WordKind.Sticky, Interactive);
            wall.gameObject.name = "Word sticky wall";
            SetSticky(wall);
            return RightEdge(wall);
        }

        static void SetSticky(WordBlock block)
        {
            block.moveFactor = 0.35f;
            block.jumpFactor = 0.6f;
            block.grip = 2f;
        }

        static float LockStation(Transform level, float x)
        {
            Label(level, x, "Caps Lock zone", "Shift is inverted: you are A unless you hold it. Find the key to switch it off.", "both");
            float left = x;

            Floor(level, "caps lock", ref x);
            x += 130f;
            float keyLeft = x + 100f;
            Floor(level, "zone", ref x);

            var zoneObject = new GameObject("Caps Lock Zone");
            zoneObject.transform.SetParent(level, false);
            zoneObject.layer = LayerMask.NameToLayer("Level");
            float widthPx = x - left;
            zoneObject.transform.position = new Vector3(X(left + widthPx * 0.5f), Y(352f), 0f);
            var trigger = zoneObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(Size(widthPx), Size(576f));

            var zone = zoneObject.AddComponent<CapsLockZone>();
            zone.tint = Rect(zoneObject.transform, "Tint", Vector2.zero, trigger.size,
                new Color(Hot.r, Hot.g, Hot.b, 0.03f), -5);
            zone.label = Text(level, "LOCK", "LOCK", left + 20f, 280f, 96f, black,
                new Color(Hot.r, Hot.g, Hot.b, 0.3f), -4);

            TextMeshPro key = Text(level, "Key", "key", keyLeft, Ground - 60f, 40f, monoBold, Player, 5);
            key.gameObject.layer = LayerMask.NameToLayer("Level");
            var keyTrigger = key.gameObject.AddComponent<BoxCollider2D>();
            keyTrigger.isTrigger = true;
            keyTrigger.size = new Vector2(Size(90f), Size(70f));
            keyTrigger.offset = new Vector2(Size(36f), Size(10f));
            key.gameObject.AddComponent<CapsLockKey>().zone = zone;

            return x;
        }

        static float BlanksStation(Transform level, float x)
        {
            Label(level, x, "Blanks", "Reach the blank in the right form to slot in. Some want a, some want A.", "both");

            WordBlock floor = Word(level, "some are A some are a", x, Ground, 72f);
            float right = RightEdge(floor);

            // Reached on foot, so walking in as a just passes through. It stands on letters without ascenders.
            Text(level, "Word C", "C", x + 20f, Ground, 88f, black, Paper);
            Goal(level, x + 90f, Ground - 90f, 84f, 90f, LetterForm.Uppercase);
            Text(level, "Word T", "T", x + 182f, Ground, 88f, black, Paper);

            // Reached by spring from the end of the sentence, which only lowercase flies high enough for.
            float springCentre = right + 50f;
            Spring(level, springCentre, Ground, padWidthPx: 80f);
            Text(level, "Word c", "c", springCentre - 70f, 232f, 72f, mono, Paper);
            Goal(level, springCentre - 20f, 180f, 40f, 52f);
            Text(level, "Word t", "t", springCentre + 26f, 232f, 72f, mono, Paper);

            return springCentre + 120f;
        }

        // ---------------------------------------------------------------- pieces

        static void Floor(Transform level, string content, ref float x)
        {
            WordBlock block = Word(level, content, x, Ground, 72f);
            x = RightEdge(block) + WordGap;
        }

        static float RightEdge(WordBlock block)
        {
            var box = block.GetComponent<BoxCollider2D>();
            float world = block.transform.position.x + box.offset.x + box.size.x * 0.5f;
            return world * PixelsPerUnit + 640f;
        }

        static void Label(Transform level, float x, string title, string rule, string form, float yPx = 112f)
        {
            string tag = form == "both" ? "both" : $"{form} only";
            Text(level, $"Label {title}", $"{title}  <size=70%><color=#FFD23F>{tag}</color></size>", x, yPx, 26f, monoBold, Paper, -2);

            TextMeshPro ruleText = Text(level, $"Rule {title}", rule, x, yPx + 26f, 15f, mono, Muted, -2);
            ruleText.textWrappingMode = TextWrappingModes.Normal;
            ruleText.rectTransform.sizeDelta = new Vector2(Size(280f), 0f);
        }

        static WordBlock Plate(Transform level, float centrePx, float surfacePx = Ground)
        {
            TextMeshPro text = Text(level, "Word period", ".", centrePx - 96f * 0.306f, surfacePx, 96f, monoBold, Interactive);
            WordBlock block = AddBlock(text, WordKind.Period);
            block.padWidth = Size(70f);
            MoveTopTo(block, surfacePx);
            return block;
        }

        static void Hook(Transform level, string name, float centrePx, float centreYPx)
        {
            TextMeshPro text = Text(level, name, ",", centrePx, centreYPx, 96f, monoBold, Interactive);
            WordBlock block = AddBlock(text, WordKind.Comma);
            block.padWidth = Size(110f);
            block.padHeight = Size(110f);
            block.impactFont = black;
            block.impactText = "SNAP";
            block.respawnSeconds = BreakRespawnSeconds;
            block.FitCollider();

            var box = block.GetComponent<BoxCollider2D>();
            var rect = (RectTransform)block.transform;
            rect.anchoredPosition = new Vector2(X(centrePx) - box.offset.x, Y(centreYPx) - box.offset.y);
        }

        static WordBlock Column(Transform level, string letters, float leftPx, float bottomPx, float fontPx,
            WordKind kind, Color color)
        {
            TextMeshPro text = Text(level, "Column", letters, leftPx, bottomPx, fontPx, mono, color);
            text.lineSpacing = -63f;
            WordBlock block = AddBlock(text, kind);
            block.impactFont = black;
            block.FitCollider();
            var box = block.GetComponent<BoxCollider2D>();
            SetHeight(block, Y(bottomPx) - (box.offset.y - box.size.y * 0.5f));
            return block;
        }
    }
}
