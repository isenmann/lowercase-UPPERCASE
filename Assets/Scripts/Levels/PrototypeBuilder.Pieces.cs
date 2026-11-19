using TMPro;
using UnityEngine;

namespace LowercaseUppercase.Levels
{
    /// <summary>
    /// Level pieces shared by the chapters: the vocabulary words placed as reusable shapes, the exits, and the
    /// Shift-switch run that ends each chapter's test level.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const float HookSpacingPx = 240f;  // The same as the Vocabulary comma station, which is known to be swingable.
        const float LiftGapPx = 150f;
        const float LiftEntryDepthPx = 65f;
        const float KerningStepPx = 82f;   // 39px holes between letters: wider than a, narrower than A.
        const float RunTunnelOffsetPx = 40f;
        const float RunBrickOffsetPx = 150f;

        // ---------------------------------------------------------------- exits

        /// <summary>
        /// The last ground word with the exit standing on it: the word the letter is missing from, with the blank
        /// between its two halves. The halves are measured, so any word fits.
        /// </summary>
        static float Exit(Transform level, float x, string floor, string before, string after, float surfacePx = Ground)
        {
            float wordLeft = x + 110f;
            WordBlock ground = Word(level, floor, x, surfacePx, 72f);
            ExitWord(level, wordLeft, surfacePx, before, after, LetterForm.Lowercase);
            return RightEdge(ground) + WordGap;
        }

        static void ExitWord(Transform level, float leftPx, float surfacePx, string before, string after, LetterForm wanted)
        {
            bool upper = wanted == LetterForm.Uppercase;
            TMP_FontAsset font = upper ? black : mono;
            float fontPx = upper ? 88f : 72f;
            float blankWidth = upper ? 84f : 40f;
            float blankHeight = upper ? 90f : 52f;

            TextMeshPro first = Text(level, $"Word {before}", before, leftPx, surfacePx, fontPx, font, Paper);
            float firstWidth = first.GetPreferredValues(before).x * PixelsPerUnit;
            float blankLeft = leftPx + firstWidth + 6f;
            Goal(level, blankLeft, surfacePx - blankHeight, blankWidth, blankHeight, wanted);
            Text(level, $"Word {after}", after, blankLeft + blankWidth + 6f, surfacePx, fontPx, font, Paper);
        }

        // ---------------------------------------------------------------- the chapter-ending run

        /// <summary>
        /// One obstacle per word, alternating: a low gap, then brick, then a low gap again. Every obstacle after the
        /// first needs the other form, so the number of switches is one less than the number of words.
        /// </summary>
        static float ShiftRun(Transform level, float x, bool underCeiling, params string[] words)
        {
            int rows = underCeiling ? CeilingTunnelRows : TunnelRows;
            string brick = underCeiling ? CeilingBrickLetters : BrickLetters;

            for (int i = 0; i < words.Length; i++)
            {
                float left = x;
                WordBlock ground = Word(level, words[i], x, Ground, 72f);
                x = RightEdge(ground) + WordGap;

                if (i % 2 == 0)
                {
                    Stacked(level, ShortLowWord, left + RunTunnelOffsetPx, rows, TunnelBottom);
                    Hash(level, left + RunTunnelOffsetPx + 115f, OnGround);
                }
                else
                {
                    BrickWall(level, left + RunBrickOffsetPx, brick);
                }
            }
            return x;
        }

        // ---------------------------------------------------------------- words that do something

        static WordBlock Ice(Transform level, string content, float leftPx, float surfacePx = Ground)
        {
            WordBlock ice = Word(level, content, leftPx, surfacePx, 72f, WordKind.Ice, Interactive, italic: true);
            ice.grip = 0.12f;
            return ice;
        }

        /// <summary>A word of brick lying flat: a stands on it, A smashes it from the side or from above.</summary>
        static WordBlock BrickWord(Transform level, string content, float leftPx, float surfacePx = Ground)
        {
            WordBlock brick = Word(level, content, leftPx, surfacePx, 72f, WordKind.Cracked, Breakable);
            brick.impactFont = black;
            return brick;
        }

        static WordBlock HotWord(Transform level, string content, float leftPx, float surfacePx = Ground)
        {
            return Word(level, content, leftPx, surfacePx, 72f, WordKind.Hot, Hot);
        }

        static WordBlock StickyFloor(Transform level, string content, float leftPx, float surfacePx = Ground)
        {
            WordBlock floor = Word(level, content, leftPx, surfacePx, 72f, WordKind.Sticky, Interactive);
            SetSticky(floor);
            return floor;
        }

