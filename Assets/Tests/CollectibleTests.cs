using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LowercaseUppercase.Tests
{
    /// <summary>The gold # in the levels: each pays once, stays taken through a death, and comes back on a fresh start.</summary>
    public class CollectibleTests
    {
        const string Level = "Crack";
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

            // A patrolling enemy could reach the letter and cause a death the test didn't ask for.
            foreach (Enemy enemy in UnityEngine.Object.FindObjectsByType<Enemy>())
                UnityEngine.Object.Destroy(enemy.gameObject);
        }

        static IEnumerator DieAndReload(LetterController letter)
        {
            letter.Burn();
            yield return WaitUntil(() => letter == null);
            yield return null;
        }

        static LetterController FindLetter() => GameObject.Find("Letter").GetComponent<LetterController>();

        static Collectible[] AllCollectibles() =>
            UnityEngine.Object.FindObjectsByType<Collectible>(FindObjectsInactive.Include);

        static Collectible LeftmostCollectible()
        {
            Collectible found = null;
            foreach (Collectible collectible in AllCollectibles())
                if (found == null || collectible.transform.position.x < found.transform.position.x)
                    found = collectible;
            return found;
        }

        static int ActiveCount()
        {
            int active = 0;
            foreach (Collectible collectible in AllCollectibles())
                if (collectible.gameObject.activeSelf) active++;
            return active;
        }

        /// <summary>Puts the letter's feet just under the # so its body overlaps it, then waits for the pickup.</summary>
        static IEnumerator TakeLeftmost(LetterController letter)
        {
            Collectible target = LeftmostCollectible();
            letter.Respawn((Vector2)target.transform.position + Vector2.down * 0.2f);
            yield return WaitUntil(() => target.IsTaken);
        }

        static readonly string[] PlayOrder =
        {
            "Overworld", "Crack", "Wall", "Plank", "Gate", "Cellar", "Space", "Skate", "Trail", "Chain", "Ladder", "Skyline", "Flames", "Tar", "Final"
        };

        [SetUp]
        public void StartANewGame() => LevelRun.NewGame();

        [UnityTest]
        public IEnumerator EveryLevelInThePlayOrderHasSomeToCollect(
            [ValueSource(nameof(PlayOrder))] string scene)
        {
            yield return LoadWithoutIntro(scene);
            int count = AllCollectibles().Length;
            Assert.That(count, Is.GreaterThan(0));

            PrototypeFlow flow = UnityEngine.Object.FindAnyObjectByType<PrototypeFlow>();
            StringAssert.EndsWith($" 0/{count}", flow.collectibles.text);
        }

        [UnityTest]
        public IEnumerator TouchingOneScoresItOnceAndHidesIt()
        {
            yield return LoadWithoutIntro(Level);
            Collectible target = LeftmostCollectible();
            yield return TakeLeftmost(FindLetter());

            Assert.AreEqual(LevelRun.PointsPerCollectible, LevelRun.Score);
            Assert.AreEqual(1, LevelRun.CollectedThisLevel);
            yield return WaitUntil(() => !target.gameObject.activeSelf);

            // Being collected again by position must not pay twice.
            // By where it was placed, as the game does: by now the # has risen away from there.
            LevelRun.Collect(target.Home);
            Assert.AreEqual(LevelRun.PointsPerCollectible, LevelRun.Score);
        }

        [UnityTest]
        public IEnumerator ATakenOneStaysTakenAfterADeath()
        {
            yield return LoadWithoutIntro(Level);
            int total = AllCollectibles().Length;
            yield return TakeLeftmost(FindLetter());

            yield return DieAndReload(FindLetter());

            Assert.AreEqual(total, AllCollectibles().Length);
            Assert.AreEqual(total - 1, ActiveCount());
            Assert.AreEqual(1, LevelRun.CollectedThisLevel);
            Assert.AreEqual(LevelRun.PointsPerCollectible, LevelRun.Score, "The points should survive a death.");
        }

        [UnityTest]
        public IEnumerator StartingTheLevelAfreshBringsThemAllBack()
        {
            yield return LoadWithoutIntro(Level);
            int total = AllCollectibles().Length;
            yield return TakeLeftmost(FindLetter());

            // What the restart key does: give back this level's points, then load it without continuing.
            LevelRun.AbandonLevel();
            yield return LoadWithoutIntro(Level);

            Assert.AreEqual(total, ActiveCount());
            Assert.AreEqual(0, LevelRun.CollectedThisLevel);
            Assert.AreEqual(0, LevelRun.Score);
        }
    }
}
