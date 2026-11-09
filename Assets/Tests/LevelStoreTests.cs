using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace LowercaseUppercase.Tests
{
    /// <summary>The player's level files: saving, listing, reading back and deleting.</summary>
    public class LevelStoreTests
    {
        string folder;

        [SetUp]
        public void UseATestFolder()
        {
            folder = Path.Combine(Application.temporaryCachePath, "store-test-levels");
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            LevelStore.FolderOverride = folder;
        }

        [TearDown]
        public void RemoveTheTestFolder()
        {
            LevelStore.FolderOverride = null;
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        [Test]
        public void ASavedLevelIsListedAndReadsBackTheSame()
        {
            var level = new CustomLevelData { name = "my first", seconds = 45f };
            level.pieces.Add(new CustomPiece(CustomPieceType.Word, 40f, 520f, "hello", CustomPieceType.Ice));
            string path = LevelStore.Save(level);

            var listed = LevelStore.List();
            Assert.That(listed.Count, Is.EqualTo(1));
            Assert.That(listed[0].Path, Is.EqualTo(path));
            Assert.That(listed[0].Name, Is.EqualTo("my first"));
            Assert.That(listed[0].Level.seconds, Is.EqualTo(45f));
            Assert.That(listed[0].Level.pieces[0].kind, Is.EqualTo(CustomPieceType.Ice));
        }

        [Test]
        public void SavingAgainKeepsTheSameFile()
        {
            var level = new CustomLevelData { name = "before" };
            string path = LevelStore.Save(level);
            level.name = "after";
            Assert.That(LevelStore.Save(level, path), Is.EqualTo(path));

            Assert.That(LevelStore.List().Count, Is.EqualTo(1));
            Assert.That(LevelStore.Load(path).name, Is.EqualTo("after"));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void AnUnreadableFileIsListedWithItsProblem()
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "broken.json"), "{ not a level");

            var listed = LevelStore.List();
            Assert.That(listed.Count, Is.EqualTo(1));
            Assert.That(listed[0].Level, Is.Null);
            Assert.That(listed[0].Problem, Is.Not.Empty);
        }

        [Test]
        public void ADeletedLevelIsGone()
        {
            string path = LevelStore.Save(new CustomLevelData());
            LevelStore.Delete(path);
            Assert.That(LevelStore.List(), Is.Empty);
        }

        [Test]
        public void NoFolderMeansNoLevels()
        {
            Assert.That(LevelStore.List(), Is.Empty);
        }
    }
}
