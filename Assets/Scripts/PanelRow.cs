using System;

namespace LowercaseUppercase
{
    /// <summary>
    /// One row of the level editor's panel, already bound to what it shows: a piece's setting or the level's.
    /// A row with <see cref="Step"/> has arrows; a text row has <see cref="Edit"/> instead, which starts typing.
    /// </summary>
    public sealed class PanelRow
    {
        public readonly string Label;
        public readonly Func<string> Show;
        public readonly Action<int> Step;
        public readonly Action Edit;
        public bool IsText => Step == null;

        public PanelRow(string label, Func<string> show, Action<int> step, Action edit = null)
        {
            Label = label;
            Show = show;
            Step = step;
            Edit = edit;
        }
    }
}
