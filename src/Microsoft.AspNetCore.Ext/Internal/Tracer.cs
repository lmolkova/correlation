using System;
using System.Diagnostics.Context;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Ext.Internal
{
    internal class Tracer
    {
        public HttpRequestMessage Inject(SpanContext spanContext, HttpRequestMessage request)
        {
            //There could be the case when there is no correlation Id
            //e.g. request is executed in app startup or in background, outside of any incoming request scope 
            if (spanContext.CorrelationId != null)
                request.Headers.Add(CorrelationIdHeaderName, spanContext.CorrelationId);

            request.Headers.Add(SpanIdHeaderName, spanContext.SpanId);
            foreach (var kv in spanContext.Baggage)
            {
                request.Headers.Add(BaggagePrefix + kv.Key, kv.Value);
            }
            return request;
        }

        public SpanContext Extract(HttpRequest request)
        {
            string correlationId = null;
            if (request.Headers.ContainsKey(CorrelationIdHeaderName))
                correlationId = request.Headers[CorrelationIdHeaderName].First();

            string requestId = null;
            if (request.Headers.ContainsKey(SpanIdHeaderName))
                requestId = request.Headers[SpanIdHeaderName].First();

            var context = new SpanContext(request.HttpContext.TraceIdentifier)
            {
                CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
                ParentSpanId = requestId
            };

            foreach (var header in request.Headers.Where(header => header.Key.StartsWith(BaggagePrefix)))
            {
                context.Baggage.Add(header.Key.Remove(0, BaggagePrefix.Length), header.Value);
            }
            return context;
        }

        /// <summary>
        /// CorrelationId header name
        /// </summary>
        public static string CorrelationIdHeaderName = "x-ms-correlation-id";

        /// <summary>
        /// RequestId header name
        /// </summary>
        public static string SpanIdHeaderName = "x-ms-request-id";


        public static string BaggagePrefix = "x-ms-baggage-";
    }
}
