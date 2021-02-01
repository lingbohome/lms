﻿using Lms.Core.Exceptions;
using Lms.Rpc.Address;
using Lms.Rpc.Routing.Descriptor;
using Lms.Rpc.Utils;

namespace Lms.Rpc.Runtime.Server.ServiceEntry
{
    public static class ServiceEntryExtensions
    {
        public static ServiceRouteDescriptor CreateLocalRouteDescriptor(this ServiceEntry serviceEntry,
            AddressType addressType)
        {
            if (!serviceEntry.IsLocal)
            {
                throw new LmsException("只允许本地服务条目生产路由描述符");
            }

            return new ServiceRouteDescriptor()
            {
                ServiceDescriptor = serviceEntry.ServiceDescriptor,
                AddressDescriptors = new[] {NetUtil.GetHostAddress(addressType).Descriptor},
            };
        }
    }
}