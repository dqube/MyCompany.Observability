using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MyCompany.Observability.Instrumentation
{
    public static class InstrumentationSources
    {
        public const string ActivitySourceName = "MyCompany.Observability";
        public const string MeterName = "MyCompany.Observability";
        public const string Version = "1.0.0";

        private static readonly ActivitySource _activitySource = new(ActivitySourceName, Version);
        private static readonly Meter _meter = new(MeterName, Version);

        public static ActivitySource ActivitySource => _activitySource;
        public static Meter Meter => _meter;

        public static void Dispose()
        {
            _activitySource?.Dispose();
            _meter?.Dispose();
        }
    }
}