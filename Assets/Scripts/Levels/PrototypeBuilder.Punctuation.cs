#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;

namespace LowercaseUppercase.Levels
{
    /// <summary>
    /// Chapter 2, levels 4 and 5. Plank teaches hyphen bridges in the open: they hold a and snap under A.
    /// Gate teaches periods and doors under a ceiling: A holds a door open, and a runs through before it shuts.
    /// Cellar, the chapter's test, follows them.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const string PlankScene = SceneFolder + "/Plank.unity";
        const string GateScene = SceneFolder + "/Gate.unity";

#if UNITY_EDITOR
        [MenuItem("lowercase UPPERCASE/Rebuild Chapter 2 Levels")]
        public static void RebuildChapter2()
        {
            PrepareAssets(out FormSettings lower, out FormSettings upper);
            BuildPlank(lower, upper);
            BuildGate(lower, upper);
            BuildCellar(lower, upper);
            SetBuildScenes();
        }
#endif

        // ---------------------------------------------------------------- plank

#if UNITY_EDITOR
        static void BuildPlank(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(PlankScene, "Plank");
            float x = 40f;

            x = PlankOpening(level, x);
            x = PlankFirstBridge(level, x);
            x = PlankGuardedBridge(level, x);
            x = PlankBrickLanding(level, x);
            x = PlankIslands(level, x);
            x = PlankHighRoad(level, x);
            x = PlankLowDoor(level, x);
            x = PlankLastBridge(level, x);
            x = Exit(level, x, "the end of the line", "pl", "nk");

            FinishScene(PlankScene, level, "Plank · 4", lower, upper, new Vector2(110f, Ground),
                "walk the pl", "a", "nk", scrollEndPx: x + 200f, startsArmed: false,
                nextScene: SceneName(GateScene));
            EditorSceneManager.OpenScene(PlankScene);
        }
#endif

        static float PlankOpening(Transform level, float x)
        {
            Hill(level, x + 40f, 3);
            Cloud(level, x + 300f, 160f);
            Cloud(level, x + 900f, 120f);

            float left = x;
            Floor(level, "a dash between words", ref x);
            HashesOn(level, Word(level, "dash", left + 560f, LowPlatform, 72f), 2);
            return x;
        }

        static float PlankFirstBridge(Transform level, float x)
        {
            Note(level, x, 440f, "a can walk this. A would snap it.");
            HashRow(level, x + 70f, OnGround, 4);
            x = RightEdge(Bridge(level, x, Ground)) + WordGap;
            PowerBlock(level, x + 200f, LowPlatform);
            Floor(level, "makes a bridge", ref x);
            return x;
        }

        static float PlankGuardedBridge(Transform level, float x)
        {
            Cloud(level, x + 200f, 150f);
            float left = x;
            WordBlock ground = Word(level, "mind the ampersand", x, Ground, 72f);
            BuildEnemy(level, ground, left + 380f);
            x = RightEdge(ground) + WordGap;

            HashArc(level, x - 20f, x + 360f, 4, OnGround, 90f);
            Note(level, x + 20f, 300f, "~ it bobs over the bridge. time it or shoot it.");
            BuildFlier(level, x + 170f, Ground - 40f, 110f);
            x = RightEdge(Bridge(level, x, Ground)) + WordGap;
            Floor(level, "and cross", ref x);
            return x;
        }

        // Small to cross, and big the moment the far side is reached.
        static float PlankBrickLanding(Transform level, float x)
        {
            Note(level, x, 440f, "small across, big at the wall");
            x = RightEdge(Bridge(level, x, Ground)) + WordGap;

            float left = x;
            WordBlock ground = Word(level, "the far side is brick", x, Ground, 72f);
            BrickWall(level, left + 200f);
            HashRow(level, left + 300f, OnGround, 3);
            return RightEdge(ground) + WordGap;
        }

