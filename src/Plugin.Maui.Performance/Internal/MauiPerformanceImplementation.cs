namespace Plugin.Maui.Performance;

sealed class MauiPerformanceImplementation : IMauiPerformance, IMauiPerformanceListener
{
    static readonly PerformanceCategory[] ReportOrder =
    [
        PerformanceCategory.Startup,
        PerformanceCategory.Page,
        PerformanceCategory.Navigation,
        PerformanceCategory.Api,
        PerformanceCategory.Database,
        PerformanceCategory.Image,
        PerformanceCategory.Render,
        PerformanceCategory.Memory,
        PerformanceCategory.Custom,
        PerformanceCategory.Profile
    ];

    readonly MauiPerformanceOptions _options;
    readonly IClock _clock;
    readonly IPlatformPerformance _platform;
    readonly DateTimeOffset _processStartedAt;
    readonly MetricStore _store;
    readonly NavigationWatcher _navigation;
    readonly ImageWatcher _images;

    readonly ConcurrentDictionary<string, long> _pageStartTimestamps = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, DateTimeOffset> _pageStartedAt = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, MemorySnapshot?> _pageMemory = new(StringComparer.Ordinal);

    long _navTimestamp;
    DateTimeOffset _navStartedAt;
    string? _navFrom;
    int _started;
    int _startupRecorded;

    public MauiPerformanceImplementation(
        MauiPerformanceOptions options,
        IClock clock,
        IPlatformPerformance platform,
        DateTimeOffset? processStartedAt = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _platform = platform ?? throw new ArgumentNullException(nameof(platform));
        _processStartedAt = ResolveProcessStart(processStartedAt, clock);
        _store = new MetricStore(options.MaxMetrics);
        _navigation = new NavigationWatcher(this);
        _images = new ImageWatcher(this, options.ImageLoadTimeout);
        MauiProfile.Bind(options.CliProfile);
    }

    public bool IsSupported => true;

    public bool IsStarted => Volatile.Read(ref _started) == 1;

    public bool IsEnabled => _options.Enabled;

    public event EventHandler<MetricRecordedEventArgs>? MetricRecorded;

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        if (!_options.Enabled)
        {
            return;
        }

