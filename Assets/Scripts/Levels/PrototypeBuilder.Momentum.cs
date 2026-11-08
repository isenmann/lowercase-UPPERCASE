#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;

namespace LowercaseUppercase.Levels
{
    /// <summary>
    /// Chapter 3, levels 7 to 9. Space pairs springs, which throw a twice as high as A, with kerning gaps, which a
    /// drops through and A walks across. Skate is ice under a ceiling. Trail tests all three on broken ground.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const string SpaceScene = SceneFolder + "/Space.unity";
        const string SkateScene = SceneFolder + "/Skate.unity";
        const string TrailScene = SceneFolder + "/Trail.unity";
        const float LowerFloor = 610f;   // The corridor under the ground, reached through kerning gaps.
        const float HighLedge = 200f;    // Only a spring from the ground reaches it, and only for a.
        const int SpringWallRows = 9;    // Taller than a jump or A's spring, lower than a's spring.
        const float KerningHolePx = KerningStepPx - 72f * 0.6f;
        const float SpringWallWidthPx = 4 * TubeFontPx * 0.6f; // "wall" in the tube font.
        const float LowerFloorCharPx = 44f * 0.6f;

#if UNITY_EDITOR
        [MenuItem("lowercase UPPERCASE/Rebuild Chapter 3 Levels")]
        public static void RebuildChapter3()
        {
            PrepareAssets(out FormSettings lower, out FormSettings upper);
            BuildSpace(lower, upper);
            BuildSkate(lower, upper);
            BuildTrail(lower, upper);
            SetBuildScenes();
        }
#endif

        // ---------------------------------------------------------------- pieces

        static WordBlock LowFloor(Transform level, string content, float leftPx)
        {
            WordBlock floor = Word(level, content, leftPx, LowerFloor, 44f);
            floor.gameObject.name = $"Word {content} (below)";
            return floor;
        }

        /// <summary>A wall on the ground that only a spring clears.</summary>
        static void SpringWall(Transform level, float leftPx)
        {
            Stacked(level, "wall", leftPx, SpringWallRows, Ground);
        }

        /// <summary>A spring in the gap after the ground ends at x. Returns where the next word may start.</summary>
        static float SpringAfter(Transform level, float x, float surfacePx = Ground)
        {
            Spring(level, x + 50f, surfacePx, padWidthPx: 80f);
            return x + 100f;
        }

        /// <summary>
        /// Kerned letters on the ground, a wall one hole past them, and a corridor below that runs under the wall to a
        /// spring back up. Running a skims over the holes, but stopping at the wall leaves it over the last one.
        /// Returns where the ground continues.
        /// </summary>
        static float KerningDown(Transform level, float x, string letters, string corridor, bool enemyBelow)
        {
            float left = x;
            float kernRight = Kerned(level, letters, x, Ground);
            float wallLeft = kernRight + KerningHolePx;
            SpringWall(level, wallLeft);

            // The corridor has to run on past the wall, or its spring throws the letter back into the loose letters.
            float neededWidth = wallLeft + SpringWallWidthPx + 60f - (left - 40f);
            while (corridor.Length * LowerFloorCharPx < neededWidth) corridor += " and on";
            WordBlock below = LowFloor(level, corridor, left - 40f);
            float belowRight = RightEdge(below);
            if (enemyBelow) BuildEnemy(level, below, kernRight + 200f);
            HashRow(level, kernRight + 60f, LowerFloor - 20f, 3);

            float next = SpringAfter(level, belowRight, LowerFloor);
            return Mathf.Max(next, kernRight + 200f);
        }

        // ---------------------------------------------------------------- space

#if UNITY_EDITOR
        static void BuildSpace(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(SpaceScene, "Space");
            float x = 40f;

            x = SpaceOpening(level, x);
            x = SpaceFirstSpring(level, x);
            x = SpaceFirstKerning(level, x);
            x = SpaceKerningOverPit(level, x);
            x = SpaceSpringArc(level, x);
            x = SpaceSecondKerning(level, x);
            x = SpaceHighRoad(level, x);
            x = SpaceEnd(level, x);

            FinishScene(SpaceScene, level, "Space · 7", lower, upper, new Vector2(110f, Ground),
                "spring into sp", "a", "ce", scrollEndPx: x + 200f, startsArmed: false,
                nextScene: SceneName(SkateScene));
            EditorSceneManager.OpenScene(SpaceScene);
        }
#endif

