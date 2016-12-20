// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.Context;
using System.Net.Http;
using System.Reflection;

namespace Microsoft.Extensions.Correlation.Internal
{
    internal class HttpDiagnosticListenerObserver : IObserver<KeyValuePair<string, object>>
    {
        private readonly EndpointFilter filter;
        private readonly IOutgoingRequestNotifier requestNotifier;
        private readonly Tracer tracer;

        public HttpDiagnosticListenerObserver(EndpointFilter filter, IOutgoingRequestNotifier requestNotifier)
        {
            this.filter = filter;
            this.requestNotifier = requestNotifier;
            this.tracer = new Tracer();
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Value == null)
                return;

            if (value.Key == "System.Net.Http.Request")
            {
                var request = (HttpRequestMessage) value.Value.GetProperty("Request");
                var timestamp = value.Value.GetProperty("Timestamp"); //long
                var requestId = value.Value.GetProperty("LoggingRequestId"); //Guid


                if (request != null && timestamp != null && requestId != null)
                {
                    if (filter.Validate(request.RequestUri))
                    {
                        var span = new SpanBuilder("Outgoing request")
                            .AsChildOf(Span.Current)
                            .WithStartTimestamp((long) timestamp)
                            .WithTag("Uri", request.RequestUri.ToString())
                            .WithTag("Method", request.Method.ToString())
                            .WithTag("LoggingRequestId", requestId.ToString())
                            .Start();

                        using (Span.Push(span))
                        {
                            var headers = tracer.Inject(span.SpanContext);
                            foreach (var header in headers)
                            {
                                request.Headers.Add(header.Key, header.Value);
                            }

                            request.Properties["span"] = span;
                            try
                            {
                                requestNotifier?.OnBeforeRequest(request);
                            }
                            catch (Exception)
                            {
                                //ignored
                            }
                        }
                    }
                }
            }
            else if (value.Key == "System.Net.Http.Response")
            {
                var response = (HttpResponseMessage) value.Value.GetProperty("Response");
                var timestamp = value.Value.GetProperty("Timestamp"); //long
                var requestId = value.Value.GetProperty("LoggingRequestId"); //Guid
                if (response != null && timestamp != null)
                {
                    if (filter.Validate(response.RequestMessage.RequestUri))
                    {
                        var span = response.RequestMessage.Properties["span"] as Span;
                        if (span != null)
                        {
                            using (Span.Push(span))
                            {
                                span.AddTag("StatusCode", response.StatusCode.ToString());
                                span.AddTag("Duration", $"{span.Duration.TotalMilliseconds}ms");
                                span.AddTag("LoggingRequestId", requestId.ToString());

                                span.Finish((long) timestamp);
                                try
                                {
                                    requestNotifier?.OnAfterResponse(response);
                                }
                                catch (Exception)
                                {
                                    //ignored
                                }
                            }
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