using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LowercaseUppercase.Tests
{
    /// <summary>The popup Esc opens in a level: it stops time, keeps the letter's controls away, and can resume or leave.</summary>
    public class PauseMenuTests
    {
        PauseMenu pause;
        LetterController letter;

        IEnumerator Load(string scene)
        {
            LevelTransition.SkipNextIntro = true;
            yield return SceneManager.LoadSceneAsync(scene);
            yield return null;
            pause = Object.FindAnyObjectByType<PrototypeFlow>().pauseMenu;
            letter = GameObject.Find("Letter").GetComponent<LetterController>();
        }

        [SetUp]
        public void StartANewGame() => LevelRun.NewGame();

        [TearDown]
        public void RestoreTime() => Time.timeScale = 1f;

        [UnityTest]
        public IEnumerator EveryLevelHasAClosedPopup(
            [Values("Overworld", "Crack", "Wall", "Plank", "Gate", "Cellar", "Space", "Skate",
                "Trail", "Chain", "Ladder", "Skyline", "Flames", "Tar", "Final")] string scene)
        {
            yield return Load(scene);
            Assert.IsNotNull(pause);
            Assert.IsFalse(pause.IsOpen);
            Assert.IsFalse(pause.popup.activeSelf);
        }

        [UnityTest]
        public IEnumerator OpeningStopsTimeAndTheControls()
        {
            yield return Load("Crack");
            float clock = LevelRun.TimeLeft;
            pause.Open();
            yield return null;
            yield return null;

            Assert.IsTrue(pause.popup.activeSelf);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsFalse(letter.GetComponent<LetterInput>().enabled);
            Assert.AreEqual(clock, LevelRun.TimeLeft, "The clock should wait while paused.");
            Assert.AreEqual(0, pause.Selected, "Resume should be chosen first.");
        }

        [UnityTest]
        public IEnumerator ResumingCarriesOn()
        {
            yield return Load("Crack");
            pause.Open();
            yield return null;
            pause.Choose(0);

            Assert.IsFalse(pause.IsOpen);
            Assert.IsFalse(pause.popup.activeSelf);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsTrue(letter.GetComponent<LetterInput>().enabled);
            Assert.AreEqual("Crack", SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator MainMenuLeavesTheLevel()
        {
            yield return Load("Crack");
            pause.Open();
            yield return null;
            pause.Choose(1);
            yield return null;
            yield return null;

            Assert.AreEqual("Menu", SceneManager.GetActiveScene().name);
            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
