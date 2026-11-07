using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LowercaseUppercase.Editor
{
    /// <summary>
    /// Two more runs built like the Overworld: the letter starts without asterisks, and dying restarts or returns to the checkpoint.
    /// Cellar is about headroom under a ceiling, and Skyline is about crossing open air.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const string CellarScene = SceneFolder + "/Cellar.unity";
        const string SkylineScene = SceneFolder + "/Skyline.unity";
        const float CeilingBaseline = 210f;
        const float TunnelBottom = 465f; // 55px above the ground: a fits, and A doesn't.
        const int TunnelRows = 14;       // Tall enough to reach above the screen, so it can't be jumped over.

        /// <summary>Builds only these two levels, so edits made to the other scenes are left alone.</summary>
        [MenuItem("lowercase UPPERCASE/Rebuild Cellar and Skyline Levels")]
        public static void RebuildCellarAndSkyline()
        {
            PrepareAssets(out FormSettings lower, out FormSettings upper);
            BuildCellar(lower, upper);
            BuildSkyline(lower, upper);
            SetBuildScenes();
        }

        // ---------------------------------------------------------------- cellar

        static void BuildCellar(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(CellarScene, "Cellar");
            float x = 40f;

            x = CellarOpening(level, x);
            x = CellarTunnel(level, x);
            x = CellarBrickWall(level, x);
            x = CellarBridgeAndDoor(level, x);
            x = ShiftRun(level, x, true, "duck down", "break out", "duck again", "break free");
            x = CellarEnd(level, x);
            Ceiling(level, x + 400f);

            FinishScene(CellarScene, level, "Cellar · 6", lower, upper, new Vector2(110f, Ground),
                "sleep in a c", "a", "ve", scrollEndPx: x + 200f, startsArmed: false,
                nextScene: SceneName(SpaceScene));
            EditorSceneManager.OpenScene(CellarScene);
        }

        static float CellarOpening(Transform level, float x)
        {
            float left = x;
            Floor(level, "down in the cellar", ref x);
            PowerBlock(level, left + 370f, LowPlatform);

            left = x;
            WordBlock ground = Word(level, "mind your head", x, Ground, 72f);
            x = RightEdge(ground) + WordGap;
            BuildEnemy(level, ground, left + 300f);
            HashesOn(level, Word(level, "shelf", left + 180f, LowPlatform, 72f), 3);
            return x;
        }

        static float CellarTunnel(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "only small letters fit below", x, Ground, 72f);
            x = RightEdge(ground) + WordGap;

            // The enemy patrols through the tunnel, where A can't go to stomp it, so it has to be shot or dodged.
            Stacked(level, "lowlowlowlow", left + 420f, TunnelRows, TunnelBottom);
            HashRow(level, left + 480f, OnGround, 4);
            BuildEnemy(level, ground, left + 200f);
            return x;
        }

        static float CellarBrickWall(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "only capitals break through", x, Ground, 72f);
            x = RightEdge(ground) + WordGap;

            WordBlock brick = Column(level, "b\nr\ni\nc\nk\nb\nr\ni\nc\nk", left + 640f, Ground, 46f, WordKind.Cracked, Breakable);
            brick.gameObject.name = "Word brick";
            BuildEnemy(level, ground, left + 120f);
            PowerBlock(level, left + 900f, LowPlatform);

            // Past the wall, which comes back on a reload and keeps this word's enemy on its far side.
            PlaceCheckpoint(level, left + 760f, Ground);
            return x;
        }

        static float CellarBridgeAndDoor(Transform level, float x)
        {
            HashRow(level, x + 80f, OnGround, 4);
            x = RightEdge(Bridge(level, x, Ground)) + WordGap;

            WordBlock plate = Plate(level, x + 30f);
            x += 80f;
            Floor(level, "then run", ref x);

            WordBlock door = Column(level, "d\no\no\nr\nd\no\no\nr", x - 80f, Ground, 72f, WordKind.Door, Hot);
            door.gameObject.name = "Word door";
            plate.linkedDoor = door;
            return x;
        }

        static float CellarEnd(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "the last of the ampersands", x, Ground, 72f);
            x = RightEdge(ground) + WordGap;
            BuildEnemy(level, ground, left + 250f);
            BuildEnemy(level, ground, left + 700f);
            HashArc(level, left + 380f, left + 600f, 3, OnGround, 110f);

            float wordLeft = x + 110f;
            Floor(level, "no more rooms", ref x);
            Text(level, "Word c", "c", wordLeft, Ground, 72f, mono, Paper);
            Goal(level, wordLeft + 50f, Ground - 52f, 40f, 52f);
            Text(level, "Word ve", "ve", wordLeft + 96f, Ground, 72f, mono, Paper);
            return x;
        }

        static void Ceiling(Transform level, float rightPx)
        {
            const string word = "ceiling ";
            const float left = -60f;
            float wordWidth = word.Length * 72f * 0.6f;
            int count = Mathf.CeilToInt((rightPx - left) / wordWidth);
            string content = string.Join("", System.Linq.Enumerable.Repeat(word, count));
            OnBaseline(level, content.TrimEnd(), left, CeilingBaseline, 72f, Breakable);
        }

        // ---------------------------------------------------------------- skyline

        static void BuildSkyline(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(SkylineScene, "Skyline");
            float x = 40f;

            x = SkylineOpening(level, x);
            x = SkylineSteps(level, x);
            x = SkylineBridgeAndSpring(level, x);
            x = SkylineIce(level, x);
            x = SkylineHooks(level, x);
            Floor(level, "land here", ref x);
            x = ShiftRun(level, x, false, "slip in", "push out", "slip in", "push out", "slip in", "push out");
            x = SkylineLiftAndStar(level, x);

            FinishScene(SkylineScene, level, "Skyline · 12", lower, upper, new Vector2(110f, Ground),
                "reach for a st", "a", "r", scrollEndPx: x + 200f, startsArmed: false,
                nextScene: SceneName(FlamesScene));
            EditorSceneManager.OpenScene(SkylineScene);
        }

        static float SkylineOpening(Transform level, float x)
        {
            Cloud(level, x + 200f, 160f);
            Cloud(level, x + 700f, 240f);

            float left = x;
            Floor(level, "the sky starts here", ref x);
            PowerBlock(level, left + 340f, LowPlatform);
            return x + 100f;
        }

        static float SkylineSteps(Transform level, float x)
        {
            Cloud(level, x + 300f, 620f);
            HashesOn(level, Platform(level, "one", 440f, ref x, 110f), 1);
            HashesOn(level, Platform(level, "more", 360f, ref x, 110f), 1);
            HashesOn(level, Platform(level, "step", 280f, ref x, 110f), 1);
            HashesOn(level, Platform(level, "up", 200f, ref x, 150f), 2);

            float left = x;
            WordBlock landing = Platform(level, "then drop down", 400f, ref x, WordGap);
            BuildEnemy(level, landing, left + 300f);
            return x;
        }

        static float SkylineBridgeAndSpring(Transform level, float x)
        {
            Cloud(level, x + 100f, 180f);
            x = RightEdge(Bridge(level, x, 400f)) + WordGap;

            PlaceCheckpoint(level, x + 150f, 400f);
            WordBlock landing = Platform(level, "stay small", 400f, ref x, 0f);
            Spring(level, RightEdge(landing) + 45f, 400f, padWidthPx: 80f);
            return RightEdge(landing) + 170f;
        }

        static float SkylineIce(Transform level, float x)
        {
            Cloud(level, x + 400f, 300f);
            // Long enough to catch a letter that holds right all the way from the spring.
            WordBlock ice = Word(level, "ice ice ice ice", x, 170f, 72f, WordKind.Ice, Interactive, italic: true);
            ice.grip = 0.12f;
            HashesOn(level, ice, 5);
            x = RightEdge(ice) + 170f;

            float left = x;
            WordBlock landing = Platform(level, "soft landing", 340f, ref x, 130f);
            BuildEnemy(level, landing, left + 260f);
            return x;
        }

        // The same spacing as the comma station in the Vocabulary level.
        static float SkylineHooks(Transform level, float x)
        {
            Cloud(level, x + 300f, 500f);
            Floor(level, "hold on", ref x);
            Hook(level, "Hook 1", x + 170f, 270f);
            Hook(level, "Hook 2", x + 410f, 270f);
            HashArc(level, x + 210f, x + 370f, 3, 300f, 50f); // Along the leap from one comma to the next.
            return x + 620f;
        }

        static float SkylineLiftAndStar(Transform level, float x)
        {
            Floor(level, "almost", ref x);

            // The same lift as Ladder's.
            x = Lift(level, x, LiftTop);

            const float top = LiftLanding;
            float wordLeft = x + 150f;
            Platform(level, "one more run", top, ref x, 0f);
            Text(level, "Word st", "st", wordLeft, top, 72f, mono, Paper);
            Goal(level, wordLeft + 94f, top - 52f, 40f, 52f);
            Text(level, "Word r", "r", wordLeft + 140f, top, 72f, mono, Paper);
            return x;
        }

        /// <summary>Starts a little left of x, because the hyphen's side bearing would otherwise leave a gap a can drop through.</summary>
        static WordBlock Bridge(Transform level, float x, float surfacePx)
        {
            WordBlock bridge = Word(level, "--------", x - 12f, surfacePx, 72f, WordKind.Hyphen, Interactive);
            bridge.impactFont = black;
            bridge.impactText = "SNAP";
            bridge.respawnSeconds = BreakRespawnSeconds;
            return bridge;
        }

        static WordBlock Platform(Transform level, string content, float surfacePx, ref float x, float gapAfterPx)
        {
            WordBlock block = Word(level, content, x, surfacePx, 72f);
            x = RightEdge(block) + gapAfterPx;
            return block;
        }
    }
}
