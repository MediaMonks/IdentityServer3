/*
 * Copyright 2014, 2015 Dominick Baier, Brock Allen
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using IdentityServer3.Core.Extensions;
using System;
using System.Diagnostics;

namespace Owin
{
    /// <summary>
    /// Container for requestId middleware
    /// </summary>
    public static class ConfigureRequestIdExtension
    {
        /// <summary>
        /// Inserts middleware that assigns a request id to the OwinContext Environment using the Trace.CorrelationManager
        /// </summary>
        /// <param name="app">AppBuilder to add middleware to</param>
        /// <returns>the <paramref name="app"/></returns>
        public static IAppBuilder ConfigureRequestId(this IAppBuilder app) //TODO: UseRequestId would be a more appropriate name, because this method inserts middleware
        {
            app.Use(async (ctx, next) =>
            {
                if (Trace.CorrelationManager.ActivityId == Guid.Empty)
                {
                    Trace.CorrelationManager.ActivityId = Guid.NewGuid();
                }

                var id = Trace.CorrelationManager.ActivityId.ToString();
                ctx.Environment.SetRequestId(id);
                
                await next();
            });

            return app;
        }
    }
}
