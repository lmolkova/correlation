// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.Context;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Correlation.Internal
{
    internal class HttpDiagnosticListenerObserver : IObserver<KeyValuePair<string, object>>
    {
        private readonly EndpointFilter filter;
        private readonly Tracer tracer;

        public HttpDiagnosticListenerObserver(EndpointFilter filter)
        {
            this.filter = filter;
            tracer = new Tracer();
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Value == null)
                return;

            if (value.Key == "System.Net.Http.Request")
            {
                var request = (HttpRequestMessage) value.Value.GetProperty("Request");
                var timestamp = value.Value.GetProperty("Timestamp"); //long

                if (request != null && timestamp != null)
                {
                    if (filter.Validate(request.RequestUri))
                    {
                        var span = new SpanBuilder("Outgoing request")
                            .AsChildOf(Span.Current)
                            .WithStartTimestamp((long) timestamp)
                            .WithTag("Uri", request.RequestUri.ToString())
                            .WithTag("Method", request.Method.ToString())
                            .Build();
                        //See WinHttpHandler: all methods in HttpClient till DiagnosticSource call are not async
                        //If HttpClient.SendAsync is called without await in user code all of those calls will share the same ExecutionContext
                        //If we don't run it in a new task, current Span will become parent of the next outgoing http request
                        Task.Run(() =>
                        {
                            using (Span.Push(span))
                                span.Start();
                        });

                        var headers = tracer.Inject(span.SpanContext);
                        foreach (var header in headers)
                        {
                            request.Headers.Add(header.Key, header.Value);
                        }

                        request.Properties["span"] = span;
                    }
                }
            }
            else if (value.Key == "System.Net.Http.Response")
            {
                var response = (HttpResponseMessage) value.Value.GetProperty("Response");
                var timestamp = value.Value.GetProperty("TimeStamp"); //long

                if (response != null)
                {
                    if (filter.Validate(response.RequestMessage.RequestUri))
                    {
                        var span = response.RequestMessage.Properties["span"] as Span;
                        if (span != null)
                        {
                            span.AddTag("StatusCode", response.StatusCode.ToString());
                            span.AddTag("Duration", $"{span.Duration.TotalMilliseconds}ms");

                            Task.Run(() =>
                            {
                                using (Span.Push(span))
                                    span.Finish((long)timestamp);
                            });
                        }
                    }
                }
            }
        }

        public void OnCompleted(){}

        public void OnError(Exception error){}
    }

    internal static class PropertyExtensions
    {
        public static object GetProperty(this object _this, string propertyName)
        {
            return _this.GetType().GetTypeInfo().GetDeclaredProperty(propertyName)?.GetValue(_this);
        }
    }
}