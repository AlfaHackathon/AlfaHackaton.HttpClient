using System.Net;
using AlfaHackaton.HttpClient.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AlfaHackaton.HttpClient;

public static class DependencyInjection
{
    public static IHttpClientBuilder AddAlfaHttpClient<TSettings>(this IServiceCollection services)
        where TSettings : class, IHttpClientSettings
    {
        services.TryAddSingleton<TSettings>();

        return services.AddHttpClient<IHttpClient<TSettings>, Http.HttpClient<TSettings>>(typeof(TSettings).FullName!, (provider, client) =>
        {
            var settings = provider.GetRequiredService<TSettings>();
            client.Timeout = settings.Timeout;

            if (!string.IsNullOrWhiteSpace(settings.BaseAddress))
            {
                client.BaseAddress = new Uri(settings.BaseAddress, UriKind.Absolute);
            }

            settings.DefaultHeaders?.Invoke(client.DefaultRequestHeaders);
        }).ConfigurePrimaryHttpMessageHandler(provider =>
        {
            var settings = provider.GetRequiredService<TSettings>();
            var handler = new HttpClientHandler { AllowAutoRedirect = settings.AutoRedirect };

            if (!string.IsNullOrWhiteSpace(settings.ProxyAddress))
            {
                handler.Proxy = new WebProxy(settings.ProxyAddress);
            }

            if (settings.IgnoreVerifySsl)
            {
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            return handler;
        });
    }

    public static IHttpClientBuilder AddAlfaHttpClient<TSettings>(this IServiceCollection services, TSettings settings)
        where TSettings : class, IHttpClientSettings
    {
        services.TryAddSingleton(settings);
        return services.AddAlfaHttpClient<TSettings>();
    }
}
