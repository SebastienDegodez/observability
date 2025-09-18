---
description: "Complete setup guide for Microcks infrastructure with Testcontainers in .NET 8/9 projects for contract and integration testing"
mode: "agent"
tools: ["codebase", "editFiles", "search", "runCommands"]
---

# Setup Microcks Infrastructure for .NET 8/9

You are an expert .NET developer specializing in integration testing with Microcks and Testcontainers.

## Your Task

Set up the complete Microcks infrastructure for contract and integration testing in a .NET 8/9 project. This includes creating the required classes, configuring NuGet packages, and preparing the test environment.

**Before we start, I need to understand your requirements:**

### Do you need Kafka support for event-driven testing?
- **Yes**: I'll include Kafka container setup for testing async messaging and events
- **No**: I'll create a simpler setup focused only on REST API contract testing

### Contract Files Location
Please specify your contract files location (relative to test project root):
- Example: `contracts/`, `../shared-contracts/`, `specs/`

Once you provide these details, I'll generate the complete setup tailored to your needs.

## Prerequisites

Verify that the project:
- Uses .NET 8 or .NET 9
- Has a test project structure in place
- Has OpenAPI/AsyncAPI contract files available

## Step 1: Install Required NuGet Packages

**Base packages** (always required):
```xml
<PackageReference Include="Microcks.Testcontainers" Version="0.3.0" />
<PackageReference Include="xunit.v3" Version="3.0.0" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.1.3">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.0" />
```

**Additional packages** (if Kafka support = Yes):
```xml
<PackageReference Include="Testcontainers.Kafka" Version="4.6.0" />
```

**Important:** Run `dotnet restore` after adding packages.

## Step 2: Copy Contract Files to Output Directory

**IMPORTANT:**
Update the path in `.WithMainArtifacts()` to match the location of your contract file as copied to the output directory.
If your contract is in `tests/WeatherApi.Tests/Contract/weather-forecast-openapi.yaml`, add this to your `.csproj`:

```xml
<ItemGroup>
    <None Include="Contract/weather-forecast-openapi.yaml" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

## Step 3: Create KestrelWebApplicationFactory

Create `KestrelWebApplicationFactory.cs`:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;

public class KestrelWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    private IHost? _host;
    private bool _useKestrel;
    private ushort _kestrelPort = 0;

    public Uri ServerAddress
    {
        get
        {
            EnsureServer();
            return ClientOptions.BaseAddress;
        }
    }

    public KestrelWebApplicationFactory<TProgram> UseKestrel(ushort port = 0)
    {
        _useKestrel = true;
        _kestrelPort = port;
        return this;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var testHost = builder.Build();
        if (_useKestrel)
        {
            builder.ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder.UseKestrel(options =>
                {
                    options.Listen(IPAddress.Any, _kestrelPort);
                });
            });
            _host = builder.Build();
            _host.Start();
            var server = _host.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>();
            ClientOptions.BaseAddress = addresses!.Addresses.Select(x => new Uri(x)).Last();
            testHost.Start();
            return testHost;
        }
        return base.CreateHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _host?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void EnsureServer()
    {
        if (_host is null && _useKestrel)
        {
            using var _ = CreateDefaultClient();
        }
    }
}
```

## Step 3: Create BaseIntegrationTest

Create `BaseIntegrationTest.cs`:

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Microcks.Testcontainers;

public class BaseIntegrationTest : IClassFixture<MicrocksWebApplicationFactory<Program>>
{
    public WebApplicationFactory<Program> Factory { get; private set; }
    public ushort Port { get; private set; }
    public MicrocksContainerEnsemble MicrocksContainerEnsemble { get; }
    public MicrocksContainer MicrocksContainer => MicrocksContainerEnsemble.MicrocksContainer;

    protected BaseIntegrationTest(MicrocksWebApplicationFactory<Program> factory)
    {
        Factory = factory;
        Port = factory.ActualPort;
        MicrocksContainerEnsemble = factory.MicrocksContainerEnsemble;
    }
}
```

## Step 4: Create MicrocksWebApplicationFactory

Create `MicrocksWebApplicationFactory.cs`:

```csharp
using System.Net;
using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using Microcks.Testcontainers;
using Microcks.Testcontainers.Connection;
using Testcontainers.Kafka; // Remove if you don't need Kafka
using Xunit;

