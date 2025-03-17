using DotNet.Testcontainers.Configurations;
using Microcks.Testcontainers;
using Microcks.Testcontainers.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using System.Net;
using System.Text.Json;

namespace WeatherApi.Tests;

/// <summary>
/// Represents the test class for the WeatherForecast API.
/// </summary>
public class WeatherForecastApiTests : IAsyncLifetime, IClassFixture<CustomWebApplicationFactory>
{
    private MicrocksContainer microcks;

    public CustomWebApplicationFactory Factory { get; }


    public WeatherForecastApiTests(CustomWebApplicationFactory factory)
    {
        this.Factory = factory;
    }

    
    public async Task InitializeAsync()
    {
        IEnumerable<ushort> ports = [5000];
        await TestcontainersSettings.ExposeHostPortsAsync(ports);

        microcks = new MicrocksBuilder()
            .WithEnvironment("JAVA_OPTIONS", "-XX:UseSVE=0")
            .WithPortBinding(8080, true)
            .WithExposedPort(8080)
            .WithMainArtifacts("weather-forecast-openapi.yaml")
            .Build();
        await microcks.StartAsync();
    }

    [Fact]
    public async Task Get_WeatherForecast_ReturnsSevenDays()
    {
        // Given
        const string path = "api/WeatherForecast";

        using var httpClient = Factory.CreateClient();

        // When
        var response = await httpClient.GetAsync(path);

        var weatherForecastStream = await response.Content.ReadAsStreamAsync();

        var weatherForecast = await JsonSerializer.DeserializeAsync<IEnumerable<WeatherForecast>>(weatherForecastStream);

        // Then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.InRange(weatherForecast!.Count(), 1, 5);
    }

    [Fact]
    public async Task Get_WeatherForecast_ReturnsSevenDays_Contract()
    {
        var testRequest = new TestRequest
        {
            TestEndpoint = "http://host.testcontainers.internal:5000",
            FilteredOperations = new List<string> { "GET /api/forecast" },
            ServiceId = "WeatherForecast API:1.0.0",
            RunnerType = TestRunnerType.OPEN_API_SCHEMA,
            Timeout = TimeSpan.FromMicroseconds(200),
        };

        var result = await microcks.TestEndpointAsync(testRequest);
        
        Assert.True(result.Success);
    }

    public async Task DisposeAsync()
    {
        await microcks.DisposeAsync();
    }
}

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("URLS", "http://+:5000;https://+:5001");
    }
}