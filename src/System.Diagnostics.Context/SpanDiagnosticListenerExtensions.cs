namespace System.Diagnostics.Context
{
    internal static class SpanDiagnosticListenerStrings
    {
        public const string SpanDiagnosticListenerName = "SpanDiagnosticListener";
        public const string SpanStartName = "System.Diagnostics.Span.Start";
        public const string SpanStopName = "System.Diagnostics.Span.Finish";
    }

    public static class SpanDiagnosticListenerExtensions
    {

        public static void LogSpanStart(this DiagnosticListener diagnosticListener, Span span)
        {
            if (diagnosticListener.IsEnabled(SpanDiagnosticListenerStrings.SpanStartName))
                diagnosticListener.Write(SpanDiagnosticListenerStrings.SpanStartName, new { Span = span });
        }

        public static void LogSpanStop(this DiagnosticListener diagnosticListener, Span span)
        {
            if (diagnosticListener.IsEnabled(SpanDiagnosticListenerStrings.SpanStopName))
                diagnosticListener.Write(SpanDiagnosticListenerStrings.SpanStopName, new { Span = span });
        }

    }
}
