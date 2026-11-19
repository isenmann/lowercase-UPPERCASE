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
            x = SlamPen(level, x);
            x = FloatPen(level, x);
            x = WindPen(level, x);
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
            x = CurlPen(level, x);
            x = CaseSwapPen(level, x);
            x = MimicPen(level, x);
            x = CursorPen(level, x);
            PowerBlock(level, x - 120f, LowPlatform);
            x = EyePen(level, x);
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

        /// <summary>
        /// Not an enemy but A's slam: a shelf at a's jump height with a cracked lid in it, over a word
        /// where armour and a spike walk. Slam through the lid and the landing knocks out what walks beside it.
        /// </summary>
        static float SlamPen(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "slam down on whatever walks below", x, Ground, 72f);
            x = RightEdge(ground) + WordGap;
            Note(level, left + 40f, 260f, "turn A in the air to slam: it breaks brick and knocks out what walks beside you");

            float shelf = left + 260f;
            WordBlock before = Word(level, "jump up", shelf, LowPlatform, 72f);
            shelf = RightEdge(before);
            WordBlock lid = BrickWord(level, "brick", shelf, LowPlatform);
            shelf = RightEdge(lid);
            Word(level, "and slam", shelf, LowPlatform, 72f);

            BuildArmour(level, ground, left + 420f);
            BuildSpike(level, ground, left + 620f);
            PlaceCheckpoint(level, x - 60f, Ground);
            return x;
        }

        const float FloatLedge = 250f; // 270 px above the ground: higher than a can jump.

        /// <summary>
        /// Not an enemy but the floating word: one rests in the floor and carries a up to a ledge too high to jump to,
        /// the next rests at the ledge and sinks under A back down to the floor. Both have nothing under them.
        /// </summary>
        static float FloatPen(Transform level, float x)
        {
            float left = x;
            Floor(level, "float words", ref x);
            Note(level, left + 40f, 200f, "a rides a float up, A rides it down, and it drifts back when you step off");

            WordBlock up = FloatWord(level, "float", x, Ground, FloatLedge);
            x = RightEdge(up) + WordGap;
            WordBlock ledge = Word(level, "up here", x, FloatLedge, 72f);
            x = RightEdge(ledge) + WordGap;
            WordBlock down = FloatWord(level, "float", x, FloatLedge, Ground);
            x = RightEdge(down) + WordGap;

            Floor(level, "and down again", ref x);
            PlaceCheckpoint(level, x - 60f, Ground);
            return x;
        }

        const float WindPitPx = 500f; // Wider than a can jump, narrower than a jumps with the wind behind it.

        /// <summary>
        /// Not an enemy but the wind: a headwind over a long word that pushes a back, so it is crossed as A; then a
        /// pit too wide to jump with a tailwind over it that carries a across; then the same pit with the wind in gusts.
        /// </summary>
        static float WindPen(Transform level, float x)
        {
            float left = x;
            Floor(level, "wind", ref x);

            float headLeft = x;
            WordBlock head = Word(level, "headwind: walk into it as A", x, Ground, 72f);
            x = RightEdge(head);
            WindStretch(level, headLeft, x, blowsLeft: true);
            Note(level, left + 40f, 200f, "wind blows a away, A walks through it");
            x += WordGap;
            Floor(level, "out", ref x);
            PlaceCheckpoint(level, x - 60f, Ground);

            x = TailwindPit(level, x, "tailwind", "and land", 0f);
            PlaceCheckpoint(level, x - 60f, Ground);
            x = TailwindPit(level, x, "gusts", "in a gust", 1.5f);
            PlaceCheckpoint(level, x - 60f, Ground);
            return x;
        }

        /// <summary>A take-off word, a pit too wide for a plain jump, a landing, and wind blowing right over the pit.</summary>
        static float TailwindPit(Transform level, float x, string takeOff, string landing, float gustSeconds)
        {
            WordBlock before = Word(level, takeOff, x, Ground, 72f);
            float pitLeft = RightEdge(before);
            float pitRight = pitLeft + WindPitPx;
            WordBlock after = Word(level, landing, pitRight, Ground, 72f);
            WindStretch(level, x + 40f, pitRight + 80f, gustSeconds: gustSeconds);
            return RightEdge(after) + WordGap;
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

        /// <summary>A curl and a walker on one word, with brick at its end for a rolling o to smash.</summary>
        static float CurlPen(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "curls roll when you kick them", x, Ground, 72f);
            x = RightEdge(ground);
            Note(level, left + 40f, 300f, "@ stomp it as A, then kick the o into the &");
            BuildCurl(level, ground, left + 260f);
            BuildEnemy(level, ground, left + 760f);
            Cracked(level, "b\nr\ni\nc\nk", x - 60f, Ground);
            Floor(level, "and on", ref x);
            PlaceCheckpoint(level, x - 60f, Ground);
            return x;
        }

        /// <summary>A case swapper walking a word. After a touch the HUD shows CAPS LOCK until the next one.</summary>
        static float CaseSwapPen(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "swappers turn your shift around", x, Ground, 72f);
            x = RightEdge(ground) + WordGap;
            Note(level, left + 40f, 300f, "\u2195 touch it and Shift works the other way");
            BuildCaseSwapper(level, ground, left + 400f);
            PlaceCheckpoint(level, x - 60f, Ground);
            return x;
        }

        /// <summary>A mimic walking a word: switch to A only just before landing on it.</summary>
        static float MimicPen(Transform level, float x)
        {
            float left = x;
            WordBlock ground = Word(level, "mimics copy your case", x, Ground, 72f);
            x = RightEdge(ground) + WordGap;
            Note(level, left + 40f, 300f, "b becomes B after you: turn A just before you land");
            BuildMimic(level, ground, left + 360f);
            PlaceCheckpoint(level, x - 60f, Ground);
            return x;
        }

        /// <summary>A stretch of words with gaps that the backspace cursor erases behind the letter.</summary>
        static float CursorPen(Transform level, float x)
        {
            float start = x;
            Note(level, x + 40f, 300f, "| keep moving: the cursor erases the words behind you");
            WordBlock first = Word(level, "backspace", x, Ground, 72f);
            x = RightEdge(first) + 110f;
            WordBlock second = Word(level, "erases every", x, Ground, 72f);
            x = RightEdge(second) + 130f;
            WordBlock third = Word(level, "word", x, Ground - 90f, 72f);
            x = RightEdge(third) + 120f;
            WordBlock fourth = Word(level, "you leave behind", x, Ground, 72f);
            x = RightEdge(fourth) + WordGap;
            BuildCursorChase(level, start + 10f, x - 10f);

            Floor(level, "safe again", ref x);
            PlaceCheckpoint(level, x - 60f, Ground);
            return x;
        }

        /// <summary>An eye waiting above a long word. It follows the letter once it is near, so it comes last.</summary>
        static float EyePen(Transform level, float x)
        {
            float left = x;
            Floor(level, "an eye is watching you so keep moving", ref x);
            Note(level, left + 40f, 300f, "\u00f6 it follows you through words: shoot it");
            BuildEye(level, left + 700f, 260f);
            return x;
        }
#endif
    }
}
