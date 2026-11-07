using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LowercaseUppercase.Tests
{
    /// <summary>Meets the enemy in the Playground scene, with the letter placed by hand.</summary>
    public class EnemyTests
    {
        const int Playground = 1;
        const float Timeout = 8f;

        LetterController letter;
        Enemy enemy;

        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(Playground);
            yield return null;
            var go = GameObject.Find("Letter");
            go.GetComponent<LetterInput>().enabled = false;
            letter = go.GetComponent<LetterController>();
            letter.SetInput(0f, false, false);
            enemy = UnityEngine.Object.FindAnyObjectByType<Enemy>();
            Assert.IsNotNull(enemy, "The Playground should have an enemy.");
            yield return WaitUntil(() => letter.IsGrounded);
        }

        static IEnumerator WaitUntil(Func<bool> condition)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - start > Timeout) Assert.Fail("Timed out waiting for a condition.");
                yield return new WaitForFixedUpdate();
            }
        }

        static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        void PlaceLetterAt(Vector2 feet)
        {
            letter.Respawn(feet);
            letter.transform.position = feet;
        }

        IEnumerator GrowInPlace()
        {
            letter.SetInput(0f, false, true);
            yield return Steps(3);
            Assert.AreEqual(LetterForm.Uppercase, letter.Form);
        }

        [UnityTest]
        public IEnumerator AnAsteriskKillsTheEnemy()
        {
            yield return Load();
            enemy.speed = 0f;
            Vector2 target = enemy.transform.position;
            PlaceLetterAt(target + new Vector2(-2f, 0f));
            yield return WaitUntil(() => letter.IsGrounded);

            letter.SetInput(1f, false, false);
            yield return Steps(2);
            letter.SetInput(0f, false, false);
            letter.GetComponent<LetterShooter>().Fire();

            yield return WaitUntil(() => enemy == null);
            Assert.IsTrue(letter != null, "The letter should survive shooting.");
        }

        [UnityTest]
        public IEnumerator LandingOnItAsUppercaseKillsItAndBouncesTheLetter()
        {
            yield return Load();
            enemy.speed = 0f;
            yield return GrowInPlace();

            PlaceLetterAt((Vector2)enemy.transform.position + Vector2.up * 2f);
            yield return WaitUntil(() => enemy == null);

            Assert.IsTrue(letter != null, "The letter should survive the stomp.");
            Assert.That(letter.Velocity.y, Is.GreaterThan(0f), "The stomp should bounce the letter upward.");
        }

        [UnityTest]
        public IEnumerator LandingOnItAsLowercaseCostsALife()
        {
            yield return Load();
            enemy.speed = 0f;
            PlaceLetterAt((Vector2)enemy.transform.position + Vector2.up * 2f);
            LetterController before = letter;
            yield return WaitUntil(() => before == null);
        }

        [UnityTest]
        public IEnumerator WalkingIntoItAsUppercaseCostsALife()
        {
            yield return Load();
            enemy.speed = 0f;
            yield return GrowInPlace();

            PlaceLetterAt((Vector2)enemy.transform.position + Vector2.left * 1.5f);
            yield return WaitUntil(() => letter.IsGrounded);

            letter.SetInput(1f, false, true);
            LetterController before = letter;
            yield return WaitUntil(() => before == null);
        }

        [UnityTest]
        public IEnumerator ItTurnsAroundAtTheEndOfItsWord()
        {
            yield return Load();
            float startDirection = enemy.direction;
            yield return WaitUntil(() => enemy.direction != startDirection);
            Assert.That(enemy.transform.position.x, Is.InRange(-9f, -3.3f), "It should stay on its word.");
        }
    }
}
