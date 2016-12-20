using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace System.Diagnostics.Context
{
    public class Span
    {
        private static readonly DiagnosticListener DiagnosticListener =
            new DiagnosticListener(SpanDiagnosticListenerStrings.SpanDiagnosticListenerName);

        public readonly string OperationName;
        public readonly DateTime StartTimestamp;
        public readonly IList<KeyValuePair<string, string>> Tags = new List<KeyValuePair<string, string>>();
        public readonly SpanContext SpanContext;

        public readonly Span Parent;

        public TimeSpan Duration { get; private set; }
        private bool isFinished;

        private readonly long preciseStartTimestamp;

        //Items provides similar functionality to HttpRequestMessage.Properties
        //Since we write to diagnostic source about Span Start and Stop, users may need to keep some properties associated with the Span, like ILogger scope
        //Items are not logged and not propagated
        public readonly IDictionary<string,object> Items = new Dictionary<string, object>();

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

        public void Start()
        {
            DiagnosticListener.LogSpanStart(this);
        }

        public void AddTag(string key, string value)
        {
            Tags.Add(new KeyValuePair<string, string>(key, value));
        }

        public void SetBaggageItem(string key, string value)
        {
            SpanContext.Baggage[key] = value;
        }

        public bool TryGetBaggageItem(string key, out string item)
        {
            return SpanContext.Baggage.TryGetValue(key, out item);
        }

        public override string ToString()
        {
            return $"operation: {OperationName}, context: {{{spanContextToString(SpanContext)}}}, tags: {{{dictionaryToString(Tags)}}}, started {StartTimestamp:o}";
        }

        public void Finish()
        {
            Finish(Stopwatch.GetTimestamp());
        }

        public void Finish(long timestamp)
        {
            if (!isFinished)
            {
                Duration = TimeSpan.FromTicks(timestamp - preciseStartTimestamp);
                DiagnosticListener.LogSpanStop(this);
            }
            isFinished = true;
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
            return new DisposableSpan(span);
        }

        private class DisposableSpan : IDisposable
        {
            private readonly Span span;

            public DisposableSpan(Span span)
            {
                this.span = span;
            }

            public void Dispose()
            {
                Current = span.Parent;
            }
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
