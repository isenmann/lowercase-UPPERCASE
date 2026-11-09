using System;
using NUnit.Framework;

namespace LowercaseUppercase.Tests
{
    /// <summary>Share codes and the "finished by its maker" flag.</summary>
    public class LevelShareTests
    {
        static CustomLevelData Sample()
        {
            var level = new CustomLevelData { name = "shared", seconds = 90f, ceiling = true };
            level.pieces.Add(new CustomPiece(CustomPieceType.Word, 40f, 520f, "start here", CustomPieceType.Solid));
            level.pieces.Add(new CustomPiece(CustomPieceType.Exit, 600f, 520f, "c_t", "a"));
            level.pieces.Add(new CustomPiece(CustomPieceType.Enemy, 300f, 520f, "", CustomPieceType.Hopper) { value = 1.5f, delay = 0.5f });
            return level;
        }

        [Test]
        public void ACodeReadsBackAsTheSameLevel()
        {
            CustomLevelData level = Sample();
            string code = LevelShareCode.Encode(level);
            Assert.That(code, Does.StartWith("luc"));

            CustomLevelData back = LevelShareCode.Decode(code);
            Assert.That(back.ToJson(false), Is.EqualTo(level.ToJson(false)));
        }

        [Test]
        public void ACodeBrokenOverLinesStillReads()
        {
            string code = LevelShareCode.Encode(Sample());
            string broken = code.Substring(0, 20) + "\n  " + code.Substring(20);
            Assert.That(LevelShareCode.Decode(broken).name, Is.EqualTo("shared"));
        }

        [Test]
        public void ThePlainFormReadsToo()
        {
            string plain = LevelShareCode.PlainPrefix
                + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Sample().ToJson(false)));
            Assert.That(LevelShareCode.Decode(plain).pieces.Count, Is.EqualTo(3));
        }

        [TestCase("")]
        [TestCase("hello there")]
        [TestCase("luc1:!!!!")]
        [TestCase("luc1:aGVsbG8=")]
        [TestCase("luc0:aGVsbG8=")]
        public void NonsenseIsRefusedWithAReason(string code)
        {
            var e = Assert.Throws<FormatException>(() => LevelShareCode.Decode(code));
            Assert.That(e.Message, Is.Not.Empty);
        }

        [Test]
        public void ACutShortCodeIsRefused()
        {
            string code = LevelShareCode.Encode(Sample());
            Assert.Throws<FormatException>(() => LevelShareCode.Decode(code.Substring(0, code.Length / 2)));
        }

        [Test]
        public void FinishingHoldsUntilSomethingThatMattersChanges()
        {
            CustomLevelData level = Sample();
            Assert.That(level.FinishedByMaker, Is.False);
            level.MarkFinished();
            Assert.That(level.FinishedByMaker, Is.True);

            level.name = "renamed";
            Assert.That(level.FinishedByMaker, Is.True, "a new name doesn't change how it plays");

            level.pieces[1].x += 8f;
            Assert.That(level.FinishedByMaker, Is.False);
            level.pieces[1].x -= 8f;
            Assert.That(level.FinishedByMaker, Is.True, "moving it back is the level that was finished");
        }

        [Test]
        public void TheFlagTravelsWithTheCode()
        {
            CustomLevelData level = Sample();
            level.MarkFinished();
            Assert.That(LevelShareCode.Decode(LevelShareCode.Encode(level)).FinishedByMaker, Is.True);
        }
    }
}
