using AlfaHackaton.HttpClient.Common.Interfaces;
using AlfaHackaton.HttpClient.Common.Settings;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AlfaHackaton.HttpClient.Tests;

public class DependencyInjectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Registration_AppliesSettings_ToFactoryClientAndHandler(bool ignoreVerifySsl)
    {
        // Контракт настроек: фабрика применяет timeout/headers, а primary handler — proxy/redirect/TLS.
        var settings = new HttpClientSettings
        {
            BaseAddress = "https://example.invalid/api/",
            Timeout = TimeSpan.FromSeconds(17),
            ProxyAddress = "http://127.0.0.1:8080",
            AutoRedirect = false,
            IgnoreVerifySsl = ignoreVerifySsl,
            DefaultHeaders = headers => headers.Add("X-Application", "hackathon")
        };
        var services = new ServiceCollection();
        var registration = services.AddAlfaHttpClient(settings);
        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(registration.Name);
        var handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(registration.Name);
        while (handler is DelegatingHandler delegating)
        {
            handler = delegating.InnerHandler!;
        }

        var primary = Assert.IsType<HttpClientHandler>(handler);
        Assert.Same(settings, provider.GetRequiredService<HttpClientSettings>());
        Assert.Equal(settings.Timeout, client.Timeout);
        Assert.Equal(settings.BaseAddress, client.BaseAddress!.AbsoluteUri);
        Assert.Equal("hackathon", Assert.Single(client.DefaultRequestHeaders.GetValues("X-Application")));
        Assert.False(primary.AllowAutoRedirect);
        Assert.Equal(new Uri(settings.ProxyAddress), primary.Proxy!.GetProxy(new Uri("https://example.invalid")));
        Assert.Equal(ignoreVerifySsl, primary.ServerCertificateCustomValidationCallback is not null);
        Assert.NotSame(provider.GetRequiredService<IHttpClient<HttpClientSettings>>(), provider.GetRequiredService<IHttpClient<HttpClientSettings>>());
    }

    [Fact]
    public async Task Registration_IsolatesSettingsTypes_WithIdenticalShortNames()
    {
        // Инвариант typed client: одинаковые имена классов в разных пространствах не смешивают адреса и пулы.
        var services = new ServiceCollection();
        var first = services.AddAlfaHttpClient(new First.Settings { BaseAddress = "https://first.invalid/" })
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler());
        var second = services.AddAlfaHttpClient(new Second.Settings { BaseAddress = "https://second.invalid/" })
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler());
        using var provider = services.BuildServiceProvider();

        var firstResponse = await provider.GetRequiredService<IHttpClient<First.Settings>>().Get("items");
        var secondResponse = await provider.GetRequiredService<IHttpClient<Second.Settings>>().Get("items");

        Assert.NotEqual(first.Name, second.Name);
        Assert.Equal("https://first.invalid/items", firstResponse.TextData);
        Assert.Equal("https://second.invalid/items", secondResponse.TextData);
    }

    private sealed class EchoHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri!.AbsoluteUri)
            });
        }
    }

    private static class First
    {
        public class Settings : HttpClientSettings;
    }

    private static class Second
    {
        public class Settings : HttpClientSettings;
    }
}
