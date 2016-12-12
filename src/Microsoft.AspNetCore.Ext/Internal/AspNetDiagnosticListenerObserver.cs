// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.Context;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Ext.Internal
{
    //functinality should be moved to  Microsoft.AspNetCore.Hosting.Internal.HostingApplication
    internal class AspNetDiagnosticListenerObserver : IObserver<KeyValuePair<string, object>>
    {
        private readonly ILogger<AspNetDiagnosticListenerObserver> logger;
        private readonly Tracer tracer = new Tracer();
        public AspNetDiagnosticListenerObserver(ILogger<AspNetDiagnosticListenerObserver> logger)
        {
            this.logger = logger;
        }

        //those events should be processed in ASP.NET rather than here
        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Key == "Microsoft.AspNetCore.Hosting.BeginRequest")
            {
                var httpContextInfo = value.Value.GetType().GetProperty("httpContext");
                var timestampInfo = value.Value.GetType().GetProperty("timestamp");
                
                var httpContext = (DefaultHttpContext) httpContextInfo?.GetValue(value.Value);
                var timestamp = timestampInfo?.GetValue(value.Value);
                if (httpContext != null && timestamp != null)
                {
                    var ctx = tracer.Extract(httpContext.Request);
                    //we BeginScope regardless of the logging level, since we don't know what will happen with request and we need to have scope e.g. for errors
                    //so this introduces performance impact, we might be able to solve with sampling
                    //And sampling is not provided by ASP.NET Core, it creates scopes for all requests currently
                    if (ctx != null)
                    {
                        var span = new Span(ctx, "Incoming Request", (long)timestamp, SpanState.Current)
                            .SetTags(httpContext.Request);
                        httpContext.Items["scope"] = logger.StartSpan(span);
                    }
                }
            }
            else if (value.Key == "Microsoft.AspNetCore.Hosting.EndRequest")
            {
                var httpContextInfo = value.Value.GetType().GetProperty("httpContext");
                var httpContext = (DefaultHttpContext)httpContextInfo?.GetValue(value.Value, null);
                if (httpContext != null)
                {
                    var scope = httpContext.Items["scope"] as IDisposable;
                    scope?.Dispose();
                }
            }
            else if (value.Key == "Microsoft.AspNetCore.Diagnostics.UnhandledException")
            {
                var httpContextInfo = value.Value.GetType().GetProperty("httpContext");
                var httpContext = (DefaultHttpContext)httpContextInfo?.GetValue(value.Value, null);
                if (httpContext != null)
                {
                    var scope = httpContext.Items["scope"] as IDisposable;
                    scope?.Dispose();
                }
            }
        }

        public void OnCompleted(){}

        public void OnError(Exception error){}
    }

    internal static class SpanExtensions
    {
        public static Span SetTags(this Span span, HttpRequest request)
        {
            span.Tags["Path"] = request.Path;
            span.Tags["Method"] = request.Method;
            return span;
        }
    }
}