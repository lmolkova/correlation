using System.Collections.Generic;
using System.Diagnostics.Context;
using System.Linq;

namespace Microsoft.Extensions.Correlation
{
    public class Tracer
    {
        public static string SpanIdHeaderName = "x-ms-request-id";
        public static string BaggagePrefix = "x-ms-";

        public SpanContext Extract(IDictionary<string, string> headers)
        {
            var context = new SpanContext();

            if (headers.ContainsKey(SpanIdHeaderName))
            {
                context.SpanId = headers[SpanIdHeaderName];
            }
            foreach (var header in headers.Where(header => header.Key.StartsWith(BaggagePrefix) && header.Key != SpanIdHeaderName))
            {
                //TODO: decode header value: dash to camelCase
                context.Baggage.Add(header.Key.Remove(0, BaggagePrefix.Length), header.Value);
            }

            return context;
        }

        public IDictionary<string, string> Inject(SpanContext spanContext)
        {
            var headers = new Dictionary<string, string> {{SpanIdHeaderName, spanContext.SpanId}};
            foreach (var kv in spanContext.Baggage)
            {
                //TODO: encode header value: camelCase to dash?
                headers.Add(BaggagePrefix + kv.Key, kv.Value);
            }
            return headers;
        }
    }
}