        _navigation.Start();
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            return;
        }

        _navigation.Dispose();
        _images.Dispose();
    }

    public PerformanceTrace Trace(string name, PerformanceCategory category = PerformanceCategory.Custom, IReadOnlyDictionary<string, string>? properties = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!_options.Enabled)
        {
            return PerformanceTrace.Disabled(_clock, name.Trim(), category);
        }

        return new PerformanceTrace(
            this,
            _clock,
            name.Trim(),
            category,
            _clock.GetTimestamp(),
            _clock.UtcNow,
            SampleMemory(),
            properties,
            record: true);
    }

    public PerformanceTrace TraceDatabase(string name = "SQLite Query", IReadOnlyDictionary<string, string>? properties = null) =>
        Trace(name, PerformanceCategory.Database, properties);

    public PerformanceTrace TraceApi(string name, IReadOnlyDictionary<string, string>? properties = null) =>
        Trace(name, PerformanceCategory.Api, properties);

    public PerformanceTrace TraceImage(string name = "Image Loading", IReadOnlyDictionary<string, string>? properties = null) =>
        Trace(name, PerformanceCategory.Image, properties);

    public void Record(string name, TimeSpan duration, PerformanceCategory category = PerformanceCategory.Custom, IReadOnlyDictionary<string, string>? properties = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_options.Enabled)
        {
            return;
        }

        var completedAt = _clock.UtcNow;
        var startedAt = completedAt - (duration < TimeSpan.Zero ? TimeSpan.Zero : duration);
        Add(new PerformanceMetric(
            name.Trim(),
            category,
            duration,
            startedAt,
            completedAt,
            memoryAtStart: null,
            memoryAtEnd: SampleMemory(),
            properties));
    }

    public void Measure(string name, Action action, PerformanceCategory category = PerformanceCategory.Custom)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var trace = Trace(name, category);
        action();
    }

    public T Measure<T>(string name, Func<T> action, PerformanceCategory category = PerformanceCategory.Custom)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var trace = Trace(name, category);
        return action();
    }

    public async Task MeasureAsync(string name, Func<Task> action, PerformanceCategory category = PerformanceCategory.Custom)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var trace = Trace(name, category);
        await action().ConfigureAwait(false);
    }

    public async Task<T> MeasureAsync<T>(string name, Func<Task<T>> action, PerformanceCategory category = PerformanceCategory.Custom)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var trace = Trace(name, category);
        return await action().ConfigureAwait(false);
    }

    public IReadOnlyList<PerformanceMetric> GetMetrics() => _store.Snapshot();

    public PerformanceReport GetReport()
    {
        var all = _store.Snapshot();
        var latest = all
            .GroupBy(static metric => metric.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.Last())
            .OrderBy(static metric => Array.IndexOf(ReportOrder, metric.Category))
            .ThenBy(static metric => metric.StartedAt)
            .ToList();

        return new PerformanceReport(latest, all, CaptureMemory(), _clock.UtcNow);
    }

    public string FormatReport() => GetReport().Format();

    public MemorySnapshot CaptureMemory() =>
        _platform.CaptureMemory(_clock.UtcNow);

    public void Clear() => _store.Clear();

    internal void Complete(
        string name,
        PerformanceCategory category,
        long startTimestamp,
        DateTimeOffset startedAt,
        MemorySnapshot? memoryAtStart,
        IReadOnlyDictionary<string, string>? properties)
    {
        var duration = _clock.GetElapsedTime(startTimestamp);
        Add(new PerformanceMetric(
            name,
            category,
            duration,
            startedAt,
            _clock.UtcNow,
            memoryAtStart,
            SampleMemory(),
            properties));
    }

    public void OnPageAppearing(string pageName, object? page)
    {
        if (!_options.Enabled)
        {
            return;
        }

        CompleteStartup();
        MauiProfile.NotifyFirstPage();
        CompleteNavigation(pageName);

        if (_options.AutoMeasurePages)
        {
            _pageStartTimestamps[pageName] = _clock.GetTimestamp();
            _pageStartedAt[pageName] = _clock.UtcNow;
            _pageMemory[pageName] = SampleMemory();
        }

        if (_options.AutoMeasureImages)
        {
            _images.Watch(page, _clock.GetTimestamp());
        }
    }

    public void OnPageDisappearing(string pageName, object? page)
    {
        if (!_options.Enabled || !_options.AutoMeasureNavigation)
        {
            return;
        }

        _navFrom = pageName;
        _navTimestamp = _clock.GetTimestamp();
        _navStartedAt = _clock.UtcNow;
    }

    public void OnPageLoaded(string pageName, object? page)
    {
        if (!_options.Enabled)
        {
            return;
        }

        if (_options.AutoMeasurePages &&
            _pageStartTimestamps.TryRemove(pageName, out var start) &&
            _pageStartedAt.TryRemove(pageName, out var startedAt))
        {
            _pageMemory.TryRemove(pageName, out var memory);
            Complete(pageName, PerformanceCategory.Page, start, startedAt, memory, null);
        }

        if (_options.AutoMeasureRendering)
        {
            var renderStart = _clock.GetTimestamp();
            var renderStartedAt = _clock.UtcNow;
            var memory = SampleMemory();
            _platform.ObserveNextFrame(_ =>
            {
                Complete("UI Render", PerformanceCategory.Render, renderStart, renderStartedAt, memory,
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["page"] = pageName });
                MauiProfile.NotifyFirstFrame();
            });
        }
        else
        {
            _platform.ObserveNextFrame(_ => MauiProfile.NotifyFirstFrame());
        }
    }

    public void OnImagesLoaded(TimeSpan duration)
    {
        if (!_options.Enabled || !_options.AutoMeasureImages)
        {
            return;
        }

        Record("Image Loading", duration, PerformanceCategory.Image);
    }

    void CompleteStartup()
    {
        if (!_options.AutoMeasureStartup || Interlocked.Exchange(ref _startupRecorded, 1) == 1)
        {
            return;
        }

        var now = _clock.UtcNow;
        var origin = _processStartedAt;
        if (now - origin > MauiPerformanceDefaults.MaxStartupLookback)
        {
            origin = now;
        }

        Record("App Startup", now - origin, PerformanceCategory.Startup);
    }

    void CompleteNavigation(string toPage)
    {
        if (!_options.AutoMeasureNavigation || _navFrom is null || _navTimestamp == 0)
        {
            return;
        }

        var from = _navFrom;
        var start = _navTimestamp;
        var startedAt = _navStartedAt;
        _navFrom = null;
        _navTimestamp = 0;

        Complete(
            $"{from} → {toPage}",
            PerformanceCategory.Navigation,
            start,
            startedAt,
            null,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["from"] = from,
                ["to"] = toPage
            });
    }

    void Add(PerformanceMetric metric)
    {
        _store.Add(metric);
        MetricRecorded?.Invoke(this, new MetricRecordedEventArgs(metric));
    }

    MemorySnapshot? SampleMemory() =>
        _options.SampleMemory ? _platform.CaptureMemory(_clock.UtcNow) : null;

    static DateTimeOffset ResolveProcessStart(DateTimeOffset? processStartedAt, IClock clock)
    {
        if (processStartedAt is { } supplied)
        {
            return supplied;
        }

        try
        {
            return Process.GetCurrentProcess().StartTime.ToUniversalTime();
        }
        catch
        {
            return clock.UtcNow;
        }
    }
}
