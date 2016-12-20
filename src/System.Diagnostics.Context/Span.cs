using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace System.Diagnostics.Context
{
    public class Span : IDisposable
    {
        public readonly string OperationName;
        public readonly DateTime StartTimestamp;
        public readonly IList<KeyValuePair<string, string>> Tags = new List<KeyValuePair<string, string>>();
        public readonly SpanContext SpanContext;

        public readonly Span Parent;

        public TimeSpan Duration { get; private set; }
        public bool IsFinished { get; private set; }

        private readonly long preciseStartTimestamp;

        internal Span(SpanContext context, string operationName, long timestamp, Span parent)
        {
            SpanContext = context;
            OperationName = operationName;
            Parent = parent;

            StartTimestamp = Stopwatch.IsHighResolution ? 
                DateTime.UtcNow.AddTicks(timestamp - Stopwatch.GetTimestamp()) : new DateTime(timestamp);
            preciseStartTimestamp = timestamp;
            Duration = TimeSpan.Zero;
        }

        public void AddTag(string key, string value)
        {
            Tags.Add(new KeyValuePair<string, string>(key, value));
        }

        public void SetBaggageItem(string key, string value)
        {
            SpanContext.Baggage.Add(new KeyValuePair<string, string>(key, value));
        }

        public bool TryGetBaggageItem(string key, out string item)
        {
            return SpanContext.Baggage.TryGetValue(key, out item);
        }

        //Return all properties: tags + context for logging purposes
        public IEnumerable<KeyValuePair<string,string>> GetProperties()
        {
            var result = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("spanId", SpanContext.SpanId),
                new KeyValuePair<string, string>("parentSpanId", SpanContext.ParentSpanId)
            };
            result.AddRange(SpanContext.Baggage);
            result.AddRange(Tags);
            return result;
        }

        public override string ToString()
        {
            return $"operation: {OperationName}, context: {{{spanContextToString(SpanContext)}}}, tags: {{{dictionaryToString(Tags)}}}, started {StartTimestamp:o}";
        }

        public void Finish(long timestamp)
        {
            if (!IsFinished)
            {
                Duration = TimeSpan.FromTicks(timestamp - preciseStartTimestamp);
            }
            IsFinished = true;
        }

        private static readonly AsyncLocal<Span> Value = new AsyncLocal<Span>();

        public static Span Current
        {
            internal set { Value.Value = value; }
            get { return Value.Value; }
        }

        public static IDisposable Push(Span span)
        {
            if (span == null)
                throw new ArgumentNullException(nameof(span));
            if (span.Parent != Current)
                throw new InvalidOperationException("Cannot push Span which parent is not Current");
            Current = span;
            return span;
        }

        public void Dispose()
        {
            Current = Parent;
        }

        private string spanContextToString(SpanContext context)
        {
            var sb = new StringBuilder();
            sb.Append($"spanId={context.SpanId},");
            sb.Append($"parentSpanId={context.ParentSpanId},");
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
