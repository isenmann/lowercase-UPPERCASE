using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LowercaseUppercase.Tests
{
    /// <summary>Checks what the Crack and Wall scenes promise: low gaps stop A, brick stops a, and each has a block and an exit.</summary>
    public class CrackAndWallTests
    {
        const string Crack = "Crack";
        const string Wall = "Wall";
        const float Timeout = 5f;

        LetterController letter;

        IEnumerator Load(string scene)
        {
            LevelTransition.SkipNextIntro = true;
            yield return SceneManager.LoadSceneAsync(scene);
            yield return null;

            // A patrolling enemy would reach a waiting letter and restart the level.
            foreach (Enemy enemy in UnityEngine.Object.FindObjectsByType<Enemy>())
                UnityEngine.Object.Destroy(enemy.gameObject);

            var go = GameObject.Find("Letter");
            go.GetComponent<LetterInput>().enabled = false;
            letter = go.GetComponent<LetterController>();
            letter.SetInput(0f, false, false);
            yield return WaitUntil(() => letter.IsGrounded);
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

        [UnityTest]
        public IEnumerator TheLetterStartsUnarmedWithABlockToHit([Values(Crack, Wall)] string scene)
        {
            yield return Load(scene);
            Assert.IsFalse(letter.GetComponent<LetterShooter>().IsArmed);

            bool found = false;
            foreach (WordBlock block in UnityEngine.Object.FindObjectsByType<WordBlock>())
                found |= block.kind == WordKind.Power;
            Assert.IsTrue(found, "The level should have a power block.");
        }

        [UnityTest]
        public IEnumerator WalkingIntoTheBlankSlotsIn([Values(Crack, Wall)] string scene)
        {
            yield return Load(scene);
            Bounds blank = UnityEngine.Object.FindAnyObjectByType<GoalBlank>().GetComponent<BoxCollider2D>().bounds;
            letter.Respawn(new Vector2(blank.min.x - 1f, blank.min.y + 0.05f));
            letter.SetInput(1f, false, false);
            yield return WaitUntil(() => letter.IsSlotted);
        }

        [UnityTest]
        public IEnumerator UppercaseCannotEnterALowGap([Values(Crack, Wall)] string scene)
        {
            yield return Load(scene);
            Bounds tunnel = LeftmostLowGap();
            float groundY = tunnel.min.y - 55f / 72f;

            letter.SetInput(1f, false, true);
            letter.Respawn(new Vector2(tunnel.min.x - 1f, groundY + 0.05f));
            yield return new WaitForSeconds(1.5f);

            Assert.AreEqual(LetterForm.Uppercase, letter.Form);
            Assert.That(letter.transform.position.x, Is.LessThan(tunnel.min.x));
        }

        [UnityTest]
        public IEnumerator LowercaseStopsAtBrickAndUppercaseBreaksIt([Values(Crack, Wall)] string scene)
        {
            yield return Load(scene);
            WordBlock brick = LeftmostBrick();
            Bounds wall = brick.GetComponent<BoxCollider2D>().bounds;
            float feetY = wall.min.y + 0.05f;

            letter.SetInput(1f, false, false);
            letter.Respawn(new Vector2(wall.min.x - 1f, feetY));
            yield return new WaitForSeconds(0.6f);
            Assert.That(letter.transform.position.x, Is.LessThan(wall.min.x));

            letter.SetInput(1f, false, true);
            yield return WaitUntil(() => letter.transform.position.x > wall.max.x);
        }

        static Bounds LeftmostLowGap()
        {
            Bounds? found = null;
            foreach (WordBlock block in UnityEngine.Object.FindObjectsByType<WordBlock>())
            {
                if (!block.name.StartsWith("Word lowlow")) continue;
                Bounds bounds = block.GetComponent<BoxCollider2D>().bounds;
                if (found == null || bounds.min.x < found.Value.min.x) found = bounds;
            }
            Assert.IsNotNull(found, "The level should have a low gap.");
            return found.Value;
        }

        static WordBlock LeftmostBrick()
        {
            WordBlock found = null;
            foreach (WordBlock block in UnityEngine.Object.FindObjectsByType<WordBlock>())
                if (block.kind == WordKind.Cracked && (found == null || block.transform.position.x < found.transform.position.x))
                    found = block;
            Assert.IsNotNull(found, "The level should have a brick wall.");
            return found;
        }
    }
}
