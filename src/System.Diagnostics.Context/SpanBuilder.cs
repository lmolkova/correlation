using System.Collections.Generic;

namespace System.Diagnostics.Context
{
    public class SpanBuilder
    {
        public SpanBuilder(string operationName)
        {
            this.operationName = operationName;
        }

        public SpanBuilder WithStartTimestamp(long timestamp)
        {
            startTimestamp = timestamp;
            return this;
        }

        public SpanBuilder WithTag(string key, string value)
        {
            tags.Add(new KeyValuePair<string, string>(key, value));
            return this;
        }

        public SpanBuilder AsChildOf(SpanContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (parentContext != null)
                throw new InvalidOperationException("Parent context already specified");

            parentContext = context;
            return this;
        }

        public SpanBuilder AsChildOf(Span parent)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (parentContext != null)
                throw new InvalidOperationException("Parent context already specified");
            parentContext = parent.SpanContext;
            parentSpan = parent;
            return this;
        }

        public Span Build()
        {
            if (parentContext == null)
                throw new InvalidOperationException("Cannot build Span without SpanContext");

            var spanContext = parentContext.GetChildSpanContext(GenerateSpanId());

            if (startTimestamp == null)
                startTimestamp = Stopwatch.GetTimestamp();

            var span = new Span(spanContext, operationName, startTimestamp.Value, parentSpan);
            foreach (var tag in tags)
            {
                span.AddTag(tag.Key, tag.Value);                
            }
            return span;
        }

        private static string GenerateSpanId()
        {
            return Guid.NewGuid().ToString();
        }

        private readonly string operationName;
        private long? startTimestamp;
        private readonly List<KeyValuePair<string, string>> tags = new List<KeyValuePair<string, string>>();
        private SpanContext parentContext;
        private Span parentSpan;
    }
}
