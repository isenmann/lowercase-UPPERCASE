using System;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// A second point of a piece that the level editor lets you drag: a lift's top, the right end of a Caps Lock
    /// stretch or a cursor's chase, how far a flier travels, the last of a row of hooks. The editor draws a line from
    /// <see cref="From"/> to <see cref="At"/> and a handle at <see cref="At"/>.
    /// </summary>
    public sealed class PieceHandle
    {
        public readonly Vector2 From;
        public readonly Vector2 At;
        /// <summary>Sets the piece from where the handle was dragged to, in snapped page pixels.</summary>
        public readonly Action<CustomPiece, Vector2> MoveTo;

        public PieceHandle(Vector2 from, Vector2 at, Action<CustomPiece, Vector2> moveTo)
        {
            From = from;
            At = at;
            MoveTo = moveTo;
        }
    }

    /// <summary>Which pieces have a handle, where it sits, and what dragging it changes. Positions are page pixels.</summary>
    public static class PieceHandles
    {
        public const float MinLiftPx = 120f;
        public const float FloatTravelLimitPx = 80f;   // Its far end stays this far inside the page, top and bottom.
        public const float HighestLiftTopPx = 80f;
        public const float MinStretchPx = 160f;
        public const float HookSpacingPx = 240f;
        public const int MaxHooks = 8;
        const float LiftCentrePx = 63f;     // The "up" word starts 20 px in and is about 86 px wide.
        const float CapsLockMiddlePx = 352f;
        const float CursorMarkAbovePx = 60f;
        const float WindFullTopPx = 64f;
        const float WindFullBottomPx = 640f;
        const float WindFullSnapPx = 24f;   // A wind's corner dragged this close to the top blows the whole height.

        public static PieceHandle For(CustomPiece piece)
        {
            switch (piece.type)
            {
                case CustomPieceType.Lift:
                {
                    float x = piece.x + LiftCentrePx;
                    float top = piece.y2 != 0f ? piece.y2 : piece.y - 300f;
                    return new PieceHandle(new Vector2(x, piece.y), new Vector2(x, top),
                        (p, at) => p.y2 = Mathf.Clamp(at.y, HighestLiftTopPx, p.y - MinLiftPx));
                }
                case CustomPieceType.Word when piece.kind == CustomPieceType.Float:
                {
                    float x = piece.x + Mathf.Max(1, piece.text.Trim().Length) * 72f * 0.6f * 0.5f;
                    float end = piece.y2 != 0f ? piece.y2 : piece.y - 270f;
                    return new PieceHandle(new Vector2(x, piece.y), new Vector2(x, end),
                        (p, at) => p.y2 = Mathf.Clamp(at.y, FloatTravelLimitPx, 720f - FloatTravelLimitPx));
                }
                case CustomPieceType.CapsLock:
                {
                    float right = piece.x2 > piece.x ? piece.x2 : piece.x + 800f;
                    return new PieceHandle(new Vector2(piece.x, CapsLockMiddlePx), new Vector2(right, CapsLockMiddlePx),
                        (p, at) => p.x2 = Mathf.Max(at.x, p.x + MinStretchPx));
                }
                case CustomPieceType.Wind:
                {
                    // The top right corner: dragging it sets the width and, below the top of the page, makes the
                    // wind a box over its bottom, y. Back up at the top it blows the whole height again.
                    float right = piece.x2 > piece.x ? piece.x2 : piece.x + 600f;
                    bool boxed = piece.y2 != 0f;
                    var from = new Vector2(piece.x, boxed ? piece.y : WindFullBottomPx);
                    var corner = new Vector2(right, boxed ? piece.y2 : WindFullTopPx);
                    return new PieceHandle(from, corner, (p, at) =>
                    {
                        p.x2 = Mathf.Max(at.x, p.x + MinStretchPx);
                        p.y2 = at.y <= WindFullTopPx + WindFullSnapPx ? 0f
                            : Mathf.Min(at.y, p.y - Levels.PrototypeBuilder.MinWindBoxPx);
                    });
                }
                case CustomPieceType.Hooks:
                {
                    int count = Mathf.Max(1, piece.count);
                    return new PieceHandle(new Vector2(piece.x, piece.y),
                        new Vector2(piece.x + (count - 1) * HookSpacingPx, piece.y),
                        (p, at) => p.count = Mathf.Clamp(1 + Mathf.RoundToInt((at.x - p.x) / HookSpacingPx), 1, MaxHooks));
                }
                case CustomPieceType.Enemy when piece.kind == CustomPieceType.Cursor:
                {
                    float y = piece.y - CursorMarkAbovePx;
                    float end = piece.x2 > piece.x ? piece.x2 : piece.x + 1500f;
                    return new PieceHandle(new Vector2(piece.x, y), new Vector2(end, y),
                        (p, at) => p.x2 = Mathf.Max(at.x, p.x + MinStretchPx));
                }
                case CustomPieceType.Enemy when piece.kind == CustomPieceType.Flier:
                {
                    float travel = Mathf.Max(0f, piece.value);
                    return new PieceHandle(new Vector2(piece.x - travel, piece.y), new Vector2(piece.x + travel, piece.y),
                        (p, at) => p.value = Mathf.Max(0f, at.x - p.x));
                }
                default:
                    return null;
            }
        }
    }
}
