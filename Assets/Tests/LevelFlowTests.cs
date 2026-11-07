using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LowercaseUppercase.Tests
{
    /// <summary>The menu, the order of the levels, and the transitions between them.</summary>
    public class LevelFlowTests
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

        static LetterController FindLetter() => GameObject.Find("Letter").GetComponent<LetterController>();
        static LevelTransition FindTransition() => UnityEngine.Object.FindAnyObjectByType<LevelTransition>();

        [UnityTest]
        public IEnumerator TheMenuIsTheFirstSceneAndPlayStartsTheFirstLevel()
        {
            yield return SceneManager.LoadSceneAsync(0);
            yield return null;
            MainMenu menu = UnityEngine.Object.FindAnyObjectByType<MainMenu>();
            Assert.IsNotNull(menu, "Scene 0 should be the menu.");

            menu.playButton.onClick.Invoke();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Overworld");
        }

        [UnityTest]
        public IEnumerator ReachingTheExitLoadsTheNextScene(
            [ValueSource(nameof(LevelOrder))] string[] levelAndNext)
        {
            yield return LoadWithoutIntro(levelAndNext[0]);
            FindLetter().SlotInto(FindLetter().transform.position);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == levelAndNext[1]);
            Assert.AreEqual(1f, Time.timeScale == 0f ? 1f : Time.timeScale, "Time should only stop for an intro.");
        }

        static readonly string[][] LevelOrder =
        {
            new[] { "Overworld", "Crack" },
            new[] { "Crack", "Wall" },
            new[] { "Wall", "Cellar" },
            new[] { "Cellar", "Skyline" },
            new[] { "Skyline", "Menu" }
        };

        [UnityTest]
        public IEnumerator TheIntroHoldsTheLevelStillThenShowsThePlayer()
        {
            LevelTransition.SkipNextIntro = false;
            yield return SceneManager.LoadSceneAsync("Cellar");
            yield return null;

            LevelTransition transition = FindTransition();
            LetterController letter = FindLetter();
            Assert.IsTrue(transition.IsPlaying);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(letter.GetComponent<LetterVisual>().Hidden);

            yield return WaitUntil(() => !transition.IsPlaying);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(letter.GetComponent<LetterVisual>().Hidden);
            yield return WaitUntil(() => letter.IsGrounded);
        }

        [UnityTest]
        public IEnumerator DyingRestartsTheLevelWithoutTheIntro()
        {
            yield return LoadWithoutIntro("Skyline");
            LetterController before = FindLetter();
            before.Burn();
            yield return WaitUntil(() => before == null);
            yield return null;

            Assert.IsFalse(FindTransition().IsPlaying);
            Assert.AreEqual(1f, Time.timeScale);
        }

        static readonly string[] AllLevels = { "Chapter1", "Playground", "Vocabulary", "Overworld", "Crack", "Wall", "Cellar", "Skyline" };

        static Checkpoint FindCheckpoint() => UnityEngine.Object.FindAnyObjectByType<Checkpoint>();

        static IEnumerator PassCheckpoint(LetterController letter, Checkpoint checkpoint)
        {
            letter.Respawn(checkpoint.respawnFeet);
            yield return WaitUntil(() => checkpoint.IsReached);
        }

        [UnityTest]
        public IEnumerator DyingBeforeTheCheckpointReturnsToTheStart([ValueSource(nameof(AllLevels))] string scene)
        {
            yield return LoadWithoutIntro(scene);
            LetterController before = FindLetter();
            float startX = before.transform.position.x;
            Assert.IsFalse(FindCheckpoint().IsReached);

            before.Burn();
            yield return WaitUntil(() => before == null);
            yield return null;

            Assert.AreEqual(startX, FindLetter().transform.position.x, 0.01f);
            Assert.IsFalse(FindCheckpoint().IsReached);
        }

        [UnityTest]
        public IEnumerator DyingPastTheCheckpointReturnsThere([ValueSource(nameof(AllLevels))] string scene)
        {
            yield return LoadWithoutIntro(scene);
            LetterController before = FindLetter();
            Checkpoint first = FindCheckpoint();
            Vector2 feet = first.respawnFeet;
            yield return PassCheckpoint(before, first);

            before.Burn();
            yield return WaitUntil(() => before == null);
            yield return null;

            LetterController letter = FindLetter();
            Assert.AreEqual(feet.x, letter.transform.position.x, 0.01f);
            Assert.IsTrue(FindCheckpoint().IsReached, "It should stay reached for the next death.");
            yield return WaitUntil(() => letter.IsGrounded);
            Assert.AreEqual(feet.y, letter.transform.position.y, 0.2f, "The letter should land on the checkpoint's word.");

            // A second death returns to the same place.
            letter.Burn();
            yield return WaitUntil(() => letter == null);
            yield return null;
            Assert.AreEqual(feet.x, FindLetter().transform.position.x, 0.01f);
        }

        [UnityTest]
        public IEnumerator ReturningKeepsTheAsterisksHeldWhenPassingTheCheckpoint([Values(false, true)] bool armed)
        {
            yield return LoadWithoutIntro("Overworld");
            LetterController before = FindLetter();
            if (armed) before.GetComponent<LetterShooter>().Arm();
            yield return PassCheckpoint(before, FindCheckpoint());

            before.Burn();
            yield return WaitUntil(() => before == null);
            yield return null;

            Assert.AreEqual(armed, FindLetter().GetComponent<LetterShooter>().IsArmed);
        }

        [UnityTest]
        public IEnumerator ReloadingAnotherWayIgnoresTheCheckpoint()
        {
            yield return LoadWithoutIntro("Overworld");
            LetterController letter = FindLetter();
            float startX = letter.transform.position.x;
            yield return PassCheckpoint(letter, FindCheckpoint());

            yield return LoadWithoutIntro("Overworld");

            Assert.AreEqual(startX, FindLetter().transform.position.x, 0.01f);
            Assert.IsFalse(FindCheckpoint().IsReached);
        }

        [UnityTest]
        public IEnumerator DyingDropsTheLetterThroughTheLevelBelowTheScreen()
        {
            yield return LoadWithoutIntro("Skyline");
            LetterController letter = FindLetter();
            yield return WaitUntil(() => letter.IsGrounded);

            Camera view = Camera.main;
            float screenBottom = view.transform.position.y - view.orthographicSize;
            letter.Burn();
            Assert.IsTrue(letter.IsFallingOff);

            Transform scaler = letter.GetComponent<LetterVisual>().scaler;
            float lowest = letter.transform.position.y;
            float tilt = 0f;
            float start = Time.realtimeSinceStartup;
            while (letter != null)
            {
                if (Time.realtimeSinceStartup - start > Timeout) Assert.Fail("The scene never restarted.");
                lowest = Mathf.Min(lowest, letter.transform.position.y);
                tilt = Mathf.Max(tilt, Quaternion.Angle(Quaternion.identity, scaler.localRotation));
                yield return null;
            }

            Assert.That(lowest, Is.LessThan(screenBottom), "The letter should leave the screen before the restart.");
            Assert.That(tilt, Is.GreaterThan(0f), "The letter should rotate while it falls.");
        }
    }
}
