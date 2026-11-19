using System.Collections.Generic;

namespace LowercaseUppercase
{
    /// <summary>How a palette chip's glyph is drawn: the colour of the piece it places.</summary>
    public enum PaletteLook { Plain, Interactive, Hot, Breakable, Danger, Player, Scenery }

    /// <summary>One tool in the level editor's palette: what its chip shows and which piece it places.</summary>
    public sealed class PaletteItem
    {
        public readonly string Name;
        public readonly string Glyph;
        public readonly PaletteLook Look;
        public readonly string Type;
        public readonly string Kind;
        /// <summary>Placing it starts typing its text, as a word does.</summary>
        public readonly bool Typed;

        public PaletteItem(string name, string glyph, PaletteLook look, string type, string kind = "", bool typed = false)
        {
            Name = name;
            Glyph = glyph;
            Look = look;
            Type = type;
            Kind = kind;
            Typed = typed;
        }
    }

    /// <summary>
    /// The level editor's palette: three pages of tools (words, things, enemies), and the piece each one places with
    /// its starting settings.
    /// </summary>
    public static class EditorPalette
    {
        public static readonly string[] PageNames = { "words", "things", "enemies" };

        public static readonly PaletteItem[][] Pages =
        {
            new[]
            {
                new PaletteItem("word", "abc", PaletteLook.Plain, CustomPieceType.Word, CustomPieceType.Solid, typed: true),
                new PaletteItem("ice word", "ice", PaletteLook.Interactive, CustomPieceType.Word, CustomPieceType.Ice, typed: true),
                new PaletteItem("hot word", "hot", PaletteLook.Hot, CustomPieceType.Word, CustomPieceType.Hot, typed: true),
                new PaletteItem("sticky word", "gum", PaletteLook.Interactive, CustomPieceType.Word, CustomPieceType.Sticky, typed: true),
                new PaletteItem("hyphen bridge", "---", PaletteLook.Interactive, CustomPieceType.Word, CustomPieceType.Plank, typed: true),
                new PaletteItem("brick word (A smashes it)", "brk", PaletteLook.Breakable, CustomPieceType.Word, CustomPieceType.Cracked, typed: true),
                new PaletteItem("floating word (a rises, A sinks)", "f~", PaletteLook.Interactive, CustomPieceType.Word, CustomPieceType.Float, typed: true),
                new PaletteItem("loose letters (! marks a riser)", "a b", PaletteLook.Plain, CustomPieceType.Kerned, typed: true),
                new PaletteItem("brick column", "b|", PaletteLook.Breakable, CustomPieceType.Brick),
                new PaletteItem("sticky wall", "s|", PaletteLook.Interactive, CustomPieceType.StickyWall),
                new PaletteItem("stacked wall", "tub", PaletteLook.Scenery, CustomPieceType.Tube),
            },
            new[]
            {
                new PaletteItem("spring", "!", PaletteLook.Interactive, CustomPieceType.Spring),
                new PaletteItem("door", "d", PaletteLook.Hot, CustomPieceType.Door),
                new PaletteItem("plate (opens the nearest door)", ".", PaletteLook.Interactive, CustomPieceType.Plate),
                new PaletteItem("lift", "up", PaletteLook.Interactive, CustomPieceType.Lift),
                new PaletteItem("comma hooks", ",", PaletteLook.Interactive, CustomPieceType.Hooks),
                new PaletteItem("Caps Lock stretch", "LCK", PaletteLook.Hot, CustomPieceType.CapsLock),
                new PaletteItem("Caps Lock key", "key", PaletteLook.Player, CustomPieceType.CapsKey),
                new PaletteItem("wind (blows a away, not A)", "»»", PaletteLook.Interactive, CustomPieceType.Wind),
                new PaletteItem("checkpoint", ";", PaletteLook.Scenery, CustomPieceType.Checkpoint),
                new PaletteItem("power block", "[*]", PaletteLook.Interactive, CustomPieceType.Power),
                new PaletteItem("#", "#", PaletteLook.Player, CustomPieceType.Hash),
                new PaletteItem("exit word", "c_t", PaletteLook.Player, CustomPieceType.Exit),
                new PaletteItem("hill", "^^", PaletteLook.Scenery, CustomPieceType.Hill),
                new PaletteItem("cloud", "~~", PaletteLook.Scenery, CustomPieceType.Cloud),
            },
            new[]
            {
                Enemy("walker", "&", CustomPieceType.Walker),
                Enemy("spike", "^", CustomPieceType.Spike),
                Enemy("armour", "§", CustomPieceType.Armour),
                Enemy("hopper", "!", CustomPieceType.Hopper),
                Enemy("flier", "~", CustomPieceType.Flier),
                Enemy("launcher", "«", CustomPieceType.Launcher),
                Enemy("drip", "'", CustomPieceType.Drip),
                Enemy("spark (on a hot word)", "°", CustomPieceType.Spark),
                Enemy("fire bar", "o·", CustomPieceType.FireBar),
                Enemy("eye", "ö", CustomPieceType.Eye),
                Enemy("curl", "@", CustomPieceType.Curl),
                Enemy("case swapper", "↕", CustomPieceType.Swapper),
                Enemy("mimic", "b", CustomPieceType.Mimic),
                Enemy("cursor", "|", CustomPieceType.Cursor),
            },
        };

