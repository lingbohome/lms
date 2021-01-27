using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lms.Core;
using Lms.Rpc.Runtime.Server.ServiceEntry.ServiceDiscovery;

namespace Lms.Rpc.Runtime.Server.ServiceEntry
{
    internal class ServiceEntryHelper
    {
        public static IEnumerable<Type> FindServiceLocalEntryTypes(ITypeFinder typeFinder)
        {
            var types = typeFinder.GetAssemblies()
                    .SelectMany(p => p.ExportedTypes)
                    .Where(p=> p.IsClass
                               && !p.IsAbstract
                               && p.GetInterfaces().Any(i=> i.GetCustomAttributes().Any(a=> a is ServiceBundleAttribute))
                    )
                ;
            return types;
        }

        public static IEnumerable<(Type,bool)> FindAllServiceEntryTypes(ITypeFinder typeFinder)
        {
            var entryTypes = new List<(Type, bool)>();
            var exportedTypes = typeFinder.GetAssemblies()
                .SelectMany(p => p.ExportedTypes);

            var entryInterfaces = exportedTypes
                    .Where(p => p.IsInterface
                                && p.GetCustomAttributes().Any(a => a is ServiceBundleAttribute)
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
        
    }
}