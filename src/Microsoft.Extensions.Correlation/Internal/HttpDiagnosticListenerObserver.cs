// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Context;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Correlation.Internal
{
    internal class HttpDiagnosticListenerObserver : IObserver<KeyValuePair<string, object>>
    {
        private readonly EndpointFilter filter;
        private readonly ILogger<HttpDiagnosticListenerObserver> logger;
        public HttpDiagnosticListenerObserver(ILogger<HttpDiagnosticListenerObserver> logger, EndpointFilter filter)
        {
            this.logger = logger;
            this.filter = filter;
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Value == null)
                return;

            if (value.Key == "System.Net.Http.Request")
            {
                var request = (HttpRequestMessage) value.Value.GetProperty("Request");
                var timestamp = value.Value.GetProperty("Timestamp");//long
                var requestId = value.Value.GetProperty("LoggingRequestId");//Guid


                if (request != null && timestamp != null && requestId != null)
                {
                    if (logger.IsEnabled(LogLevel.Information) && filter.Validate(request.RequestUri))
                    {
                        var span = new Span(new SpanContext(requestId.ToString()), "Outgoing request", (long)timestamp, SpanState.Current)
                            .SetTags(request);
                        injectContext(span.GetContext(), request);
                        request.Properties["span"] = span;
                        Task.Run(() =>
                        {
                            using (logger.StartSpan(span))
                            {
                                logger.LogInformation("Start");
                            }
                        });
                    }
                }
            }
            else if (value.Key == "System.Net.Http.Response")
            {
                var response = (HttpResponseMessage) value.Value.GetProperty("Response");
                if (response != null)
                {
                    if (logger.IsEnabled(LogLevel.Information) && filter.Validate(response.RequestMessage.RequestUri))
                    {
                        var span = response.RequestMessage.Properties["span"] as Span;
                        span.SetTags(response);
                        Task.Run(() =>
                        {
                            using (logger.StartSpan(span))
                                logger.LogInformation("Stop");
                        });
                    }
                }
            }
        }

        public void OnCompleted(){}

        public void OnError(Exception error){}

        private void injectContext(SpanContext spanContext, HttpRequestMessage request)
        {
            //There could be the case when there is no correlation Id
            //e.g. request is executed in app startup or in background, outside of any incoming request scope 
            if (spanContext.CorrelationId != null)
                request.Headers.Add(CorrelationHttpHeaders.CorrelationIdHeaderName, spanContext.CorrelationId);

            request.Headers.Add(CorrelationHttpHeaders.SpanIdHeaderName, spanContext.SpanId);
            foreach (var kv in spanContext.Baggage)
            {
                request.Headers.Add(CorrelationHttpHeaders.BaggagePrefix + kv.Key, kv.Value);
            }
        }
    }

    internal static class PropertyExtensions
    {
        public static object GetProperty(this object _this, string propertyName)
        {
            return _this.GetType().GetTypeInfo().GetDeclaredProperty(propertyName)?.GetValue(_this);
        }
    }

    internal static class SpanExtenstions
    {
        public static Span SetTags(this Span span, HttpRequestMessage request)
        {
            span.Tags["Uri"] = request.RequestUri.ToString();
            span.Tags["Method"] = request.Method.ToString();
            return span;
        }

        public static Span SetTags(this Span span, HttpResponseMessage response)
        {
            span.Tags["StatusCode"] = response.StatusCode.ToString();
            span.Tags["Duration"] = $"{TimeSpan.FromTicks(Stopwatch.GetTimestamp() -  span.PreciseStartTimestamp).TotalMilliseconds}ms";
            return span;
        }
    }
}