        static PaletteItem Enemy(string name, string glyph, string kind) =>
            new PaletteItem(name, glyph, PaletteLook.Danger, CustomPieceType.Enemy, kind);

        public static IEnumerable<PaletteItem> All
        {
            get
            {
                foreach (PaletteItem[] page in Pages)
                    foreach (PaletteItem item in page)
                        yield return item;
            }
        }

        /// <summary>The palette item that places pieces like this one, for its name.</summary>
        public static PaletteItem For(CustomPiece piece)
        {
            foreach (PaletteItem item in All)
                if (item.Type == piece.type && (item.Kind == "" || item.Kind == piece.kind)) return item;
            return null;
        }

        public static string NameOf(CustomPiece piece) => For(piece)?.Name ?? piece.type;

        /// <summary>A new piece of this tool at a page position, with the settings it starts with.</summary>
        public static CustomPiece NewPiece(PaletteItem item, float x, float y)
        {
            var piece = new CustomPiece(item.Type, x, y, "", item.Kind);
            switch (item.Type)
            {
                case CustomPieceType.Brick: piece.text = "brickbrick"; break;
                case CustomPieceType.StickyWall: piece.text = "sticky"; break;
                case CustomPieceType.Tube: piece.text = "tube"; piece.count = 3; break;
                case CustomPieceType.Lift: piece.y2 = y - 300f; break;
                case CustomPieceType.Word when item.Kind == CustomPieceType.Float: piece.y2 = y - 270f; break;
                case CustomPieceType.Hooks: piece.count = 3; break;
                case CustomPieceType.CapsLock: piece.x2 = x + 800f; break;
                case CustomPieceType.Wind: piece.x2 = x + Levels.PrototypeBuilder.DefaultWindPx; break;
                case CustomPieceType.Hill: piece.count = 3; break;
                case CustomPieceType.Exit: piece.text = "c_t"; piece.kind = "a"; break;
                case CustomPieceType.Enemy:
                    switch (item.Kind)
                    {
                        case CustomPieceType.Flier: piece.value = 120f; break;
                        // The letter comes from the left, so a launcher fires that way.
                        case CustomPieceType.Launcher: piece.flip = true; piece.count = 2; break;
                        case CustomPieceType.Cursor: piece.x2 = x + 1200f; break;
                    }
                    break;
            }
            return piece;
        }

        /// <summary>Words and loose letters: the ground that other pieces are put on.</summary>
        public static bool IsGround(CustomPiece piece) =>
            piece.type == CustomPieceType.Word || piece.type == CustomPieceType.Kerned;

        /// <summary>
        /// Pieces that stand on a surface, so the editor drops them onto the word below where they are put. Walkers
        /// and sparks find their word as the level is built anyway; this keeps the saved position honest too.
        /// </summary>
        public static bool Stands(CustomPiece piece)
        {
            switch (piece.type)
            {
                case CustomPieceType.Spring:
                case CustomPieceType.Door:
                case CustomPieceType.Plate:
                case CustomPieceType.Checkpoint:
                case CustomPieceType.Exit:
                case CustomPieceType.Brick:
                case CustomPieceType.StickyWall:
                case CustomPieceType.Tube:
                    return true;
                case CustomPieceType.Enemy:
                    switch (piece.kind)
                    {
                        case CustomPieceType.Flier:
                        case CustomPieceType.Eye:
                        case CustomPieceType.Drip:
                            return false;
                        default:
                            return true;
                    }
                default:
                    return false;
            }
        }

        /// <summary>Whether a piece's text can be typed: words, loose letters, columns and the exit word.</summary>
        public static bool HasText(CustomPiece piece)
        {
            switch (piece.type)
            {
                case CustomPieceType.Word:
                case CustomPieceType.Kerned:
                case CustomPieceType.Brick:
                case CustomPieceType.StickyWall:
                case CustomPieceType.Tube:
                case CustomPieceType.Exit:
                    return true;
                default:
                    return false;
            }
        }
    }
}
