using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace LowercaseUppercase
{
    /// <summary>
    /// A level as one line of text to paste anywhere: "luc1:" and the level's JSON, gzipped, in base64. Where gzip
    /// isn't available, "luc0:" and the JSON in plain base64; reading takes both.
    /// </summary>
    public static class LevelShareCode
    {
        public const string Prefix = "luc1:";
        public const string PlainPrefix = "luc0:";
        /// <summary>Far more than any level made in the editor; guards against pasting something huge.</summary>
        public const int MaxJsonBytes = 1 << 20;

        public static string Encode(CustomLevelData level)
        {
            byte[] json = Encoding.UTF8.GetBytes(level.ToJson(false));
            try
            {
                using (var packed = new MemoryStream())
                {
                    using (var zip = new GZipStream(packed, CompressionLevel.Optimal, leaveOpen: true))
                        zip.Write(json, 0, json.Length);
                    return Prefix + Convert.ToBase64String(packed.ToArray());
                }
            }
            catch (Exception e) when (e is NotSupportedException || e is DllNotFoundException || e is EntryPointNotFoundException)
            {
                return PlainPrefix + Convert.ToBase64String(json);
            }
        }

        /// <summary>Reads a share code, or throws a FormatException saying what is wrong with it.</summary>
        public static CustomLevelData Decode(string code)
        {
            string text = StripSpaces(code ?? "");
            if (text.Length == 0) throw new FormatException("There's no code to import.");

            bool packed;
            if (text.StartsWith(Prefix, StringComparison.Ordinal)) packed = true;
            else if (text.StartsWith(PlainPrefix, StringComparison.Ordinal)) packed = false;
            else throw new FormatException("That isn't a level code. Codes start with \"luc\".");

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(text.Substring(Prefix.Length));
            }
            catch (FormatException)
            {
                throw new FormatException("The code is cut short or has extra characters in it.");
            }

            string json;
            try
            {
                json = Encoding.UTF8.GetString(packed ? Unpack(bytes) : bytes);
            }
            catch (Exception e) when (e is InvalidDataException || e is IOException || e is ArgumentException)
            {
                throw new FormatException("The code is cut short or has extra characters in it.");
            }
            return CustomLevelData.FromJson(json);
        }

        static byte[] Unpack(byte[] bytes)
        {
            using (var zip = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress))
            using (var json = new MemoryStream())
            {
                var buffer = new byte[8192];
                int read;
                while ((read = zip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    json.Write(buffer, 0, read);
                    if (json.Length > MaxJsonBytes) throw new FormatException("The code holds far more than a level.");
                }
                return json.ToArray();
            }
        }

        // Codes pasted from chats and mails often come broken over lines.
        static string StripSpaces(string code)
        {
            var text = new StringBuilder(code.Length);
            foreach (char c in code)
                if (!char.IsWhiteSpace(c)) text.Append(c);
            return text.ToString();
        }
    }
}