        static float SpaceOpening(Transform level, float x)
        {
            Hill(level, x + 40f, 3);
            Cloud(level, x + 700f, 130f);
            float left = x;
            Floor(level, "a word that shouts", ref x);
            HashesOn(level, Word(level, "loud", left + 560f, LowPlatform, 72f), 2);
            return x;
        }

        static float SpaceFirstSpring(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "jump on the", x, Ground, 72f);
            x = RightEdge(ground);
            Note(level, left + 120f, 440f, "a flies twice as high");
            x = SpringAfter(level, x);

            // The wall stops the ground route, so the ledge past it is the way on.
            // The ledge starts past the spring, so the letter rises in open air beside it before steering on.
            SpringWall(level, x);
            WordBlock ledge = Word(level, "over the top", x + 30f, HighLedge, 72f);
            HashesOn(level, ledge, 4);
            x = RightEdge(ledge) + 60f;
            Floor(level, "and down", ref x);
            return x;
        }

        static float SpaceFirstKerning(Transform level, float x)
        {
            Note(level, x, 440f, "a drops through loose letters");
            return KerningDown(level, x, "gapgap", "the space between letters", enemyBelow: false);
        }

        // The same loose letters, with nothing below them: a falls, A walks across.
        static float SpaceKerningOverPit(Transform level, float x)
        {
            // Long, because the spring up from the corridor throws a a long way forward.
            Floor(level, "and back up", ref x);
            float left = x;
            Floor(level, "space", ref x);
            PlaceCheckpoint(level, left + 60f, Ground);
            PowerBlock(level, left + 40f, LowPlatform);

            // Brick one hole past the last letter: a stops there and drops into the pit, A breaks it and walks on.
            Note(level, x, 440f, "A walks where a falls");
            float right = Kerned(level, "spaced", x, Ground);
            HashRow(level, x + 60f, OnGround, 4);
            BrickWall(level, right + KerningHolePx);
            x = right + KerningHolePx;

            left = x;
            Floor(level, "out", ref x);
            return x;
        }

        static float SpaceSpringArc(Transform level, float x)
        {
            Cloud(level, x + 200f, 300f);
            float left = x;
            WordBlock ground = Word(level, "bounce", x, Ground, 72f);
            BuildEnemy(level, ground, left + 60f);
            x = SpringAfter(level, RightEdge(ground));
            HashArc(level, x - 100f, x + 300f, 5, Ground - 200f, 160f);

            left = x;
            WordBlock landing = Word(level, "and land far away", x + 140f, Ground, 72f);
            Hill(level, x + 600f, 2);
            return RightEdge(landing) + WordGap;
        }

        static float SpaceSecondKerning(Transform level, float x)
        {
            float left = x;
            Floor(level, "again", ref x);
            PlaceCheckpoint(level, left + 80f, Ground);
            PowerBlock(level, left + 60f, LowPlatform);
            return KerningDown(level, x, "holes", "something lives down here", enemyBelow: true);
        }

        static float SpaceHighRoad(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "up once more", x, Ground, 72f);
            BuildEnemy(level, ground, left + 200f);
            x = SpringAfter(level, RightEdge(ground));

            SpringWall(level, x);
            WordBlock ledge = Word(level, "the high road", x + 30f, HighLedge, 72f);
            HashesOn(level, ledge, 4);
            x = RightEdge(ledge) + 60f;

            left = x;
            WordBlock landing = Word(level, "and down again", x, Ground, 72f);
            BuildEnemy(level, landing, left + 350f);
            return RightEdge(landing) + WordGap;
        }

        // The exit hangs in the air above a spring, where only a gets to.
        static float SpaceEnd(Transform level, float x)
        {
            Cloud(level, x + 300f, 120f);
            WordBlock ground = Word(level, "almost in space", x, Ground, 72f);
            x = RightEdge(ground);
            float springCentre = x + 50f;
            x = SpringAfter(level, x);
            ExitWord(level, springCentre - 112f, 232f, "sp", "ce", LetterForm.Lowercase);
            Floor(level, "end", ref x);
            return x;
        }

        // ---------------------------------------------------------------- skate

