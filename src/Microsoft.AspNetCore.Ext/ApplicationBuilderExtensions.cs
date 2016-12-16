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
using Microsoft.Extensions.Correlation;
using Microsoft.Extensions.Correlation.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Ext
{
    //TODO: this should be refactored once AspNetDiagListener is eliminated
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Enables Correlation instrumentation
        /// </summary>
        /// <param name="app"><see cref="IApplicationBuilder"/> application builder</param>
        /// <returns><see cref="IApplicationBuilder"/> application builder</returns>
        public static IApplicationBuilder UseCorrelationInstrumentation(this IApplicationBuilder app)
        {
            var loggerFactory = app.ApplicationServices.GetRequiredService(typeof(ILoggerFactory)) as ILoggerFactory;

            var options = app.ApplicationServices.GetService(typeof(IOptions<CorrelationConfigurationOptions>)) as IOptions<CorrelationConfigurationOptions>;
            var instrumentaion = Initialize(options?.Value ?? new CorrelationConfigurationOptions(), loggerFactory);

            var appLifetime = app.ApplicationServices.GetRequiredService(typeof(IApplicationLifetime)) as IApplicationLifetime;

            appLifetime?.ApplicationStopped.Register(() => instrumentaion?.Dispose());
            return app;
        }

        private const string HttpListenerName = "HttpHandlerDiagnosticListener";
        private const string AspNetListenerName = "Microsoft.AspNetCore";

        public static IDisposable Initialize(CorrelationConfigurationOptions options, ILoggerFactory loggerFactory)
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

            var observer = CorrelationHttpInstrumentation.CreateObserver(options, loggerFactory);
            if (observer != null)
                observers.Add(HttpListenerName, observer);

            return DiagnosticListener.AllListeners.Subscribe(new DiagnosticListenersObserver(observers));
        }
    }
}