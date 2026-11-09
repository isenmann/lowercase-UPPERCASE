using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LowercaseUppercase.Tests
{
    /// <summary>Fires asterisks in the real scenes.</summary>
    public class ShootingTests
    {
        const int Chapter1 = 1;
        const int Playground = 2;
        const float Timeout = 5f;

        LetterController letter;
        LetterShooter shooter;

        IEnumerator Load(int buildIndex)
        {
            yield return SceneManager.LoadSceneAsync(buildIndex);
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

        static Asterisk[] Shots() => UnityEngine.Object.FindObjectsByType<Asterisk>();

        Asterisk FireOne()
        {
            int before = Shots().Length;
            shooter.Fire();
            Assert.AreEqual(before + 1, Shots().Length, "Firing should create one asterisk.");
            Asterisk[] shots = Shots();
            foreach (Asterisk shot in shots)
                if (shot.transform.position == (Vector3)letter.BodyCentre) return shot;
            return shots[shots.Length - 1];
        }

        [UnityTest]
        public IEnumerator UppercaseAsterisksAreBiggerThanLowercase()
        {
            yield return Load(Playground);
            float lower = FireOne().Radius;

            letter.SetInput(0f, false, true);
            yield return new WaitForSeconds(shooter.cooldown + 0.05f);
            Assert.AreEqual(LetterForm.Uppercase, letter.Form);
            float upper = FireOne().Radius;

            Debug.Log($"Asterisk radius: a {lower:0.000}, A {upper:0.000}");
            Assert.That(lower, Is.GreaterThan(0.05f), "The glyph should have visible ink.");
            Assert.That(upper, Is.GreaterThan(lower * 2f));
        }

        [UnityTest]
        public IEnumerator AsterisksFlyTheWayTheLetterLastMoved()
        {
            yield return Load(Playground);

            letter.SetInput(1f, false, false);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            letter.SetInput(-1f, false, false);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            letter.SetInput(0f, false, false);
            Assert.AreEqual(-1f, letter.Facing);

            Asterisk shot = FireOne();
            float startX = shot.transform.position.x;
            float startY = shot.transform.position.y;
            yield return new WaitForSeconds(0.1f);
            Assert.That(shot.transform.position.x, Is.LessThan(startX));
            Assert.AreEqual(startY, shot.transform.position.y, 0.001f);
        }

        [UnityTest]
        public IEnumerator ASolidWordStopsAnAsterisk()
        {
            yield return Load(Chapter1);
            var brick = GameObject.Find("Word brick");
            Assert.IsNotNull(brick, "Chapter 1 should have the brick word.");
            float brickRight = brick.GetComponent<Collider2D>().bounds.max.x;

            Asterisk shot = FireOne();
            yield return WaitUntil(() => shot.HasHit);
            float stoppedAt = shot.transform.position.x;

            Assert.That(stoppedAt, Is.LessThan(brickRight), "It should not pass through the word.");
            yield return WaitUntil(() => shot == null);
        }

        [UnityTest]
        public IEnumerator AnAsteriskWithNothingInItsWayPopsAtTheEndOfItsRange()
        {
            yield return Load(Playground);
            shooter.range = 1f;
            Asterisk shot = FireOne();
            float startX = shot.transform.position.x;
            yield return WaitUntil(() => shot.HasHit);
            Assert.AreEqual(startX + 1f, shot.transform.position.x, 0.01f);
            yield return WaitUntil(() => shot == null);
        }

        [UnityTest]
        public IEnumerator TheCooldownLimitsTheFireRate()
        {
            yield return Load(Playground);
            shooter.Fire();
            int afterFirst = Shots().Length;
            shooter.Fire();
            Assert.AreEqual(afterFirst, Shots().Length, "A second shot inside the cooldown should do nothing.");
            yield return new WaitForSeconds(shooter.cooldown + 0.05f);
            int beforeThird = Shots().Length;
            shooter.Fire();
            Assert.AreEqual(beforeThird + 1, Shots().Length);
        }

        [UnityTest]
        public IEnumerator TheCtrlKeyFires()
        {
            yield return Load(Playground);
            letter.GetComponent<LetterInput>().enabled = true;
            var keyboard = InputSystem.AddDevice<Keyboard>();

            try
            {
                // Left to the regular update, so the press is seen as this frame's press, as it is in play.
                // F only shoots in the Web build, where Ctrl+W would close the browser tab.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
                yield return null;
                yield return null;
                Assert.That(Shots().Length, Is.GreaterThan(0), "Ctrl should fire.");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }
    }
}
