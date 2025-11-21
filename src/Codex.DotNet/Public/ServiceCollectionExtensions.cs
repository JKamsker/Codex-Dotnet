using System;
using Codex.DotNet.Abstractions;
using Codex.DotNet.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Codex.DotNet.Public;

/// <summary>
/// Service registration helpers for Microsoft.Extensions.DependencyInjection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Codex client services and their default implementations.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configure">Optional configuration for <see cref="CodexClientOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddCodexClient(
        this IServiceCollection services,
        Action<CodexClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<IFileSystem, RealFileSystem>();
        services.TryAddSingleton<ICodexPathProvider, DefaultCodexPathProvider>();
        services.TryAddSingleton<IJsonlEventParser, JsonlEventParser>();
        services.TryAddSingleton<ICodexProcessLauncher, CodexProcessLauncher>();
        services.TryAddSingleton<ICodexSessionLocator, CodexSessionLocator>();
        services.TryAddSingleton<IJsonlTailer>(sp =>
            new JsonlTailer(
                sp.GetRequiredService<IFileSystem>(),
                sp.GetRequiredService<ILogger<JsonlTailer>>(),
                sp.GetRequiredService<IOptions<CodexClientOptions>>()));

        services.TryAddSingleton<ICodexClient, CodexClient>();

        return services;
    }
}
