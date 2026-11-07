using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LowercaseUppercase.Editor
{
    /// <summary>
    /// Chapter 5, levels 13 to 15. Flames is hot words under a ceiling, which burn anyone who lingers: a runs across,
    /// A is too slow. Tar is sticky floors and climbable walls in the open. Both bring in Caps Lock zones, where Shift
    /// works backwards until the key is found. Final uses everything, and its blank only takes A.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const string FlamesScene = SceneFolder + "/Flames.unity";
        const string TarScene = SceneFolder + "/Tar.unity";
        const string FinalScene = SceneFolder + "/Final.unity";

        [MenuItem("lowercase UPPERCASE/Rebuild Chapter 5 Levels")]
        public static void RebuildChapter5()
        {
            PrepareAssets(out FormSettings lower, out FormSettings upper);
            BuildFlames(lower, upper);
            BuildTar(lower, upper);
            BuildFinal(lower, upper);
            SetBuildScenes();
        }

        /// <summary>A word, then a hot word, then a word: the cursor moves past all three.</summary>
        static void HotBetween(Transform level, ref float x, string before, string hot, string after)
        {
            Floor(level, before, ref x);
            x = RightEdge(HotWord(level, hot, x)) + WordGap;
            Floor(level, after, ref x);
        }

        /// <summary>The top of a block's collider in mockup pixels.</summary>
        static float TopPx(WordBlock block)
        {
            var box = block.GetComponent<BoxCollider2D>();
            return 360f - (block.transform.TransformPoint(box.offset).y + box.size.y * 0.5f) * PixelsPerUnit;
        }

        // ---------------------------------------------------------------- flames

        static void BuildFlames(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(FlamesScene, "Flames");
            float x = 40f;

            x = FlamesOpening(level, x);
            x = FlamesFirstHeat(level, x);
            x = FlamesHop(level, x);
            x = FlamesLowAndHot(level, x);
            x = FlamesSteps(level, x);
            x = FlamesGuarded(level, x);
            x = FlamesBrick(level, x);
            x = FlamesLongHop(level, x);
            x = FlamesLock(level, x);
            x = FlamesLast(level, x);
            x = Exit(level, x, "out of the fire", "fl", "mes");
            Ceiling(level, x + 400f);

            FinishScene(FlamesScene, level, "Flames · 13", lower, upper, new Vector2(110f, Ground),
                "run through the fl", "a", "mes", scrollEndPx: x + 200f, startsArmed: false,
                nextScene: SceneName(TarScene));
            EditorSceneManager.OpenScene(FlamesScene);
        }

        static float FlamesOpening(Transform level, float x)
        {
            float left = x;
            Floor(level, "it is getting warm", ref x);
            PowerBlock(level, left + 330f, LowPlatform);
            HashesOn(level, Word(level, "warm", left + 560f, LowPlatform, 72f), 2);
            return x;
        }

        static float FlamesFirstHeat(Transform level, float x)
        {
            Note(level, x + 60f, 440f, "don't stop on a hot word. A is too slow.");
            HotBetween(level, ref x, "keep", "hot", "moving");
            return x;
        }

        static float FlamesHop(Transform level, float x)
        {
            Note(level, x, 440f, "hop across. the air is cool.");
            x = RightEdge(HotWord(level, "hot hot hot", x)) + WordGap;
            HashArc(level, x - 470f, x - 30f, 4, OnGround, 120f);

            float left = x;
            WordBlock ground = Word(level, "cooler", x, Ground, 72f);
            BuildEnemy(level, ground, left + 120f);
            return RightEdge(ground) + WordGap;
        }

        static float FlamesLowAndHot(Transform level, float x)
        {
            float left = x;
            Floor(level, "safe", ref x);
            PlaceCheckpoint(level, left + 50f, Ground);

            // The low gap hangs over the hot word, so a can't hop here and has to keep running.
            left = x;
            HotBetween(level, ref x, "low", "hot", "and on");
            Stacked(level, ShortLowWord, left + 90f, CeilingTunnelRows, TunnelBottom);
            HashRow(level, left + 140f, OnGround, 3);
            return x;
        }

        static float FlamesSteps(Transform level, float x)
        {
            WordBlock hot = HotWord(level, "hot", x, 450f);
            x = RightEdge(hot) + 60f;
            HashesOn(level, Platform(level, "cool", 380f, ref x, 60f), 2);
            hot = HotWord(level, "hot", x, 450f);
            x = RightEdge(hot) + 60f;
            Floor(level, "down", ref x);
            return x;
        }

        // The only cool word between two hot ones has an enemy on it.
        static float FlamesGuarded(Transform level, float x)
        {
            x = RightEdge(HotWord(level, "hot", x)) + WordGap;
            float left = x;
            WordBlock cool = Word(level, "crowded", x, Ground, 72f);
            BuildEnemy(level, cool, left + 150f);
            x = RightEdge(cool) + WordGap;
            x = RightEdge(HotWord(level, "hot", x)) + WordGap;
            Floor(level, "phew", ref x);
            return x;
        }

        // Small to cross the heat, big on the safe word for the brick.
        static float FlamesBrick(Transform level, float x)
        {
            x = RightEdge(HotWord(level, "hot", x)) + WordGap;
            float left = x;
            WordBlock ground = Word(level, "then a wall", x, Ground, 72f);
            BrickWall(level, left + 220f, CeilingBrickLetters);
            HashRow(level, left + 300f, OnGround, 3);
            x = RightEdge(ground) + WordGap;
            x = RightEdge(HotWord(level, "hot", x)) + WordGap;
            Floor(level, "and cool", ref x);
            return x;
        }

        // Too long to cross in one hop: land on the shelf above it on the way.
        static float FlamesLongHop(Transform level, float x)
        {
            float left = x;
            x = RightEdge(HotWord(level, "hot hot hot hot", x)) + WordGap;
            WordBlock shelf = Word(level, "ash", left + 260f, LowPlatform, 72f);
            HashesOn(level, shelf, 2);
            HashArc(level, left + 20f, left + 260f, 3, OnGround, 110f);
            Floor(level, "embers", ref x);
            return x;
        }

        // Inside the lock, A is the default: brick is easy, and the hot word needs Shift held to cross as a.
        static float FlamesLock(Transform level, float x)
        {
            float lockLeft = x;
            Note(level, x, 440f, "CAPS LOCK: you are A. hold Shift to be small.");
            Floor(level, "locked", ref x);

            float left = x;
            WordBlock ground = Word(level, "in caps", x, Ground, 72f);
            BrickWall(level, left + 140f, CeilingBrickLetters);
            x = RightEdge(ground) + WordGap;

            x = RightEdge(HotWord(level, "hot", x)) + WordGap;
            float keyLeft = x + 60f;
            Floor(level, "the key", ref x);
            CapsLock(level, lockLeft, x, keyLeft, Ground - 60f);
            return x;
        }

        static float FlamesLast(Transform level, float x)
        {
            float left = x;
            Floor(level, "free", ref x);
            PlaceCheckpoint(level, left + 50f, Ground);

            x = RightEdge(HotWord(level, "hot hot", x)) + WordGap;
            left = x;
            WordBlock ground = Word(level, "smoke and ash", x, Ground, 72f);
            BuildEnemy(level, ground, left + 300f);
            return RightEdge(ground) + WordGap;
        }

        // ---------------------------------------------------------------- tar

        static void BuildTar(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(TarScene, "Tar");
            float x = 40f;

            x = TarOpening(level, x);
            x = TarSlow(level, x);
            x = TarFirstWall(level, x);
            x = TarLaunch(level, x);
            x = TarGuarded(level, x);
            x = TarTwoWalls(level, x);
            x = TarLock(level, x);
            x = TarHighWall(level, x);
            x = Exit(level, x, "unstuck at last", "t", "r");

            FinishScene(TarScene, level, "Tar · 14", lower, upper, new Vector2(110f, Ground),
                "stuck in the t", "a", "r", scrollEndPx: x + 200f, startsArmed: false,
                nextScene: SceneName(FinalScene));
            EditorSceneManager.OpenScene(TarScene);
        }

        static float TarOpening(Transform level, float x)
        {
            Hill(level, x + 40f, 3);
            Cloud(level, x + 500f, 150f);
            float left = x;
            Floor(level, "the road is black", ref x);
            PowerBlock(level, left + 300f, LowPlatform);
            return x;
        }

        static float TarSlow(Transform level, float x)
        {
            Note(level, x + 60f, 440f, "tar slows you and holds you down");
            Floor(level, "dry", ref x);
            float left = x;
            WordBlock tar = StickyFloor(level, "sticky tar", x);
            HashRow(level, left + 120f, OnGround, 4);
            x = RightEdge(tar) + WordGap;
            Floor(level, "dry again", ref x);
            return x;
        }

        // Too tall to jump: press into it and jump again and again.
        static float TarFirstWall(Transform level, float x)
        {
            Cloud(level, x + 300f, 120f);
            Note(level, x, 440f, "press into a sticky wall and jump");
            float left = x;
            WordBlock ground = Word(level, "climb", x, Ground, 72f);
            x = RightEdge(ground);
            WordBlock wall = StickyWall(level, x);
            float top = TopPx(wall);
            HashRow(level, x - 40f, top - 60f, 2);
            x = RightEdge(wall) + WordGap;
            Floor(level, "and over", ref x);
            return x;
        }

        // Tar holds the jump down, so the pit is only cleared from the dry word at its edge.
        static float TarLaunch(Transform level, float x)
        {
            Note(level, x, 440f, "jump from dry ground");
            float left = x;
            WordBlock tar = StickyFloor(level, "tar tar tar", x);
            HashRow(level, left + 80f, OnGround, 3);
            x = RightEdge(tar) + WordGap;
            WordBlock dry = Word(level, "dry", x, Ground, 72f);
            x = RightEdge(dry);
            HashArc(level, x - 20f, x + WidePit + 20f, 3);
            x += WidePit;
            Floor(level, "made it over", ref x);
            return x;
        }

        static float TarTwoWalls(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "up", x, Ground, 72f);
            x = RightEdge(ground);
            WordBlock wall = StickyWall(level, x);
            HashRow(level, x - 40f, TopPx(wall) - 60f, 2);
            x = RightEdge(wall) + WordGap;

            left = x;
            WordBlock low = Word(level, "then under", x, Ground, 72f);
            Stacked(level, ShortLowWord, left + 120f, TunnelRows, TunnelBottom);
            HashRow(level, left + 170f, OnGround, 3);
            x = RightEdge(low);
            wall = StickyWall(level, x);
            x = RightEdge(wall) + WordGap;
            WordBlock after = StickyFloor(level, "and up again", x);
            BuildEnemy(level, after, x + 250f);
            return RightEdge(after) + WordGap;
        }

        static float TarGuarded(Transform level, float x)
        {
            float left = x;
            WordBlock tar = StickyFloor(level, "slow going", x);
            BuildEnemy(level, tar, left + 260f);
            x = RightEdge(tar) + WordGap;

            left = x;
            Floor(level, "firm", ref x);
            PlaceCheckpoint(level, left + 40f, Ground);
            return x;
        }

        // A by default in the lock: the brick is easy, tar wants Shift held, and the key is on top of a sticky wall.
        static float TarLock(Transform level, float x)
        {
            float lockLeft = x;
            Note(level, x, 440f, "CAPS LOCK again. hold Shift to be small.");
            float left = x;
            WordBlock ground = Word(level, "locked out", x, Ground, 72f);
            BrickWall(level, left + 200f);
            x = RightEdge(ground) + WordGap;

            left = x;
            WordBlock tar = StickyFloor(level, "tar", x);
            x = RightEdge(tar);
            WordBlock wall = StickyWall(level, x);
            float top = TopPx(wall);
            float keyLeft = RightEdge(wall) + 20f;
            WordBlock perch = Word(level, "here", RightEdge(wall), top, 72f);
            x = RightEdge(perch) + WordGap;
            CapsLock(level, lockLeft, x, keyLeft, top - 40f);

            left = x;
            Floor(level, "unlocked", ref x);
            PlaceCheckpoint(level, left + 80f, Ground);
            return x;
        }

        static float TarHighWall(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "one last climb", x, Ground, 72f);
            BuildEnemy(level, ground, left + 100f);
            x = RightEdge(ground);
            WordBlock wall = StickyWall(level, x, "t\na\nr\nw\na\nl\nl");
            float top = TopPx(wall);
            WordBlock ledge = Word(level, "on top", RightEdge(wall), top, 72f);
            HashesOn(level, ledge, 3);
            return RightEdge(ledge) + WordGap;
        }

        // ---------------------------------------------------------------- final

        static void BuildFinal(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(FinalScene, "Final");
            float x = 40f;

            x = FinalOpening(level, x);
            x = FinalBridgeAndBrick(level, x);
            x = FinalDoor(level, x);
            x = FinalUnderground(level, x);
            x = FinalIceAndHooks(level, x);
            x = FinalLock(level, x);
            x = ShiftRun(level, x, true, "small words", "big words", "small words", "big words",
                "small words", "big words", "small words");
            x = FinalEnd(level, x);
            Ceiling(level, x + 400f);

            FinishScene(FinalScene, level, "Final · 15", lower, upper, new Vector2(110f, Ground),
                "THE FIN", "A", "L WORD", scrollEndPx: x + 200f, startsArmed: false,
                nextScene: SceneName(MenuScene));
            EditorSceneManager.OpenScene(FinalScene);
        }

        static float FinalOpening(Transform level, float x)
        {
            float left = x;
            Floor(level, "every word at once", ref x);
            PowerBlock(level, left + 330f, LowPlatform);
            return x;
        }

        static float FinalBridgeAndBrick(Transform level, float x)
        {
            HashRow(level, x + 70f, OnGround, 4);
            x = RightEdge(Bridge(level, x, Ground)) + WordGap;
            float left = x;
            WordBlock ground = Word(level, "across and through", x, Ground, 72f);
            BrickWall(level, left + 200f, CeilingBrickLetters);
            BuildEnemy(level, ground, left + 400f);
            return RightEdge(ground) + WordGap;
        }

        static float FinalDoor(Transform level, float x)
        {
            float plate = x + 30f;
            x += 80f;
            float left = x;
            WordBlock ground = Word(level, "hold and run", x, Ground, 72f);
            x = RightEdge(ground) + WordGap;
            PlateAndDoor(level, plate, RightEdge(ground) - 120f);
            HashRow(level, left + 150f, OnGround, 3);

            left = x;
            Floor(level, "rest", ref x);
            PlaceCheckpoint(level, left + 40f, Ground);
            return x;
        }

        static float FinalUnderground(Transform level, float x)
        {
            return KerningDown(level, x, "downward", "and back up again", enemyBelow: true);
        }

        static float FinalIceAndHooks(Transform level, float x)
        {
            float left = x;
            WordBlock ice = Ice(level, "slide under", x);
            Stacked(level, ShortLowWord, left + 200f, CeilingTunnelRows, TunnelBottom);
            x = RightEdge(ice) + WordGap;

            WordBlock ground = Word(level, "swing", x, Ground, 72f);
            x = HookChain(level, RightEdge(ground), 2);
            HotBetween(level, ref x, "land", "hot", "on");
            return x;
        }

        // The longest lock: brick, tar and a hot word, with the key at the far end.
        static float FinalLock(Transform level, float x)
        {
            float lockLeft = x;
            float left = x;
            WordBlock ground = Word(level, "caps lock", x, Ground, 72f);
            BrickWall(level, left + 220f, CeilingBrickLetters);
            x = RightEdge(ground) + WordGap;

            x = RightEdge(StickyFloor(level, "tar", x)) + WordGap;
            x = RightEdge(HotWord(level, "hot", x)) + WordGap;
            float keyLeft = x + 80f;
            Floor(level, "the key", ref x);
            CapsLock(level, lockLeft, x, keyLeft, Ground - 60f);

            left = x;
            Floor(level, "almost", ref x);
            PlaceCheckpoint(level, left + 80f, Ground);
            return x;
        }

        // The only blank in the game that wants A.
        static float FinalEnd(Transform level, float x)
        {
            float wordLeft = x + 60f;
            // Dim, so the big exit word standing on it reads first.
            WordBlock ground = Word(level, "the last word is big", x, Ground, 72f, WordKind.Solid, Breakable);
            ExitWord(level, wordLeft + 120f, Ground, "THE FIN", "L WORD", LetterForm.Uppercase);
            return RightEdge(ground) + WordGap;
        }
    }
}
