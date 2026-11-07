using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LowercaseUppercase.Editor
{
    /// <summary>
    /// A long side-scrolling run over pits, tubes and stairs. The ground reads as one sentence, and the
    /// exit is the word the letter is missing from. The letter can't shoot until it hits a power block.
    /// Dying restarts the level: respawning at the last solid word would put it back on the enemy.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const float LowPlatform = 375f;  // A jump from the ground reaches it, and A still fits underneath.
        const float HighPlatform = 230f; // A jump from a low platform reaches it.
        const float TubeFontPx = 48f;
        const float TubeLineSpacing = -85f;
        const float StairFontPx = 72f;
        const float StairRowPx = 45f;
        static readonly Color Scenery = new Color(0.95f, 0.94f, 0.92f, 0.1f);

        /// <summary>Builds only this level, so edits made to the other scenes are left alone.</summary>
        [MenuItem("lowercase UPPERCASE/Rebuild Overworld Level")]
        public static void RebuildOverworld()
        {
            PrepareAssets(out FormSettings lower, out FormSettings upper);
            BuildOverworld(lower, upper);
            SetBuildScenes();
        }

        static void BuildOverworld(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(OverworldScene, "Overworld");
            float x = 40f;

            x = OpeningStretch(level, x);
            x = TubeStretch(level, x);
            x = PitStretch(level, x);
            x = AmpersandStretch(level, x);
            x = StairStretch(level, x);
            x = CastleStretch(level, x);

            FinishScene(OverworldScene, level, "Overworld · 4", lower, upper, new Vector2(110f, Ground),
                "home is a c", "a", "stle", scrollEndPx: x + 200f, respawnAtLastSafe: false, startsArmed: false);
            EditorSceneManager.OpenScene(OverworldScene);
        }

        // ---------------------------------------------------------------- stretches

        static float OpeningStretch(Transform level, float x)
        {
            Hill(level, x + 40f, 3);
            Cloud(level, x + 300f, 170f);
            Cloud(level, x + 980f, 130f);

            float left = x;
            Floor(level, "once upon a time", ref x);
            Note(level, left + 270f, 270f, "jump and hit it from below");
            PowerBlock(level, left + 370f, LowPlatform);

            left = x;
            WordBlock ground = Word(level, "a little letter", x, Ground, 72f);
            x = RightEdge(ground) + WordGap;
            BuildEnemy(level, ground, left + 420f);

            Word(level, "brick", left + 90f, LowPlatform, 72f);
            WordBlock high = Word(level, "mortar", left + 340f, HighPlatform, 72f);
            BuildEnemy(level, high, left + 460f);
            return x;
        }

        static float TubeStretch(Transform level, float x)
        {
            Hill(level, x + 300f, 2);
            Cloud(level, x + 500f, 150f);

            float left = x;
            WordBlock ground = Word(level, "left the page behind", x, Ground, 72f);
            x = RightEdge(ground);

            Tube(level, left + 110f, 3);
            BuildEnemy(level, ground, left + 340f);
            Tube(level, left + 500f, 5);
            return x;
        }

        static float PitStretch(Transform level, float x)
        {
            Cloud(level, x + 150f, 180f);
            x += 140f;

            float left = x;
            WordBlock ground = Word(level, "and ran", x, Ground, 72f);
            x = RightEdge(ground);
            PowerBlock(level, left + 90f, LowPlatform);
            x += 180f;

            // The upper route skips the enemies on the ground, and a third power block hangs where it ends.
            Hill(level, x + 260f, 3);
            left = x;
            ground = Word(level, "through all of the words", x, Ground, 72f);
            x = RightEdge(ground);
            BuildEnemy(level, ground, left + 300f);
            BuildEnemy(level, ground, left + 520f);

            Word(level, "high", left + 40f, LowPlatform, 72f);
            WordBlock higher = Word(level, "higher", left + 300f, HighPlatform, 72f);
            PowerBlock(level, RightEdge(higher) + 20f, LowPlatform - 60f);
            return x;
        }

        static float AmpersandStretch(Transform level, float x)
        {
            Cloud(level, x + 200f, 140f);
            x += 200f;

            float left = x;
            Hill(level, left + 700f, 2);
            WordBlock ground = Word(level, "where ampersands wander freely", x, Ground, 72f);
            x = RightEdge(ground);

            Tube(level, left + 640f, 4);
            BuildEnemy(level, ground, left + 200f);
            BuildEnemy(level, ground, left + 450f);
            BuildEnemy(level, ground, left + 950f);

            Word(level, "over", left + 200f, LowPlatform, 72f);
            Word(level, "them", left + 900f, LowPlatform, 72f);
            return x;
        }

        static float StairStretch(Transform level, float x)
        {
            Cloud(level, x + 100f, 130f);
            x += 150f;

            WordBlock ground = Word(level, "up the stairs", x, Ground, 72f);
            x = RightEdge(ground);
            Stairs(level, x, ascending: true);
            x += 130f;

            ground = Word(level, "and down", x, Ground, 72f);
            Stairs(level, x, ascending: false);
            return RightEdge(ground) + 150f;
        }

        static float CastleStretch(Transform level, float x)
        {
            Hill(level, x + 60f, 2);
            Cloud(level, x + 400f, 170f);

            float left = x;
            WordBlock ground = Word(level, "to the word it was missing from", x, Ground, 72f);
            x = RightEdge(ground) + WordGap;
            BuildEnemy(level, ground, left + 320f);

            // The last ground has no ascenders to tangle with the exit, and the gap before it stops enemies.
            float wordLeft = x + 110f;
            Floor(level, "so near now", ref x);

            // On foot, so the blank is just the last step of the run.
            Text(level, "Word c", "c", wordLeft, Ground, 72f, mono, Paper);
            Goal(level, wordLeft + 50f, Ground - 52f, 40f, 52f);
            Text(level, "Word stle", "stle", wordLeft + 96f, Ground, 72f, mono, Paper);
            return x;
        }

        // ---------------------------------------------------------------- pieces

        static void PowerBlock(Transform level, float leftPx, float topPx)
        {
            TextMeshPro text = Text(level, "Word power", "[<color=#FFD23F>*</color>]", leftPx, topPx, 72f, monoBold, Interactive);
            WordBlock block = AddBlock(text, WordKind.Power);
            MoveTopTo(block, topPx);
        }

        /// <summary>
        /// A wall of the same word stacked up. It is one word with one collider, because a letter jumping
        /// while pressed against separate rows catches its corner in the gaps between them and stops rising.
        /// </summary>
        static void Tube(Transform level, float leftPx, int rows)
        {
            Stacked(level, "tube", leftPx, rows, Ground);
        }

        /// <summary>The word repeated on several lines, as one word with one collider whose bottom is at bottomPx.</summary>
        static void Stacked(Transform level, string word, float leftPx, int rows, float bottomPx)
        {
            string content = string.Join("\n", System.Linq.Enumerable.Repeat(word, rows));
            TextMeshPro text = Text(level, $"Word {word}", content, leftPx, bottomPx, TubeFontPx, mono, Muted);
            text.lineSpacing = TubeLineSpacing;
            WordBlock block = AddBlock(text, WordKind.Solid);
            block.FitCollider();
            var box = block.GetComponent<BoxCollider2D>();
            SetHeight(block, Y(bottomPx) - (box.offset.y - box.size.y * 0.5f));
        }

        /// <summary>Stairs standing on the ground, either climbing up to edgePx or falling away from it.</summary>
        static void Stairs(Transform level, float edgePx, bool ascending)
        {
            const string word = "stairs";
            float charWidth = StairFontPx * 0.6f;

            for (int row = 0; row < word.Length; row++)
            {
                int letters = word.Length - row;
                float baseline = Ground - row * StairRowPx;
                if (ascending)
                    OnBaseline(level, word.Substring(row), edgePx - letters * charWidth, baseline, StairFontPx, Muted);
                else
                    OnBaseline(level, word.Substring(0, letters), edgePx, baseline, StairFontPx, Muted);
            }
        }

        static WordBlock OnBaseline(Transform level, string content, float leftPx, float baselinePx, float fontPx, Color color)
        {
            WordBlock block = Word(level, content, leftPx, baselinePx, fontPx, WordKind.Solid, color);
            SetHeight(block, Y(baselinePx));
            return block;
        }

        static void Note(Transform level, float leftPx, float baselinePx, string content)
        {
            Text(level, $"Note {content}", content, leftPx, baselinePx, 15f, mono, Muted, -2);
        }

        static void Cloud(Transform level, float leftPx, float baselinePx)
        {
            Text(level, "Cloud", "cloud", leftPx, baselinePx, 40f, mono, Scenery, -5);
        }

        /// <summary>Background only: rows of the word, narrowing towards the top.</summary>
        static void Hill(Transform level, float leftPx, int rows)
        {
            const float fontPx = 36f;
            float charWidth = fontPx * 0.6f;
            for (int row = 0; row < rows; row++)
            {
                string content = string.Join(" ", System.Linq.Enumerable.Repeat("hill", rows - row));
                float indent = row * (5f * charWidth) * 0.5f;
                Text(level, "Hill", content, leftPx + indent, Ground - 8f - row * 30f, fontPx, mono, Scenery, -5);
            }
        }
    }
}
