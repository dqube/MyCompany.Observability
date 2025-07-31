#if NETFRAMEWORK
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace MyCompany.Observability.Framework
{
    /// <summary>
    /// Provides access to the logger factory for .NET Framework applications
    /// </summary>
    public static class LoggerFactoryProvider
    {
        // Default logger factory instance
        private static ILoggerFactory _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => 
        {
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
        });

        /// <summary>
        /// Gets the current logger factory instance
        /// </summary>
        public static ILoggerFactory LoggerFactory => _loggerFactory;

        /// <summary>
        /// Sets the logger factory instance (called by the HTTP module configuration)
        /// </summary>
        /// <param name="loggerFactory">The logger factory to use</param>
        public static void SetLoggerFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }
    }
}
#endif