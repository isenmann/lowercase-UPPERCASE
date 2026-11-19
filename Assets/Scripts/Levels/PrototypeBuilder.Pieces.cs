using System.Collections.Generic;
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

        const float DefaultFloatRisePx = 270f;
        const float FloatRippleBelowPx = 26f;   // Below the word's baseline.
        const float FloatRipplePerLetter = 1.8f; // 40 px tildes under 72 px letters, so the ripple spans the word.

        /// <summary>
        /// A floating word resting with its surface at surfacePx, moving as far as otherEndPx, above or below. A faint
        /// ripple of tildes moves under it, and a fainter ghost of the ripple marks each end of its travel.
        /// </summary>
        static WordBlock FloatWord(Transform level, string content, float leftPx, float surfacePx, float otherEndPx)
        {
            WordBlock word = Word(level, content, leftPx, surfacePx, 72f, WordKind.Float, Interactive);
            float baselinePx = 360f - word.transform.position.y * PixelsPerUnit;

            string tildes = new string('~', Mathf.Clamp(Mathf.RoundToInt(content.Trim().Length * FloatRipplePerLetter), 3, 36));
            float rippleBaselinePx = baselinePx + FloatRippleBelowPx;
            TextMeshPro ripple = Text(level, "Float Ripple", tildes, leftPx, rippleBaselinePx, 40f, mono, FloatRipple);
            ripple.transform.SetParent(word.transform, true);

            // Where the ripple is when the word rests, and where it is at the other end. Drawn behind the ripple.
            Text(level, "Float End", tildes, leftPx, rippleBaselinePx, 40f, mono, FloatEnd, -2);
            Text(level, "Float End", tildes, leftPx, rippleBaselinePx + otherEndPx - surfacePx, 40f, mono, FloatEnd, -2);

            var floating = word.gameObject.AddComponent<FloatingWord>();
            floating.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            float travel = Size(surfacePx - otherEndPx); // Positive when the other end is higher up the page.
            floating.topY = word.transform.position.y + Mathf.Max(0f, travel);
            floating.bottomY = word.transform.position.y + Mathf.Min(0f, travel);
            return word;
        }

        public const float HeavyFontPx = 108f; // Tall enough that A can't hop onto it: 0.8 units against A's 0.6.

        /// <summary>
        /// A heavy word standing on the surface at bottomPx, which only A can push. Set in the heavy font, so it
        /// looks as heavy as it is.
        /// </summary>
        static WordBlock HeavyWord(Transform level, string content, float leftPx, float bottomPx = Ground)
        {
            TextMeshPro text = Text(level, $"Word {content}", content, leftPx, bottomPx, HeavyFontPx, black, Interactive);
            WordBlock word = AddBlock(text, WordKind.Heavy);
            var box = word.GetComponent<BoxCollider2D>();
            SetHeight(word, Y(bottomPx) - (box.offset.y - box.size.y * 0.5f));
            var heavy = word.gameObject.AddComponent<HeavyWord>();
            heavy.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            heavy.levelMask = LayerMask.GetMask("Level");
            return word;
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

        public const float InkReachPx = 8f;      // The pool reaches this far under its bottom, so a letter standing there is in it.
        const float InkSurfaceDropPx = 12f;      // The ~ line's baseline under the surface, so it sits on it.
        const float InkTildePx = 40f * 0.6f;

        /// <summary>
        /// A pool of ink from leftPx to rightPx, from its surface down to its bottom (usually the surface of the word
        /// it lies on). a floats in it, A sinks.
        /// </summary>
        static InkPool InkPool(Transform level, float leftPx, float rightPx, float surfacePx, float bottomPx,
            float surfaceRightPx = 0f)
        {
            var poolObject = new GameObject("Ink");
            poolObject.transform.SetParent(level, false);
            poolObject.layer = LayerMask.NameToLayer("Level");
            float widthPx = rightPx - leftPx;
            float bottom = bottomPx + InkReachPx;
            poolObject.transform.position = new Vector3(X(leftPx + widthPx * 0.5f), Y((surfacePx + bottom) * 0.5f), 0f);
            var trigger = poolObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(Size(widthPx), Size(bottom - surfacePx));

            Rect(poolObject.transform, "Tint", Vector2.zero, trigger.size, InkTint, -5);
            // The ~ line can stop short, where a wall stands in the ink and its surface is out of sight under it.
            float surfaceWidthPx = (surfaceRightPx > leftPx ? surfaceRightPx : rightPx) - leftPx;
            string tildes = new string('~', Mathf.Max(1, Mathf.FloorToInt(surfaceWidthPx / InkTildePx)));
            Text(poolObject.transform, "Ink Surface", tildes, leftPx, surfacePx + InkSurfaceDropPx, 40f, mono, InkSurface, -4);
            return poolObject.AddComponent<InkPool>();
        }

        const float WindTopPx = 64f;
        const float WindBottomPx = 640f;
        const float WindFullMarksBottomPx = 544f; // A full-height wind leaves out the rows under the ground.
        const float WindRowPx = 96f;
        const float WindMarkSpacingPx = 160f;
        const float WindBoxRowPx = 48f;         // A box is filled more densely, so its marks show where it ends.
        const float WindBoxMarkSpacingPx = 80f;
        const float WindMarkDropPx = 10f;       // From the middle of a row down to the marks' baseline: » is 20 px tall.
        const float WindMarkWidthPx = 22f;      // The ink of », from the mark's left edge.
        const float WindFadePx = 43f;           // How far from each end the marks fade in and out.
        const float WindBoxFadePx = 12f;
        public const float WindBoxReachPx = 8f; // A box reaches this far under its bottom, so a letter standing there is in it.
        public const float MinWindBoxPx = 96f;

        /// <summary>
        /// Wind blowing sideways from leftPx to rightPx: the whole height of the page like a Caps Lock stretch, or a
        /// box from topPx to bottomPx when they are given. Its marks are » (« when it blows left) in staggered rows
        /// that fill it, and they drift with it in play.
        /// </summary>
        static Wind WindStretch(Transform level, float leftPx, float rightPx, bool blowsLeft = false,
            float strength = 3f, float gustSeconds = 0f, float topPx = 0f, float bottomPx = 0f)
        {
            bool boxed = bottomPx > topPx;
            float top = boxed ? topPx : WindTopPx;
            float bottom = boxed ? bottomPx + WindBoxReachPx : WindBottomPx;

            var zoneObject = new GameObject("Wind");
            zoneObject.transform.SetParent(level, false);
            zoneObject.layer = LayerMask.NameToLayer("Level");
            float widthPx = rightPx - leftPx;
            zoneObject.transform.position = new Vector3(X(leftPx + widthPx * 0.5f), Y((top + bottom) * 0.5f), 0f);
            var trigger = zoneObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(Size(widthPx), Size(bottom - top));

            var wind = zoneObject.AddComponent<Wind>();
            wind.direction = blowsLeft ? -1f : 1f;
            wind.strength = strength;
            wind.gustSeconds = gustSeconds;
            wind.markWidth = Size(WindMarkWidthPx);
            wind.markFade = Size(boxed ? WindBoxFadePx : WindFadePx);

            var marks = new List<TextMeshPro>();
            string glyph = blowsLeft ? "\u00ab" : "\u00bb";
            // Spaced to fit the width and height a whole number of times, so the marks fill it evenly and stay evenly
            // spread as they wrap around. A mark's left edge runs over the width less its own, so all of it stays
            // inside. Halves round up, as the web page rounds them.
            float marksBottom = boxed ? bottomPx : WindFullMarksBottomPx;
            int rows = Mathf.Max(1, Mathf.FloorToInt((marksBottom - top) / (boxed ? WindBoxRowPx : WindRowPx) + 0.5f));
            float rowSpacing = (marksBottom - top) / rows;
            float runPx = Mathf.Max(1f, widthPx - WindMarkWidthPx);
            int perRow = Mathf.Max(1, Mathf.FloorToInt(runPx / (boxed ? WindBoxMarkSpacingPx : WindMarkSpacingPx) + 0.5f));
            float spacing = runPx / perRow;
            for (int row = 0; row < rows; row++)
            {
                float baseline = top + (row + 0.5f) * rowSpacing + WindMarkDropPx;
                for (int i = 0; i < perRow; i++)
                {
                    float x = leftPx + (i + (row % 2 == 0 ? 0.25f : 0.75f)) * spacing;
                    marks.Add(Text(zoneObject.transform, "Wind Mark", glyph, x, baseline, 40f, mono, WindMark, -3));
                }
            }
            wind.marks = marks.ToArray();
            return wind;
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
