using NUnit.Framework;

namespace LowercaseUppercase.Tests
{
    /// <summary>
    /// Many tests finish levels, which unlocks the next one in the Chapters menu. This keeps that in a separate key for the whole
    /// test run, so running the tests doesn't open levels the player hasn't reached.
    /// </summary>
    [SetUpFixture]
    public class TestLevelProgress
    {
        [OneTimeSetUp]
        public void UseTestProgress()
        {
            LevelProgress.StorageKey = LevelProgress.DefaultStorageKey + ".tests";
            LevelProgress.Clear();
        }

        [OneTimeTearDown]
        public void ForgetTestProgress()
        {
            LevelProgress.Clear();
            LevelProgress.StorageKey = LevelProgress.DefaultStorageKey;
        }
    }
}
