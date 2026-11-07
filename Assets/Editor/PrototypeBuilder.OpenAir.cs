using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LowercaseUppercase.Editor
{
    /// <summary>
    /// Chapter 4, levels 10 and 11. Chain is comma hooks over drops under a ceiling: a hangs and swings, A snaps them.
    /// Ladder is `up` lifts in the open, which carry a quickly and A slowly. Skyline, the chapter's test, follows.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const string ChainScene = SceneFolder + "/Chain.unity";
        const string LadderScene = SceneFolder + "/Ladder.unity";
        const float LiftTop = 235f;     // A lift from the ground ends here, just above a platform at LiftLanding.
        const float LiftLanding = 300f;
        const float HighLiftTop = 110f; // A second lift from LiftLanding ends here, above HighLiftLanding.
        const float HighLiftLanding = 175f;

        [MenuItem("lowercase UPPERCASE/Rebuild Chapter 4 Levels")]
        public static void RebuildChapter4()
        {
            PrepareAssets(out FormSettings lower, out FormSettings upper);
            BuildChain(lower, upper);
            BuildLadder(lower, upper);
            BuildSkyline(lower, upper);
            SetBuildScenes();
        }

        // ---------------------------------------------------------------- chain

        static void BuildChain(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(ChainScene, "Chain");
            float x = 40f;

            x = ChainOpening(level, x);
            x = ChainOneHook(level, x);
            x = ChainTwoHooks(level, x);
            x = ChainBrickLanding(level, x);
            x = ChainThreeHooks(level, x);
            x = ChainGuardedLanding(level, x);
            x = ChainLowLanding(level, x);
            x = Exit(level, x, "let go now", "ch", "in");
            Ceiling(level, x + 400f);

            FinishScene(ChainScene, level, "Chain · 10", lower, upper, new Vector2(110f, Ground),
                "hang from a ch", "a", "in", scrollEndPx: x + 200f, startsArmed: false,
                nextScene: SceneName(LadderScene));
            EditorSceneManager.OpenScene(ChainScene);
        }

        static float ChainOpening(Transform level, float x)
        {
            float left = x;
            Floor(level, "a comma is a pause", ref x);
            HashesOn(level, Word(level, "rest", left + 560f, LowPlatform, 72f), 2);
            return x;
        }

        static float ChainOneHook(Transform level, float x)
        {
            Note(level, x - 200f, 440f, "jump to a comma. jump again to let go.");
            WordBlock ground = Word(level, "hold on", x, Ground, 72f);
            x = HookChain(level, RightEdge(ground), 1);
            Floor(level, "and over", ref x);
            return x;
        }

        static float ChainTwoHooks(Transform level, float x)
        {
            Note(level, x, 440f, "hold right to swing further");
            WordBlock ground = Word(level, "swing", x, Ground, 72f);
            x = HookChain(level, RightEdge(ground), 2);
            Floor(level, "and land", ref x);
            return x;
        }

        // Small to hang, big the moment the far side is reached.
        static float ChainBrickLanding(Transform level, float x)
        {
            Note(level, x, 440f, "A would snap a comma");
            WordBlock ground = Word(level, "pause", x, Ground, 72f);
            x = HookChain(level, RightEdge(ground), 2);

            float left = x;
            WordBlock landing = Word(level, "then break", x, Ground, 72f);
            BrickWall(level, left + 220f, CeilingBrickLetters);
            x = RightEdge(landing) + WordGap;

            left = x;
            Floor(level, "hang on", ref x);
            PlaceCheckpoint(level, left + 80f, Ground);
            PowerBlock(level, left + 160f, LowPlatform);
            return x;
        }

        static float ChainThreeHooks(Transform level, float x)
        {
            WordBlock ground = Word(level, "a chain", x, Ground, 72f);
            x = HookChain(level, RightEdge(ground), 3);
            Floor(level, "of commas", ref x);
            return x;
        }

        static float ChainGuardedLanding(Transform level, float x)
        {
            WordBlock ground = Word(level, "careful", x, Ground, 72f);
            x = HookChain(level, RightEdge(ground), 2);

            float left = x;
            WordBlock landing = Word(level, "someone is waiting", x, Ground, 72f);
            BuildEnemy(level, landing, left + 300f);
            x = RightEdge(landing) + WordGap;

            left = x;
            Floor(level, "breathe", ref x);
            PlaceCheckpoint(level, left + 80f, Ground);
            return x;
        }

        static float ChainLowLanding(Transform level, float x)
        {
            WordBlock ground = Word(level, "last swing", x, Ground, 72f);
            x = HookChain(level, RightEdge(ground), 2);

            float left = x;
            WordBlock landing = Word(level, "into a low room", x, Ground, 72f);
            Stacked(level, ShortLowWord, left + 200f, CeilingTunnelRows, TunnelBottom);
            HashRow(level, left + 250f, OnGround, 3);
            return RightEdge(landing) + WordGap;
        }

        // ---------------------------------------------------------------- ladder

        static void BuildLadder(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(LadderScene, "Ladder");
            float x = 40f;

            x = LadderOpening(level, x);
            x = LadderFirstLift(level, x);
            x = LadderTwoLifts(level, x);
            x = LadderBrickTop(level, x);
            x = LadderGuardedTop(level, x);
            x = LadderLongDrop(level, x);
            x = LadderEnd(level, x);

            FinishScene(LadderScene, level, "Ladder · 11", lower, upper, new Vector2(110f, Ground),
                "climb a l", "a", "dder", scrollEndPx: x + 200f, startsArmed: false,
                nextScene: SceneName(SkylineScene));
            EditorSceneManager.OpenScene(LadderScene);
        }

        static float LadderOpening(Transform level, float x)
        {
            Hill(level, x + 40f, 3);
            Cloud(level, x + 600f, 150f);
            float left = x;
            Floor(level, "the word says up", ref x);
            return x;
        }

        static float LadderFirstLift(Transform level, float x)
        {
            Note(level, x - 160f, 440f, "step into the lift");
            x = Lift(level, x, LiftTop);
            float left = x;
            WordBlock top = Word(level, "so it goes", x, LiftLanding, 72f);
            HashesOn(level, top, 4);
            x = RightEdge(top) + 80f;
            Floor(level, "and down", ref x);
            return x;
        }

        // Lift to a platform, and a second lift from there to the top of the screen.
        static float LadderTwoLifts(Transform level, float x)
        {
            Cloud(level, x + 300f, 560f);
            x = Lift(level, x, LiftTop);
            WordBlock first = Word(level, "higher", x, LiftLanding, 72f);
            x = RightEdge(first);
            x = Lift(level, x, HighLiftTop, LiftLanding);
            WordBlock second = Word(level, "highest", x, HighLiftLanding, 72f);
            HashesOn(level, second, 4);
            x = RightEdge(second) + 100f;
            Floor(level, "a long way down", ref x);
            return x;
        }

        static float LadderBrickTop(Transform level, float x)
        {
            Note(level, x - 200f, 440f, "A rises slowly");
            x = Lift(level, x, LiftTop);
            float left = x;
            WordBlock top = Word(level, "brick at the top", x, LiftLanding, 72f);
            BrickWall(level, left + 200f, BrickLetters, LiftLanding); // Up into the sky, so it can't be jumped.
            HashRow(level, left + 300f, LiftLanding - 26f, 3);
            x = RightEdge(top) + 80f;

            left = x;
            Floor(level, "rest", ref x);
            PlaceCheckpoint(level, left + 60f, Ground);
            PowerBlock(level, left + 20f, LowPlatform);
            return x;
        }

        static float LadderGuardedTop(Transform level, float x)
        {
            Cloud(level, x + 200f, 140f);
            x = Lift(level, x, LiftTop);
            float left = x;
            WordBlock top = Word(level, "someone up here", x, LiftLanding, 72f);
            BuildEnemy(level, top, left + 400f);
            x = RightEdge(top) + 80f;

            left = x;
            WordBlock ground = Word(level, "and below", x, Ground, 72f);
            BuildEnemy(level, ground, left + 200f);
            return RightEdge(ground) + WordGap;
        }

        static float LadderLongDrop(Transform level, float x)
        {
            x = Lift(level, x, LiftTop);
            WordBlock first = Word(level, "step", x, LiftLanding, 72f);
            x = RightEdge(first);
            x = Lift(level, x, HighLiftTop, LiftLanding);
            WordBlock second = Word(level, "and then", x, HighLiftLanding, 72f);
            x = RightEdge(second) + 100f;

            float left = x;
            WordBlock ground = Word(level, "all the way down low", x, Ground, 72f);
            Stacked(level, ShortLowWord, left + 400f, TunnelRows, TunnelBottom);
            HashArc(level, left - 100f, left + 200f, 3, OnGround - 160f, -100f);
            x = RightEdge(ground) + WordGap;

            left = x;
            Floor(level, "nearly", ref x);
            PlaceCheckpoint(level, left + 80f, Ground);
            return x;
        }

        // The exit is at the top of the last lift.
        static float LadderEnd(Transform level, float x)
        {
            Floor(level, "one more", ref x);
            x = Lift(level, x, LiftTop);
            float wordLeft = x + 150f;
            WordBlock top = Word(level, "the top rung", x, LiftLanding, 72f);
            ExitWord(level, wordLeft, LiftLanding, "l", "dder", LetterForm.Lowercase);
            return RightEdge(top) + WordGap;
        }
    }
}
