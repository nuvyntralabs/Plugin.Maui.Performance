using System.Globalization;
using System.Text;

namespace Plugin.Maui.Performance;

/// <summary>
/// Builds <c>maui profile</c> argument lists with sensible defaults
/// (framework aliases, <c>30s</c> durations, startup stopping events).
/// </summary>
public static class MauiProfileCommand
{
    /// <summary>Official EventSource name used by <c>maui profile startup</c>.</summary>
    public const string OfficialProviderName = "Microsoft.Maui.ProfilingHelper";

    /// <summary>Official event that stops a startup trace.</summary>
    public const string OfficialStartupEventName = "StartupComplete";

    /// <summary>Maps <c>android</c> / <c>ios</c> (and TFMs) to a MAUI target framework.</summary>
    public static string ResolveFramework(string? alias)
    {
        var value = string.IsNullOrWhiteSpace(alias) ? "android" : alias.Trim();
        if (value.Contains('-', StringComparison.Ordinal) ||
            value.StartsWith("net", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return value.ToLowerInvariant() switch
        {
            "android" or "and" or "droid" => "net10.0-android",
            "ios" or "iphone" or "simulator" => "net10.0-ios",
            _ => throw new ArgumentException(
                $"Unknown framework '{alias}'. Use android, ios, net10.0-android, or net10.0-ios.",
                nameof(alias))
        };
    }

    /// <summary>
    /// Parses <c>30s</c>, <c>2m</c>, <c>1h</c>, a raw second count, or <c>hh:mm:ss</c>.
    /// </summary>
    public static TimeSpan? ParseDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (TimeSpan.TryParseExact(text, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var exact))
        {
            return exact;
        }

        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var parsed) && text.Contains(':', StringComparison.Ordinal))
        {
            return parsed;
        }

        var suffix = 0;
        for (var i = text.Length - 1; i >= 0; i--)
        {
            if (char.IsLetter(text[i]))
            {
                suffix++;
                continue;
            }

            break;
        }

        var numberPart = suffix == 0 ? text : text[..^suffix];
        var unit = suffix == 0 ? "s" : text[^suffix..];
        if (!double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) ||
            amount < 0)
        {
            throw new ArgumentException(
                $"Duration '{value}' is not hh:mm:ss, a second count, or a value like 30s / 2m / 1h.",
                nameof(value));
        }

        return unit.ToLowerInvariant() switch
        {
            "s" or "sec" or "secs" or "second" or "seconds" => TimeSpan.FromSeconds(amount),
            "m" or "min" or "mins" or "minute" or "minutes" => TimeSpan.FromMinutes(amount),
            "h" or "hr" or "hrs" or "hour" or "hours" => TimeSpan.FromHours(amount),
            _ => throw new ArgumentException(
                $"Unknown duration unit in '{value}'. Use s, m, or h.",
                nameof(value))
        };
    }

    /// <summary>Formats a duration as <c>hh:mm:ss</c> for <c>--duration</c>.</summary>
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        var total = (int)Math.Ceiling(duration.TotalSeconds);
        var hours = total / 3600;
        var minutes = total % 3600 / 60;
        var seconds = total % 60;
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
    }

    /// <summary>Builds the argument list passed to the <c>maui</c> executable (no executable name).</summary>
    public static IReadOnlyList<string> BuildArguments(MauiProfileSessionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var args = new List<string>
        {
            "profile",
            request.Mode == MauiProfileMode.Manual ? "manual" : "startup"
        };

        AddOption(args, "--project", request.Project);
        AddOption(args, "--framework", ResolveFramework(request.Framework));
        AddOption(args, "--device", request.Device);
        AddOption(args, "--output", request.Output);
        AddOption(args, "--format", FormatName(request.Format));

        if (!string.Equals(request.Configuration, "Release", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(request.Configuration))
        {
            AddOption(args, "--configuration", request.Configuration.Trim());
        }

        if (request.Duration is { } duration)
        {
            AddOption(args, "--duration", FormatDuration(duration));
        }

        if (request.NoBuild)
        {
            args.Add("--no-build");
        }

        if (request.DiagnosticPort is { } port and > 0)
        {
            AddOption(args, "--diagnostic-port", port.ToString(CultureInfo.InvariantCulture));
        }

        AddOption(args, "--trace-profile", request.TraceProfile);

        if (request.Mode == MauiProfileMode.Startup && request.StopOnStartupMarker)
        {
            AddOption(args, "--stopping-event-provider-name", OfficialProviderName);
            AddOption(args, "--stopping-event-event-name", OfficialStartupEventName);
        }

        return args;
    }

    /// <summary>Formats a full <c>maui profile …</c> command line.</summary>
    public static string Format(MauiProfileSessionRequest request)
    {
        var builder = new StringBuilder("maui");
        foreach (var argument in BuildArguments(request))
        {
            builder.Append(' ');
            builder.Append(Quote(argument));
        }

        return builder.ToString();
    }

    static void AddOption(List<string> args, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        args.Add(name);
        args.Add(value.Trim());
    }

    static string FormatName(MauiProfileFormat format) => format switch
    {
        MauiProfileFormat.Speedscope => "speedscope",
        MauiProfileFormat.Mibc => "mibc",
        _ => "nettrace"
    };

    static string Quote(string value)
    {
        if (value.Length > 0 &&
            value.IndexOfAny([' ', '\t', '"', '\'']) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}
