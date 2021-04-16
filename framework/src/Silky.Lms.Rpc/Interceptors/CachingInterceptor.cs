﻿using System.Linq;
using System.Threading.Tasks;
using Silky.Lms.Caching;
using Silky.Lms.Core.DependencyInjection;
using Silky.Lms.Core.DynamicProxy;
using Silky.Lms.Core.Extensions;
using Silky.Lms.Rpc.Runtime.Server;

namespace Silky.Lms.Rpc.Interceptors
{
    public class CachingInterceptor : LmsInterceptor, ITransientDependency
    {
        private readonly IDistributedInterceptCache _distributedCache;

        public CachingInterceptor(IDistributedInterceptCache distributedCache)
        {
            _distributedCache = distributedCache;
        }

        public override async Task InterceptAsync(ILmsMethodInvocation invocation)
        {
            var serviceEntry = invocation.ArgumentsDictionary["serviceEntry"] as ServiceEntry;
            var serviceKey = invocation.ArgumentsDictionary["serviceKey"] as string;
            var parameters = invocation.ArgumentsDictionary["parameters"] as object[];

            async Task<object> GetResultFirstFromCache(string cacheName, string cacheKey, ServiceEntry entry)
            {
                _distributedCache.UpdateCacheName(cacheName);
                return await _distributedCache.GetOrAddAsync(cacheKey,
                    serviceEntry.MethodInfo.GetReturnType(),
                    async () => await entry.Executor(serviceKey, parameters));
            }

            if (serviceEntry.GovernanceOptions.CacheEnabled)
            {
                var removeCachingInterceptProviders = serviceEntry.RemoveCachingInterceptProviders;
                if (removeCachingInterceptProviders.Any())
                {
                    foreach (var removeCachingInterceptProvider in removeCachingInterceptProviders)
                    {
                        var removeCacheKey =
                            serviceEntry.GetCachingInterceptKey(parameters, removeCachingInterceptProvider);
                        await _distributedCache.RemoveAsync(removeCacheKey, removeCachingInterceptProvider.CacheName,
                            true);
                    }
                }

                if (serviceEntry.GetCachingInterceptProvider != null)
                {
                    if (serviceEntry.IsTransactionServiceEntry())
                    {
                        await invocation.ProceedAsync();
                    }
                    else
                    {
                        var getCacheKey = serviceEntry.GetCachingInterceptKey(parameters,
                            serviceEntry.GetCachingInterceptProvider);
                        invocation.ReturnValue = await GetResultFirstFromCache(
                            serviceEntry.GetCacheName(),
                            getCacheKey,
                            serviceEntry);
                    }
                }
                else if (serviceEntry.UpdateCachingInterceptProvider != null)
                {
                    if (serviceEntry.IsTransactionServiceEntry())
                    {
                        await invocation.ProceedAsync();
                    }
                    else
                    {
                        var updateCacheKey = serviceEntry.GetCachingInterceptKey(parameters,
                            serviceEntry.UpdateCachingInterceptProvider);
                        await _distributedCache.RemoveAsync(updateCacheKey, serviceEntry.GetCacheName(),
                            hideErrors: true);
                        invocation.ReturnValue = await GetResultFirstFromCache(
                            serviceEntry.GetCacheName(),
                            updateCacheKey,
                            serviceEntry);
                    }
                }
                else
                {
                    await invocation.ProceedAsync();
                }
            }
            else
            {
                await invocation.ProceedAsync();
            }
        }
    }
}