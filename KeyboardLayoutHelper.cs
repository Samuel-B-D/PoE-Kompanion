namespace PoEKompanion;

using System;
using System.Collections.Generic;
using System.Diagnostics;

public static class KeyboardLayoutHelper
{
    private static readonly Dictionary<string, char> XKeysymToChar = new()
    {
        {"slash", '/'}, {"backslash", '\\'}, {"space", ' '},
        {"exclam", '!'}, {"at", '@'}, {"numbersign", '#'}, {"dollar", '$'},
        {"percent", '%'}, {"asciicircum", '^'}, {"ampersand", '&'},
        {"asterisk", '*'}, {"parenleft", '('}, {"parenright", ')'},
        {"minus", '-'}, {"underscore", '_'}, {"equal", '='}, {"plus", '+'},
        {"bracketleft", '['}, {"bracketright", ']'}, {"braceleft", '{'}, {"braceright", '}'},
        {"semicolon", ';'}, {"colon", ':'}, {"apostrophe", '\''}, {"quotedbl", '"'},
        {"comma", ','}, {"less", '<'}, {"period", '.'}, {"greater", '>'},
        {"question", '?'}, {"grave", '`'}, {"asciitilde", '~'}, {"bar", '|'},
    };

    public static Dictionary<char, KeycodMapping> BuildLayoutMap()
    {
        var layoutMap = new Dictionary<char, KeycodMapping>();

        try
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "xmodmap",
                    Arguments = "-pke",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            var error = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (!string.IsNullOrWhiteSpace(error))
            {
                Console.WriteLine($"xmodmap error: {error}");
            }

            foreach (var line in output.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(new[] { ' ', '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3 || parts[0] != "keycode") continue;

                if (!int.TryParse(parts[1], out var x11Keycode)) continue;

                // Convert X11 keycode to Linux input keycode
                var linuxKeycode = x11Keycode - 8;

                if (parts.Length > 2)
                {
                    var unshiftedSym = parts[2];
                    char unshiftedChar;

                    if (unshiftedSym.Length == 1)
                    {
                        unshiftedChar = unshiftedSym[0];
                    }
                    else if (XKeysymToChar.TryGetValue(unshiftedSym, out var mappedChar))
                    {
                        unshiftedChar = mappedChar;
                    }
                    else
                    {
                        unshiftedChar = '\0';
                    }

                    if (unshiftedChar != '\0' && !layoutMap.ContainsKey(unshiftedChar))
                    {
                        layoutMap[unshiftedChar] = new KeycodMapping(linuxKeycode, false);
                    }
                }

                if (parts.Length > 3)
                {
                    var shiftedSym = parts[3];
                    char shiftedChar;

                    if (shiftedSym.Length == 1)
                    {
                        shiftedChar = shiftedSym[0];
                    }
                    else if (XKeysymToChar.TryGetValue(shiftedSym, out var mappedChar))
                    {
                        shiftedChar = mappedChar;
                    }
                    else
                    {
                        shiftedChar = '\0';
                    }

                    if (shiftedChar != '\0' && !layoutMap.ContainsKey(shiftedChar))
                    {
                        layoutMap[shiftedChar] = new KeycodMapping(linuxKeycode, true);
                    }
                }
            }

            Console.WriteLine($"Built layout map with {layoutMap.Count} character mappings");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to build layout map from xmodmap: {ex.Message}");
        }

        return layoutMap;
    }
}
