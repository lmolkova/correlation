using System.Collections.Generic;

namespace System.Diagnostics.Context
{
    public class SpanContext
    {
        public string SpanId { get; set; }

        public string ParentSpanId { get; set; }
        public IDictionary<string, string> Baggage { get; internal set; }

        public SpanContext()
        {
            Baggage = new Dictionary<string, string>();
        }

        public SpanContext GetChildSpanContext(string spanId)
        {
            var result = new SpanContext
            {
                SpanId = spanId,
                ParentSpanId = SpanId,
                Baggage = Baggage
            };
            return result;
        }
    }
}
