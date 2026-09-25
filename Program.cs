using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuestAdbMenu;

/// <summary>
/// One entry in the menu tree. Either has Children (a submenu) or Commands (a leaf
/// that runs one or more shell commands in sequence). Never both, never neither.
/// </summary>
public class MenuNode
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("children")]
    public List<MenuNode>? Children { get; set; }

    [JsonPropertyName("commands")]
    public List<string>? Commands { get; set; }

    [JsonIgnore]
    public bool IsLeaf => Commands is { Count: > 0 };
}

public static class Program
{
    private const string DefaultFileName = "commands.json";

    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.CursorVisible = false;

        // Windows-only: if "adb" isn't recognized on PATH, fall back to an adb.exe
        // sitting next to the executable. Linux is left untouched — see AdbLocator.
        var adbFallback = AdbLocator.ResolveLocalAdbDirectory();
        if (adbFallback is not null)
        {
            ShellRunner.ExtraPathDirectory = adbFallback;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("'adb' wasn't found on PATH — using the adb.exe bundled next to this app instead.");
            Console.ResetColor();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }

        MenuNode root;
        try
        {
            root = LoadMenu(ResolveCommandsFilePath(args));
        }
        catch (Exception ex)
        {
            Console.CursorVisible = true;
            Console.WriteLine($"Failed to load menu definitions: {ex.Message}");
            return 1;
        }

        try
        {
            RunMenuLoop(root);
        }
        finally
        {
            Console.CursorVisible = true;
            Console.ResetColor();
            Console.Clear();
        }

        return 0;
    }

    // ---------------------------------------------------------------
    // Loading
    // ---------------------------------------------------------------

    private static string ResolveCommandsFilePath(string[] args)
    {
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            return args[0];

        return Path.Combine(AppContext.BaseDirectory, DefaultFileName);
    }

    private static MenuNode LoadMenu(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Could not find '{path}'. Keep commands.json next to the executable, " +
                "or pass a path to it as the first command line argument.");
        }

        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        var root = JsonSerializer.Deserialize<MenuNode>(json, options)
                   ?? throw new InvalidDataException("commands.json parsed to an empty menu.");

        if (string.IsNullOrWhiteSpace(root.Title))
            root.Title = "Menu";

        return root;
    }

    // ---------------------------------------------------------------
    // Menu navigation
    // ---------------------------------------------------------------

    private static void RunMenuLoop(MenuNode root)
    {
        var menuStack = new Stack<MenuNode>();
        menuStack.Push(root);
        var selectionStack = new Stack<int>();
        selectionStack.Push(0);

        while (true)
        {
            var current = menuStack.Peek();
            var items = current.Children ?? new List<MenuNode>();
            int selected = selectionStack.Peek();

            if (items.Count == 0)
            {
                DrawFrame(menuStack, items, 0);
                Console.WriteLine();
                Console.WriteLine("  (this submenu is empty)");
                Console.WriteLine();
                Console.WriteLine("  [Backspace] Back   [Q] Quit");
                var k = Console.ReadKey(true).Key;
                if (k == ConsoleKey.Backspace && menuStack.Count > 1)
                {
                    menuStack.Pop();
                    selectionStack.Pop();
                }
                else if (k == ConsoleKey.Q)
                {
                    return;
                }
                continue;
            }

            selected = Math.Clamp(selected, 0, items.Count - 1);
            DrawFrame(menuStack, items, selected);

            var key = Console.ReadKey(true).Key;
            switch (key)
            {
                case ConsoleKey.UpArrow:
                case ConsoleKey.K:
                    selected = (selected - 1 + items.Count) % items.Count;
                    selectionStack.Pop();
                    selectionStack.Push(selected);
                    break;

                case ConsoleKey.DownArrow:
                case ConsoleKey.J:
                    selected = (selected + 1) % items.Count;
                    selectionStack.Pop();
                    selectionStack.Push(selected);
                    break;

                case ConsoleKey.Enter:
                case ConsoleKey.RightArrow:
                case ConsoleKey.L:
                    {
                        var chosen = items[selected];
                        if (chosen.IsLeaf)
                        {
                            RunLeaf(chosen);
                        }
                        else if (chosen.Children is { Count: > 0 })
                        {
                            menuStack.Push(chosen);
                            selectionStack.Push(0);
                        }
                        else
                        {
                            menuStack.Push(chosen); // empty submenu, still navigable/back-able
                            selectionStack.Push(0);
                        }
                        break;
                    }

                case ConsoleKey.Backspace:
                case ConsoleKey.LeftArrow:
                case ConsoleKey.Escape:
                    if (menuStack.Count > 1)
                    {
                        menuStack.Pop();
                        selectionStack.Pop();
                    }
                    break;

                case ConsoleKey.Q:
                    return;
            }
        }
    }

    private static void DrawFrame(Stack<MenuNode> menuStack, List<MenuNode> items, int selected)
    {
        Console.Clear();
        var breadcrumb = string.Join("  >  ", menuStack.Reverse().Select(m => m.Title));

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(breadcrumb);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(new string('-', Math.Min(Console.WindowWidth - 1, 78)));
        Console.ResetColor();
        Console.WriteLine();

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            string marker = item.IsLeaf ? " " : ">";
            string prefix = i == selected ? " -> " : "    ";

            if (i == selected)
            {
                Console.BackgroundColor = ConsoleColor.DarkBlue;
                Console.ForegroundColor = ConsoleColor.White;
            }

            Console.Write(prefix);
            Console.Write(marker);
            Console.Write(' ');
            Console.Write(item.Title);

            if (item.IsLeaf && item.Commands!.Count > 1)
                Console.Write($"  [{item.Commands.Count} commands]");

            Console.ResetColor();
            Console.WriteLine();
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Up/Down or J/K: move    Enter/Right: select    Backspace/Left: back    Q: quit");
        Console.ResetColor();
    }

    // ---------------------------------------------------------------
    // Running an entry's ADB commands
    // ---------------------------------------------------------------

    private static void RunLeaf(MenuNode leaf)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Run: {leaf.Title}");
        Console.ResetColor();
        Console.WriteLine();

        foreach (var cmd in leaf.Commands!)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"$ {cmd}");
            Console.ResetColor();

            var result = ShellRunner.Run(cmd);

            if (!string.IsNullOrWhiteSpace(result.StdOut))
                Console.WriteLine(result.StdOut.TrimEnd());
            if (!string.IsNullOrWhiteSpace(result.StdErr))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(result.StdErr.TrimEnd());
                Console.ResetColor();
            }

            if (result.ExitCode != 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"(exit code {result.ExitCode})");
                Console.ResetColor();
            }

            Console.WriteLine();
        }

        Console.WriteLine("Done. Press any key to go back...");
        Console.ReadKey(true);
    }
}

