using TMPro;
using UnityEngine;

namespace LowercaseUppercase.Levels
{
    /// <summary>
    /// Every enemy the levels use. Each is one red glyph with an <see cref="Enemy"/> and one movement component.
    /// These are part of the game, because the random level places enemies as it loads.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        // Arrows fly above a lowercase letter (0.4 high) and into an uppercase one (0.92 high, and it only jumps 0.6).
        const float ArrowFeetAboveGround = 0.5f;
        const float LauncherFontPx = 72f;
        const float LauncherLineSpacing = -88f; // Guillemets are short, so the rows are packed tighter than a column of letters.

        // ---------------------------------------------------------------- walkers

        /// <summary>The plain walker: dies to a stomp from UPPERCASE and to an asterisk.</summary>
        static Enemy BuildEnemy(Transform parent, WordBlock ground, float leftPx) =>
            BuildWalker(parent, ground, leftPx, "&", 48f, stompable: true, shootable: true);

        /// <summary>
        /// A walker that burns an UPPERCASE letter landing on it. Only an asterisk kills it.
        /// A caret is short, so it is drawn larger than the others to stand about as tall as a lowercase letter.
        /// </summary>
        static Enemy BuildSpike(Transform parent, WordBlock ground, float leftPx) =>
            BuildWalker(parent, ground, leftPx, "^", 72f, stompable: false, shootable: true);

        /// <summary>A walker that asterisks pop on. Only a stomp from UPPERCASE kills it. A section sign is narrow, so it is drawn larger.</summary>
        static Enemy BuildArmour(Transform parent, WordBlock ground, float leftPx) =>
            BuildWalker(parent, ground, leftPx, "§", 64f, stompable: true, shootable: false);

        static Enemy BuildWalker(Transform parent, WordBlock ground, float leftPx, string glyphText, float fontPx,
            bool stompable, bool shootable)
        {
            Enemy enemy = BuildEnemyBody(parent, ground, leftPx, glyphText, fontPx, stompable, shootable);
            enemy.gameObject.AddComponent<EnemyWalk>();
            return enemy;
        }

        // ---------------------------------------------------------------- hopper and flier

        /// <summary>
        /// An exclamation mark that hops on a beat. With a side speed it hops along its word, turning like a walker.
        /// A start delay puts neighbouring hoppers out of step.
        /// </summary>
        static EnemyHop BuildHopper(Transform parent, WordBlock ground, float leftPx, float sideSpeed = 0f,
            float startDelay = 0f)
        {
            Enemy enemy = BuildEnemyBody(parent, ground, leftPx, "!", 56f, stompable: true, shootable: true);
            var hop = enemy.gameObject.AddComponent<EnemyHop>();
            hop.sideSpeed = sideSpeed;
            hop.startDelay = startDelay;
            return hop;
        }

        /// <summary>
        /// A tilde that flies to either side of where it starts, bobbing as it goes. Its feet start at
        /// <paramref name="bottomPx"/>. With no travel it bobs up and down in place, with a wider swing.
        /// </summary>
        static EnemyWave BuildFlier(Transform parent, float leftPx, float bottomPx, float travelPx)
        {
            Enemy enemy = BuildEnemyAt(parent, new Vector3(X(leftPx), Y(bottomPx), 0f), "~", 72f,
                stompable: true, shootable: true);
            var wave = enemy.gameObject.AddComponent<EnemyWave>();
            wave.travel = travelPx / PixelsPerUnit;
            if (travelPx <= 0f)
            {
                wave.amplitude = 1f;
                wave.period = 2.2f;
            }
            return wave;
        }

        // ---------------------------------------------------------------- launcher and arrows

        /// <summary>
        /// A column of guillemets standing on the ground that fires arrows the way it points. It is a solid word,
        /// so it can be stood on. The arrow template is an inactive enemy just in front of it.
        /// </summary>
        static WordBlock BuildLauncher(Transform level, float leftPx, float surfacePx, bool facesLeft, int rows = 2,
            float interval = 2f)
        {
            string mark = facesLeft ? "«" : "»";
            string letters = string.Join("\n", System.Linq.Enumerable.Repeat(mark, rows));
            TextMeshPro text = Text(level, $"Launcher {mark}", letters, leftPx, surfacePx, LauncherFontPx, mono, Paper);
            text.lineSpacing = LauncherLineSpacing;
            WordBlock launcher = AddBlock(text, WordKind.Solid);
            launcher.FitCollider();
            var fitted = launcher.GetComponent<BoxCollider2D>();
            SetHeight(launcher, Y(surfacePx) - (fitted.offset.y - fitted.size.y * 0.5f));

            var box = launcher.GetComponent<BoxCollider2D>();
            Vector3 centre = launcher.transform.TransformPoint(box.offset);
            float edge = facesLeft ? centre.x - box.size.x * 0.5f : centre.x + box.size.x * 0.5f;

            Enemy arrow = BuildEnemyAt(level, new Vector3(edge, Y(surfacePx) + ArrowFeetAboveGround, 0f),
                facesLeft ? "<" : ">", 48f, stompable: true, shootable: true);
            arrow.paysPoints = false;
            arrow.name = $"Arrow {(facesLeft ? "<" : ">")} template";
            var flight = arrow.gameObject.AddComponent<EnemyArrow>();
            flight.direction = facesLeft ? -1f : 1f;
            // The glyph's half width, so the arrow starts clear of the launcher instead of inside it.
            arrow.transform.position += new Vector3(flight.direction * 0.25f, 0f, 0f);
            arrow.gameObject.SetActive(false);

            var fire = launcher.gameObject.AddComponent<EnemyLauncher>();
            fire.arrowTemplate = flight;
            fire.interval = interval;
            return launcher;
        }

        // ---------------------------------------------------------------- bodies

        /// <summary>An enemy standing on a word, without a movement component.</summary>
        static Enemy BuildEnemyBody(Transform parent, WordBlock ground, float leftPx, string glyphText, float fontPx,
            bool stompable, bool shootable)
        {
            var groundBox = ground.GetComponent<BoxCollider2D>();
            float surfaceY = ground.transform.TransformPoint(groundBox.offset).y + groundBox.size.y * 0.5f;
            return BuildEnemyAt(parent, new Vector3(X(leftPx), surfaceY, 0f), glyphText, fontPx, stompable, shootable);
        }

        /// <summary>An enemy with its feet at a world position, without a movement component. Every enemy is drawn in Danger red.</summary>
        static Enemy BuildEnemyAt(Transform parent, Vector3 feet, string glyphText, float fontPx, bool stompable,
            bool shootable)
        {
            var go = new GameObject($"Enemy {glyphText}");
            go.transform.SetParent(parent, false);
            go.transform.position = feet;
            go.layer = LayerMask.NameToLayer("Enemy");
            go.AddComponent<Rigidbody2D>();
            go.AddComponent<BoxCollider2D>();

            // The glyph comes first, because in the game the enemy wakes the moment it is added.
            TextMeshPro glyph = Glyph(go.transform, glyphText, fontPx, monoBold);
            glyph.color = Danger;
            glyph.sortingOrder = 8;
            var enemy = go.AddComponent<Enemy>();
            enemy.levelMask = LayerMask.GetMask("Level");
            enemy.glyph = glyph;
            enemy.stompable = stompable;
            enemy.shootable = shootable;
            return enemy;
        }
    }
}
