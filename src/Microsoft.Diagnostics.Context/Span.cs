using System.Collections.Generic;
using System.Text;

namespace System.Diagnostics.Context
{
    public class Span
    {
        public readonly string OperationName;
        public readonly long StartTimestamp;
        public readonly IDictionary<string, string> Tags = new Dictionary<string, string>();

        public Span Parent { get; internal set; }

        //SpanContext is not duplicated in child spans
        private readonly SpanContext spanContext;

        public Span(SpanContext context, string operationName, long timestamp, Span parent)
        {
            spanContext = context;
            if (parent != null)
                spanContext.ParentSpanId = parent.spanContext.SpanId;

            OperationName = operationName;
            StartTimestamp = timestamp;
            Parent = parent;
        }

        //GetContext returns flattened context for Tracer.Inject
        public SpanContext GetContext()
        {
            var currentSpan = this;
            var result = new SpanContext(currentSpan.spanContext.SpanId)
            {
                ParentSpanId = spanContext.ParentSpanId
            };

            while (currentSpan != null)
            {
                foreach (var kv in currentSpan.spanContext.Baggage)
                {
                    result.Baggage[kv.Key] = kv.Value;
                }

                if (currentSpan.Parent == null)
                    result.CorrelationId = currentSpan.spanContext.CorrelationId;
                currentSpan = currentSpan.Parent;
            }
            return result;
        }

        public void SetBaggageItem(string key, string value)
        {
            spanContext.Baggage[key] = value;
        }

        public bool TryGetBaggageItem(string key, out string item)
        {
            Span current = this;
            while (current != null)
            {
                if (current.spanContext.Baggage.TryGetValue(key, out item))
                    return true;
                current = current.Parent;
            }
            item = null;
            return false;
        }

        //Return all properties: tags + flattened context for logging purposes
        public IEnumerable<KeyValuePair<string,string>> GetProperties()
        {
            var result = new List<KeyValuePair<string,string>>();
            var context = GetContext();
            result.Add(new KeyValuePair<string, string>(nameof(context.CorrelationId), context.CorrelationId));
            result.Add(new KeyValuePair<string, string>(nameof(context.SpanId), context.SpanId));
            result.Add(new KeyValuePair<string, string>(nameof(context.ParentSpanId), spanContext.ParentSpanId));
            result.AddRange(context.Baggage);
            result.AddRange(Tags);
            return result;
        }

        public override string ToString()
        {
            return $"operation: {OperationName}, context: {{{spanContextToString(spanContext)}}}, tags: {{{dictionaryToString(Tags)}}}, started {StartTimestamp}";
        }

        private string spanContextToString(SpanContext context)
        {
            var sb = new StringBuilder();
            sb.Append($"{nameof(context.SpanId)}={context.SpanId},");
            sb.Append($"{nameof(context.ParentSpanId)}={context.ParentSpanId},");
            if (context.CorrelationId != null)
                sb.Append($"{nameof(context.CorrelationId)}={context.CorrelationId},");
            sb.Append(dictionaryToString(context.Baggage));
            return sb.ToString();
        }

        private string dictionaryToString(IEnumerable<KeyValuePair<string, string>> dictionary)
        {
            var sb = new StringBuilder();
            foreach (var kv in dictionary)
            {
                if (kv.Value != null)
                    sb.Append($"{kv.Key}={kv.Value},");
            }
            if (sb.Length > 0)
                sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }
    }
}
