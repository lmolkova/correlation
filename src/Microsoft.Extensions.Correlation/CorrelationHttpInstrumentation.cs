using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Correlation.Internal;

namespace Microsoft.Extensions.Correlation
{
    //TODO: this should be refactored once AspNetDiagListener is eliminated
    public class CorrelationHttpInstrumentation
    {
        public static IDisposable Enable(CorrelationConfigurationOptions settings, IOutgoingRequestNotifier requestNotifier)
        {
            var observer = CreateObserver(settings, requestNotifier);
            if (observer != null)
            {
                return DiagnosticListener.AllListeners.Subscribe(delegate(DiagnosticListener listener)
                {
                    if (listener.Name == "HttpHandlerDiagnosticListener")
                        listener.Subscribe(observer);
                });
            }
            return new NoopDisposable();
        }

        public static IObserver<KeyValuePair<string, object>> CreateObserver(CorrelationConfigurationOptions options, IOutgoingRequestNotifier requestNotifier)
        {
            if (options.InstrumentOutgoingRequests)
            {
                return new HttpDiagnosticListenerObserver(
                    new EndpointFilter(options.EndpointFilter.Endpoints, options.EndpointFilter.Allow),
                    requestNotifier);
            }
            return null;
        }

        private class NoopDisposable : IDisposable
        {
            public void Dispose() {}
        }
    }
}