using System;

namespace SMTMS.Core.Infrastructure;

/// <summary>
/// Provides a static access point for the ServiceProvider.
/// Useful for Aspects and other non-DI contexts.
/// </summary>
public static class ServiceLocator
{
    private static IServiceProvider? _provider;

    public static IServiceProvider Provider
    {
        get => _provider ?? throw new InvalidOperationException("ServiceLocator has not been initialized.");
        private set => _provider = value;
    }

    public static void Initialize(IServiceProvider provider)
    {
        _provider = provider;
    }
}
