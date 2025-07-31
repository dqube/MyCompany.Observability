using System;
using System.Diagnostics;

namespace WebApplication1.Infrastructure
{
    public interface ISimpleLogger
    {
        void LogInformation(string message);
        void LogInformation(string message, params object[] args);
        void LogWarning(string message);
        void LogWarning(string message, params object[] args);
        void LogError(string message);
        void LogError(Exception exception, string message);
        void LogError(Exception exception, string message, params object[] args);
    }

    public class SimpleLogger : ISimpleLogger
    {
        private readonly string _categoryName;

        public SimpleLogger(string categoryName)
        {
            _categoryName = categoryName ?? "Application";
        }

        public void LogInformation(string message)
        {
            WriteLog("INFO", message);
        }

        public void LogInformation(string message, params object[] args)
        {
            try
            {
                var formattedMessage = string.Format(message, args);
                WriteLog("INFO", formattedMessage);
            }
            catch
            {
                WriteLog("INFO", message);
            }
        }

        public void LogWarning(string message)
        {
            WriteLog("WARN", message);
        }

        public void LogWarning(string message, params object[] args)
        {
            try
            {
                var formattedMessage = string.Format(message, args);
                WriteLog("WARN", formattedMessage);
            }
            catch
            {
                WriteLog("WARN", message);
            }
        }

        public void LogError(string message)
        {
            WriteLog("ERROR", message);
        }

        public void LogError(Exception exception, string message)
        {
            WriteLog("ERROR", $"{message} - Exception: {exception}");
        }

        public void LogError(Exception exception, string message, params object[] args)
        {
            try
            {
                var formattedMessage = string.Format(message, args);
                WriteLog("ERROR", $"{formattedMessage} - Exception: {exception}");
            }
            catch
            {
                WriteLog("ERROR", $"{message} - Exception: {exception}");
            }
        }

        private void WriteLog(string level, string message)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] [{level}] [{_categoryName}] {message}";
            
            // Write to debug output (visible in Visual Studio output window)
            Debug.WriteLine(logMessage);
            
            // Also write to console if available
            try
            {
                Console.WriteLine(logMessage);
            }
            catch
            {
                // Console might not be available in web context
            }
        }
    }

    public static class SimpleLoggerFactory
    {
        public static ISimpleLogger CreateLogger<T>()
        {
            return new SimpleLogger(typeof(T).Name);
        }

        public static ISimpleLogger CreateLogger(string categoryName)
        {
            return new SimpleLogger(categoryName);
        }
    }
}