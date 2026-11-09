#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;

namespace LowercaseUppercase.Levels
{
    /// <summary>
    /// A room for trying each new enemy on its own before it goes into the levels. It is not in the build
    /// settings, so it never ships and the build indices the tests use don't move. Open it and press Play.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const string EnemyRoomScene = SceneFolder + "/EnemyRoom.unity";

#if UNITY_EDITOR
        [MenuItem("lowercase UPPERCASE/Rebuild Enemy Room")]
        public static void RebuildEnemyRoom()
        {
            PrepareAssets(out FormSettings lower, out FormSettings upper);
            BuildEnemyRoom(lower, upper);
        }

        static void BuildEnemyRoom(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(EnemyRoomScene, "Enemy Room");
            float x = 40f;

            Floor(level, "the enemy room", ref x);
            PlaceCheckpoint(level, x - 60f, Ground);
            x = EnemyPen(level, x, "a plain walker", "& stomp it or shoot it", (l, g, px) => BuildEnemy(l, g, px));
            PowerBlock(level, x - 120f, LowPlatform);
            x = EnemyPen(level, x, "a spike walks here", "^ shoot it, don't land on it", (l, g, px) => BuildSpike(l, g, px));
            x = EnemyPen(level, x, "armour walks here", "§ stomp it, asterisks bounce off", (l, g, px) => BuildArmour(l, g, px));
            x = EnemyPen(level, x, "hoppers keep the beat", "! one hops in place, one hops along", (l, g, px) =>
            {
                BuildHopper(l, g, px - 100f);
                BuildHopper(l, g, px + 250f, sideSpeed: 1.5f, startDelay: 0.4f);
            });
            PowerBlock(level, x - 120f, LowPlatform);
            x = FlierPen(level, x);
            x = LauncherPen(level, x);
            x = RiserPen(level, x);
            x = DripPen(level, x);
            PowerBlock(level, x - 120f, LowPlatform);
            x = FirePen(level, x);
            x = Exit(level, x, "the end of the room", "the", "nd");

            FinishScene(EnemyRoomScene, level, "Enemy room", lower, upper, new Vector2(110f, Ground),
                "try every ", "e", "nemy", scrollEndPx: x + 200f, configure: (flow, hud) => flow.practice = true);
            EditorSceneManager.OpenScene(EnemyRoomScene);
        }

        /// <summary>A long word for one enemy to walk on, with a note above it and a checkpoint after it.</summary>
        static float EnemyPen(Transform level, float x, string floor, string note,
            System.Action<Transform, WordBlock, float> build)
        {
            float left = x;
            WordBlock ground = Word(level, floor + " and back again", x, Ground, 72f);
            x = RightEdge(ground) + WordGap;
            Note(level, left + 40f, 300f, note);
            build(level, ground, left + 300f);
            PlaceCheckpoint(level, x - 60f, Ground);
            return x;
        }

        /// <summary>One flier over a word, out of reach of a walking letter, and one bobbing up and down in a gap.</summary>
        static float FlierPen(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "fliers fly over the words", x, Ground, 72f);
            x = RightEdge(ground);
            Note(level, left + 40f, 260f, "~ one flies along, one guards the gap");
            BuildFlier(level, left + 380f, Ground - 130f, 220f);

            float gap = 150f;
            BuildFlier(level, x + gap * 0.5f - 18f, Ground - 60f, 0f);
            x += gap;
            WordBlock landing = Word(level, "and on", x, Ground, 72f);
            x = RightEdge(landing) + WordGap;
            PlaceCheckpoint(level, x - 60f, Ground);
            return x;
        }

        /// <summary>A launcher at the far end of a word, firing at head height for A. Its arrows fly over a.</summary>
        static float LauncherPen(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "arrows fly at the capitals", x, Ground, 72f);
            x = RightEdge(ground);
            Note(level, left + 40f, 300f, "< a runs under, A stomps or gets hit");
            WordBlock launcher = BuildLauncher(level, x + WordGap, Ground, facesLeft: true);
            x = RightEdge(launcher) + WordGap;
            Floor(level, "past the launcher", ref x);
            PlaceCheckpoint(level, x - 60f, Ground);
            return x;
        }

        /// <summary>Loose letters over a pit, with risers in two of the holes.</summary>
        static float RiserPen(Transform level, float x)
        {
            Note(level, x + 40f, 300f, "\u00a1 they rise from the holes, not while you stand by one");
            float right = KernedWithRisers(level, "between", x, Ground, 1, 4);
            x = right + WordGap;
            Floor(level, "the holes", ref x);
            PlaceCheckpoint(level, x - 60f, Ground);
            return x;
        }

        /// <summary>A ceiling word with drips under it, high enough that a jumping a just touches it.</summary>
        static float DripPen(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "drips fall when you pass below", x, Ground, 72f);
            x = RightEdge(ground) + WordGap;
            WordBlock ceiling = Word(level, "a dripping ceiling", left + 160f, Ground - 190f, 72f);
            Note(level, left + 40f, 230f, "' they fall when you pass under");
            BuildDrip(level, ceiling, left + 260f);
            BuildDrip(level, ceiling, left + 460f);
            BuildDrip(level, ceiling, left + 620f);
            PlaceCheckpoint(level, x - 60f, Ground);
            return x;
        }

        /// <summary>Sparks leaping out of a hot word, then a fire bar sweeping the ground.</summary>
        static float FirePen(Transform level, float x)
        {
            float left = x;
            Floor(level, "fire", ref x);
            WordBlock hot = HotWord(level, "hot hot", x);
            Note(level, left + 40f, 260f, "\u00b0 sparks leap out of hot words");
            BuildSpark(level, hot, x + 70f);
            BuildSpark(level, hot, x + 230f, startDelay: 0.7f);
            x = RightEdge(hot) + WordGap;

            left = x;
            WordBlock ground = Word(level, "and a turning bar of fire", x, Ground, 72f);
            x = RightEdge(ground) + WordGap;
            BuildFireBar(level, left + 380f, Ground - 160f);
            PlaceCheckpoint(level, x - 60f, Ground);
            return x;
        }
#endif
    }
}
