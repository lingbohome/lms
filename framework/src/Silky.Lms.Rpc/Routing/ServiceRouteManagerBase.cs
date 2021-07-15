﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Silky.Lms.Core;
using Silky.Lms.Lock;
using Silky.Lms.Lock.Provider;
using Microsoft.Extensions.Options;
using Silky.Lms.Rpc.Address.Descriptor;
using Silky.Lms.Rpc.Configuration;
using Silky.Lms.Rpc.Routing.Descriptor;
using Silky.Lms.Rpc.Runtime.Server;
using Silky.Lms.Rpc.Runtime.Server.Descriptor;
using Silky.Lms.Rpc.Utils;

namespace Silky.Lms.Rpc.Routing
{
    public abstract class ServiceRouteManagerBase : IServiceRouteManager
    {
        protected readonly ServiceRouteCache _serviceRouteCache;
        protected readonly IServiceEntryManager _serviceEntryManager;
        protected readonly RegistryCenterOptions _registryCenterOptions;
        protected readonly RpcOptions _rpcOptions;
        protected readonly ILockerProvider _lockerProvider;

        protected ServiceRouteManagerBase(ServiceRouteCache serviceRouteCache,
            IServiceEntryManager serviceEntryManager,
            ILockerProvider lockerProvider,
            IOptions<RegistryCenterOptions> registryCenterOptions,
            IOptions<RpcOptions> rpcOptions)
        {
            _serviceRouteCache = serviceRouteCache;
            _serviceEntryManager = serviceEntryManager;
            _lockerProvider = lockerProvider;
            _registryCenterOptions = registryCenterOptions.Value;
            _rpcOptions = rpcOptions.Value;
            Check.NotNullOrEmpty(_registryCenterOptions.RoutePath, nameof(_registryCenterOptions.RoutePath));
            Check.NotNullOrEmpty(_rpcOptions.Token, nameof(_rpcOptions.Token));
            _serviceRouteCache.OnRemoveServiceRoutes += async descriptors =>
            {
                if (_rpcOptions.RemoveUnhealthServer)
                {
                    await RegisterRoutesWithLockAsync(descriptors);
                }
            };
        }

        public abstract Task CreateSubscribeDataChanges();

        public abstract Task CreateWsSubscribeDataChanges(string[] wsPaths);

        public abstract Task EnterRoutes();

        public virtual async Task RegisterRpcRoutes(double processorTime, ServiceProtocol serviceProtocol)
        {
            var hostAddr = NetUtil.GetRpcAddressModel();
            var localServiceEntries = _serviceEntryManager.GetLocalEntries()
                .Where(p => p.ServiceDescriptor.ServiceProtocol == serviceProtocol);
            var serviceRouteDescriptors = localServiceEntries.Select(p => p.CreateLocalRouteDescriptor());
            await RegisterRoutes(serviceRouteDescriptors, hostAddr.Descriptor);
        }

        public virtual async Task RegisterWsRoutes(double processorTime, Type[] wsAppServiceTypes, int wsPort)
        {
            var hostAddr = NetUtil.GetAddressModel(wsPort, ServiceProtocol.Ws);
            var serviceRouteDescriptors = wsAppServiceTypes.Select(p => new ServiceRouteDescriptor()
            {
                ServiceDescriptor = new ServiceDescriptor()
                {
                    Id = WebSocketResolverHelper.Generator(WebSocketResolverHelper.ParseWsPath(p)),
                    ServiceProtocol = ServiceProtocol.Ws,
                },
                AddressDescriptors = new[]
                {
                    hostAddr.Descriptor
                },
            });

            await RegisterRoutes(serviceRouteDescriptors, hostAddr.Descriptor);
        }