        /// <summary>A climbable column standing on the ground.</summary>
        static WordBlock StickyWall(Transform level, float leftPx, string letters = "s\nt\ni\nc\nk\ny", float bottomPx = Ground)
        {
            WordBlock wall = Column(level, letters, leftPx, bottomPx, 72f, WordKind.Sticky, Interactive);
            wall.gameObject.name = "Word sticky wall";
            SetSticky(wall);
            return wall;
        }

        static WordBlock Door(Transform level, float leftPx, float bottomPx = Ground)
        {
            WordBlock door = Column(level, "d\no\no\nr\nd\no\no\nr", leftPx, bottomPx, 72f, WordKind.Door, Hot);
            door.gameObject.name = "Word door";
            return door;
        }

        /// <summary>A plate and the door it opens, which only stays open for a moment after A steps off.</summary>
        static void PlateAndDoor(Transform level, float plateCentrePx, float doorLeftPx, float plateSurfacePx = Ground)
        {
            WordBlock plate = Plate(level, plateCentrePx, plateSurfacePx);
            plate.linkedDoor = Door(level, doorLeftPx);
        }

        /// <summary>Letters of a word spaced so loosely that a drops through the holes and A walks across.</summary>
        static float Kerned(Transform level, string letters, float leftPx, float surfacePx = Ground)
        {
            var group = new GameObject($"Kerning {letters}").transform;
            group.SetParent(level, false);
            float x = leftPx;
            float right = leftPx;
            foreach (char letter in letters)
            {
                right = RightEdge(Word(group, letter.ToString(), x, surfacePx, 72f));
                x += KerningStepPx;
            }
            return right;
        }

        /// <summary>
        /// A column of rising air in a gap, entered by walking off a surface at fromPx, which rises to topPx, with faint
        /// marks so it can be seen. The landing goes 65px below the top, at the returned x.
        /// </summary>
        static float Lift(Transform level, float x, float topPx, float fromPx = Ground)
        {
            float bottomPx = fromPx + LiftEntryDepthPx;
            WordBlock up = Word(level, "up", x + 20f, Ground, 72f, WordKind.Up, Interactive);
            up.liftHeight = Size(bottomPx - topPx);
            up.FitCollider();
            SetHeight(up, Y(bottomPx));
            for (float rowY = bottomPx - 85f; rowY > topPx + 40f; rowY -= 80f)
                Text(level, "Lift Mark", "up", x + 20f, rowY, 72f, mono, new Color(Interactive.r, Interactive.g, Interactive.b, 0.18f));
            return x + LiftGapPx;
        }

        /// <summary>Commas in a row over open space. Returns where the landing should start.</summary>
        static float HookChain(Transform level, float x, int count, float yPx = 270f)
        {
            for (int i = 0; i < count; i++)
                Hook(level, $"Hook {i + 1}", x + 170f + i * HookSpacingPx, yPx);
            for (int i = 0; i + 1 < count; i++)
                Hash(level, x + 170f + (i + 0.5f) * HookSpacingPx, yPx + 40f);
            return x + 170f + (count - 1) * HookSpacingPx + 210f;
        }

        /// <summary>
        /// A stretch where Shift works backwards: the letter is A unless Shift is held. The key switches it off for
        /// good. Built on the same pieces as the Vocabulary lock station.
        /// </summary>
        static void CapsLock(Transform level, float leftPx, float rightPx, float keyLeftPx, float keyBaselinePx)
        {
            var zoneObject = new GameObject("Caps Lock Zone");
            zoneObject.transform.SetParent(level, false);
            zoneObject.layer = LayerMask.NameToLayer("Level");
            float widthPx = rightPx - leftPx;
            zoneObject.transform.position = new Vector3(X(leftPx + widthPx * 0.5f), Y(352f), 0f);
            var trigger = zoneObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(Size(widthPx), Size(576f));

            var zone = zoneObject.AddComponent<CapsLockZone>();
            zone.tint = Rect(zoneObject.transform, "Tint", Vector2.zero, trigger.size,
                new Color(Hot.r, Hot.g, Hot.b, 0.03f), -5);
            zone.label = Text(level, "LOCK", "LOCK", leftPx + 20f, 300f, 96f, black,
                new Color(Hot.r, Hot.g, Hot.b, 0.3f), -4);

            TextMeshPro key = Text(level, "Key", "key", keyLeftPx, keyBaselinePx, 40f, monoBold, Player, 5);
            key.gameObject.layer = LayerMask.NameToLayer("Level");
            var keyTrigger = key.gameObject.AddComponent<BoxCollider2D>();
            keyTrigger.isTrigger = true;
            keyTrigger.size = new Vector2(Size(90f), Size(70f));
            keyTrigger.offset = new Vector2(Size(36f), Size(10f));
            key.gameObject.AddComponent<CapsLockKey>().zone = zone;
        }
    }
}
