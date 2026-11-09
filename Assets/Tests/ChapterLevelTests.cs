using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LowercaseUppercase.Tests
{
    /// <summary>
    /// What the levels of chapters 2 to 5 promise: a block to hit at the start, checkpoints in order, and the one
    /// thing each new kind of word is there to teach.
    /// </summary>
    public class ChapterLevelTests
    {
        const float Timeout = 10f;

        static readonly string[] NewLevels =
            { "Plank", "Gate", "Space", "Skate", "Trail", "Chain", "Ladder", "Flames", "Tar", "Final" };

        static readonly string[] PlayOrder =
        {
            "Overworld", "Crack", "Wall", "Plank", "Gate", "Cellar", "Space", "Skate",
            "Trail", "Chain", "Ladder", "Skyline", "Flames", "Tar", "Final"
        };

        LetterController letter;

        IEnumerator Load(string scene)
        {
            LevelTransition.SkipNextIntro = true;
            yield return SceneManager.LoadSceneAsync(scene);
            yield return null;

            // A patrolling enemy could reach a waiting letter and restart the level.
            foreach (Enemy enemy in UnityEngine.Object.FindObjectsByType<Enemy>())
                UnityEngine.Object.Destroy(enemy.gameObject);
            // Launchers aren't enemies themselves, but they keep firing new ones.
            foreach (EnemyLauncher launcher in UnityEngine.Object.FindObjectsByType<EnemyLauncher>())
                UnityEngine.Object.Destroy(launcher);

            var go = GameObject.Find("Letter");
            go.GetComponent<LetterInput>().enabled = false;
            letter = go.GetComponent<LetterController>();
            letter.SetInput(0f, false, false);
        }

        static IEnumerator WaitUntil(Func<bool> condition)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - start > Timeout) Assert.Fail("Timed out waiting for a condition.");
                yield return null;
            }
        }

        static WordBlock Find(string name)
        {
            GameObject go = GameObject.Find(name);
            Assert.IsNotNull(go, $"The level should have {name}.");
            return go.GetComponent<WordBlock>();
        }

        static Bounds BoundsOf(WordBlock block) => block.GetComponent<BoxCollider2D>().bounds;

        [SetUp]
        public void StartANewGame() => LevelRun.NewGame();

        [UnityTest]
        public IEnumerator TheLetterStartsUnarmedWithABlockToHit([ValueSource(nameof(NewLevels))] string scene)
        {
            yield return Load(scene);
            Assert.IsFalse(letter.GetComponent<LetterShooter>().IsArmed);

            bool found = false;
            foreach (WordBlock block in UnityEngine.Object.FindObjectsByType<WordBlock>())
                found |= block.kind == WordKind.Power;
            Assert.IsTrue(found, "The level should have a power block.");
        }

        [UnityTest]
        public IEnumerator CheckpointsAreNumberedFromLeftToRight([ValueSource(nameof(PlayOrder))] string scene)
        {
            yield return Load(scene);
            PrototypeFlow flow = UnityEngine.Object.FindAnyObjectByType<PrototypeFlow>();
            Assert.That(flow.checkpoints.Length, Is.GreaterThan(0));
            for (int i = 0; i < flow.checkpoints.Length; i++)
            {
                Assert.AreEqual(i, flow.checkpoints[i].order);
                if (i > 0)
                    Assert.That(flow.checkpoints[i].transform.position.x,
                        Is.GreaterThan(flow.checkpoints[i - 1].transform.position.x));
            }
        }

        [UnityTest]
        public IEnumerator DyingReturnsToTheFurthestCheckpointReached()
        {
            yield return Load("Crack");
            Checkpoint[] checkpoints = UnityEngine.Object.FindAnyObjectByType<PrototypeFlow>().checkpoints;
            Assert.That(checkpoints.Length, Is.GreaterThanOrEqualTo(2));

            foreach (Checkpoint checkpoint in checkpoints)
            {
                letter.Respawn(checkpoint.respawnFeet);
                Checkpoint reached = checkpoint;
                yield return WaitUntil(() => reached.IsReached);
            }

            LetterController before = letter;
            before.Burn();
            yield return WaitUntil(() => before == null);
            yield return null;

            Vector2 last = checkpoints[checkpoints.Length - 1].respawnFeet;
            Assert.AreEqual(last.x, GameObject.Find("Letter").transform.position.x, 0.01f);
        }

        [UnityTest]
        public IEnumerator DyingCostsTheAsterisks([Values(false, true)] bool pastACheckpoint)
        {
            yield return Load("Crack");
            letter.GetComponent<LetterShooter>().Arm();
            if (pastACheckpoint)
            {
                Checkpoint checkpoint = UnityEngine.Object.FindAnyObjectByType<PrototypeFlow>().checkpoints[0];
                letter.Respawn(checkpoint.respawnFeet);
                yield return WaitUntil(() => checkpoint.IsReached);
            }

            LetterController before = letter;
            before.Burn();
            yield return WaitUntil(() => before == null);
            yield return null;

            Assert.IsFalse(GameObject.Find("Letter").GetComponent<LetterShooter>().IsArmed);
        }

        [UnityTest]
        public IEnumerator ABridgeHoldsLowercaseAndSnapsUnderUppercase()
        {
            yield return Load("Plank");
            WordBlock bridge = Find("Word --------");
            Bounds bounds = BoundsOf(bridge);
            letter.Respawn(new Vector2(bounds.center.x, bounds.max.y + 0.05f));
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(bridge.GetComponent<BoxCollider2D>().enabled, "a should be light enough to stand on it.");

            letter.SetInput(0f, false, true);
            yield return WaitUntil(() => !bridge.GetComponent<BoxCollider2D>().enabled);
        }

        [UnityTest]
        public IEnumerator ALiftCarriesTheLetterUpToItsLanding()
        {
            yield return Load("Ladder");
            Bounds start = BoundsOf(Find("Word the word says up"));
            Bounds landing = BoundsOf(Find("Word so it goes"));
            // Lifts have no walls, so a letter running off the edge flies through the column. Players stop in the
            // lift, so the letter drops straight into it here, then walks onto the landing once it is high enough.
            Bounds lift = UnityEngine.Object.FindObjectsByType<WordBlock>()
                .Where(w => w.kind == WordKind.Up && BoundsOf(w).min.x > start.max.x - 0.1f)
                .Select(BoundsOf)
                .OrderBy(b => b.min.x)
                .First();
            letter.Respawn(new Vector2(lift.center.x, start.max.y + 0.05f));
            letter.SetInput(0f, false, false);
            yield return WaitUntil(() => letter.transform.position.y > landing.max.y + 0.2f);
            letter.SetInput(1f, false, false);
            yield return WaitUntil(() => letter.IsGrounded && letter.Surface != null && letter.Surface.name == "Word so it goes");
            Assert.That(letter.transform.position.y, Is.EqualTo(landing.max.y).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator TheFinalBlankOnlyTakesUppercase()
        {
            yield return Load("Final");
            GoalBlank blank = UnityEngine.Object.FindAnyObjectByType<GoalBlank>();
            Bounds bounds = blank.GetComponent<BoxCollider2D>().bounds;
            Vector2 before = new Vector2(bounds.min.x - 1.2f, bounds.min.y + 0.05f);

            letter.Respawn(before);
            letter.SetInput(1f, false, false);
            yield return WaitUntil(() => letter.transform.position.x > bounds.max.x);
            Assert.IsFalse(letter.IsSlotted, "a should walk straight through the final blank.");

            letter.SetInput(0f, false, true);
            letter.Respawn(before);
            yield return new WaitForSeconds(0.3f);
            letter.SetInput(1f, false, true);
            yield return WaitUntil(() => letter.IsSlotted);
        }
    }
}
