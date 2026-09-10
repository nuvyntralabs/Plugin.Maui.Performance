using Plugin.Maui.Performance;

namespace Plugin.Maui.Performance.Sample;

public partial class MainPage : ContentPage
{
    readonly IMauiPerformance _performance;
    readonly CustomerPage _customerPage;
    readonly IHttpClientFactory _httpClientFactory;

    public MainPage(IMauiPerformance performance, CustomerPage customerPage, IHttpClientFactory httpClientFactory)
    {
        InitializeComponent();
        _performance = performance;
        _customerPage = customerPage;
        _httpClientFactory = httpClientFactory;
        _performance.MetricRecorded += (_, _) => MainThread.BeginInvokeOnMainThread(RefreshReport);
        RefreshReport();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshReport();
    }

    async void OnLoadCustomerClicked(object? sender, EventArgs e)
    {
        using var scenario = MauiProfile.Scenario("LoadCustomer");
        using var trace = MauiPerformance.Trace("LoadCustomer");

        try
        {
            var client = _httpClientFactory.CreateClient("shop");
            var request = new HttpRequestMessage(HttpMethod.Get, "users/1");
            request.Options.Set(PerformanceHttp.NameKey, "Customer API");
            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            await response.Content.ReadAsStringAsync();
        }
        catch
        {
            await MauiPerformance.MeasureAsync("Customer API", () => Task.Delay(630), PerformanceCategory.Api);
        }

        MauiPerformance.Measure("SQLite Query", () => Thread.Sleep(18), PerformanceCategory.Database);
        await MauiPerformance.MeasureAsync("Image Loading", () => Task.Delay(210), PerformanceCategory.Image);
        MauiProfile.Mark("CustomerReady");
        RefreshReport();
    }

    async void OnCustomerPageClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_customerPage);

    void OnExampleClicked(object? sender, EventArgs e)
    {
        _performance.Clear();
        _performance.Record("App Startup", TimeSpan.FromSeconds(1.82), PerformanceCategory.Startup);
        _performance.Record("Home Page", TimeSpan.FromMilliseconds(420), PerformanceCategory.Page);
        _performance.Record("Customer API", TimeSpan.FromMilliseconds(630), PerformanceCategory.Api);
        _performance.Record("SQLite Query", TimeSpan.FromMilliseconds(18), PerformanceCategory.Database);
        _performance.Record("Image Loading", TimeSpan.FromMilliseconds(210), PerformanceCategory.Image);
        RefreshReport();
    }

    void OnRefreshClicked(object? sender, EventArgs e) => RefreshReport();

    void OnClearClicked(object? sender, EventArgs e)
    {
        _performance.Clear();
        RefreshReport();
    }

    void RefreshReport()
    {
        var text = _performance.FormatReport();
        ReportLabel.Text = string.IsNullOrWhiteSpace(text) ? "No timings yet." : text;
    }
}
