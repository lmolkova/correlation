using System;
using System.Collections.Generic;
using System.Diagnostics.Context;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace SampleApp
{
    public class SpanObserver : IObserver<KeyValuePair<string, object>>
    {
        private readonly ILogger<SpanObserver> logger;
        public SpanObserver(ILoggerFactory loggerFactory)
        {
            logger = loggerFactory.CreateLogger<SpanObserver>();
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Value == null)
                return;

            //We want to add Spans to all log records written with ILogger,
            //Let's call BeginScope on span
            //But wait, when we should dispose scope? When Span is finished
            if (value.Key == "System.Diagnostics.Span.Start")
            {
                var scope = logger.BeginScope(Span.Current);
                Span.Current.Items.Add("loggerScope", scope);
                logger.LogInformation($"Span '{Span.Current.OperationName}' started");
            }
            else if (value.Key == "System.Diagnostics.Span.Finish")
            {
                logger.LogInformation($"Span '{Span.Current.OperationName}' finished");
                object scope;
                if (Span.Current.Items.TryGetValue("loggerScope", out scope))
                {
                    (scope as IDisposable)?.Dispose();
                    Span.Current.Items.Remove("loggerScope");
                }
            }
        }

        public void OnError(Exception error){}

        public void OnCompleted() { }
    }

    internal static class PropertyExtensions
    {
        public static object GetProperty(this object _this, string propertyName)
        {
            return _this.GetType().GetTypeInfo().GetDeclaredProperty(propertyName)?.GetValue(_this);
        }
    }
}
