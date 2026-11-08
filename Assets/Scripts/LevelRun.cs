using System.Collections.Generic;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// The clock and the score, which outlive the scene reload that follows a death. Time running out ends the run, so
    /// nothing refills the clock during a level. The score carries from level to level until the menu is reached.
    /// Enemies return when the level reloads, so each one pays only once per level, otherwise dying could earn points.
    /// A # that was picked up stays gone through the reload. A run has three lives; losing the last one ends it.
    /// The asterisks from a power block carry into later levels, but a death takes them away.
    /// </summary>
    public static class LevelRun
    {
        public const float LevelSeconds = 60f;
        public const int PointsPerEnemy = 100;
        public const int PointsPerSecondLeft = 10;
        public const int PointsPerCollectible = 50;
        public const int StartingLives = 3;

        static readonly HashSet<Vector2> paidEnemies = new HashSet<Vector2>();
        static readonly HashSet<Vector2> collected = new HashSet<Vector2>();

        // Only set in the moment before a reload, so any other way into a level starts it afresh.
        static bool continuingAfterDeath;
        static int scoreAtLevelStart;
        static bool armedAtLevelStart;

        public static float TimeLeft { get; set; }
        public static int Score { get; private set; }
        public static int Lives { get; private set; }
        /// <summary>Whether the letter has its asterisks. Set by the shooter when a power block arms it.</summary>
        public static bool HasAsterisks { get; set; }

        public static bool TimeIsUp => TimeLeft <= 0f;

        // Domain reload is off in this project's Play mode settings, so statics survive from one session to the next.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void NewGame()
        {
            continuingAfterDeath = false;
            scoreAtLevelStart = 0;
            Score = 0;
            Lives = StartingLives;
            HasAsterisks = false;
            armedAtLevelStart = false;
            TimeLeft = LevelSeconds;
            paidEnemies.Clear();
            collected.Clear();
        }

        /// <summary>How many # the letter has picked up in this level so far.</summary>
        public static int CollectedThisLevel => collected.Count;

        /// <summary>Called by the flow as a level's scene loads.</summary>
        public static void EnterLevel()
        {
            if (continuingAfterDeath)
            {
                continuingAfterDeath = false;
                return;
            }

            TimeLeft = LevelSeconds;
            scoreAtLevelStart = Score;
            armedAtLevelStart = HasAsterisks;
            paidEnemies.Clear();
            collected.Clear();
        }

        /// <summary>Takes one life, and the asterisks with it. True if any lives are left to carry on with.</summary>
        public static bool LoseLife()
        {
            HasAsterisks = false;
            Lives = Mathf.Max(0, Lives - 1);
            return Lives > 0;
        }

        public static void Tick(float deltaTime) => TimeLeft = Mathf.Max(0f, TimeLeft - deltaTime);

        /// <summary>Keeps the clock and score through the reload that follows a death.</summary>
        public static void ContinueAfterDeath() => continuingAfterDeath = true;

        /// <summary>A deliberate restart is a fresh attempt, so it gives back what this level earned.</summary>
        public static void AbandonLevel()
        {
            Score = scoreAtLevelStart;
            HasAsterisks = armedAtLevelStart;
        }

        public static void EnemyKilled(Vector2 spawnPosition)
        {
            if (paidEnemies.Add(spawnPosition)) Score += PointsPerEnemy;
        }

        /// <summary>Keyed by where the # was placed, like enemies, so it is only ever paid once per level.</summary>
        public static void Collect(Vector2 position)
        {
            if (collected.Add(position)) Score += PointsPerCollectible;
        }

        public static bool IsCollected(Vector2 position) => collected.Contains(position);

        public static void BankTimeLeft()
        {
            Score += Mathf.CeilToInt(TimeLeft) * PointsPerSecondLeft;
            TimeLeft = 0f;
        }
    }
}
