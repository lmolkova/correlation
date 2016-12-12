// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Ext.Internal;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Ext
{
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Enables Correlation instrumentation
        /// </summary>
        /// <param name="app"><see cref="IApplicationBuilder"/> application builder</param>
        /// <param name="configuration">Correlation confgiuration</param>
        /// <returns><see cref="IApplicationBuilder"/> application builder</returns>
        public static IApplicationBuilder UseCorrelationInstrumentation(this IApplicationBuilder app, IConfiguration configuration)
        {
            var loggerFactory = app.ApplicationServices.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            var endpointFiler = app.ApplicationServices.GetService(typeof(IEndpointFilter)) as IEndpointFilter;

            bool instrumentOutgoingRequests;
            bool.TryParse(configuration["InstrumentOutgoingRequests"], out instrumentOutgoingRequests);

            var instrumentaion = Initialize(endpointFiler ?? new EndpointFilter(), instrumentOutgoingRequests, loggerFactory);

            var appLifetime = app.ApplicationServices.GetService(typeof(IApplicationLifetime)) as IApplicationLifetime;

            appLifetime?.ApplicationStopped.Register(() => instrumentaion?.Dispose());
            return app;
        }

        private const string HttpListenerName = "HttpHandlerDiagnosticListener";
        private const string AspNetListenerName = "Microsoft.AspNetCore";

        public static IDisposable Initialize(IEndpointFilter endpointFilter, bool instrumentOutgoingRequests, ILoggerFactory loggerFactory)
        {
            var observers = new Dictionary<string, IObserver<KeyValuePair<string, object>>>
            {
                //Asp.Net listener could be removed if it's functionality is moved to ASP.NET Core
                {
                    AspNetListenerName,
                    new AspNetDiagnosticListenerObserver(
                        loggerFactory.CreateLogger<AspNetDiagnosticListenerObserver>())
                }
            };

            if (instrumentOutgoingRequests)
            {
                observers.Add(HttpListenerName,
                    new HttpDiagnosticListenerObserver(loggerFactory.CreateLogger<HttpDiagnosticListenerObserver>(),
                        endpointFilter));
            }
            return DiagnosticListener.AllListeners.Subscribe(new DiagnosticListenersObserver(observers));
        }
    }
}