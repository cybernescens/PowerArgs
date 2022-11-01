﻿using System.Text.RegularExpressions;

namespace PowerArgs.Cli;

/// <summary>
///     This class defines a text format for a serialized ConsoleBitmap. The goal is for the output to visually look
///     like the bitmap, but without losing the color info.
/// </summary>
public class ConsoleBitmapVisualSerializer
{
    /// <summary>
    ///     Serializes the given bitmap into a string that will visually look like the given bitmap, with color information
    ///     included in the output
    /// </summary>
    /// <param name="bmp">the image to serialize</param>
    /// <returns>the serialized image as a string</returns>
    public static string Serialize(ConsoleBitmap bmp)
    {
        var pallate = ColorPalette.FromBitmap(bmp);
        var ret = "";

        // horizontal line of hashes
        var bar = "";
        for (var x = 0; x < bmp.Width + 2; x++) bar += "#";

        ret += $"{bar}\n";

        for (var y = 0; y < bmp.Height; y++)
        {
            var visuals = "#";
            var colors = "#    ";
            for (var x = 0; x < bmp.Width; x++)
            {
                var pix = bmp.GetPixel(x, y);
                var fg = pix.ForegroundColor;
                var bg = pix.BackgroundColor;
                var val = pix.Value;

                visuals += val;
                colors += pallate.LookupFormatted(fg, bg);
                if (x < bmp.Width - 1) colors += "  ";
            }

            ret += visuals + colors + "\n";
        }

        // horizontal line of hashes
        ret += $"{bar}\n";
        ret += pallate.Serialize();
        return ret;
    }

