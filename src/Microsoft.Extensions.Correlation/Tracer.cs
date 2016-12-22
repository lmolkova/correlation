using System.Collections.Generic;

namespace Microsoft.Extensions.Correlation
{
    public class HeaderToBaggageMap
    {
        public readonly string BaggageHeaderPrefix;
        public const string CorrelationIdBaggageKey = "CorrelationId";
        public const string SpanIdBaggageKey = "SpanId";

        public HeaderToBaggageMap(CorrelationConfigurationOptions.HeaderOptions headerOptions)
        {
            BaggageHeaderPrefix = headerOptions.BaggageHeaderPrefix;
            Add(SpanIdBaggageKey, headerOptions.SpanIdHeader);
            Add(CorrelationIdBaggageKey, headerOptions.CorrelationIdHeader);
        }

        private readonly Dictionary<string, string> baggageKeys = new Dictionary<string, string>();
        private readonly Dictionary<string, string> headerNames = new Dictionary<string, string>();

        public void Add(string baggageKey, string headerName)
        {
            headerNames[baggageKey] = headerName;
            baggageKeys[headerName] = baggageKey;
        }

        public bool TryGetBaggageKey(string headerName, out string baggageKey)
        {
            return baggageKeys.TryGetValue(headerName, out baggageKey);
        }

        public bool TryGetHeaderName(string baggageKey, out string headerName)
        {
            return headerNames.TryGetValue(baggageKey, out headerName);
        }
    }

    public class Tracer
    {
        private readonly HeaderToBaggageMap headerMap;
        public Tracer(HeaderToBaggageMap headerMap) 
        {
            this.headerMap = headerMap;
        }

        public IDictionary<string, string> Extract(IDictionary<string, string> headers)
        {
            var context = new Dictionary<string, string>();
            foreach (var header in headers)
            {
                string baggageKey;
                if (headerMap.TryGetBaggageKey(header.Key, out baggageKey))
                {
                    context.Add(baggageKey, header.Value);
                }
                else
                {
                    context.Add(headerMap.BaggageHeaderPrefix + baggageKey, header.Value);
                }
                //TODO: encode header name: camelCase to dash? and value
            }

            return context;
        }

        public IDictionary<string, string> Inject(IDictionary<string, string> spanContext)
        {
            var headers = new Dictionary<string,string>();
            foreach (var kv in spanContext)
            {
                string headerName;
                if (headerMap.TryGetHeaderName(kv.Key, out headerName))
                {
                    headers.Add(headerName, kv.Value);
                }
                else
                {
                    headers.Add(headerMap.BaggageHeaderPrefix + kv.Key, kv.Value);
                }
                //TODO: encode header name: camelCase to dash? and value
            }
            return headers;
        }
    }
}
