﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Ext.Internal;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Correlation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Ext
{
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Enables Correlation instrumentation
        /// </summary>
        /// <param name="app"><see cref="IApplicationBuilder"/> application builder</param>
        /// <returns><see cref="IApplicationBuilder"/> application builder</returns>
        public static IApplicationBuilder UseCorrelationInstrumentation(this IApplicationBuilder app)
        {
            app.UseMiddleware<CorrelationMiddleware>();

            var options = app.ApplicationServices.GetService(typeof(IOptions<CorrelationConfigurationOptions>)) as IOptions<CorrelationConfigurationOptions>;

            var instrumentaion = CorrelationHttpInstrumentation.Enable(
                options?.Value ?? new CorrelationConfigurationOptions());

            var appLifetime = app.ApplicationServices.GetRequiredService(typeof(IApplicationLifetime)) as IApplicationLifetime;
            appLifetime?.ApplicationStopped.Register(() => instrumentaion?.Dispose());

            return app;
        }

    }
}