#if UNITY_EDITOR
        static void BuildSkate(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(SkateScene, "Skate");
            float x = 40f;

            x = SkateOpening(level, x);
            x = SkateFirstIce(level, x);
            x = SkateEnemy(level, x);
            x = SkateBrick(level, x);
            x = SkateTunnel(level, x);
            x = SkateIslands(level, x);
            x = SkateStop(level, x);
            x = SkateLake(level, x);
            x = SkateShelf(level, x);
            x = SkateTwoBricks(level, x);
            x = SkateLast(level, x);
            x = Exit(level, x, "off the ice at last", "sk", "te");
            Ceiling(level, x + 400f);

            FinishScene(SkateScene, level, "Skate · 8", lower, upper, new Vector2(110f, Ground),
                "learn to sk", "a", "te", scrollEndPx: x + 200f, startsArmed: false,
                nextScene: SceneName(TrailScene));
            EditorSceneManager.OpenScene(SkateScene);
        }
#endif

        static float SkateOpening(Transform level, float x)
        {
            float left = x;
            Floor(level, "the floor is frozen", ref x);
            HashesOn(level, Word(level, "cold", left + 560f, LowPlatform, 72f), 2);
            return x;
        }

        static float SkateFirstIce(Transform level, float x)
        {
            Note(level, x, 440f, "ice keeps you moving. jump the gap.");
            WordBlock ice = Ice(level, "ice ice ice", x);
            x = RightEdge(ice);
            HashArc(level, x - 20f, x + WidePit + 20f, 3);
            x += WidePit;
            PowerBlock(level, x + 100f, LowPlatform);
            Floor(level, "made it", ref x);
            return x;
        }

        static float SkateEnemy(Transform level, float x)
        {
            float left = x;
            WordBlock ice = Ice(level, "slide slide slide", x);
            BuildEnemy(level, ice, left + 500f);
            return RightEdge(ice) + WordGap;
        }

        // A is heavy and slow, so it stops sooner on ice, and it breaks the brick it slides into.
        static float SkateBrick(Transform level, float x)
        {
            float left = x;
            Note(level, x, 440f, "A stops sooner");
            WordBlock ice = Ice(level, "skid skid", x);
            BrickWall(level, RightEdge(ice) - 60f, CeilingBrickLetters);
            x = RightEdge(ice) + WordGap;

            left = x;
            Floor(level, "grip", ref x);
            PlaceCheckpoint(level, left + 40f, Ground);
            PowerBlock(level, left + 20f, LowPlatform);
            return x;
        }

        static float SkateTunnel(Transform level, float x)
        {
            float left = x;
            WordBlock ice = Ice(level, "glide under glide", x);
            Stacked(level, ShortLowWord, left + 280f, CeilingTunnelRows, TunnelBottom);
            HashRow(level, left + 330f, OnGround, 3);
            return RightEdge(ice) + WordGap;
        }

        static float SkateIslands(Transform level, float x)
        {
            for (int i = 0; i < 3; i++)
            {
                float left = x;
                WordBlock ice = Ice(level, "ice", x);
                Hash(level, left + 65f, OnGround - 60f);
                x = RightEdge(ice) + 110f;
            }
            return x;
        }

        // A short safe word between two pits: stop on it, or jump straight on.
        static float SkateStop(Transform level, float x)
        {
            float left = x;
            WordBlock stop = Word(level, "stop", x, Ground, 72f);
            PlaceCheckpoint(level, left + 60f, Ground);
            PowerBlock(level, left + 20f, LowPlatform);
            x = RightEdge(stop) + 120f;

            left = x;
            WordBlock ice = Ice(level, "spin spin spin", x);
            BuildEnemy(level, ice, left + 300f);
            x = RightEdge(ice);
            HashArc(level, x - 20f, x + WidePit + 20f, 3);
            return x + WidePit;
        }

        static float SkateLake(Transform level, float x)
        {
            float left = x;
            WordBlock ice = Ice(level, "a frozen lake", x);
            BuildEnemy(level, ice, left + 120f);
            BuildEnemy(level, ice, left + 420f);
            x = RightEdge(ice);
            HashArc(level, x - 20f, x + WidePit + 20f, 3);
            return x + WidePit;
        }

        // An icy shelf over the ground, and an enemy below it: the high road is quicker and pays.
        static float SkateShelf(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "under the ice shelf", x, Ground, 72f);
            BuildEnemy(level, ground, left + 450f);
            WordBlock shelf = Ice(level, "ice shelf", left + 120f, LowPlatform);
            HashesOn(level, shelf, 5);
            return RightEdge(ground) + WordGap;
        }

        static float SkateTwoBricks(Transform level, float x)
        {
            float left = x;
            WordBlock ice = Ice(level, "crack the ice twice", x);
            BrickWall(level, left + 260f, CeilingBrickLetters);
            BrickWall(level, left + 560f, CeilingBrickLetters);
            HashRow(level, left + 340f, OnGround, 3);
            return RightEdge(ice) + WordGap;
        }

        // Slide in small, then grow for the brick at the end.
        static float SkateLast(Transform level, float x)
        {
            float left = x;
            WordBlock ice = Ice(level, "low and then hard", x);
            Stacked(level, ShortLowWord, left + 120f, CeilingTunnelRows, TunnelBottom);
            BrickWall(level, left + 560f, CeilingBrickLetters);
            return RightEdge(ice) + WordGap;
        }

        // ---------------------------------------------------------------- trail

