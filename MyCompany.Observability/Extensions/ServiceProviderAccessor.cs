using System;
using Microsoft.Extensions.DependencyInjection;
using MyCompany.Observability.Services;

#if NETFRAMEWORK
namespace MyCompany.Observability.Extensions
{
#else
namespace MyCompany.Observability.Extensions;
#endif

/// <summary>
/// Provides static access to the global service provider for use in extension methods
/// </summary>
public static class ServiceProviderAccessor
{
    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// Sets the global service provider (called from Global.asax)
    /// </summary>
    public static void SetServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Gets the RedactionService from the global service provider
    /// </summary>
    public static IRedactionService? GetRedactionService()
    {
        return _serviceProvider?.GetService<IRedactionService>();
    }

    /// <summary>
    /// Gets any service from the global service provider
    /// </summary>
    public static T? GetService<T>() where T : class
    {
        return _serviceProvider?.GetService<T>();
    }
}

#if NETFRAMEWORK
}
#endif