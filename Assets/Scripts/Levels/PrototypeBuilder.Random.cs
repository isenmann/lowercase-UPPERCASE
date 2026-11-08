using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LowercaseUppercase.Levels
{
    /// <summary>What the random level's scene needs to know about the level just built into it.</summary>
    public struct RandomLevelLayout
    {
        public Checkpoint[] Checkpoints;
        public string SentenceBefore;
        public string MissingLetter;
        public string SentenceAfter;
        /// <summary>The camera's furthest x, in world units.</summary>
        public float CameraMaxX;
        public bool UnderCeiling;
        /// <summary>The sections used, in order, for the log.</summary>
        public string Sections;
    }

    /// <summary>
    /// A random level made from the sections of the 15 hand-made levels, built while its scene loads. The scene itself
    /// is made in the editor like the others, with the letter, HUD and camera but no level; <see cref="RandomLevel"/>
    /// calls <see cref="BuildRandomLevel"/> to fill it in.
    /// Only sections without teaching notes are used, and each one is followed by a short plain word, so every section
    /// starts on flat ground the way it did in its own level. A level is either open air or under a ceiling, because
    /// the sections of the two kinds don't mix: springs, lifts and high ledges would hit the ceiling. The chosen
    /// sections are sorted by chapter, so the level gets harder.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const string RandomScene = SceneFolder + "/Random.unity";
        const int RandomSectionCount = 6;
        const float RandomLevelPlaceholderEndPx = 4000f;

        sealed class Section
        {
            public readonly string Name;
            public readonly int Chapter;
            public readonly bool UnderCeiling;
            public readonly System.Func<Transform, float, float> Build;

            public Section(string name, int chapter, bool underCeiling, System.Func<Transform, float, float> build)
            {
                Name = name;
                Chapter = chapter;
                UnderCeiling = underCeiling;
                Build = build;
            }
        }

        static readonly string[] Connectors =
        {
            "and", "then", "on and on", "keep going", "next", "so far", "still here", "onward",
            "and so", "more words", "step by step", "a pause", "here we go", "word by word"
        };

        static readonly string[] RestWords = { "rest", "breathe", "a quiet word", "halfway", "sit down" };

        static readonly string[] OpeningPhrases =
        {
            "roll the dice", "a word at random", "nobody wrote this", "shuffled pages", "who knows what next"
        };

        static readonly string[] ExitFloors =
        {
            "the end of this one", "nearly finished now", "the last word waits", "and that is all"
        };

        // Words with an a that isn't the first or last letter, split around it.
        static readonly string[][] ExitWords =
        {
            new[] { "m", "p" }, new[] { "st", "r" }, new[] { "c", "t" }, new[] { "l", "mp" }, new[] { "fl", "g" },
            new[] { "sn", "il" }, new[] { "h", "t" }, new[] { "dr", "gon" }, new[] { "p", "per" }, new[] { "w", "ve" }
        };

        static readonly string[] SentenceStarts = { "find the", "follow the", "look for the", "chase the", "draw a" };

        /// <summary>
        /// Builds the level for this seed under the level root. The same seed always gives the same level, so a
        /// reload after a death brings back the same one.
        /// </summary>
        public static RandomLevelLayout BuildRandomLevel(Transform level, int seed, TMP_FontAsset monoFont,
            TMP_FontAsset monoBoldFont, TMP_FontAsset blackFont, Sprite whiteSprite)
        {
            mono = monoFont;
            monoBold = monoBoldFont;
            black = blackFont;
            white = whiteSprite;
            levelCheckpoints.Clear();

            var rng = new System.Random(seed);
            bool underCeiling = rng.Next(2) == 0;

            List<Section> chosen = RandomSections(rng)
                .Where(s => s.UnderCeiling == underCeiling)
                .OrderBy(s => rng.Next())
                .Take(RandomSectionCount)
                .OrderBy(s => s.Chapter)
                .ToList();

            float x = 40f;
            x = RandomOpening(level, x, rng, underCeiling);

            for (int i = 0; i < chosen.Count; i++)
            {
                x = chosen[i].Build(level, x);
                Floor(level, Pick(rng, Connectors), ref x);
                if (i == chosen.Count / 2 - 1) x = RandomRest(level, x, rng);
            }

            string[] word = Pick(rng, ExitWords);
            x = Exit(level, x, Pick(rng, ExitFloors), word[0], word[1]);
            if (underCeiling) Ceiling(level, x + 400f);

            var layout = new RandomLevelLayout
            {
                Checkpoints = levelCheckpoints.ToArray(),
                SentenceBefore = $"{Pick(rng, SentenceStarts)} {word[0]}",
                MissingLetter = "a",
                SentenceAfter = word[1],
                CameraMaxX = CameraMaxX(x + 200f),
                UnderCeiling = underCeiling,
                Sections = string.Join(", ", chosen.Select(s => s.Name))
            };
            levelCheckpoints.Clear();
            return layout;
        }

        /// <summary>The same limit FinishScene gives the camera of a scrolling level.</summary>
        static float CameraMaxX(float scrollEndPx) => X(scrollEndPx - 640f);

        static float RandomOpening(Transform level, float x, System.Random rng, bool underCeiling)
        {
            if (!underCeiling)
            {
                Hill(level, x + 40f, 3);
                Cloud(level, x + 300f, 160f);
                Cloud(level, x + 900f, 130f);
            }
            float left = x;
            Floor(level, Pick(rng, OpeningPhrases), ref x);
            PowerBlock(level, left + 300f, LowPlatform);
            return x;
        }

        /// <summary>A checkpoint halfway, with a power block above it, on a word no enemy walks on.</summary>
        static float RandomRest(Transform level, float x, System.Random rng)
        {
            float left = x;
            Floor(level, Pick(rng, RestWords), ref x);
            PlaceCheckpoint(level, left + 60f, Ground);
            PowerBlock(level, left + 20f, LowPlatform);
            return x;
        }

        static T Pick<T>(System.Random rng, IList<T> items) => items[rng.Next(items.Count)];

        /// <summary>
        /// Every section that can stand on its own. Left out: openings and ends, sections with teaching notes, and sections
        /// that only work straight after another one (the Skyline climb is kept together as one).
        /// </summary>
        static IEnumerable<Section> RandomSections(System.Random rng)
        {
            // Open air.
            yield return new Section("Overworld tubes", 1, false, TubeStretch);
            yield return new Section("Overworld ampersands", 1, false, AmpersandStretch);
            yield return new Section("Overworld stairs", 1, false, StairStretch);
            yield return new Section("Wall stairs", 1, false, WallStairs);
            yield return new Section("Wall door", 1, false, WallDoor);
            yield return new Section("Wall thick wall", 1, false, WallThickWall);
            yield return new Section("Wall up and over", 1, false, WallUpAndOver);
            yield return new Section("Wall low road", 1, false, WallLowRoad);
            yield return new Section("Wall on the hill", 1, false, WallOnTheHill);
            yield return new Section("Wall closing run", 1, false, WallClosingRun);
            yield return new Section("Plank guarded bridge", 2, false, PlankGuardedBridge);
            yield return new Section("Plank islands", 2, false, PlankIslands);
            yield return new Section("Plank high road", 2, false, PlankHighRoad);
            yield return new Section("Plank low door", 2, false, PlankLowDoor);
            yield return new Section("Plank last bridge", 2, false, PlankLastBridge);
            yield return new Section("Space spring arc", 3, false, SpaceSpringArc);
            yield return new Section("Space second kerning", 3, false, SpaceSecondKerning);
            yield return new Section("Space high road", 3, false, SpaceHighRoad);
            yield return new Section("Trail steps", 3, false, TrailSteps);
            yield return new Section("Trail spring to ice", 3, false, TrailSpringToIce);
            yield return new Section("Trail underground", 3, false, TrailUnderground);
            yield return new Section("Trail loose bridge", 3, false, TrailLooseBridge);
            yield return new Section("Trail high arc", 3, false, TrailHighArc);
            yield return new Section("Ladder two lifts", 4, false, LadderTwoLifts);
            yield return new Section("Ladder guarded top", 4, false, LadderGuardedTop);
            yield return new Section("Ladder long drop", 4, false, LadderLongDrop);
            yield return new Section("Skyline climb", 4, false,
                (level, x) => SkylineIce(level, SkylineBridgeAndSpring(level, SkylineSteps(level, x))));
            yield return new Section("Skyline hooks", 4, false, SkylineHooks);
            yield return new Section("Tar guarded", 5, false, TarGuarded);
            yield return new Section("Tar two walls", 5, false, TarTwoWalls);
            yield return new Section("Tar high wall", 5, false, TarHighWall);
            yield return new Section("Shift run", 6, false,
                (level, x) => ShiftRun(level, x, false, RunWords(rng, 4 + rng.Next(3))));

            // Under a ceiling.
            yield return new Section("Crack tunnel then wall", 1, true, CrackTunnelThenWall);
            yield return new Section("Crack big small big", 1, true, CrackBigSmallBig);
            yield return new Section("Crack long hall", 1, true, CrackLongHall);
            yield return new Section("Crack second stomp", 1, true, CrackSecondStomp);
            yield return new Section("Gate guard", 2, true, GateGuard);
            yield return new Section("Gate shelf", 2, true, GateShelf);
            yield return new Section("Gate two doors", 2, true, GateTwoDoors);
            yield return new Section("Gate brick plate", 2, true, GateBrickPlate);
            yield return new Section("Gate low after door", 2, true, GateLowAfterDoor);
            yield return new Section("Cellar tunnel", 2, true, CellarTunnel);
            yield return new Section("Cellar brick wall", 2, true, CellarBrickWall);
            yield return new Section("Cellar bridge and door", 2, true, CellarBridgeAndDoor);
            yield return new Section("Skate enemy", 3, true, SkateEnemy);
            yield return new Section("Skate tunnel", 3, true, SkateTunnel);
            yield return new Section("Skate islands", 3, true, SkateIslands);
            yield return new Section("Skate stop", 3, true, SkateStop);
            yield return new Section("Skate lake", 3, true, SkateLake);
            yield return new Section("Skate shelf", 3, true, SkateShelf);
            yield return new Section("Skate two bricks", 3, true, SkateTwoBricks);
            yield return new Section("Skate last", 3, true, SkateLast);
            yield return new Section("Chain three hooks", 4, true, ChainThreeHooks);
            yield return new Section("Chain guarded landing", 4, true, ChainGuardedLanding);
            yield return new Section("Chain low landing", 4, true, ChainLowLanding);
            yield return new Section("Flames low and hot", 5, true, FlamesLowAndHot);
            yield return new Section("Flames steps", 5, true, FlamesSteps);
            yield return new Section("Flames guarded", 5, true, FlamesGuarded);
            yield return new Section("Flames brick", 5, true, FlamesBrick);
            yield return new Section("Flames long hop", 5, true, FlamesLongHop);
            yield return new Section("Flames last", 5, true, FlamesLast);
            yield return new Section("Final bridge and brick", 5, true, FinalBridgeAndBrick);
            yield return new Section("Final door", 5, true, FinalDoor);
            yield return new Section("Final underground", 5, true, FinalUnderground);
            yield return new Section("Final ice and hooks", 5, true, FinalIceAndHooks);
            yield return new Section("Final caps lock", 5, true, FinalLock);
            yield return new Section("Shift run", 6, true,
                (level, x) => ShiftRun(level, x, true, RunWords(rng, 4 + rng.Next(3))));
        }

        /// <summary>Ground words for a Shift run, alternating a word for a and a word for A.</summary>
        static string[] RunWords(System.Random rng, int count)
        {
            string[] small = { "slip in", "duck down", "small words", "a little", "squeeze" };
            string[] big = { "push out", "break out", "big words", "then a lot", "smash" };
            var words = new string[count];
            for (int i = 0; i < count; i++) words[i] = Pick(rng, i % 2 == 0 ? small : big);
            return words;
        }

#if UNITY_EDITOR
        /// <summary>Builds only the random level's scene, so edits made to the other scenes are left alone.</summary>
        [MenuItem("lowercase UPPERCASE/Rebuild Random Level Scene")]
        public static void RebuildRandomScene()
        {
            PrepareAssets(out FormSettings lower, out FormSettings upper);
            BuildRandomScene(lower, upper);
            SetBuildScenes();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(RandomScene);
        }

        /// <summary>
        /// The letter, HUD and camera with an empty level. Finishing it loads the scene again, which builds the next
        /// random level, and its game over screen leaves the high scores alone.
        /// </summary>
        static void BuildRandomScene(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(RandomScene, "Random");
            FinishScene(RandomScene, level, "Random", lower, upper, new Vector2(110f, Ground),
                "", "a", "", scrollEndPx: RandomLevelPlaceholderEndPx, startsArmed: false,
                nextScene: SceneName(RandomScene), configure: (flow, hud) =>
                {
                    flow.gameOver.recordsHighScores = false;
                    flow.gameOver.transform.Find("High Scores Heading").gameObject.SetActive(false);

                    var random = flow.gameObject.AddComponent<RandomLevel>();
                    random.level = level;
                    random.flow = flow;
                    random.feedback = Camera.main.GetComponent<ScreenFeedback>();
                    random.title = hud.Find("Title").GetComponent<TMP_Text>();
                    random.mono = mono;
                    random.monoBold = monoBold;
                    random.black = black;
                    random.white = white;
                });
        }
#endif
    }
}
