using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Context
{
    public class SpanContext
    {
        public readonly string SpanId;
        public string ParentSpanId { get; set; }
        public string CorrelationId { get; set; }

        public readonly IDictionary<string, string> Baggage = new Dictionary<string, string>();

        public SpanContext(string spanId)
        {
            if (spanId == null)
                throw new ArgumentNullException(nameof(spanId));

            SpanId = spanId;
        }
    }
}
