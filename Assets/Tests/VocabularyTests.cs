using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LowercaseUppercase.Tests
{
    /// <summary>Tries every station of the Vocabulary level with scripted input, in both forms where it matters.</summary>
    public class VocabularyTests
    {
        const int Vocabulary = 2;
        const float Timeout = 12f;

        LetterController letter;

        float X => letter.transform.position.x;
        float FeetY => letter.transform.position.y;

        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(Vocabulary);
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

        static Bounds BoundsOf(string name)
        {
            var box = GameObject.Find(name).GetComponent<BoxCollider2D>();
            Vector3 centre = box.transform.TransformPoint(box.offset);
            return new Bounds(centre, box.size);
        }

        static bool IsSolid(string name) => GameObject.Find(name).GetComponent<BoxCollider2D>().enabled;

        IEnumerator StandOn(string name, float fromLeft = 0.5f)
        {
            Bounds word = BoundsOf(name);
            float x = Mathf.Lerp(word.min.x, word.max.x, fromLeft);
            letter.Respawn(new Vector2(x, word.max.y + 0.1f));
            yield return WaitUntil(() => letter.IsGrounded);
        }

        IEnumerator Become(LetterForm form)
        {
            letter.SetInput(0f, false, form == LetterForm.Uppercase);
            yield return Steps(2);
            Assert.AreEqual(form, letter.Form);
        }

        [UnityTest]
        public IEnumerator PeriodOnlyOpensTheDoorUnderUppercaseAndStaysOpenLongEnoughToRun()
        {
            yield return Load();
            yield return StandOn("Word period");
            yield return Steps(5);
            Assert.IsTrue(IsSolid("Word door"), "a is too light to press the period.");

            yield return Become(LetterForm.Uppercase);
            yield return Steps(3);
            Assert.IsFalse(IsSolid("Word door"), "A should open the door.");

            // Shrink and run from the plate through the door before it closes.
            float doorRight = BoundsOf("Word door").max.x;
            letter.SetInput(1f, false, false);
            yield return WaitUntil(() => X > doorRight + 0.5f, 2f);

            letter.SetInput(0f, false, false);
            yield return new WaitForSeconds(3f);
            Assert.IsTrue(IsSolid("Word door"), "The door should close again.");
        }

        [UnityTest]
        public IEnumerator HyphenBridgeCarriesLowercaseAndSnapsUnderUppercase()
        {
            yield return Load();
            const string bridge = "Word --------";

            yield return StandOn(bridge);
            yield return Steps(20);
            Assert.IsTrue(IsSolid(bridge), "a should not break the bridge.");
            Assert.IsTrue(letter.IsGrounded);

            yield return Become(LetterForm.Uppercase);
            yield return Steps(3);
            Assert.IsFalse(IsSolid(bridge), "A should snap the bridge.");

            yield return new WaitForSeconds(3.5f);
            Assert.IsTrue(IsSolid(bridge), "The bridge should return in this playground.");
        }

        [UnityTest]
        public IEnumerator CommaLetsLowercaseHangAndSwingAcrossTheWholeGap()
        {
            yield return Load();
            Bounds hook = BoundsOf("Word hook");
            Bounds first = BoundsOf("Hook 1");
            Bounds second = BoundsOf("Hook 2");
            Bounds landing = BoundsOf("Kerning Gap/Word g");

            yield return StandOn("Word hook", 0.2f);

            // Run and jump off the edge toward the first comma.
            letter.SetInput(1f, true, false);
            yield return WaitUntil(() => X > hook.max.x - 0.4f);
            letter.PressJump();
            yield return WaitUntil(() => letter.IsHanging);
            Assert.AreEqual(first.center.x, letter.HookAnchor.x, 0.01f);

            // Swing, then jump to the second comma near the front of the swing.
            yield return WaitUntil(() => X > first.center.x + 0.5f && letter.Velocity.x > 2f);
            letter.PressJump();
            yield return Steps(2);
            Assert.IsFalse(letter.IsHanging, "Jump should let go.");
            yield return WaitUntil(() => letter.IsHanging);
            Assert.AreEqual(second.center.x, letter.HookAnchor.x, 0.01f);

            yield return WaitUntil(() => X > second.center.x + 0.5f && letter.Velocity.x > 2f);
            letter.PressJump();
            yield return Steps(2);
            yield return WaitUntil(() => letter.IsGrounded);

            Debug.Log($"Comma crossing landed at x {X:0.00}, landing word starts at {landing.min.x:0.00}");
            Assert.That(X, Is.GreaterThan(landing.min.x - 0.2f), "Should have crossed the gap.");
        }

        [UnityTest]
        public IEnumerator CommaSnapsForUppercase()
        {
            yield return Load();
            Bounds first = BoundsOf("Hook 1");

            letter.Respawn(new Vector2(first.center.x, first.center.y + 0.1f));
            letter.SetInput(0f, false, false);
            yield return WaitUntil(() => letter.IsHanging);

            letter.SetInput(0f, false, true);
            yield return Steps(3);
            Assert.IsFalse(letter.IsHanging, "Shift should drop the letter.");
            Assert.IsFalse(IsSolid("Hook 1"), "The comma should have snapped.");
        }

        [UnityTest]
        public IEnumerator KerningGapDropsLowercaseButCarriesUppercase()
        {
            yield return Load();
            Bounds first = BoundsOf("Kerning Gap/Word g");
            float hole = first.max.x + 0.26f;

            letter.Respawn(new Vector2(hole, first.max.y + 0.3f));
            letter.SetInput(0f, false, true);
            yield return Steps(1);
            letter.SetInput(0f, false, true);
            yield return Steps(25);
            Assert.AreEqual(LetterForm.Uppercase, letter.Form);
            Assert.That(FeetY, Is.GreaterThan(first.max.y - 0.1f), "A is too wide for the hole.");

            letter.Respawn(new Vector2(hole, first.max.y + 0.3f));
            letter.SetInput(0f, false, false);
            yield return Steps(40);
            Assert.That(FeetY, Is.LessThan(first.min.y), "a should drop through the hole.");
            Assert.IsTrue(letter.IsGrounded, "a should land on the word below.");
        }

        [UnityTest]
        public IEnumerator LowerPathSpringLeadsBackUpToTheNextWord()
        {
            yield return Load();
            Bounds next = BoundsOf("Word ice ice");
            yield return StandOn("Word under gap", 0.97f);

            letter.SetInput(1f, false, false);
            yield return WaitUntil(() => letter.Velocity.y > 1f);
            yield return WaitUntil(() =>
            {
                letter.SetInput(X < next.min.x + 1f ? 1f : 0f, false, false);
                return letter.IsGrounded && letter.Velocity.y <= 0f;
            });

            Assert.AreEqual(next.max.y, FeetY, 0.15f, "Should land on the next word.");
        }

        [UnityTest]
        public IEnumerator HotBurnsWhoeverStaysAndLowercaseCanRunAcross()
        {
            yield return Load();
            Bounds hot = BoundsOf("Word hot");
            Bounds cool = BoundsOf("Word cool");

            // Running from the ice at full speed as a gets across unburned.
            yield return StandOn("Word cool", 0.1f);
            letter.Respawn(new Vector2(hot.min.x - 0.3f, hot.max.y + 0.05f));
            bool burned = false;
            letter.Burned += () => burned = true;
            letter.SetInput(1f, false, false);
            yield return WaitUntil(() => X > cool.min.x + 0.3f, 3f);
            Assert.IsFalse(burned, "A fast a should make it across.");

            // Standing still burns, and returns the letter to the last solid word.
            letter.Respawn(new Vector2(hot.center.x, hot.max.y + 0.05f));
            letter.SetInput(0f, false, false);
            yield return WaitUntil(() => burned || X > cool.min.x, 2f);
            yield return null;
            Assert.IsTrue(burned, "Standing on hot should burn.");
        }

        [UnityTest]
        public IEnumerator UpLiftsLowercaseHighEnoughForTheWordAbove()
        {
            yield return Load();
            Bounds lift = BoundsOf("Word up");
            Bounds above = BoundsOf("Word above");

            letter.Respawn(new Vector2(lift.center.x, lift.min.y + 0.5f));
            letter.SetInput(0f, false, false);
            yield return WaitUntil(() => FeetY > above.max.y + 0.2f, 3f);

            letter.SetInput(1f, false, false);
            yield return WaitUntil(() => letter.IsGrounded, 3f);
            Assert.AreEqual(above.max.y, FeetY, 0.15f, "Should land on the word above.");
        }

        [UnityTest]
        public IEnumerator UpLiftsUppercaseMoreSlowly()
        {
            yield return Load();
            Bounds lift = BoundsOf("Word up");

            letter.Respawn(new Vector2(lift.center.x, lift.min.y + 0.5f));
            letter.SetInput(0f, false, false);
            yield return Steps(25);
            float lowerSpeed = letter.Velocity.y;

            letter.Respawn(new Vector2(lift.center.x, lift.min.y + 0.5f));
            letter.SetInput(0f, false, true);
            yield return Steps(25);
            float upperSpeed = letter.Velocity.y;

            Debug.Log($"Lift speeds: a {lowerSpeed:0.0}, A {upperSpeed:0.0}");
            Assert.That(upperSpeed, Is.GreaterThan(0f));
            Assert.That(lowerSpeed, Is.GreaterThan(upperSpeed));
        }

        [UnityTest]
        public IEnumerator StickySlowsYouDownAndItsWallCanBeClimbed()
        {
            yield return Load();
            Bounds wall = BoundsOf("Word sticky wall");

            yield return StandOn("Word sticky", 0.1f);
            letter.SetInput(1f, false, false);
            yield return Steps(25);
            Assert.That(letter.Velocity.x, Is.LessThan(letter.lowercase.runSpeed * 0.5f), "Sticky should slow a down.");

            yield return WaitUntil(() => X > wall.min.x - 0.4f, 5f);

            // Keep pressing right and jump every time the letter is holding on.
            float start = Time.realtimeSinceStartup;
            while (FeetY < wall.max.y - 0.05f)
            {
                if (Time.realtimeSinceStartup - start > Timeout) Assert.Fail("Could not climb the wall.");
                letter.SetInput(1f, true, false);
                if (letter.IsClinging || letter.IsGrounded) letter.PressJump();
                yield return new WaitForFixedUpdate();
            }
        }

        [UnityTest]
        public IEnumerator CapsLockInvertsShiftUntilTheKeyIsFound()
        {
            yield return Load();
            yield return StandOn("Word caps lock");

            letter.SetInput(0f, false, false);
            yield return Steps(5);
            Assert.IsTrue(letter.IsCapsLocked);
            Assert.AreEqual(LetterForm.Uppercase, letter.Form, "Without Shift the zone makes you A.");

            letter.SetInput(0f, false, true);
            yield return Steps(3);
            Assert.AreEqual(LetterForm.Lowercase, letter.Form, "Holding Shift makes you a in the zone.");

            GameObject key = GameObject.Find("Key");
            letter.Respawn(key.transform.position);
            letter.SetInput(0f, false, false);
            yield return Steps(5);
            Assert.IsFalse(letter.IsCapsLocked, "The key should switch the zone off.");
            Assert.AreEqual(LetterForm.Lowercase, letter.Form);
        }

        [UnityTest]
        public IEnumerator CapsLockGapNeedsTheInvertedShift()
        {
            yield return Load();
            Bounds start = BoundsOf("Word caps lock");
            Bounds target = BoundsOf("Word zone");

            // As A, the short hop falls into the gap.
            yield return StandOn("Word caps lock", 0.6f);
            letter.SetInput(1f, true, false);
            yield return WaitUntil(() => X > start.max.x - 0.5f);
            letter.PressJump();
            yield return Steps(2);
            yield return WaitUntil(() => letter.IsGrounded || FeetY < start.min.y - 0.5f, 3f);
            Assert.That(FeetY, Is.LessThan(start.min.y), "A should not clear the gap.");

            // Holding Shift gives a, whose jump clears it.
            yield return StandOn("Word caps lock", 0.6f);
            letter.SetInput(1f, true, true);
            yield return WaitUntil(() => X > start.max.x - 0.5f);
            letter.PressJump();
            yield return Steps(2);
            yield return WaitUntil(() => letter.IsGrounded, 3f);
            Assert.That(X, Is.GreaterThan(target.min.x - 0.1f), "a should clear the gap.");
        }

        [UnityTest]
        public IEnumerator BlanksOnlyAcceptTheFormTheyWant()
        {
            yield return Load();
            GoalBlank[] blanks = UnityEngine.Object.FindObjectsByType<GoalBlank>(FindObjectsInactive.Exclude);
            Assert.AreEqual(2, blanks.Length);
            GoalBlank upperBlank = Array.Find(blanks, b => b.wantedForm == LetterForm.Uppercase);
            Bounds bounds = upperBlank.GetComponent<BoxCollider2D>().bounds;

            // Walk through as a: nothing happens.
            letter.Respawn(new Vector2(bounds.min.x - 1f, bounds.min.y + 0.1f));
            letter.SetInput(1f, false, false);
            yield return WaitUntil(() => X > bounds.max.x + 0.3f, 3f);
            Assert.IsFalse(letter.IsSlotted, "This blank wants A.");

            // Walk in as A: slots in.
            letter.Respawn(new Vector2(bounds.min.x - 1f, bounds.min.y + 0.1f));
            letter.SetInput(1f, false, true);
            yield return WaitUntil(() => letter.IsSlotted, 3f);
        }

        [UnityTest]
        public IEnumerator FallingRespawnsOnTheLastSolidWordWithoutReloading()
        {
            yield return Load();
            yield return Steps(5);
            Vector3 safe = letter.transform.position;
            LetterController same = letter;

            letter.Respawn(new Vector2(safe.x, -5.9f));
            letter.SetInput(0f, false, false);
            yield return new WaitForSeconds(0.5f);

            Assert.IsTrue(same != null, "The scene should not have reloaded.");
            Assert.AreEqual(safe.y, FeetY, 0.3f);
        }
    }
}
