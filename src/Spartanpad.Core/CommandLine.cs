namespace Spartanpad.Core;

public enum PrintMode
{
    None,
    DefaultPrinter,
    NamedPrinter,
}

/// <summary>
/// The reference command line (§8): <c>[/A | /W] [/P | /PT printer] [file]</c>. Switches may use
/// <c>/</c> or <c>-</c> and are case-insensitive. Anything after the file is kept in
/// <see cref="Rest"/>.
/// </summary>
public sealed record CommandLine(
    TextEncodingKind? ForcedEncoding,
    PrintMode PrintMode,
    string? PrinterName,
    string? FilePath,
    IReadOnlyList<string> Rest)
{
    public static CommandLine Parse(IReadOnlyList<string> args)
    {
        TextEncodingKind? forced = null;
        var print = PrintMode.None;
        string? printer = null;
        string? file = null;
        var rest = new List<string>();
        int i = 0;

        while (i < args.Count && file is null)
        {
            string arg = args[i];
            if (arg.Length >= 2 && (arg[0] == '/' || arg[0] == '-'))
            {
                string sw = arg[1..].ToUpperInvariant();
                switch (sw)
                {
                    case "A":
                        forced = TextEncodingKind.Ansi;
                        i++;
                        continue;
                    case "W":
                        forced = TextEncodingKind.Utf16LE;
                        i++;
                        continue;
                    case "P":
                        print = PrintMode.DefaultPrinter;
                        i++;
                        continue;
                    case "PT":
                        print = PrintMode.NamedPrinter;
                        i++;
                        if (i < args.Count)
                        {
                            file = args[i++];
                        }

                        if (i < args.Count)
                        {
                            printer = args[i++];
                        }

                        continue;
                    case ".SETUP":
                        i++;
                        continue;
                }
            }

            file = arg;
            i++;
        }

        for (; i < args.Count; i++)
        {
            rest.Add(args[i]);
        }

        return new CommandLine(forced, print, printer, file, rest);
    }
}