        static float PlankIslands(Transform level, float x)
        {
            Hill(level, x + 600f, 2);
            float left = x;
            Floor(level, "rest", ref x);
            PlaceCheckpoint(level, left + 60f, Ground);
            PowerBlock(level, left + 20f, LowPlatform);

            for (int i = 0; i < 3; i++)
            {
                x = RightEdge(Bridge(level, x, Ground)) + WordGap;
                left = x;
                WordBlock island = Word(level, i == 1 ? "island" : "isle", x, Ground, 72f);
                if (i == 1) BuildSpike(level, island, left + 80f);
                else Hash(level, left + 86f, OnGround - 70f);
                x = RightEdge(island) + WordGap;
            }
            return x;
        }

        // The ground route has two enemies. The bridge above skips them, and only a can use it.
        static float PlankHighRoad(Transform level, float x)
        {
            Cloud(level, x + 500f, 110f);
            float left = x;
            WordBlock ground = Word(level, "under the bridge it is busy", x, Ground, 72f);
            BuildEnemy(level, ground, left + 300f);
            BuildArmour(level, ground, left + 760f);

            WordBlock up = Word(level, "up", left + 60f, LowPlatform, 72f);
            WordBlock high = Bridge(level, RightEdge(up) + WordGap, LowPlatform);
            float highRight = RightEdge(high);
            HashRow(level, RightEdge(up) + 80f, LowPlatform - 26f, 4);
            Word(level, "over", highRight + WordGap, LowPlatform, 72f);
            return RightEdge(ground) + WordGap;
        }

        static float PlankLowDoor(Transform level, float x)
        {
            float left = x;
            Floor(level, "nearly over", ref x);
            PlaceCheckpoint(level, left + 100f, Ground);
            PowerBlock(level, left + 250f, LowPlatform);

            x = RightEdge(Bridge(level, x, Ground)) + WordGap;
            left = x;
            WordBlock ground = Word(level, "a low door", x, Ground, 72f);
            Stacked(level, ShortLowWord, left + 120f, TunnelRows, TunnelBottom);
            HashRow(level, left + 170f, OnGround, 3);
            return RightEdge(ground) + WordGap;
        }

        static float PlankLastBridge(Transform level, float x)
        {
            Cloud(level, x + 300f, 170f);
            float left = x;
            WordBlock ground = Word(level, "then a wall", x, Ground, 72f);
            BuildEnemy(level, ground, left + 40f);
            BrickWall(level, left + 220f);
            x = RightEdge(ground) + WordGap;

            HashArc(level, x - 20f, x + 360f, 3, OnGround, 90f);
            return RightEdge(Bridge(level, x, Ground)) + WordGap;
        }

        // ---------------------------------------------------------------- gate

#if UNITY_EDITOR
        static void BuildGate(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(GateScene, "Gate");
            float x = 40f;

            x = GateOpening(level, x);
            x = GateFirstDoor(level, x);
            x = GateLongRun(level, x);
            x = GateGuard(level, x);
            x = GateShelf(level, x);
            x = GateTwoDoors(level, x);
            x = GateBrickPlate(level, x);
            x = GateLowAfterDoor(level, x);
            x = Exit(level, x, "the gate is near", "g", "te");
            Ceiling(level, x + 400f);

            FinishScene(GateScene, level, "Gate · 5", lower, upper, new Vector2(110f, Ground),
                "wait at the g", "a", "te", scrollEndPx: x + 200f, startsArmed: false,
                nextScene: SceneName(CellarScene));
            EditorSceneManager.OpenScene(GateScene);
        }
#endif

        static float GateOpening(Transform level, float x)
        {
            float left = x;
            Floor(level, "behind every door", ref x);
            HashesOn(level, Word(level, "hall", left + 560f, LowPlatform, 72f), 2);

            // Arrows fly back along the hall: a runs under them and hops the launcher, A gets hit.
            Note(level, x - 330f, 440f, "< arrows fly over a and hit A");
            WordBlock launcher = BuildLauncher(level, x, Ground, facesLeft: true, interval: 2.4f);
            return RightEdge(launcher) + WordGap;
        }

