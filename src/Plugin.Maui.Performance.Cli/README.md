# maui-perf

A small [dotnet tool](https://learn.microsoft.com/dotnet/core/tools/global-tools) that wraps [`maui profile`](https://learn.microsoft.com/en-us/dotnet/maui/developer-tools/cli/profile?view=net-maui-10.0) with shorter commands.

```bash
dotnet tool install -g Plugin.Maui.Performance.Cli
maui-perf startup -f android
maui-perf screen -f ios --format speedscope --duration 30s
```

`startup` always passes the official stopping events so the trace ends when `MauiProfile.StartupComplete()` (or first frame) fires:

```
--stopping-event-provider-name Microsoft.Maui.ProfilingHelper
--stopping-event-event-name StartupComplete
```

Pair it with `Plugin.Maui.Performance` in the app:

```csharp
builder.UseMauiPerformance(options =>
{
    options.CliProfile.CompleteOn = CliProfileCompleteOn.FirstFrame;
});
```

`maui profile` supports **Android** and **iOS simulator** only. Install the MAUI CLI so the `maui` command is on your PATH.