/// <summary>
/// Locates a fallback adb.exe next to the executable, for Windows machines where
/// "adb" isn't on PATH (e.g. Platform Tools installed but never added to PATH).
/// Deliberately Windows-only: Linux distros install adb through the package
/// manager, which already puts it on PATH, so this never touches Linux behavior.
/// </summary>
public static class AdbLocator
{
    private const string BundledAdbFileName = "adb.exe";

    public static string? ResolveLocalAdbDirectory()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        if (IsAdbOnPath())
            return null;

        var candidate = Path.Combine(AppContext.BaseDirectory, BundledAdbFileName);
        return File.Exists(candidate) ? AppContext.BaseDirectory : null;
    }

    private static bool IsAdbOnPath()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("adb");

            using var process = Process.Start(psi);
            if (process is null)
                return false;

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            // If "where" itself can't be found or run, assume adb isn't reliably
            // resolvable either and let the bundled-exe fallback take over.
            return false;
        }
    }
}

public readonly record struct ShellResult(int ExitCode, string StdOut, string StdErr);

/// <summary>
/// Runs a command line through the platform's shell so pipes/quoting in commands.json
/// (e.g. "adb shell \"getprop | grep oculus\"") behave the same on Windows and Linux.
/// </summary>
public static class ShellRunner
{
    /// <summary>
    /// When set (Windows-only fallback, see AdbLocator), this directory is prepended
    /// to the child process's PATH so a bundled adb.exe is found even though "adb"
    /// isn't resolvable on the system PATH. Left null on Linux, so Linux processes
    /// inherit PATH unmodified.
    /// </summary>
    public static string? ExtraPathDirectory { get; set; }

    public static ShellResult Run(string commandLine)
    {
        var psi = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (OperatingSystem.IsWindows())
        {
            psi.FileName = "cmd.exe";
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(commandLine);
        }
        else
        {
            psi.FileName = "/bin/bash";
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(commandLine);
        }

        if (OperatingSystem.IsWindows() && ExtraPathDirectory is not null)
        {
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            psi.EnvironmentVariables["PATH"] = $"{ExtraPathDirectory};{currentPath}";
        }

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
                return new ShellResult(-1, "", "Failed to start process.");

            string stdOut = process.StandardOutput.ReadToEnd();
            string stdErr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return new ShellResult(process.ExitCode, stdOut, stdErr);
        }
        catch (Exception ex)
        {
            return new ShellResult(-1, "", $"Failed to run command: {ex.Message}");
        }
    }
}
