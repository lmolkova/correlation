using System.Diagnostics;
using System.Diagnostics.Context;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Correlation;

namespace Microsoft.AspNetCore.Ext.Internal
{
    internal class CorrelationMiddleware
    {
        private readonly RequestDelegate next;
        //TODO: we need to ensure correlationId is in the baggage or generate one
        //there is a dependency between header name and baggage key name, and this is error prone
        //Tracer knows about header names, but it should not generate correlationId
        private readonly Tracer tracer;
        public CorrelationMiddleware(RequestDelegate next, HeaderToBaggageMap headerMap)
        {
            this.next = next;
            tracer = new Tracer(headerMap);
        }

        public async Task Invoke(HttpContext context)
        {
            var spanContext = tracer.Extract(context.Request.Headers.ToDictionary(kv => kv.Key, kv => kv.Value.First()));
            var span = Span.CreateSpan(spanContext, "Incoming request", Stopwatch.GetTimestamp());
            span.AddTag("Path", context.Request.Path);
            span.AddTag("Method", context.Request.Method);
            span.AddTag("RequestId", context.TraceIdentifier);

            string parentSpanId;
            if (spanContext.TryGetValue(HeaderToBaggageMap.SpanIdBaggageKey, out parentSpanId))
            {
                span.AddTag("ParentSpanId", parentSpanId);
            }

            string correlationId;
            if (!span.TryGetBaggageItem(HeaderToBaggageMap.CorrelationIdBaggageKey, out correlationId))
                span.SetBaggageItem(HeaderToBaggageMap.CorrelationIdBaggageKey, span.SpanContext[HeaderToBaggageMap.SpanIdBaggageKey]);

            using (Span.Push(span))
            {
                span.Start();
                await next.Invoke(context);
                span.Finish();
            }
        }
    }
}
