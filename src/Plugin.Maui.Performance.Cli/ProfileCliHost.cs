using System.Diagnostics;
using Plugin.Maui.Performance;

namespace Plugin.Maui.Performance.Cli;

/// <summary>
/// Runs parsed <c>maui-perf</c> commands by delegating to the <c>maui</c> CLI.
/// </summary>
public static class ProfileCliHost
{
    public static int Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr, Func<string[], int>? invokeMaui = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        var parsed = ProfileCliParser.Parse(args);
        if (parsed.ShowHelp)
        {
            stdout.WriteLine(ProfileCliParser.Usage);
            return 0;
        }

        if (parsed.Error is not null)
        {
            stderr.WriteLine(parsed.Error);
            stderr.WriteLine();
            stderr.WriteLine(ProfileCliParser.Usage);
            return 1;
        }

        var request = parsed.Request ?? throw new InvalidOperationException("Missing session request.");
        string command;
        try
        {
            command = MauiProfileCommand.Format(request);
        }
        catch (ArgumentException exception)
        {
            stderr.WriteLine(exception.Message);
            return 1;
        }

        stdout.WriteLine(command);
        if (parsed.PrintOnly)
        {
            return 0;
        }

        var arguments = MauiProfileCommand.BuildArguments(request);
        var runner = invokeMaui ?? InvokeMaui;
        return runner([.. arguments]);
    }

    static int InvokeMaui(string[] arguments)
    {
        var maui = ResolveMaui();
        if (maui is null)
        {
            Console.Error.WriteLine("The 'maui' CLI was not found on PATH.");
            Console.Error.WriteLine("Install .NET MAUI developer tools, then retry. See");
            Console.Error.WriteLine("https://learn.microsoft.com/en-us/dotnet/maui/developer-tools/cli/profile");
            return 127;
        }

        var start = new ProcessStartInfo
        {
            FileName = maui,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start);
        if (process is null)
        {
            Console.Error.WriteLine($"Failed to start '{maui}'.");
            return 127;
        }

        process.WaitForExit();
        return process.ExitCode;
    }

    static string? ResolveMaui()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            "maui",
            Path.Combine(home, ".dotnet", "tools", "maui"),
            Path.Combine(home, ".dotnet", "tools", "maui.exe"),
            Path.Combine(home, ".maui", "maui"),
            Path.Combine(home, ".maui", "maui.exe")
        };

        foreach (var candidate in candidates)
        {
            if (LooksRunnable(candidate))
            {
                return candidate;
            }
        }

        return "maui";
    }

    static bool LooksRunnable(string path)
    {
        if (string.Equals(path, "maui", StringComparison.Ordinal))
        {
            return false;
        }

        return File.Exists(path);
    }
}