public class MicrocksWebApplicationFactory<TProgram> : KestrelWebApplicationFactory<TProgram>, IAsyncLifetime where TProgram : class
{
    private const string MicrocksImage = "quay.io/microcks/microcks-uber:1.12.1";
    
    public KafkaContainer? KafkaContainer { get; private set; } // Remove if you don't need Kafka
    public MicrocksContainerEnsemble MicrocksContainerEnsemble { get; private set; } = null!;
    public ushort ActualPort { get; private set; }
    public HttpClient? HttpClient { get; private set; }

    private ushort GetAvailablePort()
    {
        using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Any, 0));
        return (ushort)((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    public async ValueTask InitializeAsync()
    {
        ActualPort = GetAvailablePort();
        UseKestrel(ActualPort);
        await TestcontainersSettings.ExposeHostPortsAsync(ActualPort, default);
        var network = new NetworkBuilder().Build();

        // Remove this entire Kafka section if you don't need event-driven testing
        KafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.9.0")
            .WithNetwork(network)
            .WithNetworkAliases("kafka")
            .WithListener("kafka:19092")
            .Build();
        await this.KafkaContainer.StartAsync(default);
        // End of Kafka section to remove

        this.MicrocksContainerEnsemble = new MicrocksContainerEnsemble(network, MicrocksImage)
            .WithAsyncFeature()
            .WithMainArtifacts("contracts/your-api.yaml") // Update with your actual contract files path
            .WithKafkaConnection(new KafkaConnection("kafka:19092")); // Remove if you don't need Kafka

        await this.MicrocksContainerEnsemble.StartAsync();
        HttpClient = this.CreateClient();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Add external API mocking here if needed
        // Example with Microcks mocks:
        // var microcksContainer = this.MicrocksContainerEnsemble.MicrocksContainer;
        // var externalApiEndpoint = microcksContainer.GetRestMockEndpoint("External API", "1.0.0");
        // builder.UseSetting("ExternalApi:BaseUrl", $"{externalApiEndpoint}/");
    }

    public async override ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (KafkaContainer != null) // Remove if you don't need Kafka
            await this.KafkaContainer.DisposeAsync(); // Remove if you don't need Kafka
        await this.MicrocksContainerEnsemble.DisposeAsync();
    }
}
```

## REST-only Setup Checklist

- Remove all Kafka-related code, usings, and configuration.
- Remove `.WithAsyncFeature()` and `.WithKafkaConnection()` from the Microcks ensemble setup.
- Remove Kafka disposal logic.
- Update the contract files path in `.WithMainArtifacts()` to match your output directory.

## Step 5: Test Class Example

```csharp
using Xunit;

public class WeatherApiContractTest : BaseIntegrationTest
{
    public WeatherApiContractTest(MicrocksWebApplicationFactory<Program> factory)
        : base(factory) { }

    [Fact]
    public async Task TestOpenApiContract()
    {
        var request = new TestRequest
        {
            ServiceId = "WeatherForecast API:1.0.0",
            RunnerType = TestRunnerType.OPEN_API_SCHEMA,
            TestEndpoint = "http://host.testcontainers.internal:" + Port + "/api"
        };

        var testResult = await this.MicrocksContainer.TestEndpointAsync(request);

        var json = System.Text.Json.JsonSerializer.Serialize(testResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        System.Console.WriteLine(json);

        Assert.False(testResult.InProgress, "Test should not be in progress");
        Assert.True(testResult.Success, "Test should be successful");
    }
}
```

## Common Error Troubleshooting

- **Contract file not found:** Ensure `.WithMainArtifacts()` path matches the output directory and `.csproj` copy settings.
- **Missing NuGet packages:** Restore all required packages before building.
- **Async method signature mismatch:** All xUnit lifecycle methods must return `ValueTask`.
- **Kafka code present in REST-only setup:** Remove all Kafka-related code, usings, and configuration.

## Step 6: Final Code Generation

Follow the "Remove if..." comments in the template code to customize it for your specific needs.
