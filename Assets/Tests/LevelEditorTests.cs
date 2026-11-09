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
    }
}
