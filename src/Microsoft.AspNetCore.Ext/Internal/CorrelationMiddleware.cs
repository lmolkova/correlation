using System.Diagnostics.Context;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Correlation;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Ext.Internal
{
    internal class CorrelationMiddleware
    {
        private readonly RequestDelegate next;
        private readonly Tracer tracer;
        //just for testing purposes
        private readonly ILogger logger;

        public CorrelationMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
        {
            this.next = next;
            tracer = new Tracer();
            logger = loggerFactory.CreateLogger<CorrelationMiddleware>();
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
                .Start();

            string correlationId;
            if (!span.TryGetBaggageItem(CorrelationIdKey, out correlationId))
                span.SetBaggageItem(CorrelationIdKey, span.SpanContext.SpanId);
            
            //let's make ConsoleLogger happy
            using (logger.BeginScope(span))
            using (Span.Push(span))
            {
                await next.Invoke(context);
            }
        }

        private const string CorrelationIdKey = "correlationId";
    }
}