    /// <summary>
    ///     Deserializes the given string into a ConsoleBitmap
    /// </summary>
    /// <param name="s">the serialized string</param>
    /// <returns>the deserialized image</returns>
    public static ConsoleBitmap Deserialize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) throw new ArgumentNullException("s cannot be null or whitespace");

        var lines = s.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n').Where(l => string.IsNullOrEmpty(l) == false)
            .ToArray();

        var width = lines[0].LastIndexOf("#", StringComparison.Ordinal) - 1; // minus 1 for the hashes that surround the image
        if (width < 0) throw new FormatException("Tab hash not found at line 1");

        var height = FindHeight(lines);

        var valueLines = lines.Skip(1).Take(height).ToArray();
        var pallateLines = lines.Skip(height + 2).ToArray();
        var pallate = ColorPalette.Deserialize(pallateLines);
        var defaultPallateColorCode = pallate.EnumeratePallate()[0].Key;

        var ret = new ConsoleBitmap(width, height);
        for (var lineIndex = 0; lineIndex < valueLines.Length; lineIndex++)
        {
            var line = valueLines[lineIndex].Substring(1); // substring removes the leading hash
            var values = line.Substring(0, width);
            var colors = line.Substring(width + 1).Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries); // width + 1 to remove trailing hash

            for (var x = 0; x < width; x++)
            {
                var character = line[x];
                // Conditional statement below allows text that doesn't have the color informatio to be serialized.
                // This is really useful since you want to be able to sketch your image in a normal text editor without having to
                // format the colors by hand. By leaving the color information out of your sketch (still surrounded with hashes) you
                // can import that image and then serialize it out. On the way out the serializer will add default colors for you.
                // You can then happily edit from there.
                var colorCode = colors.Length == width ? int.Parse(colors[x]) : defaultPallateColorCode;
                var lookedUp = pallate.Lookup(colorCode);
                ret.Pixels[x, lineIndex] = new ConsoleCharacter(
                    character,
                    lookedUp.ForegroundColor,
                    lookedUp.BackgroundColor);
            }
        }

        return ret;
    }

    /// <summary>
    ///     Finds the height of the image by looking between the top and bottom bars of hashes
    /// </summary>
    /// <param name="lines">the lines of text that make up the image</param>
    /// <returns>the height of the image</returns>
    private static int FindHeight(string[] lines)
    {
        var height = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            var isEnd = Regex.IsMatch(line, @"^#+$");
            if (isEnd)
            {
                height = i - 1;
                break;
            }
        }

        if (height < 0) throw new FormatException("bottom line of hashes not found");

        return height;
    }

    private class ColorPalette
    {
        private readonly Dictionary<int, ColorPalleteEntry> map = new();
        private int nextId;
        private readonly Dictionary<ColorPalleteEntry, int> reverseMap = new();

        public int Add(RGB fg, RGB bg)
        {
            var toCheck = new ColorPalleteEntry { ForegroundColor = fg, BackgroundColor = bg };
            if (reverseMap.TryGetValue(toCheck, out var ret) == false)
            {
                ret = nextId++;
                map.Add(ret, toCheck);
                reverseMap.Add(toCheck, ret);
                return ret;
            }

            return ret;
        }

        public void Set(int key, in RGB fg, in RGB bg)
        {
            if (map.TryGetValue(key, out var entry) == false)
            {
                var toSet = new ColorPalleteEntry { ForegroundColor = fg, BackgroundColor = bg };
                map.Add(key, toSet);
                reverseMap.Add(toSet, key);
                nextId = map.Keys.Max() + 1;
            }
            else
            {
                throw new InvalidOperationException("Key already set: " + key);
            }
        }

        public KeyValuePair<int, ColorPalleteEntry>[] EnumeratePallate() => map.OrderBy(e => e.Key).ToArray();

        public int Lookup(ColorPalleteEntry entry) => reverseMap[entry];

        public int Lookup(in RGB fg, in RGB bg) =>
            reverseMap[new ColorPalleteEntry { ForegroundColor = fg, BackgroundColor = bg }];

        public string Format(int code)
        {
            var maxColors = map.Count;
            var paddedLength = maxColors < 10
                ? 1
                : maxColors < 100
                    ? 2
                    : maxColors < 1000
                        ? 3
                        : 4;

            var codeString = code + "";
            while (codeString.Length < paddedLength)
            {
                codeString = "0" + codeString;
            }

            return codeString;
        }

        public string LookupFormatted(in RGB fg, in RGB bg) => Format(Lookup(fg, bg));
        public ColorPalleteEntry Lookup(int id) => map[id];

        public string Serialize()
        {
            var ret = "";
            foreach (var entry in EnumeratePallate())
            {
                var formattedEntry = $"{Format(entry.Key)}";
                var fgVal = entry.Value.ForegroundColor.ToString();
                var bgVal = entry.Value.BackgroundColor.ToString();

                var paddedBgValue = "  " + bgVal;
                while (fgVal.Length + paddedBgValue.Length < 20)
                {
                    paddedBgValue = paddedBgValue = " " + paddedBgValue;
                }

                ret += $"{Format(entry.Key)}  {fgVal}{paddedBgValue}\n";
            }

            return ret;
        }

        public static ColorPalette Deserialize(string[] lines)
        {
            var ret = new ColorPalette();
            foreach (var line in lines)
            {
                string?[] split = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (split.Length != 3) throw new FormatException($"Could not parse pallate line: '{line}'");

                var formattedCode = split[0];
                ret.Set(int.Parse(formattedCode), RGB.Parse(split[1]), RGB.Parse(split[2]));
            }

            if (ret.EnumeratePallate().None())
            {
                ret.Add(RGB.White, RGB.Black);
            }

            return ret;
        }

        public static ColorPalette FromBitmap(ConsoleBitmap bmp)
        {
            var ret = new ColorPalette();
            for (var y = 0; y < bmp.Height; y++)
            {
                for (var x = 0; x < bmp.Width; x++)
                {
                    var pixel = bmp.GetPixel(x, y);
                    var fg = pixel.ForegroundColor;
                    var bg = pixel.BackgroundColor;
                    ret.Add(fg, bg);
                }
            }

            return ret;
        }
    }

    private class ColorPalleteEntry
    {
        public RGB ForegroundColor { get; set; }
        public RGB BackgroundColor { get; set; }

        public override string ToString() => $"{ForegroundColor}|{BackgroundColor}";

        public static ColorPalleteEntry Parse(string s)
        {
            string?[] colorInfoSplit = s.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            if (colorInfoSplit.Length != 2) throw new FormatException($"Could not parse color pallate entry '{s}'");

            return new ColorPalleteEntry {
                ForegroundColor = RGB.Parse(colorInfoSplit[0]),
                BackgroundColor = RGB.Parse(colorInfoSplit[1])
            };
        }

        public override bool Equals(object obj)
        {
            var other = obj as ColorPalleteEntry;
            if (other == null) return false;

            return ForegroundColor.Equals(other.ForegroundColor) && BackgroundColor.Equals(other.BackgroundColor);
        }

        public override int GetHashCode() => $"{ForegroundColor}-{BackgroundColor}".GetHashCode();
    }
}