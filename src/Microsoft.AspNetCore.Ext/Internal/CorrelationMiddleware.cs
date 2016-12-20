using System;
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
        private readonly Tracer tracer;
        //TODO: we need to ensure correlationId is in the baggage or generate one
        //there is a dependency between header name and baggage key name, and this is error prone
        //Tracer knows about header names, but it should not generate correlationId
        private const string CorrelationIdBaggageKey = "correlation-id";

        public CorrelationMiddleware(RequestDelegate next)
        {
            this.next = next;
            tracer = new Tracer();
        }

        public async Task Invoke(HttpContext context)
        {
            var spanContext = tracer.Extract(
                context.Request.Headers.ToDictionary(kv => kv.Key, kv => kv.Value.First()));
            var span = new SpanBuilder("Incoming request")
                .AsChildOf(spanContext)
                .WithTag("Path", context.Request.Path)
                .WithTag("Method", context.Request.Method)
                .WithTag("RequestId", context.TraceIdentifier)
                .Build();

            string correlationId;
            if (!span.TryGetBaggageItem(CorrelationIdBaggageKey, out correlationId))
                span.SetBaggageItem(CorrelationIdBaggageKey, Guid.NewGuid().ToString());

            using (Span.Push(span))
            {
                span.Start();
                await next.Invoke(context);
                span.Finish();
            }
        }
    }
}
