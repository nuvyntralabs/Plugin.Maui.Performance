# Changelog

## 1.0.7

- Add `MauiProfile` as an intuitive wrapper for `maui profile` / `Microsoft.Maui.ProfilingHelper`: session detection, first-page / first-frame / named-scenario stop, marks, and command helpers.
- Add the `maui-perf` dotnet tool (`Plugin.Maui.Performance.Cli`) with `android`/`ios` aliases, `30s` durations, and official `StartupComplete` stopping events.
- Publish `Plugin.Maui.Performance.Cli` from the same CI as the library (PackAsTool, no snupkg).

## 1.0.6

- Align the README pack path with the shipped package version.

## 1.0.3

- Rebrand package metadata and catalog references to MauiEssentials.


## 1.0.2

- LLM-friendly README, llms.txt, AGENTS.md, and improved NuGet title/tags for coding-agent discoverability.

## 1.0.1

- Add the NuGet package link and badge to the README

## 1.0.0

- Lightweight performance profiler for .NET MAUI on iOS and Android
- `MauiPerformance.Trace("LoadCustomer")` named timings
- Automatic startup, page, navigation, image, and first-frame measurement
- `PerformanceDelegatingHandler` for API latency
- `TraceDatabase` / `Measure` helpers for SQLite and other local work
- Memory snapshots (working set, free RAM, pressure) on traces and the report
- Compact report: `App Startup 1.82 sec`, `Home Page 420 ms`, …
