using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LowercaseUppercase.Tests
{
    /// <summary>The level editor's format: reading it, building it into the custom level scene, and its rules.</summary>
    public class CustomLevelTests
    {
        const string Scene = "CustomLevel";
        const float Timeout = 10f;

        static IEnumerator WaitUntil(Func<bool> condition)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - start > Timeout) Assert.Fail("Timed out waiting for a condition.");
                yield return null;
            }
        }

        static IEnumerator Load(CustomLevelData data)
        {
            CustomRun.Level = data;
            LevelTransition.SkipNextIntro = true;
            yield return SceneManager.LoadSceneAsync(Scene);
            yield return null;
        }

        static LetterController FindLetter() => GameObject.Find("Letter").GetComponent<LetterController>();

        static WordBlock FindWord(string content) => UnityEngine.Object.FindObjectsByType<WordBlock>()
            .FirstOrDefault(w => w.GetComponent<TMPro.TMP_Text>().text == content);

        static CustomLevelData SmallLevel()
        {
            var data = new CustomLevelData { name = "Test room", seconds = 30f, startX = 200f };
            data.pieces.Add(new CustomPiece(CustomPieceType.Word, 40f, 520f, "a floor to stand on"));
            data.pieces.Add(new CustomPiece(CustomPieceType.Word, 900f, 520f, "cold", CustomPieceType.Ice));
            data.pieces.Add(new CustomPiece(CustomPieceType.Checkpoint, 700f, 520f));
            data.pieces.Add(new CustomPiece(CustomPieceType.Checkpoint, 400f, 520f));
            data.pieces.Add(new CustomPiece(CustomPieceType.Hash, 500f, 494f));
            data.pieces.Add(new CustomPiece(CustomPieceType.Exit, 1000f, 520f, "c_t", "a"));
            return data;
        }

        [SetUp]
        public void StartANewGame() => LevelRun.NewGame();

        [TearDown]
        public void ForgetTheLevel() => CustomRun.Level = null;

        [Test]
        public void ALevelSurvivesARoundTripThroughJson()
        {
            CustomLevelData data = SmallLevel();
            CustomLevelData read = CustomLevelData.FromJson(data.ToJson());

            Assert.That(read.name, Is.EqualTo("Test room"));
            Assert.That(read.seconds, Is.EqualTo(30f));
            Assert.That(read.pieces.Count, Is.EqualTo(data.pieces.Count));
            Assert.That(read.pieces[1].kind, Is.EqualTo(CustomPieceType.Ice));
            Assert.That(read.pieces[5].text, Is.EqualTo("c_t"));
        }

        [Test]
        public void BrokenOrNewerLevelsAreRefusedWithAReason()
        {
            Assert.Throws<FormatException>(() => CustomLevelData.FromJson(""));
            Assert.Throws<FormatException>(() => CustomLevelData.FromJson("{ not json"));
            Assert.Throws<FormatException>(() => CustomLevelData.FromJson("{\"version\": 99}"));
        }

        [UnityTest]
        public IEnumerator TheSampleLevelLoadsWhenNoLevelIsHandedOver()
        {
            yield return Load(null);

            Assert.That(UnityEngine.Object.FindAnyObjectByType<GoalBlank>(), Is.Not.Null);
            Assert.That(FindWord("brick road"), Is.Not.Null);
            Assert.That(FindWord("slippery words").kind, Is.EqualTo(WordKind.Ice));
            Assert.That(FindWord("--------").kind, Is.EqualTo(WordKind.Hyphen));
            Assert.That(UnityEngine.Object.FindObjectsByType<WordBlock>().Any(w => w.kind == WordKind.Cracked));
            Assert.That(UnityEngine.Object.FindObjectsByType<WordBlock>().Any(w => w.kind == WordKind.Power));
            Assert.That(UnityEngine.Object.FindObjectsByType<Collectible>().Length, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator AHandedOverLevelIsBuiltWithItsSettings()
        {
            yield return Load(SmallLevel());

            var flow = UnityEngine.Object.FindAnyObjectByType<PrototypeFlow>();
            Assert.That(flow.endlessLives, Is.True);
            Assert.That(flow.sentenceBefore, Is.EqualTo("the c"));
            Assert.That(LevelRun.TimeLeft, Is.InRange(29f, 30f));
            Assert.That(FindWord("a floor to stand on"), Is.Not.Null);
            Assert.That(FindWord("cold").kind, Is.EqualTo(WordKind.Ice));
            Assert.That(FindLetter().transform.position.x, Is.EqualTo((200f - 640f) / 72f).Within(0.01f));

            // Numbered left to right, whatever order they were placed in.
            Assert.That(flow.checkpoints.Length, Is.EqualTo(2));
            Assert.That(flow.checkpoints[0].transform.position.x, Is.LessThan(flow.checkpoints[1].transform.position.x));
            Assert.That(flow.checkpoints[0].order, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ADeathCostsNoLife()
        {
            yield return Load(SmallLevel());
            int lives = LevelRun.Lives;

            LetterController letter = FindLetter();
            letter.Burn();
            yield return WaitUntil(() => letter == null);
            yield return WaitUntil(() => GameObject.Find("Letter") != null);

            Assert.That(LevelRun.Lives, Is.EqualTo(lives));
            Assert.That(UnityEngine.Object.FindAnyObjectByType<GameOverScreen>(FindObjectsInactive.Include).IsShowing, Is.False);
        }

        [UnityTest]
        public IEnumerator RunningOutOfTimeRestartsTheLevelWithAFullClock()
        {
            yield return Load(SmallLevel());
            LetterController letter = FindLetter();
            LevelRun.TimeLeft = 0.05f;

            yield return WaitUntil(() => letter == null);
            yield return WaitUntil(() => GameObject.Find("Letter") != null);
            yield return null;

            Assert.That(LevelRun.TimeLeft, Is.InRange(29f, 30f));
            Assert.That(UnityEngine.Object.FindAnyObjectByType<GameOverScreen>(FindObjectsInactive.Include).IsShowing, Is.False);
        }

        [Test]
        public void UnknownPiecesAreSkipped()
        {
            var data = new CustomLevelData();
            data.pieces.Add(new CustomPiece("teleporter", 100f, 100f));
            var root = new GameObject("Level").transform;
            try
            {
                LogAssert.Expect(LogType.Warning, "Custom level: skipped a piece of unknown type 'teleporter'.");
                var layout = Levels.PrototypeBuilder.BuildCustomLevel(root, data, null, null, null, null);
                Assert.That(root.childCount, Is.EqualTo(0));
                Assert.That(layout.Checkpoints, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
        }

        /// <summary>One of every piece from step 0b, laid out along open ground with a high word for the drip.</summary>
        static CustomLevelData EveryPiece()
        {
            var data = new CustomLevelData { name = "Every piece" };
            var p = data.pieces;
            p.Add(new CustomPiece(CustomPieceType.Word, 0f, 520f, "the long floor that carries every single piece of the level along with it as far as it goes and further still"));
            p.Add(new CustomPiece(CustomPieceType.Word, 1400f, 300f, "a high shelf"));
            p.Add(new CustomPiece(CustomPieceType.Word, 4800f, 520f, "a hot word", CustomPieceType.Hot));
            p.Add(new CustomPiece(CustomPieceType.Tube, 300f, 520f, "tube") { count = 2 });
            p.Add(new CustomPiece(CustomPieceType.Spring, 500f, 520f));
            p.Add(new CustomPiece(CustomPieceType.Door, 900f, 520f));
            p.Add(new CustomPiece(CustomPieceType.Plate, 700f, 520f));
            p.Add(new CustomPiece(CustomPieceType.Hooks, 1000f, 250f) { count = 3 });
            p.Add(new CustomPiece(CustomPieceType.CapsLock, 1800f, 0f) { x2 = 2400f });
            p.Add(new CustomPiece(CustomPieceType.CapsKey, 2300f, 500f));
            p.Add(new CustomPiece(CustomPieceType.Hill, 100f, 0f));
            p.Add(new CustomPiece(CustomPieceType.Cloud, 400f, 150f));
            p.Add(new CustomPiece(CustomPieceType.Kerned, 5400f, 520f, "b\u00a1etwe\u00a1en"));
            p.Add(new CustomPiece(CustomPieceType.Lift, 6100f, 520f) { y2 = 250f });
            string[] walkers =
            {
                CustomPieceType.Walker, CustomPieceType.Spike, CustomPieceType.Armour, CustomPieceType.Swapper,
                CustomPieceType.Mimic, CustomPieceType.Curl, CustomPieceType.Hopper
            };
            for (int i = 0; i < walkers.Length; i++)
                p.Add(new CustomPiece(CustomPieceType.Enemy, 2600f + i * 150f, 200f, "", walkers[i]));
            p.Add(new CustomPiece(CustomPieceType.Enemy, 1500f, 520f, "", CustomPieceType.Drip));
            p.Add(new CustomPiece(CustomPieceType.Enemy, 4900f, 400f, "", CustomPieceType.Spark));
            p.Add(new CustomPiece(CustomPieceType.Enemy, 3800f, 400f, "", CustomPieceType.Flier) { value = 100f });
            p.Add(new CustomPiece(CustomPieceType.Enemy, 4000f, 520f, "", CustomPieceType.Launcher) { flip = true });
            p.Add(new CustomPiece(CustomPieceType.Enemy, 4300f, 520f, "", CustomPieceType.FireBar));
            p.Add(new CustomPiece(CustomPieceType.Enemy, 4500f, 400f, "", CustomPieceType.Eye));
            p.Add(new CustomPiece(CustomPieceType.Enemy, 200f, 520f, "", CustomPieceType.Cursor) { x2 = 600f });
            return data;
        }

        static T[] All<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include);

        [UnityTest]
        public IEnumerator EveryPieceIsBuilt()
        {
            yield return Load(EveryPiece());

            WordBlock[] words = All<WordBlock>();
            Assert.That(words.Count(w => w.kind == WordKind.Spring), Is.EqualTo(1), "spring");
            Assert.That(words.Count(w => w.kind == WordKind.Comma), Is.EqualTo(3), "hooks");
            Assert.That(words.Count(w => w.kind == WordKind.Up), Is.EqualTo(1), "lift");
            Assert.That(words.Single(w => w.kind == WordKind.Period).linkedDoor,
                Is.SameAs(words.Single(w => w.kind == WordKind.Door)), "plate opens the door");
            Assert.That(All<CapsLockKey>().Single().zone, Is.SameAs(All<CapsLockZone>().Single()), "key");
            Assert.That(All<EnemyRise>().Length, Is.EqualTo(2), "risers");
            Assert.That(All<EnemyDrip>().Length, Is.EqualTo(1), "drip");
            Assert.That(All<EnemySpark>().Length, Is.EqualTo(1), "spark");
            Assert.That(All<EnemyWave>().Length, Is.EqualTo(1), "flier");
            Assert.That(All<EnemyLauncher>().Length, Is.EqualTo(1), "launcher");
            Assert.That(All<EnemyFireBar>().Length, Is.EqualTo(1), "fire bar");
            Assert.That(All<EnemyFollow>().Length, Is.EqualTo(1), "eye");
            Assert.That(All<EnemyCursor>().Length, Is.EqualTo(1), "cursor");
            Assert.That(All<EnemyCurl>().Length, Is.EqualTo(1), "curl");
            Assert.That(All<EnemyHop>().Length, Is.EqualTo(1), "hopper");
            Assert.That(All<EnemyCaseSwap>().Length, Is.EqualTo(1), "swapper");
            Assert.That(All<EnemyMimic>().Length, Is.EqualTo(1), "mimic");
            // & ^ § and the swapper and mimic walk.
            Assert.That(All<EnemyWalk>().Length, Is.EqualTo(5), "walkers");
        }

        [Test]
        public void EnemiesDropOntoTheWordBelowThem()
        {
            var data = new CustomLevelData();
            data.pieces.Add(new CustomPiece(CustomPieceType.Word, 0f, 520f, "the ground"));
            data.pieces.Add(new CustomPiece(CustomPieceType.Enemy, 100f, 100f, "", CustomPieceType.Walker));
            var root = new GameObject("Level").transform;
            try
            {
                Levels.PrototypeBuilder.BuildCustomLevel(root, data, null, null, null, null);
                Enemy enemy = root.GetComponentInChildren<Enemy>();
                Assert.That(enemy, Is.Not.Null);
                Assert.That(enemy.transform.position.y, Is.EqualTo((360f - 520f) / 72f).Within(0.05f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void AWalkerWithNoGroundIsSkipped()
        {
            var data = new CustomLevelData();
            data.pieces.Add(new CustomPiece(CustomPieceType.Enemy, 100f, 100f, "", CustomPieceType.Walker));
            var root = new GameObject("Level").transform;
            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("skipped the walker"));
                Levels.PrototypeBuilder.BuildCustomLevel(root, data, null, null, null, null);
                Assert.That(root.GetComponentInChildren<Enemy>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
        }
    }
}
