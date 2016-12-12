using System.Threading;

namespace Microsoft.Diagnostics.Context
{
    public class SpanState
    {
        private static readonly AsyncLocal<Span> Value = new AsyncLocal<Span>();

        public static Span Current
        {
            set { Value.Value = value; }
            get { return Value.Value; }
        }

        public static void Push(Span span)
        {
            Current = span;
        }

        public static void Pop()
        {
            Current = Current.Parent;
        }
    }
}
