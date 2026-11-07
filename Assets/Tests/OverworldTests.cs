using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LowercaseUppercase.Tests
{
    /// <summary>Plays the power blocks and the exit of the Overworld scene.</summary>
    public class OverworldTests
    {
        const int Overworld = 3;
        const float Timeout = 5f;

        LetterController letter;
        LetterShooter shooter;

        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(Overworld);
            yield return null;
            var go = GameObject.Find("Letter");
            go.GetComponent<LetterInput>().enabled = false;
            letter = go.GetComponent<LetterController>();
            shooter = go.GetComponent<LetterShooter>();
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

        static WordBlock FirstPowerBlock()
        {
            WordBlock first = null;
            foreach (WordBlock block in UnityEngine.Object.FindObjectsByType<WordBlock>())
                if (block.kind == WordKind.Power && (first == null || block.WorldCentre.x < first.WorldCentre.x))
                    first = block;
            Assert.IsNotNull(first, "The level should have a power block.");
            return first;
        }

        IEnumerator StandAt(float x, float feetY)
        {
            letter.Respawn(new Vector2(x, feetY));
            yield return WaitUntil(() => letter.IsGrounded);
        }

        [UnityTest]
        public IEnumerator TheLetterCannotShootBeforeHittingABlock()
        {
            Assert.IsFalse(shooter.IsArmed);
            shooter.Fire();
            yield return null;
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<Asterisk>().Length);
        }

        [UnityTest]
        public IEnumerator HittingABlockFromBelowGivesTheLetterItsShot()
        {
            WordBlock block = FirstPowerBlock();
            yield return StandAt(block.WorldCentre.x, letter.transform.position.y);

            letter.SetInput(0f, true, false);
            letter.PressJump();
            yield return WaitUntil(() => block.IsSpent);
            letter.SetInput(0f, false, false);

            Assert.IsTrue(shooter.IsArmed);
            yield return new WaitForSeconds(shooter.cooldown);
            shooter.Fire();
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<Asterisk>().Length);
        }

        [UnityTest]
        public IEnumerator LandingOnABlockDoesNotSpendIt()
        {
            WordBlock block = FirstPowerBlock();
            letter.Respawn(block.WorldCentre + Vector2.up * 1.5f);
            yield return WaitUntil(() => letter.IsGrounded);
            Assert.AreEqual(block, letter.Surface);
            Assert.IsFalse(block.IsSpent);
            Assert.IsFalse(shooter.IsArmed);
        }

        [UnityTest]
        public IEnumerator WalkingIntoTheMissingLetterOfTheLastWordSlotsIn()
        {
            Bounds blank = UnityEngine.Object.FindAnyObjectByType<GoalBlank>().GetComponent<BoxCollider2D>().bounds;
            yield return StandAt(blank.min.x - 1.5f, blank.min.y + 0.1f);

            letter.SetInput(1f, false, false);
            yield return WaitUntil(() => letter.IsSlotted);
        }
    }
}
