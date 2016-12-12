// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using Microsoft.Diagnostics.Context;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Ext.Internal
{
    internal class HttpDiagnosticListenerObserver : IObserver<KeyValuePair<string, object>>
    {
        private readonly IEndpointFilter filter;
        private readonly Tracer tracer = new Tracer();
        private readonly ILogger<HttpDiagnosticListenerObserver> logger;
        public HttpDiagnosticListenerObserver(ILogger<HttpDiagnosticListenerObserver> logger, IEndpointFilter filter)
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
                var requestInfo = value.Value.GetType().GetProperty("Request");
                var timestampInfo = value.Value.GetType().GetProperty("Timestamp");
                var requestIdInfo = value.Value.GetType().GetProperty("LoggingRequestId");

                var request = (HttpRequestMessage) requestInfo?.GetValue(value.Value);
                var timestamp = timestampInfo?.GetValue(value.Value);//long
                var requestId = requestIdInfo?.GetValue(value.Value);//Guid

                if (request != null && timestamp != null && requestId != null)
                {
                    if (logger.IsEnabled(LogLevel.Information) && filter.Validate(request.RequestUri))
                    {
                        var span = new Span(new SpanContext(requestId.ToString()), "Outgoing request", (long) timestamp, SpanState.Current)
                            .SetTags(request);
                        tracer.Inject(span.GetContext(), request);

                        request.Properties["scope"] = logger.StartSpan(span);
                        logger.LogInformation("Start");
                    }
                }
            }
            else if (value.Key == "System.Net.Http.Response")
            {
                var responseInfo = value.Value.GetType().GetProperty("Response");
                var response = (HttpResponseMessage) responseInfo?.GetValue(value.Value, null);
                if (response != null)
                {
                    if (logger.IsEnabled(LogLevel.Information) && filter.Validate(response.RequestMessage.RequestUri))
                    {
                        SpanState.Current.SetTags(response);
                        logger.LogInformation("Stop");

                        var scope = response.RequestMessage.Properties["scope"] as IDisposable;
                        scope?.Dispose();
                    }
                }
            }
        }

        public void OnCompleted(){}

        public void OnError(Exception error){}
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
            span.Tags["Duration"] = $"{new TimeSpan(Stopwatch.GetTimestamp() - span.StartTimestamp).TotalMilliseconds}ms";
            return span;
        }
    }
}