using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using static System.Console;

namespace OpenTelemetry.Console;

internal class Program
{
    private static readonly HttpClient s_httpClient = new();

    private const string AppName = "SampleApp"; // To make the AppName come directly from assembly, we can use AssemblyName.Name; where AssemblyName = Assembly.GetExecutingAssembly().GetName();
    private const string Version = "1.0.0.0"; // To make the Version come directly from assembly we can use AssemblyName.Version.ToString();
    private static readonly ActivitySource s_activitySource = new(AppName, Version);

    private static async Task Main(string[] args)
    {
        string url = args.Length > 0 ? args[0] : "http://numbersapi.com/random/math?json";

        // Initialize the OpenTelemetry TracerProvider.
        Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(AppName, Version))
            .AddSource(AppName)
            .AddHttpClientInstrumentation(options =>
            {
                options.Enrich = (activity, eventName, rawObject) =>
                    AddEnrichment(activity, eventName, rawObject);
            })
            .AddConsoleExporter()
            .AddJaegerExporter()
            .Build();
        using var activity = s_activitySource.StartActivity(nameof(Main));
        var response = await GetDataAsync(url);
        WriteLine(response);
        ReadLine();
    }

    private static void AddEnrichment(Activity activity, string eventName, object rawObject)
    {
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

    private static async Task<string> GetDataAsync(string url)
    {
        using var activity = s_activitySource.StartActivity(nameof(GetDataAsync)); // Or MethodInfo.GetCurrentMethod().Name
        activity?.AddTag("url", url);
        var response = await s_httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        return content;
    }
}