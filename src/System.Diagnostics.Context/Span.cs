using System.Collections.Generic;
using System.Linq;
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
        public readonly IDictionary<string,string> SpanContext;
        public readonly Span Parent;
        public readonly string SpanId;

        public TimeSpan Duration { get; private set; }
        private bool isFinished;

        private readonly long preciseStartTimestamp;

        //Items provides similar functionality to HttpRequestMessage.Properties
        //Since we write to diagnostic source about Span Start and Stop, users may need to keep some properties associated with the Span, like ILogger scope
        //Items are not logged and not propagated
        public readonly IDictionary<string,object> Items = new Dictionary<string, object>();

        internal Span(IDictionary<string, string> context, string operationName, long timestamp, Span parent)
        {
            SpanContext = context.ToDictionary(kv => kv.Key, kv => kv.Value);
            SpanId = GenerateSpanId();
            OperationName = operationName;
            Parent = parent;

            StartTimestamp = Stopwatch.IsHighResolution ? 
                DateTime.UtcNow.AddTicks(timestamp - Stopwatch.GetTimestamp()) : new DateTime(timestamp);
            preciseStartTimestamp = timestamp;
            Duration = TimeSpan.Zero;
        }

        public static Span CreateSpan(IDictionary<string, string> context, string operationName, long timestamp)
        {
            return new Span(context, operationName, timestamp, Current);
        }

        public static Span CreateSpan(string operationName, long timestamp)
        {
            if (Current == null)
            {
                throw new InvalidOperationException("Cannot create Span without Parent and Context");
            }

            return new Span(Current.SpanContext, operationName, timestamp, Current);
        }

        private static string GenerateSpanId()
        {
            return Guid.NewGuid().ToString();
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
            SpanContext[key] = value;
        }

        public bool TryGetBaggageItem(string key, out string item)
        {
            return SpanContext.TryGetValue(key, out item);
        }

        public override string ToString()
        {
            return $"operation: {OperationName}, context: {{{dictionaryToString(SpanContext)}}}, tags: {{{dictionaryToString(Tags)}}}, started {StartTimestamp:o}";
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
