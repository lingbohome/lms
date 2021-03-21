using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lms.Core;
using Lms.Rpc.Address;
using Lms.Rpc.Runtime.Server.ServiceDiscovery;

namespace Lms.Rpc.Runtime.Server
{
    public static class ServiceEntryHelper
    {
        internal static IEnumerable<Type> FindServiceLocalEntryTypes(ITypeFinder typeFinder)
        {
            var types = typeFinder.GetAssemblies()
                    .SelectMany(p => p.ExportedTypes)
                    .Where(p => p.IsClass
                                && !p.IsAbstract
                                && !p.IsGenericType
                                && p.GetInterfaces().Any(i =>
                                    i.GetCustomAttributes().Any(a => a is ServiceRouteAttribute))
                    )
                    .OrderBy(p =>
                        p.GetCustomAttributes().OfType<ServiceKeyAttribute>().Select(q => q.Weight).FirstOrDefault()
                    )
                ;
            return types;
        }

        internal static IEnumerable<(Type, bool)> FindAllServiceEntryTypes(ITypeFinder typeFinder)
        {
            var entryTypes = new List<(Type, bool)>();
            var exportedTypes = typeFinder.GetAssemblies()
                .SelectMany(p => p.ExportedTypes);

            var entryInterfaces = exportedTypes
                    .Where(p => p.IsInterface
                                && p.GetCustomAttributes().Any(a => a is ServiceRouteAttribute)
                                && !p.IsGenericType
                    )
                ;
            foreach (var entryInterface in entryInterfaces)
            {
                entryTypes.Add(exportedTypes.Any(t => entryInterface.IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
                    ? (entryInterface, true)
                    : (entryInterface, false));
            }

            return entryTypes;
        }

        public static IEnumerable<Type> FindServiceEntryProxyTypes(ITypeFinder typeFinder)
        {
            var proxyTypes = FindAllServiceEntryTypes(typeFinder).Where(p => !p.Item2)
                .Select(p => p.Item1);
            return proxyTypes;
        }

        public static IEnumerable<(Type, bool)> FindWsServiceTypeInfos(ITypeFinder typeFinder)
        {
            var entryTypes = new List<(Type, bool)>();
            var exportedTypes = typeFinder.GetAssemblies()
                .SelectMany(p => p.ExportedTypes);

            var entryInterfaces = exportedTypes
                    .Where(p => p.IsInterface
                                && p.GetCustomAttributes().Any(a =>
                                    (a as IRouteTemplateProvider)?.ServiceProtocol == ServiceProtocol.Ws)
                                && !p.IsGenericType
                    )
                ;
            foreach (var entryInterface in entryInterfaces)
            {
                entryTypes.Add(exportedTypes.Any(t => entryInterface.IsAssignableFrom(t)
                                                      && t.IsClass
                                                      && t.BaseType?.FullName ==
                                                      "Lms.DotNetty.Protocol.Ws.WsAppServiceBase"
                                                      && !t.IsAbstract)
                    ? (entryInterface, true)
                    : (entryInterface, false));
            }

            return entryTypes;
        }
    }
}