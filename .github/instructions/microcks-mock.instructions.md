---
applyTo: 'tests/**/*.cs'
description: How to use Microcks mocks in .NET integration tests.
---

# Using Microcks Mocks in Integration Tests

## Purpose
This instruction explains how to consume mock endpoints exposed by Microcks in your .NET integration tests. Use this to simulate third-party APIs or services.

## Prerequisite
- Microcks must be set up in your test environment. See `microcks-setup.instructions.md`.

## Steps
1. Start Microcks using your `MicrocksWebApplicationFactory`.
2. Retrieve the mock endpoint URL from the running Microcks container.
3. Inject or configure this URL in your application under test.

## Example: Injecting a Mock Endpoint
```csharp
// In your test setup or WebApplicationFactory:
var mockEndpoint = microcksContainer.GetRestMockEndpoint("External API", "1.0.0");
// Pass this URL to your app config or DI
builder.UseSetting("ExternalApi:BaseUrl", mockEndpoint);
```

## Best Practices
- Use network aliases for container-to-container communication
- Document which mocks are used for which tests

## References
- https://microcks.io/documentation/testing/using-mocks/
