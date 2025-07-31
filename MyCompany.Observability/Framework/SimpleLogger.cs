using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace MyCompany.Observability.Framework
{
   
public static class LoggerFactoryProvider
    {
        // Make the field private and nullable to address CS8618 and CA2211
        private static ILoggerFactory? _loggerFactory;

        public static ILoggerFactory? LoggerFactory
        {
            get => _loggerFactory;
            set => _loggerFactory = value;
        }
    }
   
}