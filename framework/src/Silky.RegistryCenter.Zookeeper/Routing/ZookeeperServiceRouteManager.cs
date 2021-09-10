﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Silky.Core.DependencyInjection;
using Silky.Core.Extensions;
using Silky.Core.Serialization;
using Silky.Rpc.Address.Descriptor;
using Silky.Rpc.Configuration;
using Silky.Rpc.Routing;
using Silky.Rpc.Routing.Descriptor;
using Silky.Rpc.Runtime.Server;
using Silky.Rpc.Utils;
using Silky.Zookeeper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using org.apache.zookeeper;
using Silky.Core;
using Silky.Lock.Extensions;
using Silky.RegistryCenter.Zookeeper.Routing.Watchers;
using Silky.Rpc.Address;

namespace Silky.RegistryCenter.Zookeeper.Routing
{
    public class ZookeeperServiceRouteManager : ServiceRouteManagerBase, ISingletonDependency
    {
        private readonly IZookeeperClientProvider _zookeeperClientProvider;
        private readonly ISerializer _serializer;
        public ILogger<ZookeeperServiceRouteManager> Logger { get; set; }

        private ConcurrentDictionary<(string, IZookeeperClient), ServiceRouteWatcher> m_routeWatchers = new();

        private ConcurrentDictionary<(string, IZookeeperClient), ServiceRouteSubDirectoryWatcher>
            m_routeSubDirWatchers = new();

        public ZookeeperServiceRouteManager(ServiceRouteCache serviceRouteCache,
            IServiceManager serviceManager,
            IZookeeperClientProvider zookeeperClientProvider,
            IOptionsMonitor<RegistryCenterOptions> registryCenterOptions,
            IOptionsMonitor<RpcOptions> rpcOptions,
            ISerializer serializer)
            : base(serviceRouteCache,
                serviceManager,
                registryCenterOptions,
                rpcOptions)
        {
            _zookeeperClientProvider = zookeeperClientProvider;
            _serializer = serializer;
            Logger = NullLogger<ZookeeperServiceRouteManager>.Instance;
        }


        protected override async Task RegisterRouteAsync(ServiceRouteDescriptor serviceRouteDescriptor)
        {
            var zookeeperClients = _zookeeperClientProvider.GetZooKeeperClients();
            foreach (var zookeeperClient in zookeeperClients)
            {
                var synchronizationProvider = zookeeperClient.GetSynchronizationProvider();
                var @lock = synchronizationProvider.CreateLock(string.Format(LockName.RegisterRoute,
                    serviceRouteDescriptor.Service.Id));
                await @lock.ExecForHandle(async () =>
                {
                    var routePath = CreateRoutePath(serviceRouteDescriptor.Service);
                    // The latest routing data must be obtained from the service registry.
                    // When the service is expanded and contracted, the locally cached routing data is not the latest
                    var centreServiceRoute = await GetRouteDescriptorAsync(routePath, zookeeperClient);
                    if (centreServiceRoute != null)
                    {
                        serviceRouteDescriptor.Addresses = serviceRouteDescriptor.Addresses
                            .Concat(centreServiceRoute.Addresses).Distinct().OrderBy(p => p.ToString());
                    }

                    var jsonString = _serializer.Serialize(serviceRouteDescriptor);
                    var data = jsonString.GetBytes();
                    if (!await zookeeperClient.ExistsAsync(routePath))
                    {
                        await zookeeperClient.CreateRecursiveAsync(routePath, data, ZooDefs.Ids.OPEN_ACL_UNSAFE);
                        Logger.LogDebug($"Node {routePath} does not exist and will be created");
                    }
                    else
                    {
                        await zookeeperClient.SetDataAsync(routePath, data);
                        Logger.LogDebug($"The cached routing data of the {routePath} node has been updated.");
                    }
                });
            }
        }

