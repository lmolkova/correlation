using System;
using Microsoft.Diagnostics.Context;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Ext
{
    public static class LoggerExtensions
    {
        public static IDisposable StartSpan(this ILogger logger, Span span)
        {
            return new DisposableSpan(span, logger.BeginScope(span));
        }

        private class DisposableSpan : IDisposable
        {
            private readonly IDisposable loggerScope;

            public DisposableSpan(Span span, IDisposable loggerScope)
            {
                this.loggerScope = loggerScope;
                SpanState.Push(span);
            }

            public override string ToString()
            {
                return SpanState.Current.ToString();
            }

            public void Dispose()
            {
                loggerScope?.Dispose();
                SpanState.Pop();
            }
        }
    }
}