        protected virtual async Task RegisterRoutes(IEnumerable<ServiceRouteDescriptor> serviceRouteDescriptors,
            AddressDescriptor addressDescriptor)
        {
            await EnterRoutes();
            var registrationCentreServiceRoutes = _serviceRouteCache.ServiceRouteDescriptors.Where(p =>
                serviceRouteDescriptors.Any(q => q.ServiceDescriptor.Equals(p.ServiceDescriptor)));
            var centreServiceRoutes = registrationCentreServiceRoutes as ServiceRouteDescriptor[] ??
                                      registrationCentreServiceRoutes.ToArray();
            if (centreServiceRoutes.Any())
            {
                await RemoveExceptRouteAsyncs(registrationCentreServiceRoutes, addressDescriptor);
            }
            else
            {
                await CreateSubDirectory();
            }

            await RegisterRoutesWithLockAsync(serviceRouteDescriptors);
        }

        protected abstract Task CreateSubDirectory();
        
        protected async Task RegisterRoutesWithLockAsync(IEnumerable<ServiceRouteDescriptor> serviceRouteDescriptors)
        {
            using var locker = await _lockerProvider.CreateLockAsync("RegisterRoutes");
            
            await locker.Lock(async () =>
            {
                var registrationCentreServiceRoutes = _serviceRouteCache.ServiceRouteDescriptors.Where(p =>
                    serviceRouteDescriptors.Any(q => q.ServiceDescriptor.Equals(p.ServiceDescriptor)));
                foreach (var serviceRouteDescriptor in serviceRouteDescriptors)
                {
                    var centreServiceRoute = registrationCentreServiceRoutes.SingleOrDefault(p =>
                        p.ServiceDescriptor.Equals(serviceRouteDescriptor.ServiceDescriptor));
                    if (centreServiceRoute != null)
                    {
                        serviceRouteDescriptor.AddressDescriptors = serviceRouteDescriptor.AddressDescriptors
                            .Concat(centreServiceRoute.AddressDescriptors).Distinct().OrderBy(p => p.ToString());
                    }

                    await RegisterRouteAsync(serviceRouteDescriptor);
                }

            });
        }


        protected async Task RegisterRouteWithLockAsync(ServiceRouteDescriptor serviceRouteDescriptor)
        {
            using var locker = await _lockerProvider.CreateLockAsync(serviceRouteDescriptor.ServiceDescriptor.Id);
            await locker.Lock(async () => { await RegisterRouteAsync(serviceRouteDescriptor); });
        }

        protected abstract Task RegisterRouteAsync(ServiceRouteDescriptor serviceRouteDescriptor);

        protected virtual async Task RemoveExceptRouteAsyncs(
            IEnumerable<ServiceRouteDescriptor> serviceRouteDescriptors, AddressDescriptor addressDescriptor)
        {
            var oldServiceDescriptorIds =
                _serviceRouteCache.ServiceRouteDescriptors.Select(i => i.ServiceDescriptor.Id).ToArray();
            var newServiceDescriptorIds = serviceRouteDescriptors.Select(i => i.ServiceDescriptor.Id).ToArray();
            var removeServiceDescriptorIds = oldServiceDescriptorIds.Except(newServiceDescriptorIds).ToArray();
            
            using var locker = await _lockerProvider.CreateLockAsync("RemoveExceptRoute");
            await locker.Lock(async () =>
            {
                foreach (var removeServiceDescriptorId in removeServiceDescriptorIds)
                {
                    var removeRoute =
                        _serviceRouteCache.ServiceRouteDescriptors.FirstOrDefault(p =>
                            p.ServiceDescriptor.Id == removeServiceDescriptorId);
                    if (removeRoute != null && removeRoute.AddressDescriptors.Any())
                    {
                        if (removeRoute.AddressDescriptors.Any(p => p.Equals(addressDescriptor)))
                        {
                            removeRoute.AddressDescriptors =
                                removeRoute.AddressDescriptors.Where(p => !p.Equals(addressDescriptor)).ToList();
                            await RegisterRouteAsync(removeRoute);
                        }
                    }
                }
            });
            
           
        }
    }
}