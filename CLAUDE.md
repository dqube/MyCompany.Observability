# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a comprehensive observability library for .NET applications that provides OpenTelemetry integration, request/response logging, and data redaction capabilities. The library supports multiple .NET frameworks (.NET 9.0, .NET Framework 4.6.2, .NET Standard 2.0) and both web and console applications.

## Development Commands

### Building

```bash
cd MyCompany.Observability
dotnet build                           # Build the project (all target frameworks)
dotnet build -c Release               # Build in Release configuration
dotnet build --no-restore             # Build without restoring packages
dotnet restore                         # Restore NuGet packages
```

### Package Management

```bash
cd MyCompany.Observability
dotnet add package [PackageName]       # Add a NuGet package
dotnet pack                           # Create NuGet package
```

## Project Structure

- **Solution File**: `MyCompany.Observability\MyCompany.Observability.sln`
- **Project File**: `MyCompany.Observability\MyCompany.Observability.csproj` (multi-targeting)
- **Configuration Classes**: `Configuration\ObservabilityOptions.cs`
- **Services**: `Services\RedactionService.cs` - handles sensitive data redaction
- **Middleware**: `Middleware\RequestResponseLoggingMiddleware.cs` - HTTP logging
- **Extensions**: `Extensions\ServiceCollectionExtensions.cs`, `Extensions\ApplicationBuilderExtensions.cs`
- **Console Support**: `Console\ConsoleObservability.cs`
- **Documentation**: `README.md` with comprehensive usage examples
- **Examples**: `ExampleUsage\` folder with working examples (excluded from build)

## Architecture Overview

### Core Components

1. **ObservabilityOptions**: Central configuration class with nested options for different features
2. **RedactionService**: Handles automatic redaction of sensitive data in JSON, XML, and plain text
3. **RequestResponseLoggingMiddleware**: ASP.NET Core middleware for HTTP request/response logging
4. **ServiceCollectionExtensions**: DI container registration and OpenTelemetry configuration
5. **ApplicationBuilderExtensions**: ASP.NET Core pipeline configuration
6. **ConsoleObservability**: Factory methods for console application setup

### Multi-Framework Support

- **NET 9.0**: Full feature support with latest APIs
- **.NET Standard 2.0**: Core functionality with limited request buffering
- **.NET Framework 4.6.2**: Full support using System.Text.Json and System.Net.Http

### Framework-Specific Code

The library uses conditional compilation (`#if NET462`, `#if !NET462`, `#if NETSTANDARD2_0`) to provide framework-specific implementations while maintaining a unified API. All frameworks now use System.Text.Json 9.0.0 for consistent JSON processing and security updates.

## Key Features Implemented

- **OpenTelemetry Integration**: Tracing, metrics, and logging with console and OTLP exporters
- **Request/Response Logging**: Comprehensive HTTP logging with configurable options
- **Data Redaction**: Automatic redaction of sensitive information (passwords, tokens, keys)
- **Multi-Framework Support**: Works across .NET Core, .NET Framework, and .NET Standard
- **Configurable Exports**: Console and OTLP exporters with customizable settings
- **Flexible Configuration**: Both code-based and JSON configuration support

## Usage Patterns

### Web Applications (ASP.NET Core)
```csharp
// In Program.cs or Startup.cs
services.AddMyCompanyObservability(configuration, "ServiceName", "1.0.0");
app.UseRequestResponseLogging();
```

### Console Applications
```csharp
var serviceProvider = ConsoleObservability.BuildConsoleObservability(
    "ServiceName", "1.0.0", options => { /* configure */ });
```

### .NET Framework Web Applications
```csharp
services.AddMyCompanyObservability(options => { /* configure */ }, "ServiceName", "1.0.0");
// Use RequestResponseLoggingHandler for HTTP clients
```

## Configuration Options

All configuration is centralized in `ObservabilityOptions` with support for:
- Request/response logging toggles and filtering
- Sensitive data redaction rules and replacement text
- Export batch sizes and timeouts
- Console and OTLP exporter settings
- Log levels and content type filtering

## Development Notes

- The project generates warnings for nullable reference types in .NET Standard 2.0 (expected behavior)
- Example files are excluded from build to prevent compilation errors across different target frameworks
- The library is designed to be framework-agnostic with conditional compilation for platform-specific features
- All public APIs maintain consistency across target frameworks
- System.Text.Json 9.0.0 is used across all target frameworks for consistent JSON processing, improved performance, and security updates
- Support packages (System.Memory, System.Buffers, System.Numerics.Vectors) are included for .NET Framework compatibility
