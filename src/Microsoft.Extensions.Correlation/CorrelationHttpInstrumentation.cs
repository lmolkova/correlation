using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Correlation.Internal;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Correlation
{
    //TODO: this should be refactored once AspNetDiagListener is eliminated
    public class CorrelationHttpInstrumentation
    {
        private const string HttpListenerName = "HttpHandlerDiagnosticListener";
        public static IDisposable Enable(CorrelationConfigurationOptions settings, ILoggerFactory loggerFactory)
        {
            var observers = new Dictionary<string, IObserver<KeyValuePair<string, object>>>();

            if (settings.InstrumentOutgoingRequests)
            {
                var observer = CreateObserver(settings, loggerFactory);
                if (observer != null)
                {
                    observers.Add(HttpListenerName, observer);
                    return DiagnosticListener.AllListeners.Subscribe(new DiagnosticListenersObserver(observers));
                }
            }

            return new NoopDisposable();
        }

        public static IObserver<KeyValuePair<string, object>> CreateObserver(CorrelationConfigurationOptions options, ILoggerFactory loggerFactory)
        {
            if (options.InstrumentOutgoingRequests)
            {
                return new HttpDiagnosticListenerObserver(loggerFactory.CreateLogger<HttpDiagnosticListenerObserver>(),
                        new EndpointFilter(options.EndpointFilter.Endpoints, options.EndpointFilter.Allow));
            }
            return null;
        }

        private class NoopDisposable : IDisposable
        {
            public void Dispose() {}
        }
    }
}