using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace WeatherApi.OpenTelemetry;

public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Permet de configurer l'open telemetry
    /// L'exporter est sur la variable OTEL_EXPORTER_OTLP_ENDPOINT
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static WebApplicationBuilder ConfigureOpenTelemetry(this WebApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(o =>
        {
            o.IncludeFormattedMessage = true;
            o.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName: builder.Environment.ApplicationName))
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                       .AddRuntimeInstrumentation()
                       .AddHttpClientInstrumentation();

                // Package prerelease
                //.AddEventCountersInstrumentation(c =>
                //{
                //    // https://learn.microsoft.com/en-us/dotnet/core/diagnostics/available-counters
                //    c.AddEventSources(
                //        "Microsoft.AspNetCore.Hosting",
                //        "Microsoft-AspNetCore-Server-Kestrel",
                //        "System.Net.Http",
                //        "System.Net.Sockets",
                //        "System.Net.NameResolution",
                //        "System.Net.Security");
                //});
            })
            .WithTracing(tracing =>
            {
                if (builder.Environment.IsDevelopment())
                {
                    tracing.SetSampler(new AlwaysOnSampler());
                }

                tracing.AddAspNetCoreInstrumentation()
                       .AddSource(Instrumentation.ActivitySourceName)
                       .AddHttpClientInstrumentation();
            });

        // Variable d'environment de la specification OTEL !
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.Configure<OpenTelemetryLoggerOptions>(logging => logging.AddOtlpExporter());
            builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddOtlpExporter());
            builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddOtlpExporter());
        }

        return builder;
    }

}