#if UNITY_EDITOR
        static void BuildTrail(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(TrailScene, "Trail");
            float x = 40f;

            x = TrailOpening(level, x);
            x = TrailSteps(level, x);
            x = TrailSpringToIce(level, x);
            x = TrailUnderground(level, x);
            x = TrailLooseBridge(level, x);
            x = TrailHighArc(level, x);
            x = TrailRest(level, x);
            x = ShiftRun(level, x, false, "a little", "then a lot", "a little", "then a lot", "and small");
            x = Exit(level, x, "here it ends", "tr", "il");

            FinishScene(TrailScene, level, "Trail · 9", lower, upper, new Vector2(110f, Ground),
                "follow the tr", "a", "il", scrollEndPx: x + 200f, startsArmed: false,
                nextScene: SceneName(ChainScene));
            EditorSceneManager.OpenScene(TrailScene);
        }
#endif

        static float TrailOpening(Transform level, float x)
        {
            Hill(level, x + 40f, 3);
            Cloud(level, x + 500f, 140f);
            float left = x;
            Floor(level, "a path", ref x);
            return x + 60f;
        }

        static float TrailSteps(Transform level, float x)
        {
            HashesOn(level, Platform(level, "of", 460f, ref x, 100f), 1);
            HashesOn(level, Platform(level, "steps", 400f, ref x, 100f), 2);
            WordBlock ice = Ice(level, "and ice", x, 340f);
            x = RightEdge(ice) + 160f;
            return x;
        }

        static float TrailSpringToIce(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "down low", x, Ground, 72f);
            BuildEnemy(level, ground, left + 120f);
            PowerBlock(level, left + 180f, LowPlatform);
            x = SpringAfter(level, RightEdge(ground));

            SpringWall(level, x);
            WordBlock ledge = Ice(level, "up high on ice", x + 30f, HighLedge);
            HashesOn(level, ledge, 4);
            x = RightEdge(ledge) + 60f;
            return x;
        }

        static float TrailUnderground(Transform level, float x)
        {
            float left = x;
            Floor(level, "under", ref x);
            PlaceCheckpoint(level, left + 50f, Ground);
            PowerBlock(level, left + 40f, LowPlatform);
            return KerningDown(level, x, "ground", "the trail goes under the wall", enemyBelow: true);
        }

        static float TrailLooseBridge(Transform level, float x)
        {
            float left = x;
            Floor(level, "then", ref x);
            float right = Kerned(level, "across", x, Ground);
            HashRow(level, x + 60f, OnGround - 30f, 4);
            x = right + WordGap;
            WordBlock ice = Ice(level, "slick", x);
            return RightEdge(ice) + 140f;
        }

        static float TrailHighArc(Transform level, float x)
        {
            Cloud(level, x + 200f, 250f);
            float left = x;
            WordBlock ground = Word(level, "leap", x, Ground, 72f);
            x = SpringAfter(level, RightEdge(ground));
            HashArc(level, x - 100f, x + 300f, 5, Ground - 200f, 160f);
            return x + 140f;
        }

        static float TrailRest(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "far below", x, Ground, 72f);
            BuildEnemy(level, ground, left + 260f);
            x = RightEdge(ground) + WordGap;

            left = x;
            Floor(level, "breathe", ref x);
            PlaceCheckpoint(level, left + 100f, Ground);
            return x;
        }
    }
}
