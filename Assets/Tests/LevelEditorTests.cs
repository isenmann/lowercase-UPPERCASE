using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LowercaseUppercase.Tests
{
    /// <summary>The level editor: typing words onto the page, editing them, and the frozen preview.</summary>
    public class LevelEditorTests
    {
        LevelEditor editor;

        string folder;

        [UnitySetUp]
        public IEnumerator OpenTheEditor()
        {
            // The editor saves as it goes; tests keep their levels out of the player's.
            folder = System.IO.Path.Combine(Application.temporaryCachePath, "editor-test-levels");
            if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true);
            LevelStore.FolderOverride = folder;
            CustomRun.EditPath = null;
            yield return SceneManager.LoadSceneAsync("LevelEditor");
            yield return null;
            editor = Object.FindAnyObjectByType<LevelEditor>();
        }

        [TearDown]
        public void ForgetTheTest()
        {
            CustomRun.EndTest();
            CustomRun.Level = null;
            CustomRun.EditPath = null;
            CustomRun.ReturnToMyLevels = false;
            LevelStore.FolderOverride = null;
            if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true);
        }

        static IEnumerator WaitFor<T>() where T : Object
        {
            float start = Time.realtimeSinceStartup;
            while (Object.FindAnyObjectByType<T>() == null)
            {
                if (Time.realtimeSinceStartup - start > 10f) Assert.Fail($"Timed out waiting for a {typeof(T).Name}.");
                yield return null;
            }
            yield return null;
        }

        void TypeWord(string word)
        {
            foreach (char c in word) editor.Type(c);
        }

        WordBlock Built(string content) => editor.Preview.GetComponentsInChildren<WordBlock>()
            .FirstOrDefault(w => w.GetComponent<TMPro.TMP_Text>().text == content);

        [Test]
        public void ANewLevelHasAWordToStartOn()
        {
            Assert.That(editor.Level.pieces.Count, Is.EqualTo(1));
            Assert.That(Built("start here"), Is.Not.Null);
        }

        [Test]
        public void ClickingEmptyPageStartsAWordOnTheGrid()
        {
            editor.ClickAt(new Vector2(803f, 437f));
            TypeWord("a ledge");
            editor.Commit();

            CustomPiece piece = editor.Level.pieces.Last();
            Assert.That(piece.text, Is.EqualTo("a ledge"));
            Assert.That(piece.x, Is.EqualTo(800f));
            Assert.That(piece.y, Is.EqualTo(440f));
            Assert.That(editor.Typing, Is.Null);
            Assert.That(Built("a ledge"), Is.Not.Null);
        }

        [Test]
        public void ThePreviewFollowsEveryKey()
        {
            editor.ClickAt(new Vector2(800f, 440f));
            TypeWord("ab");
            Assert.That(Built("ab"), Is.Not.Null);
            editor.Backspace();
            Assert.That(Built("a"), Is.Not.Null);
        }

        [Test]
        public void EscapeDropsANewWord()
        {
            editor.ClickAt(new Vector2(800f, 440f));
            TypeWord("gone");
            editor.Cancel();
            Assert.That(editor.Level.pieces.Count, Is.EqualTo(1));
            Assert.That(Built("gone"), Is.Null);
        }

        [Test]
        public void AnEmptiedWordIsRemoved()
        {
            editor.ClickAt(new Vector2(800f, 440f));
            editor.Commit();
            Assert.That(editor.Level.pieces.Count, Is.EqualTo(1));
        }

        [Test]
        public void ClickingAWordEditsItAndEscapeRestoresIt()
        {
            // On "here": the letter's start marker sits on the start of the word.
            editor.ClickAt(new Vector2(350f, 505f));
            editor.ClickAt(new Vector2(350f, 505f));
            Assert.That(editor.Typing, Is.SameAs(editor.Level.pieces[0]));

            editor.Backspace();
            TypeWord("!");
            Assert.That(editor.Typing.text, Is.EqualTo("start her!"));
            editor.Cancel();
            Assert.That(editor.Level.pieces[0].text, Is.EqualTo("start here"));
        }

        [Test]
        public void ClickingElsewherePlacesTheWordBeingTyped()
        {
            editor.ClickAt(new Vector2(800f, 440f));
            TypeWord("first");
            editor.ClickAt(new Vector2(800f, 240f));
            Assert.That(editor.Level.pieces.Any(p => p.text == "first"));
            Assert.That(editor.Typing.y, Is.EqualTo(240f));
        }

        [UnityTest]
        public IEnumerator ThePreviewStaysStill()
        {
            editor.Level.pieces.Add(new CustomPiece(CustomPieceType.Enemy, 300f, 500f, "", CustomPieceType.Walker));
            editor.Rebuild();
            Enemy enemy = editor.Preview.GetComponentInChildren<Enemy>();
            Vector3 start = enemy.transform.position;

            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

            Assert.That(enemy.transform.position, Is.EqualTo(start));
            Assert.That(enemy.GetComponent<EnemyWalk>().enabled, Is.False);
        }

        static PaletteItem Tool(string name) => EditorPalette.All.First(i => i.Name == name);

        [Test]
        public void ThingsDropOntoTheWordTheyArePutOn()
        {
            editor.palette.Select(Tool("checkpoint"));
            CustomPiece checkpoint = editor.Place(Tool("checkpoint"), new Vector2(200f, 480f));
            Assert.That(checkpoint.y, Is.EqualTo(520f));
        }

        [Test]
        public void WithAThingToolAClickOnAWordPlacesTheThing()
        {
            editor.palette.Select(Tool("walker"));
            editor.ClickAt(new Vector2(200f, 525f));

            Assert.That(editor.Level.pieces.Count, Is.EqualTo(2));
            Assert.That(editor.Selected.kind, Is.EqualTo(CustomPieceType.Walker));
            Assert.That(editor.Selected.y, Is.EqualTo(520f));
        }

        [Test]
        public void WithAThingToolAClickOnAThingSelectsIt()
        {
            editor.palette.Select(Tool("walker"));
            editor.ClickAt(new Vector2(200f, 480f));
            CustomPiece walker = editor.Selected;
            editor.Deselect();

            editor.ClickAt(new Vector2(204f, 505f));
            Assert.That(editor.Selected, Is.SameAs(walker));
            Assert.That(editor.Level.pieces.Count, Is.EqualTo(2));
        }

        [Test]
        public void ThePanelStepsTheSelectedPiece()
        {
            editor.palette.Select(Tool("hyphen bridge"));
            editor.ClickAt(new Vector2(800f, 520f));
            TypeWord("----");
            editor.Commit();

            Assert.That(editor.panel.title.text, Is.EqualTo(EditorPalette.NameOf(editor.Selected)));
            Assert.That(editor.panel.Shown[1].Label, Is.EqualTo("kind"));
            Assert.That(editor.Selected.kind, Is.EqualTo(CustomPieceType.Plank));
            editor.panel.Shown[1].Step(1);
            Assert.That(editor.Selected.kind, Is.EqualTo(CustomPieceType.Cracked));
            editor.panel.Shown[1].Step(1);
            Assert.That(editor.Selected.kind, Is.EqualTo(CustomPieceType.Solid), "The last kind should wrap round.");
        }

        [Test]
        public void ThePanelsButtonsCanBeClicked()
        {
            editor.palette.Select(Tool("comma hooks"));
            editor.ClickAt(new Vector2(800f, 300f));
            Assert.That(editor.Selected.count, Is.EqualTo(3));

            editor.ClickHud(editor.panel.moreButtons[0].GetComponent<Renderer>().bounds.center);
            Assert.That(editor.Selected.count, Is.EqualTo(4));

            editor.ClickHud(editor.panel.deleteButton.GetComponent<Renderer>().bounds.center);
            Assert.That(editor.Selected, Is.Null);
            Assert.That(editor.Level.pieces.Count, Is.EqualTo(1));
        }

        [Test]
        public void DoubleClickingAWordEditsIt()
        {
            editor.ClickAt(new Vector2(350f, 505f));
            Assert.That(editor.Typing, Is.Null);
            Assert.That(editor.Selected, Is.SameAs(editor.Level.pieces[0]));
            editor.ClickAt(new Vector2(350f, 505f));
            Assert.That(editor.Typing, Is.SameAs(editor.Level.pieces[0]));
        }

        [Test]
        public void AnExclamationMarkInLooseLettersHidesARiser()
        {
            editor.palette.Select(Tool("loose letters (! marks a riser)"));
            editor.ClickAt(new Vector2(800f, 520f));
            TypeWord("ab!c");
            editor.Commit();
            Assert.That(editor.Level.pieces.Last().text, Is.EqualTo("ab\u00a1c"));
            Assert.That(editor.Preview.GetComponentsInChildren<EnemyRise>(true).Length, Is.EqualTo(1));
        }

        [Test]
        public void TheStartCanBeSelected()
        {
            editor.ClickAt(new Vector2(110f, 500f));
            Assert.That(editor.StartSelected, Is.True);
            Assert.That(editor.panel.title.text, Is.EqualTo("letter start"));
        }

        [Test]
        public void ALiftsTopIsDraggedByItsHandle()
        {
            CustomPiece lift = editor.Place(Tool("lift"), new Vector2(1600f, 520f));
            Assert.That(editor.HandleAt(new Vector2(1663f, 220f)), Is.Not.Null);

            editor.DragHandleTo(new Vector2(1663f, 304f));
            Assert.That(lift.y2, Is.EqualTo(304f));
            // Never lower than the shortest lift, never above the page.
            editor.DragHandleTo(new Vector2(1663f, 500f));
            Assert.That(lift.y2, Is.EqualTo(520f - PieceHandles.MinLiftPx));
            editor.DragHandleTo(new Vector2(1663f, 0f));
            Assert.That(lift.y2, Is.EqualTo(PieceHandles.HighestLiftTopPx));
        }

        [Test]
        public void AHookRowGrowsByItsHandle()
        {
            CustomPiece hooks = editor.Place(Tool("comma hooks"), new Vector2(800f, 240f));
            editor.DragHandleTo(new Vector2(800f + 4f * PieceHandles.HookSpacingPx + 40f, 240f));
            Assert.That(hooks.count, Is.EqualTo(5));
            Assert.That(editor.Preview.GetComponentsInChildren<WordBlock>().Count(w => w.kind == WordKind.Comma),
                Is.EqualTo(5));
        }

        [Test]
        public void StretchesAndFliersAreSizedByTheirHandles()
        {
            CustomPiece caps = editor.Place(Tool("Caps Lock stretch"), new Vector2(1000f, 300f));
            editor.DragHandleTo(new Vector2(1400f, 352f));
            Assert.That(caps.x2, Is.EqualTo(1400f));

            CustomPiece flier = editor.Place(Tool("flier"), new Vector2(600f, 300f));
            editor.DragHandleTo(new Vector2(840f, 300f));
            Assert.That(flier.value, Is.EqualTo(240f));
        }

        [Test]
        public void MovingALiftKeepsItsHeight()
        {
            CustomPiece lift = editor.Place(Tool("lift"), new Vector2(1600f, 520f));
            float height = lift.y - lift.y2;
            editor.MoveSelectedBy(new Vector2(80f, -40f));
            Assert.That(lift.x, Is.EqualTo(1680f));
            Assert.That(lift.y, Is.EqualTo(480f));
            Assert.That(lift.y - lift.y2, Is.EqualTo(height));
        }

        [UnityTest]
        public IEnumerator ATestPlayComesBackToTheSameLevel()
        {
            CustomPiece walker = editor.Place(Tool("walker"), new Vector2(300f, 480f));
            CustomLevelData level = editor.Level;
            editor.view.transform.position = new Vector3(3f, 0f, -10f);

            editor.Play();
            yield return WaitFor<CustomLevel>();
            var custom = Object.FindAnyObjectByType<CustomLevel>();
            Assert.That(custom.Data, Is.SameAs(level));
            Assert.That(custom.flow.leaveScene, Is.EqualTo(CustomRun.EditorScene));
            Assert.That(custom.flow.GetComponent<LevelTransition>().nextScene, Is.EqualTo(CustomRun.EditorScene));

            CustomLevel.BackToEditor();
            yield return WaitFor<LevelEditor>();
            editor = Object.FindAnyObjectByType<LevelEditor>();
            Assert.That(editor.Level, Is.SameAs(level));
            Assert.That(editor.Selected, Is.SameAs(walker));
            Assert.That(editor.view.transform.position.x, Is.EqualTo(3f).Within(0.001f));
            Assert.That(CustomRun.Testing, Is.False);
        }

        [UnityTest]
        public IEnumerator PlayHereStartsTheLetterThere()
        {
            editor.Play(new Vector2(400f, 400f));
            yield return WaitFor<CustomLevel>();
            var letter = Object.FindAnyObjectByType<LetterController>();
            Assert.That(letter.transform.position.x, Is.EqualTo(LevelEditor.WorldX(400f)).Within(0.05f));
        }
    
        // ---------------------------------------------------------------- settings, undo, warnings (step 5)

        PanelRow SettingRow(string label) => editor.panel.Shown.First(r => r.Label == label);

        void ClickRowValue(string label)
        {
            int row = editor.panel.Shown.ToList().FindIndex(r => r.Label == label);
            editor.ClickHud(editor.panel.values[row].GetComponent<Renderer>().bounds.center);
        }

        [Test]
        public void TheSettingsButtonShowsTheLevelsSettings()
        {
            editor.ClickHud(editor.settingsButton.GetComponent<Renderer>().bounds.center);
            Assert.That(editor.SettingsShown, Is.True);
            Assert.That(editor.panel.Shown.Select(r => r.Label),
                Is.EqualTo(new[] { "name", "sentence", "letter", "time", "ceiling" }));
        }

        [Test]
        public void TheSentenceIsTypedWithABlankForTheLetter()
        {
            editor.ShowSettings();
            ClickRowValue("sentence");
            Assert.That(editor.TypingSetting, Is.Not.Null);
            for (int i = 0; i < 10; i++) editor.Backspace();
            TypeWord("a b_g dog");
            editor.Commit();

            Assert.That(editor.Level.sentenceBefore, Is.EqualTo("a b"));
            Assert.That(editor.Level.sentenceAfter, Is.EqualTo("g dog"));
        }

        [Test]
        public void ASentenceWithoutABlankIsRefused()
        {
            editor.ShowSettings();
            ClickRowValue("sentence");
            for (int i = 0; i < 10; i++) editor.Backspace();
            TypeWord("no blank");
            editor.Commit();

            Assert.That(LevelSettings.Sentence(editor.Level), Is.EqualTo("the c_t"));
        }

        [Test]
        public void TheTimeLimitStepsByFiveSeconds()
        {
            editor.ShowSettings();
            SettingRow("time").Step(1);
            Assert.That(editor.Level.seconds, Is.EqualTo(65f));
            SettingRow("ceiling").Step(1);
            Assert.That(editor.Level.ceiling, Is.True);
        }

        [Test]
        public void UndoTakesAPlacedPieceBackAndRedoBringsItAgain()
        {
            editor.palette.Select(Tool("checkpoint"));
            editor.ClickAt(new Vector2(400f, 520f));
            editor.Checkpoint();
            Assert.That(editor.Level.pieces.Count, Is.EqualTo(2));

            editor.Undo();
            Assert.That(editor.Level.pieces.Count, Is.EqualTo(1));
            editor.Redo();
            Assert.That(editor.Level.pieces.Count, Is.EqualTo(2));
            Assert.That(editor.Level.pieces[1].type, Is.EqualTo(CustomPieceType.Checkpoint));
        }

        [Test]
        public void UndoingAMoveKeepsThePieceSelected()
        {
            editor.palette.Select(Tool("comma hooks"));
            editor.ClickAt(new Vector2(800f, 300f));
            editor.Checkpoint();
            editor.MoveSelectedBy(new Vector2(80f, 0f));
            editor.Checkpoint();

            editor.Undo();
            Assert.That(editor.Selected, Is.Not.Null);
            Assert.That(editor.Selected.x, Is.EqualTo(800f));
        }

        [Test]
        public void ANewLevelWarnsThatItHasNoExit()
        {
            Assert.That(editor.Warnings.Select(w => w.Text), Does.Contain(EditorWarnings.NoExit));
            Assert.That(editor.warningsText.text, Does.Contain(EditorWarnings.NoExit));

            editor.palette.Select(Tool("exit word"));
            editor.ClickAt(new Vector2(400f, 520f));
            editor.Checkpoint();
            Assert.That(editor.Warnings.Select(w => w.Text), Does.Not.Contain(EditorWarnings.NoExit));
        }

        [Test]
        public void AnEnemyInTheAirIsWarnedAbout()
        {
            editor.palette.Select(Tool("walker"));
            editor.ClickAt(new Vector2(800f, 200f));
            editor.Checkpoint();
            EditorWarning warning = editor.Warnings.FirstOrDefault(w => w.Text == EditorWarnings.NoGround);
            Assert.That(warning, Is.Not.Null);
            Assert.That(warning.Piece, Is.SameAs(editor.Level.pieces[1]));
        }

        [Test]
        public void OverlappingWordsAreWarnedAbout()
        {
            editor.Open(new CustomLevelData
            {
                pieces =
                {
                    new CustomPiece(CustomPieceType.Word, 40f, 520f, "first", CustomPieceType.Solid),
                    new CustomPiece(CustomPieceType.Word, 80f, 520f, "second", CustomPieceType.Solid),
                    new CustomPiece(CustomPieceType.Word, 800f, 520f, "apart", CustomPieceType.Solid),
                },
            });
            Assert.That(editor.Warnings.Count(w => w.Text == EditorWarnings.Overlap), Is.EqualTo(2));
        }

        [Test]
        public void ASpringUnderAWordIsWarnedAbout()
        {
            editor.Open(new CustomLevelData
            {
                pieces =
                {
                    new CustomPiece(CustomPieceType.Word, 40f, 520f, "the ground here", CustomPieceType.Solid),
                    new CustomPiece(CustomPieceType.Word, 40f, 440f, "a low roof", CustomPieceType.Solid),
                    new CustomPiece(CustomPieceType.Spring, 120f, 520f),
                },
            });
            Assert.That(editor.Warnings.Select(w => w.Text), Does.Contain(EditorWarnings.NoRoom));
        }
    
        // ---------------------------------------------------------------- saving (step 6)

        [Test]
        public void AChangeIsSavedToAFile()
        {
            Assert.That(CustomRun.EditPath, Is.Null, "a new level has no file until it changes");
            editor.palette.Select(Tool("checkpoint"));
            editor.ClickAt(new Vector2(400f, 520f));
            editor.Checkpoint();

            Assert.That(CustomRun.EditPath, Is.Not.Null);
            CustomLevelData saved = LevelStore.Load(CustomRun.EditPath);
            Assert.That(saved.pieces.Count, Is.EqualTo(2));
            Assert.That(LevelStore.List().Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LeavingOpensMyLevelsWithTheLevelListed()
        {
            editor.palette.Select(Tool("checkpoint"));
            editor.ClickAt(new Vector2(400f, 520f));
            editor.Leave();
            yield return WaitFor<MainMenu>();
            yield return null;

            var myLevels = Object.FindAnyObjectByType<MyLevelsMenu>();
            Assert.That(myLevels, Is.Not.Null);
            Assert.That(myLevels.gameObject.activeInHierarchy, Is.True);
            Assert.That(myLevels.Levels.Count, Is.EqualTo(1));
            Assert.That(myLevels.Levels[0].Name, Is.EqualTo("untitled"));
        }
    
        // ---------------------------------------------------------------- preview picture

        [UnityTest]
        public IEnumerator MyLevelsShowsAPictureOfTheCurrentLevel()
        {
            editor.Level.pieces.Add(new CustomPiece(CustomPieceType.Word, 4000f, 520f, "far away", CustomPieceType.Solid));
            editor.Rebuild();
            editor.Leave();
            yield return WaitFor<MainMenu>();

            var preview = Object.FindAnyObjectByType<LevelPreviewStrip>();
            float start = Time.realtimeSinceStartup;
            while (preview.image.texture == null)
            {
                if (Time.realtimeSinceStartup - start > 10f) Assert.Fail("The picture was never drawn.");
                yield return null;
            }

            Assert.That(preview.Shown.Name, Is.EqualTo("untitled"));
            Assert.That(preview.image.texture.height, Is.EqualTo(360));
            Assert.That(preview.scroll.content.rect.width, Is.GreaterThan(preview.scroll.viewport.rect.width),
                "a level wider than the strip scrolls");
            Assert.That(Object.FindObjectsByType<WordBlock>(), Is.Empty,
                "the level built for the picture is gone again");
        }
    }
}
