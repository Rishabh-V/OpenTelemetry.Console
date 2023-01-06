using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using static System.Console;

namespace OpenTelemetry.Console;

internal class Program
{
    // To make the AppName come directly from assembly, we can use AssemblyName.Name; where AssemblyName = Assembly.GetExecutingAssembly().GetName();
    private const string AppName = "SampleApp";

    // To make the Version come directly from assembly we can use AssemblyName.Version.ToString();
    private const string Version = "1.0.0.0";
    
    private static readonly HttpClient s_httpClient = new();    
    private static readonly ActivitySource s_activitySource = new(AppName, Version);
    private static readonly Meter meter = new (AppName, Version);
    
    private static Counter<long> counter;
    private static ILogger<Program> s_logger; 
    
    private static async Task Main(string[] args)
    {
        string url = args.Length > 0 ? args[0] : "http://numbersapi.com/random/math?json";
        
        var resourceBuilder = ResourceBuilder.CreateDefault().AddService(AppName, Version);

        // Initialize the OpenTelemetry TracerProvider.
        _ = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddSource(AppName)
            .AddHttpClientInstrumentation(options =>
            {
                options.Enrich = (activity, eventName, rawObject) =>
                    AddEnrichment(activity, eventName, rawObject);
                options.RecordException = true;
            })
            .AddConsoleExporter()
            .AddJaegerExporter()
            .Build();

        // Initialize the OpenTelemetry MeterProvider.
        _ = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddConsoleExporter()
            .AddMeter(meter.Name)
            .Build();
        counter = meter.CreateCounter<long>($"{AppName}-counter");

        // Create a loggerFactory to get the logger.
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options => options.AddConsoleExporter());
        });

        s_logger = loggerFactory.CreateLogger<Program>();

        using (var activity = s_activitySource.StartActivity(nameof(Main)))
        {
            var response = await GetDataAsync(url);
            WriteLine(response);
        }
        
        ReadLine();
    }

    private static async Task<string> GetDataAsync(string url)
    {
        s_logger.LogInformation($"Starting {nameof(GetDataAsync)}");
        using var activity = s_activitySource.StartActivity(nameof(GetDataAsync)); // Or MethodInfo.GetCurrentMethod().Name
        activity?.SetTag("url", url);
        // Act like a request conuter. Counter will increment by 1 everytime the request is made.
        counter.Add(1);
        activity?.AddEvent(new ActivityEvent("Calling API."));
        var response = await s_httpClient.GetAsync(url);
        activity?.AddEvent(new ActivityEvent("Parsing response."));
        var content = await response.Content.ReadAsStringAsync();
        s_logger.LogInformation($"Ending {nameof(GetDataAsync)}");
        return content;
    }

    private static void AddEnrichment(Activity activity, string eventName, object rawObject)
    {
        // TODO: Create constants.
        if (eventName == "OnStartActivity")
        {
            if (rawObject is HttpRequestMessage httpRequest)
            {
                foreach (var item in httpRequest.Headers)
                {
                    activity?.SetTag($"http.request.header.{item.Key}", item.Value);
                }
            }
        }
        else if (eventName == "OnStopActivity")
        {
            if (rawObject is HttpResponseMessage httpResponse)
            {
                foreach (var item in httpResponse.Headers)
                {
                    activity?.SetTag($"http.response.header.{item.Key}", item.Value);
                }
            }
        }
    }
}