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
    /// <summary>Plays the real scenes with scripted input instead of a keyboard.</summary>
    public class MovementTests
    {
        const int Chapter1 = 0;
        const int Playground = 1;
        const float Timeout = 12f;

        LetterController letter;

        IEnumerator Load(int buildIndex)
        {
            yield return SceneManager.LoadSceneAsync(buildIndex);
            yield return null;
            var go = GameObject.Find("Letter");
            go.GetComponent<LetterInput>().enabled = false;
            letter = go.GetComponent<LetterController>();
            letter.SetInput(0f, false, false);
            yield return WaitUntil(() => letter.IsGrounded);
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout = Timeout)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - start > timeout) Assert.Fail("Timed out waiting for a condition.");
                yield return new WaitForFixedUpdate();
            }
        }

        static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        float X => letter.transform.position.x;
        float FeetY => letter.transform.position.y;

        static float PxX(float px) => (px - 640f) / 72f;

        IEnumerator MeasureJumpPeak(Action<float> report)
        {
            float start = FeetY;
            float peak = start;
            letter.SetInput(0f, true, letter.Form == LetterForm.Uppercase);
            letter.PressJump();
            yield return Steps(2);
            while (!letter.IsGrounded)
            {
                peak = Mathf.Max(peak, FeetY);
                yield return new WaitForFixedUpdate();
            }
            report(peak - start);
        }

        [UnityTest]
        public IEnumerator LowercaseJumpsMuchHigherThanUppercase()
        {
            yield return Load(Playground);

            float lowerPeak = 0f;
            yield return MeasureJumpPeak(h => lowerPeak = h);

            letter.SetInput(0f, false, true);
            yield return Steps(5);
            Assert.AreEqual(LetterForm.Uppercase, letter.Form);

            float upperPeak = 0f;
            yield return MeasureJumpPeak(h => upperPeak = h);

            Debug.Log($"Jump peaks: a {lowerPeak:0.00}, A {upperPeak:0.00}");
            Assert.That(lowerPeak, Is.InRange(2.2f, 3.1f));
            Assert.That(upperPeak, Is.InRange(0.5f, 0.75f));
        }

        [UnityTest]
        public IEnumerator ReleasingJumpEarlyGivesALowerJump()
        {
            yield return Load(Playground);
            float start = FeetY;
            float peak = start;
            letter.SetInput(0f, true, false);
            letter.PressJump();
            yield return Steps(4);
            letter.SetInput(0f, false, false);
            while (!letter.IsGrounded)
            {
                peak = Mathf.Max(peak, FeetY);
                yield return new WaitForFixedUpdate();
            }
            Debug.Log($"Short hop peak: {peak - start:0.00}");
            Assert.That(peak - start, Is.LessThan(1.5f));
        }

        [UnityTest]
        public IEnumerator UnderALowCeilingShiftDoesNothingUntilThereIsRoom()
        {
            yield return Load(Playground);

            letter.transform.position = new Vector3(PxX(510f), FeetY, 0f);
            letter.SetInput(1f, false, false);
            yield return WaitUntil(() => X > PxX(640f));
            letter.SetInput(0f, false, true);
            yield return Steps(20);
            Assert.AreEqual(LetterForm.Lowercase, letter.Form, "Should not grow under the ceiling word.");

            // Shift stays held: walking out from under the word grows the letter on its own.
            letter.SetInput(1f, false, true);
            yield return WaitUntil(() => letter.Form == LetterForm.Uppercase);
            Assert.That(X, Is.GreaterThan(PxX(746f)), "Should only grow once clear of the ceiling word.");
        }

        [UnityTest]
        public IEnumerator GrowingBesideAWallNudgesAwayInsteadOfBeingBlocked()
        {
            yield return Load(Chapter1);
            GameObject brick = GameObject.Find("Word brick");
            Bounds wall = brick.GetComponent<Collider2D>().bounds;

            // Stand directly against the wall as a, on the word "on".
            letter.transform.position = new Vector3(wall.min.x - 0.25f, wall.min.y + 0.05f, 0f);
            letter.SetInput(1f, false, false);
            yield return Steps(25);
            letter.SetInput(0f, false, true);
            yield return Steps(5);

            Assert.AreEqual(LetterForm.Uppercase, letter.Form);
        }

        [UnityTest]
        public IEnumerator OnlyUppercaseSmashesTheCrackedWord()
        {
            yield return Load(Chapter1);
            var brick = GameObject.Find("Word brick").GetComponent<Collider2D>();
            Bounds wall = brick.bounds;

            letter.transform.position = new Vector3(wall.min.x - 0.6f, wall.min.y + 0.05f, 0f);
            letter.SetInput(1f, false, false);
            yield return Steps(40);
            Assert.IsTrue(brick.enabled, "a must not break the word.");
            Assert.That(X, Is.LessThan(wall.min.x), "a must be stopped by the word.");

            letter.SetInput(1f, false, true);
            yield return WaitUntil(() => !brick.enabled, 3f);
        }

        [UnityTest]
        public IEnumerator LowercaseSpringGoesTwiceAsHighAsUppercase()
        {
            yield return Load(Playground);
            GameObject pad = GameObject.Find("Word !");
            Bounds bounds = pad.GetComponent<Collider2D>().bounds;
            float surface = bounds.max.y;

            float lowerPeak = 0f;
            yield return DropOnSpring(bounds, surface, false, h => lowerPeak = h);
            float upperPeak = 0f;
            yield return DropOnSpring(bounds, surface, true, h => upperPeak = h);

            Debug.Log($"Spring peaks: a {lowerPeak:0.00}, A {upperPeak:0.00}");
            Assert.That(lowerPeak / upperPeak, Is.InRange(1.7f, 2.3f));
        }

        IEnumerator DropOnSpring(Bounds pad, float surface, bool shift, Action<float> report)
        {
            letter.SetInput(0f, false, shift);
            letter.transform.position = new Vector3(pad.center.x, surface + 0.3f, 0f);
            yield return WaitUntil(() => letter.Velocity.y > 1f);

            float peak = FeetY;
            while (letter.Velocity.y > 0f)
            {
                peak = Mathf.Max(peak, FeetY);
                yield return new WaitForFixedUpdate();
            }
            report(peak - surface);
            yield return WaitUntil(() => letter.Velocity.y < -0.5f);
        }

        [UnityTest]
        public IEnumerator ReleasingShiftInAFallTurnsItIntoAForwardGlide()
        {
            yield return Load(Playground);

            Vector3 start = new Vector3(PxX(1000f), 3f, 0f);

            letter.transform.position = start;
            letter.SetInput(0f, false, false);
            yield return Steps(12);
            float plainFallSpeedX = Mathf.Abs(letter.Velocity.x);

            letter.transform.position = start;
            letter.SetInput(1f, false, true);
            yield return Steps(12);
            float fallSpeed = -letter.Velocity.y;
            float speedBefore = letter.Velocity.x;
            letter.SetInput(1f, false, false);
            yield return Steps(1);

            Debug.Log($"Glide: fall {fallSpeed:0.0}, speed {speedBefore:0.0} -> {letter.Velocity.x:0.0}");
            Assert.That(letter.Velocity.x, Is.GreaterThan(speedBefore + 2f));
            Assert.That(plainFallSpeedX, Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator Chapter1CanBeFinishedLikeTheConceptDescribes()
        {
            yield return Load(Chapter1);
            var brick = GameObject.Find("Word brick").GetComponent<Collider2D>();
            float blankX = GameObject.Find("Goal Blank").transform.position.x;

            // 1. From "the", hop up to "sat".
            letter.SetInput(1f, true, false);
            yield return WaitUntil(() => X > PxX(165f));
            letter.PressJump();
            yield return Steps(2);
            float satCentre = PxX(356f);
            yield return WaitUntil(() =>
            {
                float offset = satCentre - X;
                letter.SetInput(Mathf.Abs(offset) < 0.3f ? 0f : Mathf.Sign(offset), true, false);
                return letter.IsGrounded;
            });
            Assert.That(X, Is.InRange(PxX(285f), PxX(430f)), "Should have landed on sat.");

            // 2. Run off "sat" and drop onto "ice".
            letter.SetInput(1f, false, false);
            yield return WaitUntil(() => X > PxX(430f));
            yield return Steps(2);
            yield return WaitUntil(() => letter.IsGrounded);
            Assert.That(X, Is.InRange(PxX(500f), PxX(700f)), "Should have landed on ice or on.");

            // 3. Hold Shift and smash the cracked word.
            letter.SetInput(1f, false, true);
            yield return WaitUntil(() => !brick.enabled);
            Assert.AreEqual(LetterForm.Uppercase, letter.Form);

            // 4. Release Shift, run across the words onto the spring.
            letter.SetInput(1f, false, false);
            yield return WaitUntil(() => letter.Velocity.y > 1f);

            // 5. Steer up into the blank.
            yield return WaitUntil(() =>
            {
                float offset = blankX - X;
                letter.SetInput(Mathf.Abs(offset) < 0.05f ? 0f : Mathf.Sign(offset) * 0.6f, false, false);
                return letter.IsSlotted;
            });

            Assert.IsTrue(letter.IsSlotted);
        }

        [TestCase(1f / 60f)]
        [TestCase(0.1f)]
        [TestCase(0.5f)]
        public void SquashSpringSettlesAtAnyFrameRate(float frameTime)
        {
            Vector2 scale = new Vector2(1.5f, 0.45f);
            Vector2 velocity = Vector2.zero;

            for (int i = 0; i < 300; i++)
                LetterVisual.StepSpring(ref scale, ref velocity, frameTime, 260f, 15f);

            Assert.That((scale - Vector2.one).magnitude, Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator KeyboardBindingsDriveTheLetter()
        {
            yield return Load(Playground);
            var input = letter.GetComponent<LetterInput>();
            input.enabled = true;
            var keyboard = InputSystem.AddDevice<Keyboard>();

            try
            {
                float startX = X;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
                InputSystem.Update();
                yield return Steps(8);
                Assert.That(X, Is.GreaterThan(startX), "Right arrow should move the letter.");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                InputSystem.Update();
                yield return Steps(3);
                Assert.AreEqual(LetterForm.Uppercase, letter.Form, "Holding Shift should grow the letter.");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                yield return Steps(3);
                Assert.AreEqual(LetterForm.Lowercase, letter.Form, "Releasing Shift should shrink the letter.");

                float groundY = FeetY;
                // Left to the regular update, so the press is seen as this frame's press, as it is in play.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return null;
                yield return Steps(10);
                Assert.That(FeetY, Is.GreaterThan(groundY + 0.5f), "Space should jump.");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        public IEnumerator FallingOutOfTheWorldRestartsTheSentence()
        {
            yield return Load(Chapter1);
            Vector3 start = letter.transform.position;
            letter.transform.position = new Vector3(PxX(240f), -3f, 0f);

            yield return new WaitForSeconds(1.5f);

            var fresh = GameObject.Find("Letter");
            Assert.That(fresh.transform.position.y, Is.GreaterThan(start.y - 0.5f));
        }
    }
}
