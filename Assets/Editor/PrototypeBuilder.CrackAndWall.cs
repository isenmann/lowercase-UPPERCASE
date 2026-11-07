using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LowercaseUppercase.Editor
{
    /// <summary>
    /// The second and third levels. Crack teaches the contrast between the forms under a ceiling: a fits low gaps,
    /// and A smashes cracked words. Wall tests the same two ideas in the open, with almost no notes.
    /// Build indices come after the older scenes so theirs don't change, but they play straight after the Overworld.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const string CrackScene = SceneFolder + "/Crack.unity";
        const string WallScene = SceneFolder + "/Wall.unity";
        const string BrickLetters = "b\nr\ni\nc\nk\nb\nr\ni\nc\nk";
        const string ShortLowWord = "lowlowlo";
        const string LongLowWord = "lowlowlowlowlow";
        const string CeilingBrickLetters = "b\nr\ni\nc\nk\nb\nr\ni\nc"; // Stops inside the ceiling instead of poking out above it.
        const int CeilingTunnelRows = 9;  // The same: just tall enough to meet the ceiling.
        const float WidePit = 170f; // Wider than A can jump, and well within what a can.

        /// <summary>Builds only these two levels, so edits made to the other scenes are left alone.</summary>
        [MenuItem("lowercase UPPERCASE/Rebuild Crack and Wall Levels")]
        public static void RebuildCrackAndWall()
        {
            PrepareAssets(out FormSettings lower, out FormSettings upper);
            BuildCrack(lower, upper);
            BuildWall(lower, upper);
            SetBuildScenes();
        }

        // ---------------------------------------------------------------- crack

        static void BuildCrack(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(CrackScene, "Crack");
            float x = 40f;

            x = CrackOpening(level, x);
            x = CrackFirstTunnel(level, x);
            x = CrackFirstWall(level, x);
            x = CrackStomp(level, x);
            x = CrackRest(level, x);
            x = CrackTunnelThenWall(level, x);
            x = CrackSecondRest(level, x);
            x = CrackBigSmallBig(level, x);
            x = CrackLongHall(level, x);
            x = CrackSecondStomp(level, x);
            x = CrackEnd(level, x);
            Ceiling(level, x + 400f);

            FinishScene(CrackScene, level, "Crack · 7", lower, upper, new Vector2(110f, Ground),
                "hide in a cr", "a", "ck", scrollEndPx: x + 200f, startsArmed: false,
                nextScene: SceneName(WallScene));
            EditorSceneManager.OpenScene(CrackScene);
        }

        static float CrackOpening(Transform level, float x)
        {
            float left = x;
            Floor(level, "a small letter", ref x);
            PowerBlock(level, left + 340f, LowPlatform);
            return x;
        }

        static float CrackFirstTunnel(Transform level, float x)
        {
            float left = x;
            Floor(level, "can slip under low words", ref x);
            Note(level, left + 60f, 440f, "a fits here. A does not.");
            Stacked(level, ShortLowWord, left + 420f, CeilingTunnelRows, TunnelBottom);
            return x;
        }

        static float CrackFirstWall(Transform level, float x)
        {
            float left = x;
            Floor(level, "and a big letter", ref x);
            Note(level, left + 60f, 440f, "hold Shift and run into the brick");
            BrickWall(level, left + 420f, CeilingBrickLetters);
            return x;
        }

        static float CrackStomp(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "can land on ampersands", x, Ground, 72f);
            Note(level, left + 60f, 300f, "jump off the shelf and grow on the way down");
            Word(level, "shelf", left + 180f, LowPlatform, 72f);
            BuildEnemy(level, ground, left + 520f);
            return RightEdge(ground) + WidePit;
        }

        // The middle of the level. No enemy can walk here, because each one stays on its own word.
        static float CrackRest(Transform level, float x)
        {
            float left = x;
            Note(level, left - 150f, 440f, "too wide for A");
            Floor(level, "rest here", ref x);
            PlaceCheckpoint(level, left + 160f, Ground);
            return x;
        }

        static float CrackTunnelThenWall(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "ampersands hide in the low words", x, Ground, 72f);
            x = RightEdge(ground) + WordGap;
            Stacked(level, LongLowWord, left + 520f, CeilingTunnelRows, TunnelBottom);
            BuildEnemy(level, ground, left + 300f);
            BrickWall(level, left + 1150f, CeilingBrickLetters);

            left = x;
            Floor(level, "then small again", ref x);
            Stacked(level, ShortLowWord, left + 220f, CeilingTunnelRows, TunnelBottom);
            return x;
        }

        // The second half has no notes: it only repeats what the first half taught, closer together.
        static float CrackSecondRest(Transform level, float x)
        {
            float left = x;
            Floor(level, "rest again", ref x);
            PlaceCheckpoint(level, left + 120f, Ground);
            return x;
        }

        static float CrackBigSmallBig(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "two bricks and a low door", x, Ground, 72f);
            BuildEnemy(level, ground, left + 60f);
            BrickWall(level, left + 300f, CeilingBrickLetters);
            Stacked(level, ShortLowWord, left + 560f, CeilingTunnelRows, TunnelBottom);
            BrickWall(level, left + 900f, CeilingBrickLetters);
            return RightEdge(ground) + WordGap;
        }

        // A long squeeze, then brick almost straight after it: grow the moment the ceiling lifts.
        static float CrackLongHall(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "a long hall and a hard door", x, Ground, 72f);
            BuildEnemy(level, ground, left + 20f);
            Stacked(level, LongLowWord, left + 160f, CeilingTunnelRows, TunnelBottom);
            BrickWall(level, left + 700f, CeilingBrickLetters);
            return RightEdge(ground) + WidePit;
        }

        static float CrackSecondStomp(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "watch your head", x, Ground, 72f);
            Word(level, "ledge", left + 100f, LowPlatform, 72f);
            BuildEnemy(level, ground, left + 420f);
            return RightEdge(ground) + WidePit;
        }

        static float CrackEnd(Transform level, float x)
        {
            float wordLeft = x + 110f;
            Floor(level, "one more room", ref x);
            Text(level, "Word cr", "cr", wordLeft, Ground, 72f, mono, Paper);
            Goal(level, wordLeft + 94f, Ground - 52f, 40f, 52f);
            Text(level, "Word ck", "ck", wordLeft + 140f, Ground, 72f, mono, Paper);
            return x;
        }

        // ---------------------------------------------------------------- wall

        static void BuildWall(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(WallScene, "Wall");
            float x = 40f;

            x = WallOpening(level, x);
            x = WallStairs(level, x);
            x = WallDoor(level, x);
            x = WallThickWall(level, x);
            x = WallUpAndOver(level, x);
            x = WallLowRoad(level, x);
            x = WallOnTheHill(level, x);
            x = WallClosingRun(level, x);
            x = WallEnd(level, x);

            FinishScene(WallScene, level, "Wall · 8", lower, upper, new Vector2(110f, Ground),
                "knock on the w", "a", "ll", scrollEndPx: x + 200f, startsArmed: false,
                nextScene: SceneName(CellarScene));
            EditorSceneManager.OpenScene(WallScene);
        }

        static float WallOpening(Transform level, float x)
        {
            Hill(level, x + 40f, 2);
            Cloud(level, x + 300f, 170f);
            Cloud(level, x + 980f, 140f);

            float left = x;
            Floor(level, "knock knock", ref x);
            PowerBlock(level, left + 200f, LowPlatform);

            left = x;
            WordBlock ground = Word(level, "who is there", x, Ground, 72f);
            Word(level, "hello", left + 60f, LowPlatform, 72f);
            BuildEnemy(level, ground, left + 300f);
            return RightEdge(ground) + WidePit;
        }

        static float WallStairs(Transform level, float x)
        {
            Cloud(level, x + 200f, 200f);
            WordBlock ground = Word(level, "only a letter", x, Ground, 72f);
            x = RightEdge(ground);
            Stairs(level, x, ascending: true);
            return x + WidePit;
        }

        static float WallDoor(Transform level, float x)
        {
            Hill(level, x + 500f, 2);
            Floor(level, "let it in", ref x);

            float left = x;
            WordBlock ground = Word(level, "the door is low", x, Ground, 72f);
            x = RightEdge(ground) + WordGap;
            Stacked(level, ShortLowWord, left + 240f, TunnelRows, TunnelBottom);
            BuildEnemy(level, ground, left + 60f);
            return x;
        }

        static float WallThickWall(Transform level, float x)
        {
            Cloud(level, x + 560f, 150f);

            float left = x;
            WordBlock ground = Word(level, "the wall is thick", x, Ground, 72f);
            BuildEnemy(level, ground, left + 100f);
            BrickWall(level, left + 360f);

            // Past the wall, which comes back on a reload and keeps this word's enemy on its far side.
            PlaceCheckpoint(level, left + 520f, Ground);
            return RightEdge(ground) + WidePit;
        }

        // Only a can climb the stairs: each step is taller than A can jump.
        static float WallUpAndOver(Transform level, float x)
        {
            Hill(level, x, 2);
            WordBlock ground = Word(level, "up and over", x, Ground, 72f);
            x = RightEdge(ground);
            Stairs(level, x, ascending: true);
            x += WidePit;

            Cloud(level, x + 100f, 150f);
            ground = Word(level, "and down again", x, Ground, 72f);
            Stairs(level, x, ascending: false);
            return RightEdge(ground) + WordGap;
        }

        static float WallLowRoad(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "the low road", x, Ground, 72f);
            BuildEnemy(level, ground, left + 20f);
            Stacked(level, ShortLowWord, left + 240f, TunnelRows, TunnelBottom);
            x = RightEdge(ground) + WordGap;

            left = x;
            Floor(level, "almost there", ref x);
            PlaceCheckpoint(level, left + 160f, Ground);
            return x;
        }

        // Small to climb the stairs, then big for the wall at the bottom of the other side.
        static float WallOnTheHill(Transform level, float x)
        {
            Hill(level, x, 2);
            WordBlock ground = Word(level, "a wall on the hill", x, Ground, 72f);
            x = RightEdge(ground);
            Stairs(level, x, ascending: true);
            x += WidePit;

            float left = x;
            Cloud(level, x + 200f, 160f);
            ground = Word(level, "knock it down", x, Ground, 72f);
            Stairs(level, x, ascending: false);
            BrickWall(level, left + 380f);
            BuildEnemy(level, ground, left + 280f); // Pacing between the bottom step and the wall, right where the letter lands.
            return RightEdge(ground) + WordGap;
        }

        // The ending for this chapter: grow for the wall, then shrink again for the pit straight after it.
        static float WallClosingRun(Transform level, float x)
        {
            Floor(level, "so mind the gap", ref x);

            float left = x;
            Cloud(level, x + 400f, 180f);
            WordBlock ground = Word(level, "small and then big", x, Ground, 72f);
            Stacked(level, ShortLowWord, left + 100f, TunnelRows, TunnelBottom);
            BuildEnemy(level, ground, left + 400f);
            BrickWall(level, left + 620f);
            return RightEdge(ground) + WidePit;
        }

        static float WallEnd(Transform level, float x)
        {
            float wordLeft = x + 110f;
            Floor(level, "see you soon", ref x);
            Text(level, "Word w", "w", wordLeft, Ground, 72f, mono, Paper);
            Goal(level, wordLeft + 50f, Ground - 52f, 40f, 52f);
            Text(level, "Word ll", "ll", wordLeft + 96f, Ground, 72f, mono, Paper);
            return x;
        }

        // ---------------------------------------------------------------- pieces

        static void BrickWall(Transform level, float leftPx, string letters = BrickLetters)
        {
            WordBlock brick = Column(level, letters, leftPx, Ground, 46f, WordKind.Cracked, Breakable);
            brick.gameObject.name = "Word brick";
        }
    }
}