        protected override async Task RemoveExceptRouteAsync(AddressDescriptor addressDescriptor)
        {
            var zookeeperClients = _zookeeperClientProvider.GetZooKeeperClients();
            foreach (var zookeeperClient in zookeeperClients)
            {
                var allServiceRouteDescriptor = await GetServiceRouteDescriptors(zookeeperClient);
                var serviceRouteDescriptors = allServiceRouteDescriptor as ServiceRouteDescriptor[] ??
                                              allServiceRouteDescriptor.ToArray();
                if (!serviceRouteDescriptors.Any())
                {
                    continue;
                }

                var removeExceptRouteDescriptors = serviceRouteDescriptors.Where(p =>
                    p.Addresses.Any(p => p.Equals(addressDescriptor))
                    && p.Service.Application != EngineContext.Current.HostName
                );
                var lockProvider = zookeeperClient.GetSynchronizationProvider();
                foreach (var removeExceptRouteDescriptor in removeExceptRouteDescriptors)
                {
                    var @lock = lockProvider.CreateLock(string.Format(LockName.RegisterRoute,
                        removeExceptRouteDescriptor.Service.Id));
                    await @lock.ExecForHandle(async () =>
                    {
                        var routePath = CreateRoutePath(removeExceptRouteDescriptor.Service);
                        removeExceptRouteDescriptor.Addresses =
                            removeExceptRouteDescriptor.Addresses
                                .Where(p => !p.Equals(addressDescriptor))
                                .ToList();
                        var jsonString = _serializer.Serialize(removeExceptRouteDescriptor);
                        var data = jsonString.GetBytes();
                        await zookeeperClient.SetDataAsync(routePath, data);
                    });
                }
            }
        }

        protected override async Task CreateSubDirectoryIfNotExistAndSubscribeChildrenChange()
        {
            var zookeeperClients = _zookeeperClientProvider.GetZooKeeperClients();
            foreach (var zookeeperClient in zookeeperClients)
            {
                await CreateSubDirectoryIfNotExistAndSubscribeChildrenChange(zookeeperClient);
            }
        }

        private async Task CreateSubDirectoryIfNotExistAndSubscribeChildrenChange(IZookeeperClient zookeeperClient)
        {
            var subDirectoryPath = _registryCenterOptions.RoutePath;
            try
            {
                var synchronizationProvider = zookeeperClient.GetSynchronizationProvider();
                var @lock = synchronizationProvider.CreateLock(
                    string.Format(LockName.CreateSubDirectoryIfNotExistAndSubscribeChildrenChange,
                        subDirectoryPath.Replace("/", "_")));
                await @lock.ExecForHandle(async () =>
                {
                    if (!await zookeeperClient.ExistsAsync(subDirectoryPath))
                    {
                        await zookeeperClient.CreateRecursiveAsync(subDirectoryPath, null, ZooDefs.Ids.OPEN_ACL_UNSAFE);
                    }
                });
            }
            catch (KeeperException.NodeExistsException e)
            {
                Logger.LogWarning("The directory {subDirectoryPath}has been created", e);
            }

            await CreateSubscribeChildrenChange(zookeeperClient, subDirectoryPath);
        }

        public override async Task EnterRoutes()
        {
            var zookeeperClients = _zookeeperClientProvider.GetZooKeeperClients();
            foreach (var zookeeperClient in zookeeperClients)
            {
                var serviceRouteDescriptors = await GetServiceRouteDescriptors(zookeeperClient);
                var routeDescriptors = serviceRouteDescriptors as ServiceRouteDescriptor[] ??
                                       serviceRouteDescriptors.ToArray();
                if (routeDescriptors.Any())
                {
                    foreach (var serviceRouteDescriptor in routeDescriptors)
                    {
                        _serviceRouteCache.UpdateCache(serviceRouteDescriptor);
                    }

                    break;
                }
            }
        }

        private async Task<IEnumerable<ServiceRouteDescriptor>> GetServiceRouteDescriptors(
            IZookeeperClient zookeeperClient)
        {
            var serviceRouteDescriptors = new List<ServiceRouteDescriptor>();
            if (await zookeeperClient.ExistsAsync(_registryCenterOptions.RoutePath))
            {
                var children =
                    await zookeeperClient.GetChildrenAsync(_registryCenterOptions.RoutePath);
                foreach (var child in children)
                {
                    var routePath = CreateRoutePath(child);
                    var serviceRouteDescriptor = await GetRouteDescriptorAsync(routePath, zookeeperClient);
                    serviceRouteDescriptors.Add(serviceRouteDescriptor);
                }
            }

            return serviceRouteDescriptors;
        }


        private async Task<ServiceRouteDescriptor> GetRouteDescriptorAsync(string routePath,
            IZookeeperClient zookeeperClient)
        {
            if (!await zookeeperClient.ExistsAsync(routePath))
            {
                return null;
            }

            var data = await zookeeperClient.GetDataAsync(routePath);
            if (data == null || !data.Any())
            {
                return null;
            }

            var jsonString = data.ToArray().GetString();
            return _serializer.Deserialize<ServiceRouteDescriptor>(jsonString);
        }

        private string CreateRoutePath(ServiceDescriptor serviceDescriptor)
        {
            return CreateRoutePath(serviceDescriptor.Id);
        }

