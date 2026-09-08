# Changelog

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