        // The plate is right beside the door, so A can hold it open and walk through.
        static float GateFirstDoor(Transform level, float x)
        {
            Note(level, x - 60f, 440f, "stand on the period as A");
            float plate = x + 30f;
            x += 80f;
            float left = x;
            Floor(level, "press and pass", ref x);
            PlateAndDoor(level, plate, left + 200f);
            HashRow(level, left + 300f, OnGround, 3);
            return x;
        }

        static float GateLongRun(Transform level, float x)
        {
            Note(level, x - 40f, 440f, "A holds it open. a runs.");
            float plate = x + 30f;
            x += 80f;
            float left = x;
            WordBlock ground = Word(level, "then run before it shuts", x, Ground, 72f);
            x = RightEdge(ground) + WordGap;
            PlateAndDoor(level, plate, RightEdge(ground) - 120f);
            HashRow(level, left + 250f, OnGround, 5);
            return x;
        }

        static float GateGuard(Transform level, float x)
        {
            float left = x;
            Floor(level, "wait here", ref x);
            PlaceCheckpoint(level, left + 120f, Ground);
            PowerBlock(level, left + 220f, LowPlatform);

            float plate = x + 30f;
            x += 80f;
            left = x;
            WordBlock ground = Word(level, "the ampersand waits", x, Ground, 72f);
            x = RightEdge(ground) + WordGap;
            BuildSpike(level, ground, left + 400f);
            PlateAndDoor(level, plate, RightEdge(ground) - 120f);
            return x;
        }

        // A jumps too low to reach the shelf, so a climbs up and grows on the plate.
        static float GateShelf(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "the plate is up on a shelf", x, Ground, 72f);
            x = RightEdge(ground) + WordGap;

            WordBlock shelf = Word(level, "shelf", left + 180f, LowPlatform, 72f);
            float shelfRight = RightEdge(shelf);
            PlateAndDoor(level, shelfRight + 40f, RightEdge(ground) - 120f, LowPlatform);
            Word(level, "edge", shelfRight + 80f, LowPlatform, 72f);
            HashesOn(level, shelf, 3);
            return x;
        }

        static float GateTwoDoors(Transform level, float x)
        {
            float plate = x + 30f;
            x += 80f;
            WordBlock first = Word(level, "one door", x, Ground, 72f);
            x = RightEdge(first) + WordGap;
            PlateAndDoor(level, plate, RightEdge(first) - 120f);

            plate = x + 30f;
            x += 80f;
            float left = x;
            WordBlock second = Word(level, "and another", x, Ground, 72f);
            x = RightEdge(second) + WordGap;
            PlateAndDoor(level, plate, RightEdge(second) - 120f);
            HashRow(level, left + 80f, OnGround, 3);
            return x;
        }

        static float GateBrickPlate(Transform level, float x)
        {
            float left = x;
            Floor(level, "nearly through", ref x);
            PlaceCheckpoint(level, left + 120f, Ground);

            left = x;
            WordBlock ground = Word(level, "break in", x, Ground, 72f);
            BrickWall(level, left + 140f, CeilingBrickLetters);
            x = RightEdge(ground);

            // Already A after the brick, so the plate comes straight after it.
            float plate = x + 30f;
            x += 80f;
            WordBlock after = Word(level, "and hold it", x, Ground, 72f);
            x = RightEdge(after) + WordGap;
            PlateAndDoor(level, plate, RightEdge(after) - 120f);
            return x;
        }

        static float GateLowAfterDoor(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "then slip under", x, Ground, 72f);
            Stacked(level, LongLowWord, left + 120f, CeilingTunnelRows, TunnelBottom);
            HashRow(level, left + 180f, OnGround, 5);
            return RightEdge(ground) + WordGap;
        }
    }
}
