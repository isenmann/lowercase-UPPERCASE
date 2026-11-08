using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LowercaseUppercase.Tests
{
    /// <summary>The 60 second clock, and the points for enemies and for the time left.</summary>
    public class TimerAndScoreTests
    {
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

        static IEnumerator LoadWithoutIntro(string scene)
        {
            LevelTransition.SkipNextIntro = true;
            yield return SceneManager.LoadSceneAsync(scene);
            yield return null;
        }

        static IEnumerator DieAndReload(LetterController letter)
        {
            letter.Burn();
            yield return WaitUntil(() => letter == null);
            yield return null;
        }

        static LetterController FindLetter() => GameObject.Find("Letter").GetComponent<LetterController>();
        static Checkpoint FindCheckpoint() => UnityEngine.Object.FindAnyObjectByType<Checkpoint>();
        static Enemy FindEnemy() => UnityEngine.Object.FindAnyObjectByType<Enemy>();

        static readonly string[] AllLevels =
        {
            "Chapter1", "Playground", "Vocabulary",
            "Overworld", "Crack", "Wall", "Plank", "Gate", "Cellar", "Space", "Skate",
            "Trail", "Chain", "Ladder", "Skyline", "Flames", "Tar", "Final"
        };

        [SetUp]
        public void StartANewGame()
        {
            LevelRun.NewGame();
            HighScores.StorageKey = HighScores.DefaultStorageKey + ".tests";
            HighScores.Clear();
        }

        [TearDown]
        public void ForgetTheTestHighScores()
        {
            HighScores.Clear();
            HighScores.StorageKey = HighScores.DefaultStorageKey;
        }

        [UnityTest]
        public IEnumerator EveryLevelStartsWithAFullClockThatCountsDown(
            [ValueSource(nameof(AllLevels))] string scene)
        {
            LevelRun.TimeLeft = 1f;
            yield return LoadWithoutIntro(scene);
            Assert.That(LevelRun.TimeLeft, Is.InRange(LevelRun.LevelSeconds - 1f, LevelRun.LevelSeconds));

            float before = LevelRun.TimeLeft;
            yield return WaitUntil(() => LevelRun.TimeLeft < before);
        }

        [UnityTest]
        public IEnumerator TheIntroDoesNotUseUpTheClock()
        {
            LevelTransition.SkipNextIntro = false;
            yield return SceneManager.LoadSceneAsync("Cellar");
            yield return null;

            LevelTransition transition = UnityEngine.Object.FindAnyObjectByType<LevelTransition>();
            Assert.IsTrue(transition.IsPlaying);
            yield return WaitUntil(() => !transition.IsPlaying);
            Assert.That(LevelRun.TimeLeft, Is.GreaterThan(LevelRun.LevelSeconds - 1f));
        }

        [UnityTest]
        public IEnumerator DyingDoesNotRestartTheClock()
        {
            yield return LoadWithoutIntro("Skyline");
            LevelRun.TimeLeft = 30f;

            yield return DieAndReload(FindLetter());

            Assert.That(LevelRun.TimeLeft, Is.InRange(25f, 30f));
        }

        [UnityTest]
        public IEnumerator RunningOutOfTimeEndsTheRun(
            [ValueSource(nameof(AllLevels))] string scene)
        {
            yield return LoadWithoutIntro(scene);
            LetterController letter = FindLetter();
            PrototypeFlow flow = UnityEngine.Object.FindAnyObjectByType<PrototypeFlow>();
            LevelRun.EnemyKilled(Vector2.zero);
            LevelRun.TimeLeft = 0.01f;

            yield return WaitUntil(() => letter.IsFallingOff);
            Assert.IsFalse(flow.gameOver.gameObject.activeSelf, "The fall should play before the game over screen.");

            yield return WaitUntil(() => flow.IsGameOver);
            Assert.IsTrue(flow.gameOver.gameObject.activeSelf);
            Assert.AreEqual($"score {LevelRun.PointsPerEnemy}", flow.gameOver.score.text);
            Assert.IsTrue(flow.gameOver.IsEnteringName, "The first score in an empty table should ask for a name.");
            flow.gameOver.ConfirmName();

            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Menu");
            yield return null;
            Assert.AreEqual(0, LevelRun.Score);
        }

        [UnityTest]
        public IEnumerator AHighScoreTakesATypedName()
        {
            for (int i = 1; i <= HighScores.Size; i++) HighScores.Insert(i * 1000);
            yield return LoadWithoutIntro("Crack");
            PrototypeFlow flow = UnityEngine.Object.FindAnyObjectByType<PrototypeFlow>();
            for (int i = 0; i < 25; i++) LevelRun.EnemyKilled(new Vector2(i, 0f));
            LevelRun.TimeLeft = 0.01f;

            yield return WaitUntil(() => flow.IsGameOver);
            GameOverScreen screen = flow.gameOver;
            Assert.IsTrue(screen.IsEnteringName);
            Assert.AreEqual(8, screen.Place, "2500 should land below 3000 and above 2000.");
            Assert.IsFalse(UnityEngine.Object.FindAnyObjectByType<LetterInput>().enabled, "Typing must not move the letter.");

            foreach (char c in " ad<a>  b!") screen.Type(c);
            Assert.AreEqual("ada  b!", screen.TypedName, "No leading space and no tag characters.");
            foreach (char c in "cdefghijklmnop") screen.Type(c);
            Assert.AreEqual(HighScores.MaxNameLength, screen.TypedName.Length);
            screen.ConfirmName();

            var entries = HighScores.Load();
            Assert.AreEqual(HighScores.Size, entries.Count);
            Assert.AreEqual(2500, entries[8].score);
            Assert.AreEqual("ada  b!cdefg", entries[8].name);
            Assert.AreEqual(2000, entries[9].score, "1000 should be the one pushed out.");
        }

        [UnityTest]
        public IEnumerator FinishingTheLastLevelShowsTheTableWithoutGameOver()
        {
            yield return LoadWithoutIntro("Final");
            PrototypeFlow flow = UnityEngine.Object.FindAnyObjectByType<PrototypeFlow>();
            LevelTransition transition = flow.GetComponent<LevelTransition>();
            Assert.AreSame(flow.gameOver, transition.endScreen, "Final should end on the high score table.");

            LevelRun.EnemyKilled(Vector2.zero);
            flow.gameOver.Show(LevelRun.Score, gameOver: false);
            Assert.IsFalse(flow.gameOver.heading.activeSelf, "A finished run isn't a game over.");
            Assert.IsTrue(flow.gameOver.IsEnteringName);
            yield return null;
            Assert.AreEqual("Final", SceneManager.GetActiveScene().name, "Typing must not leave the level.");
        }

        [UnityTest]
        public IEnumerator OnlyTheLastLevelEndsOnTheTable(
            [Values("Overworld", "Crack", "Tar")] string scene)
        {
            yield return LoadWithoutIntro(scene);
            Assert.IsNull(UnityEngine.Object.FindAnyObjectByType<LevelTransition>().endScreen);
        }

        [UnityTest]
        public IEnumerator ALowScoreOnlyShowsTheTable()
        {
            for (int i = 1; i <= HighScores.Size; i++) HighScores.Insert(i * 1000);
            yield return LoadWithoutIntro("Crack");
            PrototypeFlow flow = UnityEngine.Object.FindAnyObjectByType<PrototypeFlow>();
            LevelRun.EnemyKilled(Vector2.zero);
            LevelRun.TimeLeft = 0.01f;

            yield return WaitUntil(() => flow.IsGameOver);
            Assert.IsFalse(flow.gameOver.IsEnteringName);
            Assert.AreEqual(-1, flow.gameOver.Place);
            Assert.AreEqual(HighScores.Size, HighScores.Load().Count);
            Assert.AreEqual(1000, HighScores.Load()[HighScores.Size - 1].score);
        }

        [UnityTest]
        public IEnumerator RunningOutOfTimePastTheCheckpointDoesNotReturnThere()
        {
            yield return LoadWithoutIntro("Skyline");
            LetterController letter = FindLetter();
            Checkpoint checkpoint = FindCheckpoint();
            letter.Respawn(checkpoint.respawnFeet);
            yield return WaitUntil(() => checkpoint.IsReached);

            LevelRun.TimeLeft = 0.01f;
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Menu");

            // The next visit to the level starts from the beginning with a full clock.
            yield return LoadWithoutIntro("Skyline");
            Assert.IsFalse(FindCheckpoint().IsReached);
            Assert.That(LevelRun.TimeLeft, Is.GreaterThan(LevelRun.LevelSeconds - 1f));
        }

        [UnityTest]
        public IEnumerator KillingAnEnemyGivesPointsOnlyOncePerLevel()
        {
            yield return LoadWithoutIntro("Playground");
            FindEnemy().Kill();
            Assert.AreEqual(LevelRun.PointsPerEnemy, LevelRun.Score);

            yield return DieAndReload(FindLetter());
            Assert.AreEqual(LevelRun.PointsPerEnemy, LevelRun.Score, "The score should survive a death.");

            FindEnemy().Kill();
            Assert.AreEqual(LevelRun.PointsPerEnemy, LevelRun.Score, "The returned enemy should not pay again.");
        }

        [UnityTest]
        public IEnumerator ReachingTheExitTurnsTheTimeLeftIntoPoints()
        {
            yield return LoadWithoutIntro("Overworld");
            LetterController letter = FindLetter();
            LevelRun.TimeLeft = 20f;

            letter.SlotInto(letter.transform.position);

            Assert.That(LevelRun.Score, Is.InRange(20 * LevelRun.PointsPerSecondLeft - LevelRun.PointsPerSecondLeft,
                20 * LevelRun.PointsPerSecondLeft));
            Assert.AreEqual(0f, LevelRun.TimeLeft);

            // The score carries into the next level, which gets a full clock.
            int banked = LevelRun.Score;
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Crack");
            yield return null;
            Assert.AreEqual(banked, LevelRun.Score);
            Assert.That(LevelRun.TimeLeft, Is.GreaterThan(LevelRun.LevelSeconds - 1f));
        }

        [Test]
        public void ADeliberateRestartGivesBackWhatTheLevelEarned()
        {
            LevelRun.EnemyKilled(Vector2.zero);
            LevelRun.EnterLevel();
            LevelRun.EnemyKilled(Vector2.one);
            Assert.AreEqual(2 * LevelRun.PointsPerEnemy, LevelRun.Score);

            LevelRun.AbandonLevel();
            Assert.AreEqual(LevelRun.PointsPerEnemy, LevelRun.Score);
        }

        [UnityTest]
        public IEnumerator TheMenuClearsTheScore()
        {
            LevelRun.EnemyKilled(Vector2.zero);
            yield return SceneManager.LoadSceneAsync("Menu");
            yield return null;
            Assert.AreEqual(0, LevelRun.Score);
        }
    }
}