        private string CreateRoutePath(string child)
        {
            var routePath = _registryCenterOptions.RoutePath;
            if (!routePath.EndsWith("/"))
            {
                routePath += "/";
            }

            routePath += child;
            return routePath;
        }

        protected override async Task RemoveUnHealthServiceRoute(string serviceId, IAddressModel addressModel)
        {
            var zookeeperClients = _zookeeperClientProvider.GetZooKeeperClients();
            foreach (var zookeeperClient in zookeeperClients)
            {
                var routePath = CreateRoutePath(serviceId);

                var lockProvider = zookeeperClient.GetSynchronizationProvider();
                var @lock = lockProvider.CreateLock(
                    string.Format(LockName.RegisterRoute, serviceId));
                await @lock.ExecForHandle(async () =>
                {
                    var serviceCenterDescriptor = await GetRouteDescriptorAsync(routePath, zookeeperClient);
                    if (serviceCenterDescriptor != null &&
                        serviceCenterDescriptor.Addresses.Any(p => p.Equals(addressModel.Descriptor)))
                    {
                        serviceCenterDescriptor.Addresses =
                            serviceCenterDescriptor.Addresses.Where(
                                p => !p.Equals(addressModel.Descriptor));
                        var jsonString = _serializer.Serialize(serviceCenterDescriptor);
                        var data = jsonString.GetBytes();
                        await zookeeperClient.SetDataAsync(routePath, data);
                    }
                });
            }
        }


        public override async Task CreateSubscribeServiceRouteDataChanges()
        {
            var allServices = _serviceManager.GetAllService();
            foreach (var service in allServices)
            {
                var serviceRoutePath = CreateRoutePath(service.Id);
                var zookeeperClients = _zookeeperClientProvider.GetZooKeeperClients();
                
                foreach (var zookeeperClient in zookeeperClients)
                {
                    await CreateSubscribeDataChange(zookeeperClient, serviceRoutePath);
                    if (EngineContext.Current.IsContainHttpCoreModule())
                    {
                        var wsServiceId =
                            WebSocketResolverHelper.Generator(WebSocketResolverHelper.ParseWsPath(service.ServiceType));
                        var wsServiceRoutePath = CreateRoutePath(wsServiceId);
                        await CreateSubscribeDataChange(zookeeperClient, wsServiceRoutePath);
                    }
                }
            }
        }
        
        internal async Task CreateSubscribeDataChange(IZookeeperClient zookeeperClient, string path)
        {
            var watcher = new ServiceRouteWatcher(path, _serviceRouteCache, _serializer);
            await zookeeperClient.SubscribeDataChange(path, watcher.HandleNodeDataChange);
            m_routeWatchers.AddOrUpdate((path, zookeeperClient), watcher, (k, v) => watcher);
        }

        private async Task CreateSubscribeChildrenChange(IZookeeperClient zookeeperClient, string path)
        {
            var watcher = new ServiceRouteSubDirectoryWatcher(path, this);
            await zookeeperClient.SubscribeChildrenChange(path, watcher.SubscribeChildrenChange);
            m_routeSubDirWatchers.AddOrUpdate((path, zookeeperClient), watcher, (k, v) => watcher);
        }

        public async Task RemoveLocalHostServiceRoute()
        {
            var serviceRouteDescriptors = _serviceRouteCache.ServiceRouteDescriptors
                .Where(p => p.Addresses.Any(p =>
                    p.Address == NetUtil.GetHostAddress(_rpcOptions.Host)));

            foreach (var serviceRouteDescriptor in serviceRouteDescriptors)
            {
                serviceRouteDescriptor.Addresses =
                    serviceRouteDescriptor.Addresses.Where(p =>
                        p != p.ConvertToAddressModel().Descriptor);
                var zookeeperClients = _zookeeperClientProvider.GetZooKeeperClients();
                foreach (var zookeeperClient in zookeeperClients)
                {
                    var lockProvider = zookeeperClient.GetSynchronizationProvider();
                    var routePath = CreateRoutePath(serviceRouteDescriptor.Service);
                    var @lock = lockProvider.CreateLock(
                        string.Format(LockName.RegisterRoute, serviceRouteDescriptor.Service.Id));
                    await using (await @lock.AcquireAsync())
                    {
                        var jsonString = _serializer.Serialize(serviceRouteDescriptor);
                        var data = jsonString.GetBytes();
                        await zookeeperClient.SetDataAsync(routePath, data);
                    }
                }
            }
        }
    }
}