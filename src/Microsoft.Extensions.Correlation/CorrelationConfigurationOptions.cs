// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.Extensions.Correlation
{
    public class CorrelationConfigurationOptions
    {
        public CorrelationConfigurationOptions()
        {
            InstrumentOutgoingRequests = true;
            EndpointFilter = new EndpointFilterOptions();
            Headers = new HeaderOptions();
        }

        public class EndpointFilterOptions
        {
            public bool Allow { get; set; }
            public List<string> Endpoints { get; set; }

            public EndpointFilterOptions()
            {
                Allow = false;
                Endpoints = new List<string> { @"core\.windows\.net", @"dc\.services\.visualstudio\.com"};
            }
        }

        public bool InstrumentOutgoingRequests { get; set; }
        public EndpointFilterOptions EndpointFilter { get; set; }
        public HeaderOptions Headers { get; set; }

        public class HeaderOptions
        {
            public string CorrelationIdHeaderName { get; set; }
            public string ActivityIdHeaderName { get; set; }
            public string BaggageHeaderPrefix { get; set; }

            public HeaderOptions()
            {
                CorrelationIdHeaderName = "x-ms-correlation-id";
                ActivityIdHeaderName = "x-ms-request-id";
                BaggageHeaderPrefix = "x-baggage-";
            }

            public string GetBaggageKey(string headerName)
            {
                if (headerName == CorrelationIdHeaderName)
                    return CorrelationIdBaggageKey;

                if (headerName.StartsWith(BaggageHeaderPrefix))
                    return headerName.Remove(0, BaggageHeaderPrefix.Length);

                return null;
            }

            public string GetHeaderName(string baggageKey)
            {
                if (baggageKey == CorrelationIdBaggageKey)
                    return CorrelationIdHeaderName;

                return BaggageHeaderPrefix + baggageKey;
            }

            private const string CorrelationIdBaggageKey = "CorrelationId";
        }
    }
}

