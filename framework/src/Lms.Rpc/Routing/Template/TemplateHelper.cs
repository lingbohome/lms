﻿using System;
using System.Collections.Generic;
using System.Linq;
using Lms.Core.Extensions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;

namespace Lms.Rpc.Routing.Template
{
    internal static class TemplateHelper
    {
        private static IDictionary<HttpMethod, string> constraintDefualtMethods = new Dictionary<HttpMethod, string>()
        {
            {HttpMethod.Get, "Get"},
            {HttpMethod.Post, "Create"},
            {HttpMethod.Put, "Update"},
            {HttpMethod.Delete, "Delete"},
        };


        public static string GenerateServerEntryTemplate(string routeTemplate, string methodEntryTemplate,
            HttpMethod httpMethod, bool isSpecify, string methodName)
        {
            var serverEntryTemplate = routeTemplate;
            var prefixRouteTemplate = routeTemplate;
            if (isSpecify)
            {
                if (methodEntryTemplate.IsNullOrEmpty())
                {
                    var constraintDefualtMethod = constraintDefualtMethods[httpMethod];
                    if (!constraintDefualtMethod.IsNullOrEmpty() &&
                        !methodName.StartsWith(constraintDefualtMethod, StringComparison.OrdinalIgnoreCase))
                    {
                        serverEntryTemplate = $"{prefixRouteTemplate}/{methodName}";
                    }
                }
                else
                {
                    serverEntryTemplate = $"{prefixRouteTemplate}/{methodEntryTemplate}";
                }
            }
            else
            {
                var constraintDefualtMethod = constraintDefualtMethods[httpMethod];
                if (!constraintDefualtMethod.IsNullOrEmpty() &&
                    !methodName.StartsWith(constraintDefualtMethod, StringComparison.OrdinalIgnoreCase))
                {
                    serverEntryTemplate = $"{prefixRouteTemplate}/{methodName}";
                }
            }

            return serverEntryTemplate;
        }
    }
}