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

        // ---------------------------------------------------------------- riser and drip

        /// <summary>
        /// Loose letters like <see cref="Kerned"/>, with a riser hidden in each of the given holes
        /// (0 is the hole after the first letter). Neighbouring risers come up out of step. Returns the right edge.
        /// </summary>
        static float KernedWithRisers(Transform level, string letters, float leftPx, float surfacePx, params int[] holes)
        {
            var group = new GameObject($"Kerning {letters}").transform;
            group.SetParent(level, false);
            var blocks = new WordBlock[letters.Length];
            for (int i = 0; i < letters.Length; i++)
                blocks[i] = Word(group, letters[i].ToString(), leftPx + i * KerningStepPx, surfacePx, 72f);

            for (int n = 0; n < holes.Length; n++)
            {
                int hole = holes[n];
                float holeCentre = (RightEdge(blocks[hole]) + LeftEdge(blocks[hole + 1])) * 0.5f;
                Enemy enemy = BuildEnemyBody(level, blocks[hole], holeCentre, "¡", 64f, stompable: false,
                    shootable: true);
                enemy.gameObject.AddComponent<EnemyRise>().startDelay = 0.5f + n * 0.8f;
            }
            return RightEdge(blocks[letters.Length - 1]);
        }

        /// <summary>A drip hanging under a ceiling word. It finds the ceiling itself as the level starts.</summary>
        static EnemyDrip BuildDrip(Transform parent, WordBlock ceiling, float leftPx)
        {
            var box = ceiling.GetComponent<BoxCollider2D>();
            float bottomY = ceiling.transform.TransformPoint(box.offset).y - box.size.y * 0.5f;
            Enemy enemy = BuildEnemyAt(parent, new Vector3(X(leftPx), bottomY - 0.5f, 0f), "'", 72f, stompable: false,
                shootable: true);
            return enemy.gameObject.AddComponent<EnemyDrip>();
        }

        // ---------------------------------------------------------------- fire

        /// <summary>A spark that leaps out of a hot word. Fire can't be stomped or shot, and it is drawn in hot-word orange.</summary>
        static EnemySpark BuildSpark(Transform parent, WordBlock hotWord, float leftPx, float startDelay = 0f)
        {
            Enemy enemy = BuildEnemyBody(parent, hotWord, leftPx, "°", 72f, stompable: false, shootable: false);
            enemy.glyph.color = Hot;
            var spark = enemy.gameObject.AddComponent<EnemySpark>();
            spark.startDelay = startDelay;
            return spark;
        }

        /// <summary>
        /// A hot o with a row of dots turning around it. The o is a hot word, so it burns too.
        /// The dots are fire: they can't be stomped or shot.
        /// </summary>
        static EnemyFireBar BuildFireBar(Transform level, float leftPx, float surfacePx, int dotCount = 4,
            float degreesPerSecond = -90f)
        {
            WordBlock centre = Word(level, "o", leftPx, surfacePx, 72f, WordKind.Hot, Hot);
            var box = centre.GetComponent<BoxCollider2D>();
            Vector3 middle = centre.transform.TransformPoint(box.offset);

            var bar = new GameObject("Fire Bar");
            bar.transform.SetParent(level, false);
            bar.transform.position = new Vector3(middle.x, middle.y, 0f);
            var fire = bar.AddComponent<EnemyFireBar>();
            fire.degreesPerSecond = degreesPerSecond;
            fire.dots = new Enemy[dotCount];
            for (int i = 0; i < dotCount; i++)
            {
                Enemy dot = BuildEnemyAt(bar.transform, bar.transform.position, "·", 72f, stompable: false,
                    shootable: false);
                dot.glyph.color = Hot;
                dot.name = $"Fire Dot {i + 1}";
                fire.dots[i] = dot;
            }
            return fire;
        }

        // ---------------------------------------------------------------- eye, curl and case swapper

        /// <summary>An eye that drifts toward the letter through words once the letter comes near. Shoot it; it can't be stomped.</summary>
        static EnemyFollow BuildEye(Transform parent, float leftPx, float bottomPx)
        {
            Enemy enemy = BuildEnemyAt(parent, new Vector3(X(leftPx), Y(bottomPx), 0f), "\u00f6", 64f,
                stompable: false, shootable: true);
            return enemy.gameObject.AddComponent<EnemyFollow>();
        }

        /// <summary>An @ that walks, curls into an o under a stomp, and rolls when kicked.</summary>
        static EnemyCurl BuildCurl(Transform parent, WordBlock ground, float leftPx)
        {
            Enemy enemy = BuildEnemyBody(parent, ground, leftPx, "@", 56f, stompable: true, shootable: true);
            return enemy.gameObject.AddComponent<EnemyCurl>();
        }

        /// <summary>A walking up-down arrow that turns Shift around when touched, like Caps Lock.</summary>
        static EnemyCaseSwap BuildCaseSwapper(Transform parent, WordBlock ground, float leftPx)
        {
            Enemy enemy = BuildWalker(parent, ground, leftPx, "\u2195", 64f, stompable: true, shootable: true);
            return enemy.gameObject.AddComponent<EnemyCaseSwap>();
        }

        static float LeftEdge(WordBlock block)
        {
            var box = block.GetComponent<BoxCollider2D>();
            float world = block.transform.position.x + box.offset.x - box.size.x * 0.5f;
            return world * PixelsPerUnit + 640f;
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
