using System;
using System.Collections.Generic;
using System.Diagnostics.Context;
using Microsoft.Extensions.Logging;
using Nest;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace SampleApp
{
    public static class LoggerFactoryExtensions
    {
        public static ILoggerFactory AddElasicSearch(this ILoggerFactory factory)
        {
            factory.AddProvider(new ElasticSerachProvider());
            return factory;
        }
    }

    public class ElasticSerachProvider : ILoggerProvider
    {
        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new ElastiSearchLogger(categoryName);
        }
    }

    public class ElastiSearchLogger : ILogger
    {
        private readonly ElasticClient client;
        private const string IndexName = "myindex";
        private readonly string categoryName;

        private readonly bool initialized;

        public ElastiSearchLogger(string categoryName)
        {
            this.categoryName = categoryName;
            var node = new Uri("http://localhost:9200");
            var settings = new ConnectionSettings(node);
            try
            {
                client = new ElasticClient(settings);
                initialized = true;
            }
            catch (Exception)
            {
                //ignored
            }
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!initialized) return;

            var currentSpan = SpanState.Current;

            //this is an example of custom context propagation
            string isSampledStr;

            if (currentSpan != null && currentSpan.TryGetBaggageItem("isSampled", out isSampledStr))
            {
                if (isSampledStr != bool.TrueString)
                    return;
            }
            //send to elasticsearch
            var document = new Dictionary<string,object>
            {
                ["Message"] = formatter(state, exception),
                ["LogLevel"] = logLevel,
                ["Exception"] = exception,
                ["EventId"] = eventId,
                ["Timestamp"] = DateTime.UtcNow,
                ["CategoryName"] = categoryName
            };

            if (currentSpan != null)
            {
                document["OperationName"] = currentSpan.OperationName;
                document["OperationStarted"] = currentSpan.StartTimestamp;
                foreach (var kv in currentSpan.GetProperties())
                    document[kv.Key] = kv.Value;
            }

            client.Index(document, idx => idx.Index(IndexName));
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        //logger could use SpanState.Current or keep the last Span it received in static AsyncLocal variable
        public IDisposable BeginScope<TState>(TState state)
        {
            return new NoopScope();
        }

        private class NoopScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
