using System;
using System.Collections.Generic;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// One setting of a piece in the level editor's panel. Stepping changes it by one notch either way: a number by its
    /// step, a choice to the next option, a switch on or off. A text row is edited by typing instead.
    /// </summary>
    public sealed class PieceProperty
    {
        public readonly string Label;
        public readonly Func<CustomPiece, string> Show;
        public readonly Action<CustomPiece, int> Step;
        public bool IsText => Step == null;

        public PieceProperty(string label, Func<CustomPiece, string> show, Action<CustomPiece, int> step)
        {
            Label = label;
            Show = show;
            Step = step;
        }
    }

    /// <summary>The settings the level editor's panel offers for each kind of piece.</summary>
    public static class PieceProperties
    {
        static readonly string[] WordKinds =
        {
            CustomPieceType.Solid, CustomPieceType.Ice, CustomPieceType.Hot, CustomPieceType.Sticky, CustomPieceType.Plank
        };

        public static List<PieceProperty> For(CustomPiece piece)
        {
            var list = new List<PieceProperty>();
            switch (piece.type)
            {
                case CustomPieceType.Word:
                    list.Add(TextRow("text"));
                    list.Add(Choice("kind", p => p.kind, (p, v) => p.kind = v, WordKinds));
                    break;
                case CustomPieceType.Kerned:
                    list.Add(TextRow("letters"));
                    break;
                case CustomPieceType.Brick:
                case CustomPieceType.StickyWall:
                    list.Add(TextRow("letters"));
                    break;
                case CustomPieceType.Tube:
                    list.Add(TextRow("word"));
                    list.Add(Whole("rows", p => p.count, (p, v) => p.count = v, 1, 8, 3));
                    break;
                case CustomPieceType.Spring:
                    list.Add(Number("pad width", p => p.value, (p, v) => p.value = v, 8f, 40f, 200f, 80f, "0 px"));
                    break;
                case CustomPieceType.Lift:
                    list.Add(Number("height", p => p.y2 != 0f ? p.y - p.y2 : 300f, (p, v) => p.y2 = p.y - v, 40f, 120f, 520f, 300f, "0 px"));
                    break;
                case CustomPieceType.Hooks:
                    list.Add(Whole("hooks", p => p.count, (p, v) => p.count = v, 1, 8, 1));
                    break;
                case CustomPieceType.CapsLock:
                    list.Add(Number("width", p => p.x2 > p.x ? p.x2 - p.x : 800f, (p, v) => p.x2 = p.x + v, 80f, 160f, 4000f, 800f, "0 px"));
                    break;
                case CustomPieceType.Hill:
                    list.Add(Whole("rows", p => p.count, (p, v) => p.count = v, 1, 6, 3));
                    break;
                case CustomPieceType.Exit:
                    list.Add(TextRow("word (_ is the blank)"));
                    list.Add(Choice("wants", p => p.kind == "A" ? "A" : "a", (p, v) => p.kind = v, "a", "A"));
                    break;
                case CustomPieceType.Enemy:
                    AddEnemy(piece, list);
                    break;
            }
            return list;
        }

        static void AddEnemy(CustomPiece piece, List<PieceProperty> list)
        {
            switch (piece.kind)
            {
                case CustomPieceType.Walker:
                case CustomPieceType.Spike:
                case CustomPieceType.Armour:
                case CustomPieceType.Swapper:
                case CustomPieceType.Mimic:
                    list.Add(Facing("starts walking"));
                    break;
                case CustomPieceType.Hopper:
                    list.Add(Number("side speed", p => p.value, (p, v) => p.value = v, 0.5f, 0f, 4f, 0f, "0.0"));
                    list.Add(Facing("hops"));
                    list.Add(Delay());
                    break;
                case CustomPieceType.Flier:
                    list.Add(Number("travel", p => p.value, (p, v) => p.value = v, 40f, 0f, 600f, 0f, "0 px"));
                    list.Add(Facing("flies"));
                    break;
                case CustomPieceType.Launcher:
                    list.Add(Facing("fires"));
                    list.Add(Whole("rows", p => p.count, (p, v) => p.count = v, 1, 5, 2));
                    list.Add(Number("every", p => p.value, (p, v) => p.value = v, 0.5f, 0.5f, 6f, 2f, "0.0 s"));
                    break;
                case CustomPieceType.Spark:
                    list.Add(Delay());
                    break;
                case CustomPieceType.FireBar:
                    list.Add(Whole("dots", p => p.count, (p, v) => p.count = v, 1, 8, 4));
                    list.Add(Number("turn", p => p.value, (p, v) => p.value = v, 30f, -360f, 360f, -90f, "0 °/s"));
                    break;
                case CustomPieceType.Cursor:
                    list.Add(Number("chase", p => p.x2 > p.x ? p.x2 - p.x : 1500f, (p, v) => p.x2 = p.x + v, 80f, 160f, 6000f, 1500f, "0 px"));
                    list.Add(Number("speed", p => p.value, (p, v) => p.value = v, 0.5f, 1f, 8f, 3.5f, "0.0"));
                    break;
            }
        }

        // ---------------------------------------------------------------- kinds of row

        static PieceProperty TextRow(string label) => new PieceProperty(label, p => p.text, null);

        static PieceProperty Choice(string label, Func<CustomPiece, string> get, Action<CustomPiece, string> set,
            params string[] options)
        {
            return new PieceProperty(label, get, (p, direction) =>
            {
                int index = Array.IndexOf(options, get(p));
                if (index < 0) index = 0;
                set(p, options[(index + direction + options.Length) % options.Length]);
            });
        }

        static PieceProperty Facing(string label) =>
            new PieceProperty(label, p => p.flip ? "left" : "right", (p, direction) => p.flip = !p.flip);

        static PieceProperty Delay() =>
            Number("delay", p => p.delay, (p, v) => p.delay = v, 0.25f, 0f, 5f, 0f, "0.00 s", zeroIsDefault: false);

        /// <summary>A number in a field where 0 means "the default", so 0 is shown and stepped as that default.</summary>
        static PieceProperty Number(string label, Func<CustomPiece, float> get, Action<CustomPiece, float> set,
            float step, float min, float max, float fallback, string format, bool zeroIsDefault = true)
        {
            Func<CustomPiece, float> current = p =>
            {
                float value = get(p);
                return zeroIsDefault && Mathf.Approximately(value, 0f) && !Mathf.Approximately(min, 0f) ? fallback : value;
            };
            return new PieceProperty(label,
                p => current(p).ToString(format, System.Globalization.CultureInfo.InvariantCulture),
                (p, direction) =>
                {
                    float next = Mathf.Clamp(current(p) + direction * step, min, max);
                    // Where 0 means the default, a value passing through 0 skips it.
                    if (zeroIsDefault && Mathf.Approximately(next, 0f) && min < 0f) next += direction * step;
                    set(p, next);
                });
        }

        static PieceProperty Whole(string label, Func<CustomPiece, int> get, Action<CustomPiece, int> set,
            int min, int max, int fallback)
        {
            Func<CustomPiece, int> current = p => get(p) > 0 ? get(p) : fallback;
            return new PieceProperty(label, p => current(p).ToString(),
                (p, direction) => set(p, Mathf.Clamp(current(p) + direction, min, max)));
        }
    }
}
