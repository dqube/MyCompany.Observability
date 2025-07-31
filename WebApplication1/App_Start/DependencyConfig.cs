using System;
using System.Web.Http;
using MyCompany.Observability.Extensions;
using MyCompany.Observability.Configuration;
using MyCompany.Observability.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WebApplication1.App_Start
{
    public static class DependencyConfig
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        public static void RegisterDependencies()
        {
            var services = new ServiceCollection();

            // Load configuration from app.config
            var options = ConfigurationHelper.LoadFromAppConfig();

            // Register observability services
            services.AddMyCompanyObservability(opt =>
            {
                opt.ServiceName = options.ServiceName;
                opt.ServiceVersion = options.ServiceVersion;
                opt.ServiceNamespace = options.ServiceNamespace;
                opt.ServiceInstanceId = options.ServiceInstanceId;
                opt.EnableRequestResponseLogging = options.EnableRequestResponseLogging;
                opt.EnableRedaction = options.EnableRedaction;
                opt.LogLevel = options.LogLevel;
                opt.ExportBatchSize = options.ExportBatchSize;
                opt.ExportTimeout = options.ExportTimeout;
                opt.Exporter = options.Exporter;
                opt.Redaction = options.Redaction;
                opt.RequestResponseLogging = options.RequestResponseLogging;
                opt.Tracing = options.Tracing;
                opt.Metrics = options.Metrics;
                opt.ServiceAttributes = options.ServiceAttributes;
            });

            // Register additional services specific to Web API
            services.AddSingleton<ILoggerFactory>(provider => new LoggerFactory());

            // Build the service provider
            ServiceProvider = services.BuildServiceProvider();

            // Configure Web API dependency resolver
            GlobalConfiguration.Configuration.DependencyResolver = new WebApiDependencyResolver(ServiceProvider);
        }

        public static void Cleanup()
        {
            if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    // Simple dependency resolver for Web API
    public class WebApiDependencyResolver : System.Web.Http.Dependencies.IDependencyResolver
    {
        private readonly IServiceProvider _serviceProvider;

        public WebApiDependencyResolver(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public System.Web.Http.Dependencies.IDependencyScope BeginScope()
        {
            return new WebApiDependencyScope(_serviceProvider.CreateScope());
        }

        public object GetService(Type serviceType)
        {
            return _serviceProvider.GetService(serviceType);
        }

        public System.Collections.Generic.IEnumerable<object> GetServices(Type serviceType)
        {
            return _serviceProvider.GetServices(serviceType);
        }

        public void Dispose()
        {
            // Service provider is managed by the application lifecycle
        }
    }

    public class WebApiDependencyScope : System.Web.Http.Dependencies.IDependencyScope
    {
        private readonly IServiceScope _scope;

        public WebApiDependencyScope(IServiceScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }

        public object GetService(Type serviceType)
        {
            return _scope.ServiceProvider.GetService(serviceType);
        }

        public System.Collections.Generic.IEnumerable<object> GetServices(Type serviceType)
        {
            return _scope.ServiceProvider.GetServices(serviceType);
        }

        public void Dispose()
        {
            _scope?.Dispose();
        }
    }
}