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
            public string CorrelationIdHeader { get; set; }
            public string SpanIdHeader { get; set; }
            public string BaggageHeaderPrefix { get; set; }

            public HeaderOptions()
            {
                CorrelationIdHeader = "x-ms-correlation-id";
                SpanIdHeader = "x-ms-request-id";
                BaggageHeaderPrefix = "x-baggage-";
            }
        }
    }
}

