﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using Autofac;
using Autofac.Integration.WebApi;
using Microsoft.AspNet.WebHooks;
using Microsoft.AspNet.WebHooks.Config;
using Microsoft.AspNet.WebHooks.Diagnostics;
using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs.Extensions.WebHooks
{
    /// <summary>
    /// Class managing routing of requests to registered WebHook Receivers. It initializes an
    /// <see cref="HttpConfiguration"/> and loads all registered WebHook Receivers.
    /// </summary>
    internal class WebHookReceiverManager : IDisposable
    {
        internal const string WebHookJobFunctionInvokerKey = "WebHookJobFunctionInvoker";

        private readonly Dictionary<string, IWebHookReceiver> _receiverLookup;
        private readonly TraceWriter _trace;
        private HttpConfiguration _httpConfiguration;
        private bool disposedValue = false;

        public WebHookReceiverManager(TraceWriter trace)
        {
            _trace = trace;
            _httpConfiguration = new HttpConfiguration();

            var builder = new ContainerBuilder();
            ILogger logger = new WebHookLogger(_trace);
            builder.RegisterInstance<ILogger>(logger);
            builder.RegisterInstance<IWebHookHandler>(new WebJobsWebHookHandler());
            var container = builder.Build();

            WebHooksConfig.Initialize(_httpConfiguration);

            _httpConfiguration.DependencyResolver = new AutofacWebApiDependencyResolver(container);

            IEnumerable<IWebHookReceiver> receivers = _httpConfiguration.DependencyResolver.GetReceivers();
            _receiverLookup = receivers.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<HttpResponseMessage> TryHandle(HttpRequestMessage request, Func<HttpRequestMessage, Task<HttpResponseMessage>> invokeJobFunction)
        {
            // First check if there is a registered WebHook Receiver for this request, and if
            // so use it
            string route = request.RequestUri.LocalPath.ToLowerInvariant();
            IWebHookReceiver receiver = null;
            string receiverId = null;

            if (TryParseReceiver(route, out receiver, out receiverId))
            {
                HttpRequestContext context = new HttpRequestContext
                {
                    Configuration = _httpConfiguration
                };
                request.SetConfiguration(_httpConfiguration);

                // add the anonymous handler function from above to the request properties
                // so our custom WebHookHandler can invoke it at the right time
                request.Properties.Add(WebHookJobFunctionInvokerKey, invokeJobFunction);

                return await receiver.ReceiveAsync(receiverId, context, request);
            }

            return null;
        }

        public bool TryParseReceiver(string route, out IWebHookReceiver receiver, out string receiverId)
        {
            receiver = null;
            receiverId = null;

            string[] routeSegements = route.ToLowerInvariant().TrimStart('/').Split('/');
            if (routeSegements.Length == 1 || routeSegements.Length == 2)
            {
                string receiverName = routeSegements[0];
                if (!_receiverLookup.TryGetValue(receiverName, out receiver))
                {
                    return false;
                }

                // parse the optional WebHook ID from the route if specified
                if (routeSegements.Length == 2)
                {
                    receiverId = routeSegements[1];
                }

                return true;
            }

            return false;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_httpConfiguration != null)
                    {
                        _httpConfiguration.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Custom <see cref="WebHookHandler"/> used to integrate ASP.NET WebHooks into the WebJobs
        /// WebHook request pipeline.
        /// When a request is dispatched to a <see cref="WebHookReceiver"/>, after validating the request
        /// fully, it will delegate to this handler, allowing us to resume processing and dispatch the request
        /// to the WebJob function.
        /// </summary>
        private class WebJobsWebHookHandler : WebHookHandler
        {
            public override async Task ExecuteAsync(string receiver, WebHookHandlerContext context)
            {
                // At this point, the WebHookReceiver has validated this request, so we
                // now need to dispatch it to the actual job function.

                // get the request handler from message properties
                var requestHandler = (Func<HttpRequestMessage, Task<HttpResponseMessage>>)
                    context.Request.Properties[WebHookJobFunctionInvokerKey];

                // Invoke the job function
                context.Response = await requestHandler(context.Request);
            }
        }
    }
}
