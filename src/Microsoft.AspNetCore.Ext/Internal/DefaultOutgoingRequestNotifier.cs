using System.Diagnostics.Context;
using System.Net.Http;
using Microsoft.Extensions.Correlation;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Ext.Internal
{
    internal class DefaultOutgoingRequestNotifier : IOutgoingRequestNotifier
    {
        private readonly ILogger logger;
        public DefaultOutgoingRequestNotifier(ILoggerFactory loggerFactory)
        {
            logger = loggerFactory.CreateLogger<DefaultOutgoingRequestNotifier>();
        }

        public void OnBeforeRequest(HttpRequestMessage request)
        {
            using (logger.BeginScope(Span.Current))
                logger.LogInformation("Start");
        }

        public void OnAfterResponse(HttpResponseMessage response)
        {
            using (logger.BeginScope(Span.Current))
                logger.LogInformation("Stop");
        }
    }
}
