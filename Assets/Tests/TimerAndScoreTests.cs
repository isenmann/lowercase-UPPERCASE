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

        [SetUp]
        public void StartANewGame() => LevelRun.NewGame();

        [UnityTest]
        public IEnumerator EveryLevelStartsWithAFullClockThatCountsDown(
            [Values("Chapter1", "Playground", "Vocabulary", "Overworld", "Cellar", "Skyline")] string scene)
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
            [Values("Chapter1", "Playground", "Vocabulary", "Overworld", "Cellar", "Skyline")] string scene)
        {
            yield return LoadWithoutIntro(scene);
            LetterController letter = FindLetter();
            PrototypeFlow flow = UnityEngine.Object.FindAnyObjectByType<PrototypeFlow>();
            LevelRun.EnemyKilled(Vector2.zero);
            LevelRun.TimeLeft = 0.01f;

            yield return WaitUntil(() => letter.IsFallingOff);
            Assert.IsFalse(flow.gameOverScreen.activeSelf, "The fall should play before the game over screen.");

            yield return WaitUntil(() => flow.IsGameOver);
            Assert.IsTrue(flow.gameOverScreen.activeSelf);
            Assert.AreEqual($"score {LevelRun.PointsPerEnemy}", flow.gameOverScore.text);

            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Menu");
            yield return null;
            Assert.AreEqual(0, LevelRun.Score);
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
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Cellar");
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
