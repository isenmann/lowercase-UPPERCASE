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

        [UnitySetUp]
        public IEnumerator OpenTheEditor()
        {
            yield return SceneManager.LoadSceneAsync("LevelEditor");
            yield return null;
            editor = Object.FindAnyObjectByType<LevelEditor>();
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
            editor.ClickAt(new Vector2(120f, 505f));
            editor.ClickAt(new Vector2(120f, 505f));
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

            Assert.That(editor.panel.Piece, Is.SameAs(editor.Selected));
            Assert.That(editor.panel.Properties[1].Label, Is.EqualTo("kind"));
            Assert.That(editor.Selected.kind, Is.EqualTo(CustomPieceType.Plank));
            editor.panel.Properties[1].Step(editor.Selected, 1);
            Assert.That(editor.Selected.kind, Is.EqualTo(CustomPieceType.Solid));
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
            editor.ClickAt(new Vector2(120f, 505f));
            Assert.That(editor.Typing, Is.Null);
            Assert.That(editor.Selected, Is.SameAs(editor.Level.pieces[0]));
            editor.ClickAt(new Vector2(120f, 505f));
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
    }
}
