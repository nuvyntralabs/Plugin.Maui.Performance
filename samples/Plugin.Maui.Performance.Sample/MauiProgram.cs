using Microsoft.Extensions.Logging;
using Plugin.Maui.Performance;

namespace Plugin.Maui.Performance.Sample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<CustomerPage>();
        builder.Services.AddHttpClient("shop", client =>
        {
            client.BaseAddress = new Uri("https://jsonplaceholder.typicode.com/");
        }).AddHttpMessageHandler(() => new PerformanceDelegatingHandler());

        builder
            .UseMauiApp<App>()
            .UseMauiPerformance(options =>
            {
                options.AutoMeasureStartup = true;
                options.AutoMeasurePages = true;
                options.AutoMeasureNavigation = true;
                options.AutoMeasureImages = true;
                options.AutoMeasureRendering = true;
                options.SampleMemory = true;
                options.CliProfile.CompleteOn = CliProfileCompleteOn.FirstFrame;
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
