// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Duende.IdentityServer.Licensing.V2;
using Duende.IdentityServer.Models;
using FluentAssertions;
using IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace IntegrationTests.Hosting;

public class LicenseTests : IDisposable
{
    private string client_id = "client";
    private string client_secret = "secret";
    private string scope_name = "api";

    private IdentityServerPipeline _mockPipeline = new();

    public LicenseTests()
    {
        _mockPipeline.Clients.Add(new Client
        {
            ClientId = client_id,
            ClientSecrets = [new Secret(client_secret.Sha256())],
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = ["api"],
        });
        _mockPipeline.ApiScopes = [new ApiScope(scope_name)];
    }

    public void Dispose()
    {
        // Some of our tests involve copying test license files so that the pipeline will read them.
        // This should ensure that they are cleanup up after each test.
        var contentRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
        var path1 = Path.Combine(contentRoot, "Duende_License.key");
        if (File.Exists(path1))
        {
            File.Delete(path1);
        }
        var path2 = Path.Combine(contentRoot, "Duende_IdentityServer_License.key");
        if (File.Exists(path2))
        {
            File.Delete(path2);
        }
    }
    
    [Fact]
    public async Task unlicensed_protocol_requests_log_a_warning()
    {
        var threshold = 5u;
        _mockPipeline.OnPostConfigure += builder =>
        {
            var counter = builder.ApplicationServices.GetRequiredService<ProtocolRequestCounter>();
            counter.Threshold = threshold;
        };
        _mockPipeline.Initialize(enableLogging: true);
        
        // The actual protocol parameters aren't the point of this test, this could be any protocol request 
        var data = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", client_id },
            { "client_secret", client_secret },
            { "scope", scope_name },
        };
        var form = new FormUrlEncodedContent(data);
        
        for (int i = 0; i < threshold + 1; i++)
        {
            await _mockPipeline.BackChannelClient.PostAsync(IdentityServerPipeline.TokenEndpoint, form);
        }

        _mockPipeline.MockLogger.LogMessages.Should().Contain(
            $"IdentityServer has handled {threshold + 1} protocol requests without a license. In future versions, unlicensed IdentityServer instances will shut down after {threshold} protocol requests. Please contact sales to obtain a license. If you are running in a test environment, please use a test license");
    }
}