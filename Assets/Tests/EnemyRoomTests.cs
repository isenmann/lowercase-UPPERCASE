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
    /// Meets the newer enemies in the Enemy Room. The room is not in the build settings,
    /// so it is loaded by path, which only works in the editor.
    /// </summary>
    public class EnemyRoomTests
    {
        const string EnemyRoom = "Assets/Scenes/EnemyRoom.unity";
        const float Timeout = 8f;

        LetterController letter;

        [SetUp]
        public void StartANewGame() => LevelRun.NewGame();

        IEnumerator Load()
        {
#if UNITY_EDITOR
            LevelTransition.SkipNextIntro = true;
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                EnemyRoom, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            letter = FindLetter();
            yield return WaitUntil(() => letter.IsGrounded);
#else
            Assert.Ignore("The Enemy Room can only be loaded in the editor.");
            yield break;
#endif
        }

        static LetterController FindLetter()
        {
            var letter = GameObject.Find("Letter").GetComponent<LetterController>();
            letter.GetComponent<LetterInput>().enabled = false;
            letter.SetInput(0f, false, false);
            return letter;
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

        static T[] All<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>();

        static EnemyLauncher Launcher() => UnityEngine.Object.FindAnyObjectByType<EnemyLauncher>();

        void StandBeforeTheLauncher(float distance)
        {
            Bounds launcher = Launcher().GetComponent<BoxCollider2D>().bounds;
            var feet = new Vector2(launcher.min.x - distance, launcher.min.y + 0.05f);
            letter.Respawn(feet);
            letter.transform.position = feet;
        }

        [UnityTest]
        public IEnumerator AHopperLeavesItsWordAndLandsBackOnIt()
        {
            yield return Load();
            EnemyHop hopper = All<EnemyHop>().First(h => h.sideSpeed == 0f);
            float groundY = hopper.transform.position.y;

            yield return WaitUntil(() => hopper.transform.position.y > groundY + 0.5f);
            yield return WaitUntil(() => !hopper.IsAirborne);
            Assert.AreEqual(groundY, hopper.transform.position.y, 0.01f, "It should land back on its word.");
        }

        [UnityTest]
        public IEnumerator AMovingHopperDriftsAlongItsWord()
        {
            yield return Load();
            EnemyHop hopper = All<EnemyHop>().First(h => h.sideSpeed > 0f);
            float startX = hopper.transform.position.x;
            yield return WaitUntil(() => Mathf.Abs(hopper.transform.position.x - startX) > 0.5f);
        }

        [UnityTest]
        public IEnumerator AFlierTravelsAndBobs()
        {
            yield return Load();
            EnemyWave flier = All<EnemyWave>().First(w => w.travel > 0f);
            Vector2 start = flier.transform.position;
            float lowest = start.y, highest = start.y, furthest = 0f;
            yield return WaitUntil(() =>
            {
                Vector2 now = flier.transform.position;
                lowest = Mathf.Min(lowest, now.y);
                highest = Mathf.Max(highest, now.y);
                furthest = Mathf.Max(furthest, Mathf.Abs(now.x - start.x));
                return furthest > 1f && highest - lowest > flier.amplitude;
            });
            Assert.That(furthest, Is.LessThanOrEqualTo(flier.travel + 0.1f), "It should turn at the end of its travel.");
        }

        [UnityTest]
        public IEnumerator TheLauncherIsQuietWhileOffScreen()
        {
            yield return Load();
            float start = Time.time;
            yield return WaitUntil(() => Time.time - start > 3f);
            Assert.IsEmpty(All<EnemyArrow>(), "No arrows should be fired while the launcher is out of sight.");
        }

        [UnityTest]
        public IEnumerator ArrowsFlyOverLowercase()
        {
            yield return Load();
            StandBeforeTheLauncher(3f);
            LetterController standing = letter;
            float letterX = standing.transform.position.x;

            yield return WaitUntil(() => All<EnemyArrow>().Any(a => a.transform.position.x < letterX - 1f));
            Assert.IsTrue(standing != null, "An arrow should pass over a lowercase letter.");
        }

        [UnityTest]
        public IEnumerator ArrowsHitUppercase()
        {
            yield return Load();
            letter.SetInput(0f, false, true);
            yield return WaitUntil(() => letter.Form == LetterForm.Uppercase);
            StandBeforeTheLauncher(3f);

            LetterController standing = letter;
            yield return WaitUntil(() => standing == null);
        }

        static T Leftmost<T>() where T : Component =>
            All<T>().OrderBy(c => c.transform.position.x).First();

        void PlaceLetterAt(Vector2 feet)
        {
            letter.Respawn(feet);
            letter.transform.position = feet;
        }

        [UnityTest]
        public IEnumerator ARiserComesUpAndGoesBackDown()
        {
            yield return Load();
            Enemy riser = Leftmost<EnemyRise>().GetComponent<Enemy>();
            Assert.IsFalse(riser.Box.enabled, "It should start hidden.");
            yield return WaitUntil(() => riser.Box.enabled);
            yield return WaitUntil(() => !riser.Box.enabled);
        }

        [UnityTest]
        public IEnumerator ARiserStaysDownWhileTheLetterStandsBesideItsHole()
        {
            yield return Load();
            EnemyRise riser = Leftmost<EnemyRise>();
            float holeX = riser.transform.position.x;
            WordBlock beside = UnityEngine.Object.FindObjectsByType<WordBlock>()
                .Where(w => w.transform.parent != null && w.transform.parent.name.StartsWith("Kerning"))
                .OrderBy(w => Mathf.Abs(w.GetComponent<BoxCollider2D>().bounds.center.x - holeX))
                .First();
            Bounds top = beside.GetComponent<BoxCollider2D>().bounds;
            PlaceLetterAt(new Vector2(top.center.x, top.max.y + 0.01f));

            float start = Time.time;
            yield return WaitUntil(() => Time.time - start > riser.downSeconds + riser.startDelay + 1f);
            Assert.IsFalse(riser.GetComponent<Enemy>().Box.enabled, "It shouldn't rise next to the letter.");
        }

        [UnityTest]
        public IEnumerator ADripFallsWhenTheLetterPassesUnderAndGrowsBack()
        {
            yield return Load();
            EnemyDrip drip = Leftmost<EnemyDrip>();
            Enemy body = drip.GetComponent<Enemy>();
            float hangY = drip.transform.position.y;
            // Beside it rather than under it, so the drip lands on the floor instead of the letter.
            PlaceLetterAt(new Vector2(drip.transform.position.x + drip.triggerHalfWidth * 0.8f, letter.transform.position.y));

            yield return WaitUntil(() => drip.transform.position.y < hangY - 0.5f);
            yield return WaitUntil(() => !body.Box.enabled);
            Assert.IsTrue(letter != null, "A drip that misses should splash on the floor.");
            yield return WaitUntil(() => body.Box.enabled);
            Assert.AreEqual(hangY, drip.transform.position.y, 0.01f, "It should grow back on its ceiling.");
        }

        [UnityTest]
        public IEnumerator SparksLeapAndCannotBeShot()
        {
            yield return Load();
            EnemySpark spark = Leftmost<EnemySpark>();
            Enemy body = spark.GetComponent<Enemy>();
            Assert.IsFalse(body.stompable || body.shootable, "Fire can't be stomped or shot.");
            float restY = spark.transform.position.y;
            yield return WaitUntil(() => spark.IsLeaping && spark.transform.position.y > restY + 1f);
            yield return WaitUntil(() => !spark.IsLeaping);
            Assert.IsFalse(body.Box.enabled, "It should hide in its hot word between leaps.");
        }

        [UnityTest]
        public IEnumerator TheFireBarTurns()
        {
            yield return Load();
            EnemyFireBar bar = UnityEngine.Object.FindAnyObjectByType<EnemyFireBar>();
            Vector2 tip = bar.dots[bar.dots.Length - 1].transform.position;
            yield return WaitUntil(() => Vector2.Distance(bar.dots[bar.dots.Length - 1].transform.position, tip) > 1f);
        }

        [UnityTest]
        public IEnumerator ShootingAnArrowPaysNothing()
        {
            yield return Load();
            StandBeforeTheLauncher(6f);
            yield return WaitUntil(() => All<EnemyArrow>().Length > 0);

            int score = LevelRun.Score;
            Enemy arrow = All<EnemyArrow>()[0].GetComponent<Enemy>();
            arrow.Kill();
            yield return null;
            Assert.AreEqual(score, LevelRun.Score);
        }
    }
}
