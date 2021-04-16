﻿using System.Net;
using Silky.Lms.Core.DependencyInjection;
using Silky.Lms.Rpc.Address.Descriptor;

namespace Silky.Lms.Rpc.Address.HealthCheck
{
    public interface IHealthCheck : ISingletonDependency
    {
        void Monitor(IAddressModel addressModel);

        bool IsHealth(IPEndPoint ipEndPoint);

        bool IsHealth(AddressDescriptor addressDescriptor);

        bool IsHealth(IAddressModel addressModel);

        void ChangeHealthStatus(IAddressModel addressModel, bool isHealth, int unHealthCeilingTimes = 0);

        void RemoveAddress(IAddressModel addressModel);

        void RemoveAddress(IPAddress ipAddress, int port);
        
        event HealthChangeEvent OnHealthChange;
        event RemoveAddressEvent OnRemveAddress;
        event UnhealthEvent OnUnhealth;
        event AddMonitorEvent OnAddMonitor;
        
    }
}