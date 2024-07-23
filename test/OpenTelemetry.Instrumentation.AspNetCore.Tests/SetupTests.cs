// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests;

[Collection("AspNetCore")]
public class SetupTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public SetupTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    // https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/1971
    [Fact]
    public async Task AddAspNetCoreInstrumentation_IsIdempotent()
    {
        var exportedItems = new List<Activity>();
        int filterCalls = 0;
        int enrichRequestCalls = 0;

        using (var client = this.factory
                   .WithWebHostBuilder(builder =>
                   {
                       builder.ConfigureTestServices((IServiceCollection services) =>
                       {
                           services.AddOpenTelemetry()
                               .WithTracing(builder => builder
                                   .AddAspNetCoreInstrumentation(options =>
                                   {
                                       options.Filter = context =>
                                       {
                                           filterCalls++;
                                           return true;
                                       };
                                       options.EnrichWithHttpRequest = (activity, request) =>
                                       {
                                           enrichRequestCalls++;
                                       };
                                   })
                                   .AddAspNetCoreInstrumentation() // 2nd call on purpose to validate idempotency
                                   .AddInMemoryExporter(exportedItems));
                       });
                   })
                   .CreateClient())
        {
            using var response = await client.GetAsync(new Uri("/api/values"));
        }

        SpinWait.SpinUntil(() => exportedItems.Count == 1, TimeSpan.FromSeconds(1));

        Assert.Equal(1, filterCalls);
        Assert.Equal(1, enrichRequestCalls);
    }
}
