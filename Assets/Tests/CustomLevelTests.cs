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
            Assert.That(UnityEngine.Object.FindAnyObjectByType<GameOverScreen>().IsShowing, Is.False);
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
            Assert.That(UnityEngine.Object.FindAnyObjectByType<GameOverScreen>().IsShowing, Is.False);
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
    }
}
