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

        // ---------------------------------------------------------------- slam

        static Bounds SlamFloor() => GameObject.Find("Word slam down on whatever walks below").GetComponent<BoxCollider2D>().bounds;

        static WordBlock SlamLid()
        {
            Bounds floor = SlamFloor();
            return All<WordBlock>().Single(b => b.kind == WordKind.Cracked
                && b.transform.position.x > floor.min.x && b.transform.position.x < floor.max.x);
        }

        static Enemy SlamPenEnemy(string glyph)
        {
            Bounds floor = SlamFloor();
            Enemy found = All<Enemy>().Single(e => e.glyph.text == glyph
                && e.transform.position.x > floor.min.x && e.transform.position.x < floor.max.x);
            found.GetComponent<EnemyWalk>().speed = 0f;
            return found;
        }

        [UnityTest]
        public IEnumerator TurningUppercaseInTheAirSlamsThroughTheLid()
        {
            yield return Load();
            Bounds lid = SlamLid().GetComponent<BoxCollider2D>().bounds;
            Collider2D lidCollider = SlamLid().GetComponent<Collider2D>();
            foreach (Enemy e in new[] { SlamPenEnemy("^"), SlamPenEnemy("\u00a7") })
                e.Body.position = new Vector2(SlamFloor().max.x - 1f, e.Body.position.y);

            bool started = false, landed = false;
            letter.SlamStarted += () => started = true;
            letter.Slammed += _ => landed = true;
            PlaceLetterAt(new Vector2(lid.center.x, lid.max.y + 2f));
            letter.SetInput(0f, false, true);

            yield return WaitUntil(() => landed);
            Assert.IsTrue(started, "Turning A in the air should start a slam.");
            Assert.IsFalse(lidCollider.enabled, "The slam should break the lid on its way down.");
            Assert.That(letter.transform.position.y, Is.LessThan(lid.min.y), "The letter should land below the lid.");
            Assert.IsFalse(letter.IsSlamming);
        }

        [UnityTest]
        public IEnumerator TheSlamKnocksOutASpikeBesideTheLandingButNotOneFarAway()
        {
            yield return Load();
            Enemy spike = SlamPenEnemy("^");
            Enemy armour = SlamPenEnemy("\u00a7");
            Vector2 spikeFeet = spike.transform.position;
            float landingX = SlamFloor().max.x - 1.5f;
            spike.Body.position = new Vector2(landingX - letter.shockRadius * 0.7f, spikeFeet.y);
            armour.Body.position = new Vector2(landingX - letter.shockRadius - 2f, spikeFeet.y);
            yield return new WaitForFixedUpdate();

            int knockedOut = -1;
            letter.Slammed += n => knockedOut = n;
            PlaceLetterAt(new Vector2(landingX, spikeFeet.y + 1.2f));
            letter.SetInput(0f, false, true);

            yield return WaitUntil(() => knockedOut >= 0);
            Assert.AreEqual(1, knockedOut);
            Assert.IsTrue(spike == null || !spike.Box.enabled, "The spike beside the landing should be knocked out.");
            Assert.IsTrue(armour != null && armour.Box.enabled, "The armour out of reach should be left alone.");
            Assert.IsTrue(letter != null && !letter.IsFallingOff, "The slam shouldn't hurt the letter.");
        }

        [UnityTest]
        public IEnumerator UppercaseFromACapsLockChangeDoesNotSlam()
        {
            yield return Load();
            Bounds floor = SlamFloor();
            Bounds power = All<WordBlock>().Single(b => b.kind == WordKind.Power
                && b.transform.position.x > floor.min.x && b.transform.position.x < floor.max.x).GetComponent<BoxCollider2D>().bounds;

            bool started = false, denied = false, grewInTheAir = false;
            letter.SlamStarted += () => started = true;
            letter.GrowDenied += () => denied = true;
            letter.FormChanged += form => grewInTheAir = form == LetterForm.Uppercase && !letter.IsGrounded;

            // Half under the power block: no room to grow until the letter has moved out, still falling.
            PlaceLetterAt(new Vector2(power.max.x - 0.05f, power.min.y - 0.45f));
            letter.SetInput(1f, false, false);
            letter.SwapCase();
            yield return WaitUntil(() => letter.Form == LetterForm.Uppercase);

            Assert.IsTrue(denied, "The power block should hold the growing back at first.");
            Assert.IsTrue(grewInTheAir, "The letter should grow in the air once it is clear.");
            Assert.IsFalse(started, "Only the player's own Shift should slam, even when growing has to wait.");
        }

        IEnumerator GrowInPlace()
        {
            letter.SetInput(0f, false, true);
            yield return WaitUntil(() => letter.Form == LetterForm.Uppercase);
        }

        [UnityTest]
        public IEnumerator StompingACurlTurnsItIntoAStillO()
        {
            yield return Load();
            EnemyCurl curl = UnityEngine.Object.FindAnyObjectByType<EnemyCurl>();
            curl.walkSpeed = 0f;
            yield return GrowInPlace();

            PlaceLetterAt((Vector2)curl.transform.position + Vector2.up * 1.2f);
            yield return WaitUntil(() => curl.IsCurled);
            Assert.AreEqual("o", curl.GetComponent<Enemy>().glyph.text);
            Assert.IsTrue(letter != null, "The stomp shouldn't hurt the letter.");
        }

        [UnityTest]
        public IEnumerator WalkingIntoAStillOKicksIt()
        {
            yield return Load();
            EnemyCurl curl = UnityEngine.Object.FindAnyObjectByType<EnemyCurl>();
            curl.walkSpeed = 0f;
            yield return GrowInPlace();
            PlaceLetterAt((Vector2)curl.transform.position + Vector2.up * 1.2f);
            yield return WaitUntil(() => curl.IsCurled);

            // Off to the side at once, before the bounce comes down on the o and kicks it.
            PlaceLetterAt((Vector2)curl.transform.position + Vector2.left * 2f);
            letter.SetInput(0f, false, false);
            yield return WaitUntil(() => letter.Form == LetterForm.Lowercase && letter.IsGrounded);
            Assert.IsFalse(curl.IsRolling, "The o should lie still until it is kicked.");

            letter.SetInput(1f, false, false);
            yield return WaitUntil(() => curl.IsRolling);
            Assert.IsTrue(letter != null, "Kicking a still o shouldn't hurt the letter.");
        }

        [UnityTest]
        public IEnumerator TouchingACaseSwapperTurnsShiftAround()
        {
            yield return Load();
            EnemyCaseSwap swapper = UnityEngine.Object.FindAnyObjectByType<EnemyCaseSwap>();
            swapper.GetComponent<EnemyWalk>().speed = 0f;
            PlaceLetterAt((Vector2)swapper.transform.position + Vector2.left * 1.5f);
            yield return WaitUntil(() => letter.IsGrounded);

            letter.SetInput(1f, false, false);
            yield return WaitUntil(() => letter.IsCapsLocked);
            letter.SetInput(0f, false, false);
            yield return WaitUntil(() => letter.Form == LetterForm.Uppercase);
            Assert.IsTrue(letter != null, "A case swapper shouldn't hurt.");
        }

        [UnityTest]
        public IEnumerator TheEyeWakesAndDriftsTowardTheLetter()
        {
            yield return Load();
            EnemyFollow eye = UnityEngine.Object.FindAnyObjectByType<EnemyFollow>();
            float startDistance = 6f;
            PlaceLetterAt(new Vector2(eye.transform.position.x - startDistance, letter.transform.position.y));
            yield return WaitUntil(() => eye.IsAwake);
            Vector2 woke = eye.transform.position;
            yield return WaitUntil(() => Vector2.Distance(eye.transform.position, woke) > 1f);
            Assert.That(eye.transform.position.x, Is.LessThan(woke.x), "It should drift toward the letter.");
        }

        [UnityTest]
        public IEnumerator TheMimicCopiesTheLettersCaseAMomentLater()
        {
            yield return Load();
            EnemyMimic mimic = UnityEngine.Object.FindAnyObjectByType<EnemyMimic>();
            Assert.IsFalse(mimic.IsUpper);

            letter.SetInput(0f, false, true);
            yield return WaitUntil(() => letter.Form == LetterForm.Uppercase);
            Assert.IsFalse(mimic.IsUpper, "It should take a moment to copy.");
            yield return WaitUntil(() => mimic.IsUpper);
            Assert.AreEqual("B", mimic.GetComponent<Enemy>().glyph.text);

            letter.SetInput(0f, false, false);
            yield return WaitUntil(() => !mimic.IsUpper);
        }

        [UnityTest]
        public IEnumerator AnUppercaseMimicCantBeStomped()
        {
            yield return Load();
            EnemyMimic mimic = UnityEngine.Object.FindAnyObjectByType<EnemyMimic>();
            mimic.GetComponent<EnemyWalk>().speed = 0f;
            yield return GrowInPlace();
            yield return WaitUntil(() => mimic.IsUpper);

            PlaceLetterAt((Vector2)mimic.transform.position + Vector2.up * 1.2f);
            LetterController dropping = letter;
            yield return WaitUntil(() => dropping == null);
        }

        [UnityTest]
        public IEnumerator TheCursorErasesTheWordsBehindTheLetterAndStops()
        {
            yield return Load();
            EnemyCursor cursor = UnityEngine.Object.FindAnyObjectByType<EnemyCursor>();
            cursor.speed = 12f; // The stretch takes about 9 s at its own speed, longer than the timeout.
            Bounds safe = GameObject.Find("Word safe again").GetComponent<BoxCollider2D>().bounds;
            PlaceLetterAt(new Vector2(safe.center.x, safe.max.y + 0.01f));

            yield return WaitUntil(() => cursor.IsDone);
            WordBlock first = GameObject.Find("Word backspace").GetComponent<WordBlock>();
            Assert.IsTrue(first.IsErased, "Words in the stretch should be erased.");
            Assert.IsFalse(GameObject.Find("Word safe again").GetComponent<WordBlock>().IsErased,
                "Words past the stretch should stay.");
            Assert.IsTrue(letter != null, "A letter past the stretch should be safe.");
        }

        [UnityTest]
        public IEnumerator StandingStillInFrontOfTheCursorCostsALife()
        {
            yield return Load();
            EnemyCursor cursor = UnityEngine.Object.FindAnyObjectByType<EnemyCursor>();
            PlaceLetterAt(new Vector2(cursor.transform.position.x + cursor.startLead + 0.5f, letter.transform.position.y));
            LetterController standing = letter;
            yield return WaitUntil(() => standing == null);
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
