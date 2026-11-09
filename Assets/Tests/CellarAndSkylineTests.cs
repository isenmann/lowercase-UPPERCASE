using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LowercaseUppercase.Tests
{
    /// <summary>Checks what the Cellar and Skyline scenes promise: no asterisks at the start, a block to get them, and an exit.</summary>
    public class CellarAndSkylineTests
    {
        const int Cellar = 5;
        const int Skyline = 6;
        const float Timeout = 5f;

        LetterController letter;

        IEnumerator Load(int buildIndex)
        {
            LevelTransition.SkipNextIntro = true;
            yield return SceneManager.LoadSceneAsync(buildIndex);
            yield return null;
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
        public IEnumerator TheLetterStartsUnarmedWithABlockToHit([Values(Cellar, Skyline)] int buildIndex)
        {
            yield return Load(buildIndex);
            Assert.IsFalse(letter.GetComponent<LetterShooter>().IsArmed);

            bool found = false;
            foreach (WordBlock block in UnityEngine.Object.FindObjectsByType<WordBlock>())
                found |= block.kind == WordKind.Power;
            Assert.IsTrue(found, "The level should have a power block.");
        }

        [UnityTest]
        public IEnumerator WalkingIntoTheBlankSlotsIn([Values(Cellar, Skyline)] int buildIndex)
        {
            yield return Load(buildIndex);
            Bounds blank = UnityEngine.Object.FindAnyObjectByType<GoalBlank>().GetComponent<BoxCollider2D>().bounds;
            letter.Respawn(new Vector2(blank.min.x - 1f, blank.min.y + 0.05f));
            letter.SetInput(1f, false, false);
            yield return WaitUntil(() => letter.IsSlotted);
        }

        [UnityTest]
        public IEnumerator UppercaseCannotEnterTheCellarTunnel()
        {
            yield return Load(Cellar);

            // The one patrolling this stretch would reach the waiting letter and restart the level.
            foreach (Enemy enemy in UnityEngine.Object.FindObjectsByType<Enemy>())
                UnityEngine.Object.Destroy(enemy.gameObject);
            // Launchers aren't enemies themselves, but they keep firing new ones.
            foreach (EnemyLauncher launcher in UnityEngine.Object.FindObjectsByType<EnemyLauncher>())
                UnityEngine.Object.Destroy(launcher);

            var tunnel = GameObject.Find("Word lowlowlowlow").GetComponent<BoxCollider2D>().bounds;
            float groundY = tunnel.min.y - 55f / 72f;

            letter.SetInput(1f, false, true);
            letter.Respawn(new Vector2(tunnel.min.x - 1f, groundY + 0.05f));
            yield return new WaitForSeconds(1.5f);

            Assert.AreEqual(LetterForm.Uppercase, letter.Form);
            Assert.That(letter.transform.position.x, Is.LessThan(tunnel.min.x));
        }
    }
}
