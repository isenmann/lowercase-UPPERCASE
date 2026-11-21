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
        /// <summary>The level's first checkpoint. Not just any one: which one Unity finds first changes between loads.</summary>
        static Checkpoint FindCheckpoint()
        {
            Checkpoint first = null;
            foreach (Checkpoint checkpoint in UnityEngine.Object.FindObjectsByType<Checkpoint>())
                if (first == null || checkpoint.order < first.order) first = checkpoint;
            return first;
        }
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
        public IEnumerator AGamepadEntersTheNameArcadeStyle()
        {
            for (int i = 1; i <= HighScores.Size; i++) HighScores.Insert(i * 1000);
            yield return LoadWithoutIntro("Crack");
            PrototypeFlow flow = UnityEngine.Object.FindAnyObjectByType<PrototypeFlow>();
            for (int i = 0; i < 25; i++) LevelRun.EnemyKilled(new Vector2(i, 0f));
            LevelRun.TimeLeft = 0.01f;

            yield return WaitUntil(() => flow.IsGameOver);
            GameOverScreen screen = flow.gameOver;
            Assert.IsTrue(screen.IsEnteringName);
            Assert.AreEqual("", screen.TypedName);
            Assert.AreEqual(0, screen.Cursor, "The cursor starts on the Enter sign after the (empty) name.");

            screen.StepLetter(1);
            Assert.AreEqual("A", screen.TypedName, "Up on the Enter sign starts a new letter at A.");
            screen.StepLetter(-1);
            Assert.AreEqual("", screen.TypedName, "Down from A on the last letter is the Enter sign again.");
            screen.StepLetter(-1);
            Assert.AreEqual("?", screen.TypedName, "Down on the Enter sign starts at the end of the list.");
            for (int i = 0; i < 5; i++) screen.StepLetter(-1);
            Assert.AreEqual("9", screen.TypedName, "The space is skipped at the start of a name.");

            screen.PadAccept();
            Assert.AreEqual(1, screen.Cursor, "A on a letter goes one slot right.");
            screen.StepLetter(1);
            screen.StepLetter(1);
            screen.PadAccept();
            Assert.AreEqual("9B", screen.TypedName);
            Assert.IsTrue(screen.IsEnteringName);

            screen.MoveCursor(-5);
            Assert.AreEqual(0, screen.Cursor);
            screen.StepLetter(1);
            Assert.AreEqual("-B", screen.TypedName, "A letter within the name skips the space at the start, too.");
            screen.MoveCursor(1);
            screen.StepLetter(-1);
            screen.StepLetter(-1);
            Assert.AreEqual("-", screen.TypedName, "Down from A on the last letter takes it away.");
            screen.Type('o');
            screen.MoveCursor(-1);
            screen.Type('x');
            Assert.AreEqual("-xo", screen.TypedName, "The keyboard types at the cursor.");
            screen.DeleteBack();
            screen.MoveCursor(1);
            Assert.AreEqual(2, screen.Cursor);
            screen.PadAccept();
            Assert.IsFalse(screen.IsEnteringName, "A on the Enter sign saves.");

            Assert.AreEqual("-o", HighScores.Load()[8].name);
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
        public IEnumerator EachDeathCostsALifeAndTheHudShowsIt()
        {
            yield return LoadWithoutIntro("Skyline");
            PrototypeFlow flow = UnityEngine.Object.FindAnyObjectByType<PrototypeFlow>();
            Assert.AreEqual(LevelRun.StartingLives, LevelRun.Lives);
            Assert.AreEqual(LevelRun.StartingLives, CountLetters(flow.lives.text, $">{flow.lifeGlyph}</color>"), "The HUD should show one life glyph per life.");

            yield return DieAndReload(FindLetter());
            Assert.AreEqual(LevelRun.StartingLives - 1, LevelRun.Lives);
            flow = UnityEngine.Object.FindAnyObjectByType<PrototypeFlow>();
            Assert.IsFalse(flow.IsGameOver);
            yield return null;
            string lost = ColorUtility.ToHtmlStringRGBA(flow.lostLifeColor);
            Assert.AreEqual(1, CountLetters(flow.lives.text, lost), "One of the three letters should be dimmed.");
        }

        [UnityTest]
        public IEnumerator LosingTheLastLifeEndsTheRun()
        {
            yield return LoadWithoutIntro("Skyline");
            for (int i = 1; i < LevelRun.StartingLives; i++)
            {
                yield return DieAndReload(FindLetter());
                Assert.IsFalse(UnityEngine.Object.FindAnyObjectByType<PrototypeFlow>().IsGameOver);
            }
            Assert.AreEqual(1, LevelRun.Lives);

            PrototypeFlow flow = UnityEngine.Object.FindAnyObjectByType<PrototypeFlow>();
            LevelRun.EnemyKilled(Vector2.zero);
            FindLetter().Burn();
            yield return WaitUntil(() => flow.IsGameOver);
            Assert.AreEqual(0, LevelRun.Lives);
            Assert.AreEqual("Skyline", SceneManager.GetActiveScene().name, "The last death should not reload the level.");
            Assert.IsTrue(flow.gameOver.gameObject.activeSelf);
            Assert.IsTrue(flow.gameOver.heading.activeSelf);
            Assert.AreEqual($"score {LevelRun.PointsPerEnemy}", flow.gameOver.score.text);
            flow.gameOver.ConfirmName();

            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Menu");
            yield return null;
            Assert.AreEqual(LevelRun.StartingLives, LevelRun.Lives, "The menu should start the next run with all lives.");
        }

        static int CountLetters(string text, string part)
        {
            int count = 0;
            for (int i = text.IndexOf(part); i >= 0; i = text.IndexOf(part, i + part.Length)) count++;
            return count;
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

        /// <summary>Pays the given number of enemies, each at its own place so each one pays.</summary>
        static void KillEnemies(int count, float row)
        {
            for (int i = 0; i < count; i++) LevelRun.EnemyKilled(new Vector2(i, row));
        }

        static int EnemiesFor(int points) => Mathf.CeilToInt((float)points / LevelRun.PointsPerEnemy);

        [Test]
        public void EveryFifteenHundredPointsGiveBackALostLife()
        {
            LevelRun.NewGame();
            LevelRun.EnterLevel();
            LevelRun.LoseLife();
            LevelRun.LoseLife();
            Assert.AreEqual(1, LevelRun.Lives);

            KillEnemies(EnemiesFor(LevelRun.PointsPerLifeRefill) - 1, 0f);
            Assert.AreEqual(1, LevelRun.Lives, "Not yet at 1500.");
            KillEnemies(1, 1f);
            Assert.AreEqual(2, LevelRun.Lives);

            KillEnemies(EnemiesFor(LevelRun.PointsPerLifeRefill), 2f);
            Assert.AreEqual(3, LevelRun.Lives);
        }

        [Test]
        public void PointsNeverAddALifeBeyondTheStartingLives()
        {
            LevelRun.NewGame();
            LevelRun.EnterLevel();
            KillEnemies(EnemiesFor(LevelRun.PointsPerLifeRefill), 0f);
            Assert.AreEqual(LevelRun.StartingLives, LevelRun.Lives);

            // The 1500 passed with every life left is used up: losing a life afterwards waits for the next 1500.
            LevelRun.LoseLife();
            KillEnemies(1, 1f);
            Assert.AreEqual(LevelRun.StartingLives - 1, LevelRun.Lives);
            KillEnemies(EnemiesFor(LevelRun.PointsPerLifeRefill), 2f);
            Assert.AreEqual(LevelRun.StartingLives, LevelRun.Lives);
        }

        [Test]
        public void ADeliberateRestartTakesBackALifeTheLevelGaveBack()
        {
            LevelRun.NewGame();
            LevelRun.EnterLevel();
            LevelRun.LoseLife();
            KillEnemies(EnemiesFor(LevelRun.PointsPerLifeRefill), 0f);
            Assert.AreEqual(LevelRun.StartingLives, LevelRun.Lives);

            LevelRun.AbandonLevel();
            Assert.AreEqual(LevelRun.StartingLives - 1, LevelRun.Lives);
            Assert.AreEqual(0, LevelRun.Score);

            // Earning as many points again gives the life back again. Other enemies: the ones paid stay paid.
            KillEnemies(EnemiesFor(LevelRun.PointsPerLifeRefill), 5f);
            Assert.AreEqual(LevelRun.StartingLives, LevelRun.Lives);
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

        [Test]
        public void AsterisksCarryIntoTheNextLevelButADeathTakesThemAway()
        {
            LevelRun.EnterLevel();
            LevelRun.HasAsterisks = true;
            LevelRun.AbandonLevel();
            Assert.IsFalse(LevelRun.HasAsterisks);

            LevelRun.HasAsterisks = true;
            LevelRun.EnterLevel();
            Assert.IsTrue(LevelRun.HasAsterisks);
            LevelRun.AbandonLevel();
            Assert.IsTrue(LevelRun.HasAsterisks);

            LevelRun.LoseLife();
            Assert.IsFalse(LevelRun.HasAsterisks);

            LevelRun.HasAsterisks = true;
            LevelRun.NewGame();
            Assert.IsFalse(LevelRun.HasAsterisks);
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
