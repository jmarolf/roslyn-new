using System.CommandLine.Binding;
using System.Diagnostics;
using System.IO;
using System.Reflection;

using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Roslyn.New
{
    internal class LoggerProvider : BinderBase<Tracer>
    {
        protected override Tracer GetBoundValue(BindingContext bindingContext)
            => GetLogger(bindingContext);

        static Tracer GetLogger(BindingContext bindingContext)
        {
            var assemblyName = Assembly.GetExecutingAssembly().GetName();
            var serviceName = assemblyName.Name ?? "Unknown";
            var serviceVersion = assemblyName.Version?.ToString() ?? "Unknown";

            var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
                .AddSource(serviceName)
                 .SetResourceBuilder(
                   ResourceBuilder.CreateDefault()
                   .AddService(serviceName: serviceName, serviceVersion: serviceVersion));


            var logLevel = bindingContext.ParseResult.GetValueForOption(Builder.VerbosityOption);
            if (logLevel <=LogLevel.Trace)
            {
                tracerProviderBuilder = tracerProviderBuilder.AddConsoleExporter();
            }

            return tracerProviderBuilder.Build().GetTracer(serviceName);
        }
    }
}
