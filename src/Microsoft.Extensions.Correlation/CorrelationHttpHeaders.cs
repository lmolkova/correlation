namespace Microsoft.Extensions.Correlation
{
    public class CorrelationHttpHeaders
    {
        public static string CorrelationIdHeaderName = "x-ms-correlation-id";

        public static string SpanIdHeaderName = "x-ms-request-id";

        public static string ParentSpanIdHeaderName = "x-ms-parent-request-id";

        public static string BaggagePrefix = "x-ms-baggage-";
    }
}
