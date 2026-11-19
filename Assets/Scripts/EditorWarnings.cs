using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>Something in a level that is probably a mistake, and the piece it is about (null for the level).</summary>
    public sealed class EditorWarning
    {
        public readonly string Text;
        public readonly CustomPiece Piece;

        public EditorWarning(string text, CustomPiece piece = null)
        {
            Text = text;
            Piece = piece;
        }
    }

    /// <summary>
    /// The level editor's checks: no exit, solid pieces inside each other, enemies with nothing to walk on, springs
    /// with no room to bounce, ink pools not held in by walls. They are warnings, not rules; a level with them still
    /// plays.
    /// </summary>
    public static class EditorWarnings
    {
        public const string NoExit = "no exit word";
        public const string TwoExits = "more than one exit word";
        public const string Overlap = "pieces overlap";
        public const string NoGround = "enemy with no word under it";
        public const string NoHotGround = "spark with no hot word under it";
        public const string NoRoom = "spring with no room above it";
        public const string WrongLetter = "the exit wants a different letter than the sentence";
        public const string InkUnwalled = "ink pool needs a wall on each side";
        public const string InkNoFloor = "ink pool needs a floor under all of it";

        /// <summary>How deep two solid pieces may sink into each other before it counts, in pixels.</summary>
        const float OverlapSlackPx = 2f;
        /// <summary>The air a spring needs above its pad, in pixels.</summary>
        const float SpringRoomPx = 120f;
        const float DefaultPadPx = 80f;
        /// <summary>
        /// How far a wall may stand from a pool's side, or a gap in it may be, and still hold the ink, in pixels: one
        /// grid step, since a word's edge is where its letters are drawn and rarely on the grid.
        /// </summary>
        const float InkWallSlackPx = LevelEditor.GridPx;
        /// <summary>A gap in a pool's floor narrower than this lets nothing through: both letters are wider.</summary>
        const float InkFloorGapPx = 24f;

        static readonly HashSet<string> Solid = new HashSet<string>
        {
            CustomPieceType.Word, CustomPieceType.Brick, CustomPieceType.StickyWall, CustomPieceType.Tube,
            CustomPieceType.Kerned, CustomPieceType.Door, CustomPieceType.Lift, CustomPieceType.Power,
        };

        /// <summary>Enemies the builder leaves out when there is no word under them.</summary>
        static readonly HashSet<string> Grounded = new HashSet<string>
        {
            CustomPieceType.Walker, CustomPieceType.Spike, CustomPieceType.Armour, CustomPieceType.Swapper,
            CustomPieceType.Mimic, CustomPieceType.Curl, CustomPieceType.Hopper, CustomPieceType.Spark,
        };

        /// <param name="built">What each piece built in the editor's preview.</param>
        public static List<EditorWarning> Find(CustomLevelData level, IReadOnlyDictionary<CustomPiece, List<Transform>> built)
        {
            var warnings = new List<EditorWarning>();

            List<CustomPiece> exits = level.pieces.Where(p => p.type == CustomPieceType.Exit).ToList();
            if (exits.Count == 0) warnings.Add(new EditorWarning(NoExit));
            foreach (CustomPiece extra in exits.Skip(1)) warnings.Add(new EditorWarning(TwoExits, extra));
            foreach (CustomPiece exit in exits)
                if ((exit.kind == "A" ? "A" : "a") != (level.missingLetter == "A" ? "A" : "a"))
                    warnings.Add(new EditorWarning(WrongLetter, exit));

            var boxes = new Dictionary<CustomPiece, List<Rect>>();
            foreach (CustomPiece piece in level.pieces)
                if (built.TryGetValue(piece, out List<Transform> parts)) boxes[piece] = SolidBoxes(parts);

            // Each pair once, and each piece flagged once even if it overlaps several.
            var overlapping = new HashSet<CustomPiece>();
            List<CustomPiece> solids = level.pieces.Where(p => Solid.Contains(p.type) && boxes.ContainsKey(p)).ToList();
            for (int i = 0; i < solids.Count; i++)
                for (int j = i + 1; j < solids.Count; j++)
                    if (Overlaps(boxes[solids[i]], boxes[solids[j]]))
                    {
                        overlapping.Add(solids[i]);
                        overlapping.Add(solids[j]);
                    }
            foreach (CustomPiece piece in level.pieces)
                if (overlapping.Contains(piece)) warnings.Add(new EditorWarning(Overlap, piece));

            foreach (CustomPiece piece in level.pieces)
            {
                if (piece.type == CustomPieceType.Enemy && Grounded.Contains(piece.kind) && !boxes.ContainsKey(piece))
                    warnings.Add(new EditorWarning(piece.kind == CustomPieceType.Spark ? NoHotGround : NoGround, piece));
                if (piece.type == CustomPieceType.Spring && !HasRoom(piece, boxes))
                    warnings.Add(new EditorWarning(NoRoom, piece));
                if (piece.type == CustomPieceType.Ink && !IsWalledIn(piece, level, boxes))
                    warnings.Add(new EditorWarning(InkUnwalled, piece));
                if (piece.type == CustomPieceType.Ink && !HasFloor(piece, boxes))
                    warnings.Add(new EditorWarning(InkNoFloor, piece));
            }
            return warnings;
        }

        /// <summary>The ink's surface and bottom in page pixels, as the builder makes them.</summary>
        static void InkExtent(CustomPiece pool, out float left, out float right, out float surface, out float bottom)
        {
            left = pool.x;
            right = pool.x2 > pool.x ? pool.x2 : pool.x + Levels.PrototypeBuilder.DefaultInkWidthPx;
            bottom = pool.y;
            surface = pool.y2 != 0f
                ? Mathf.Min(pool.y2, pool.y - Levels.PrototypeBuilder.MinInkDepthPx)
                : pool.y - Levels.PrototypeBuilder.DefaultInkDepthPx;
        }

        /// <summary>
        /// Whether something holds the ink in on both sides: solid pieces at each side that, together, close it from
        /// the pool's bottom up to its surface or higher. A wall can be stacked from several pieces, and another pool
        /// beside it holds it in as far up as its own surface. Heavy and floating words move, so they don't count.
        /// </summary>
        static bool IsWalledIn(CustomPiece pool, CustomLevelData level, Dictionary<CustomPiece, List<Rect>> boxes)
        {
            InkExtent(pool, out float left, out float right, out float surface, out float bottom);
            return IsHeldIn(pool, left, surface, bottom, level, boxes) && IsHeldIn(pool, right, surface, bottom, level, boxes);
        }

        /// <summary>
        /// Whether the pool's bottom is closed from side to side, so a sinking A lands on something: by words lying
        /// at the bottom, or walls standing on it. Gaps too narrow for a letter don't matter.
        /// </summary>
        static bool HasFloor(CustomPiece pool, Dictionary<CustomPiece, List<Rect>> boxes)
        {
            InkExtent(pool, out float leftPx, out float rightPx, out _, out float bottomPx);
            float slack = InkWallSlackPx / LevelEditor.PixelsPerUnit;
            float gap = InkFloorGapPx / LevelEditor.PixelsPerUnit;
            float left = LevelEditor.World(new Vector2(leftPx, 0f)).x;
            float right = LevelEditor.World(new Vector2(rightPx, 0f)).x;
            float bottom = LevelEditor.World(new Vector2(0f, bottomPx)).y;

            // What crosses the bottom line, as spans along it.
            var spans = new List<Vector2>();
            foreach (KeyValuePair<CustomPiece, List<Rect>> entry in boxes)
            {
                CustomPiece piece = entry.Key;
                if (piece == pool || piece.type == CustomPieceType.Heavy) continue;
                if (piece.type == CustomPieceType.Word && piece.kind == CustomPieceType.Float) continue;
                foreach (Rect box in entry.Value)
                    if (box.yMin <= bottom + slack && box.yMax >= bottom - slack)
                        spans.Add(new Vector2(box.xMin, box.xMax));
            }

            // Walk them from the left side to the right; a gap a letter fits through is a hole.
            float reached = left + slack;
            foreach (Vector2 span in spans.OrderBy(s => s.x))
            {
                if (span.x > reached + gap) break;
                reached = Mathf.Max(reached, span.y);
            }
            return reached >= right - slack;
        }

        static bool IsHeldIn(CustomPiece pool, float sidePx, float surfacePx, float bottomPx, CustomLevelData level,
            Dictionary<CustomPiece, List<Rect>> boxes)
        {
            float slack = InkWallSlackPx / LevelEditor.PixelsPerUnit;
            float side = LevelEditor.World(new Vector2(sidePx, 0f)).x;
            float surface = LevelEditor.World(new Vector2(0f, surfacePx)).y;
            float bottom = LevelEditor.World(new Vector2(0f, bottomPx)).y;

            // What stands at this side, as spans of height.
            var spans = new List<Vector2>();
            foreach (KeyValuePair<CustomPiece, List<Rect>> entry in boxes)
            {
                CustomPiece piece = entry.Key;
                if (piece == pool || piece.type == CustomPieceType.Heavy) continue;
                if (piece.type == CustomPieceType.Word && piece.kind == CustomPieceType.Float) continue;
                foreach (Rect box in entry.Value)
                    if (box.xMin <= side + slack && box.xMax >= side - slack)
                        spans.Add(new Vector2(box.yMin, box.yMax));
            }
            foreach (CustomPiece other in level.pieces)
            {
                if (other == pool || other.type != CustomPieceType.Ink) continue;
                InkExtent(other, out float otherLeft, out float otherRight, out float otherSurface, out float otherBottom);
                if (otherLeft > sidePx + InkWallSlackPx || otherRight < sidePx - InkWallSlackPx) continue;
                spans.Add(new Vector2(LevelEditor.World(new Vector2(0f, otherBottom)).y,
                    LevelEditor.World(new Vector2(0f, otherSurface)).y));
            }

            // Climb the spans from the bottom; a gap on the way up lets the ink out.
            float reached = bottom + slack;
            foreach (Vector2 span in spans.OrderBy(s => s.x))
            {
                if (span.x > reached + slack) break;
                reached = Mathf.Max(reached, span.y);
            }
            return reached >= surface - 0.01f;
        }

        /// <summary>The solid boxes a piece built, as world rectangles.</summary>
        static List<Rect> SolidBoxes(List<Transform> parts)
        {
            var rects = new List<Rect>();
            foreach (Transform part in parts)
            {
                if (part == null) continue;
                foreach (BoxCollider2D box in part.GetComponentsInChildren<BoxCollider2D>(true))
                {
                    if (box.isTrigger) continue;
                    Vector3 centre = box.transform.TransformPoint(box.offset);
                    Vector3 scale = box.transform.lossyScale;
                    var size = new Vector2(Mathf.Abs(box.size.x * scale.x), Mathf.Abs(box.size.y * scale.y));
                    rects.Add(new Rect((Vector2)centre - size * 0.5f, size));
                }
            }
            return rects;
        }

        static bool Overlaps(List<Rect> a, List<Rect> b)
        {
            float slack = OverlapSlackPx / LevelEditor.PixelsPerUnit;
            foreach (Rect first in a)
            {
                var inner = new Rect(first.xMin + slack, first.yMin + slack, first.width - 2f * slack, first.height - 2f * slack);
                if (inner.width <= 0f || inner.height <= 0f) continue;
                foreach (Rect second in b)
                    if (inner.Overlaps(second)) return true;
            }
            return false;
        }

        static bool HasRoom(CustomPiece spring, Dictionary<CustomPiece, List<Rect>> boxes)
        {
            float half = (spring.value > 0f ? spring.value : DefaultPadPx) * 0.5f;
            Vector2 low = LevelEditor.World(new Vector2(spring.x - half, spring.y - OverlapSlackPx * 4f));
            Vector2 high = LevelEditor.World(new Vector2(spring.x + half, spring.y - SpringRoomPx));
            Rect air = Rect.MinMaxRect(low.x, low.y, high.x, high.y);
            foreach (KeyValuePair<CustomPiece, List<Rect>> entry in boxes)
            {
                if (entry.Key == spring || entry.Key.type == CustomPieceType.Spring) continue;
                if (entry.Value.Any(box => box.Overlaps(air))) return false;
            }
            return true;
        }
    }
}
