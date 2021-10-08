using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Silky.Core.Rpc;
using Silky.HealthChecks.Rpc.ServerCheck;
using Silky.Rpc.Endpoint;
using Silky.Rpc.Runtime.Server;
using Silky.Rpc.Security;

namespace Silky.HealthChecks.Rpc
{
    public class SilkyRpcHealthCheck : IHealthCheck
    {
        private readonly IServerManager _serverManager;
        private readonly IServerHealthCheck _serverHealthCheck;
        private readonly ICurrentRpcToken _currentRpcToken;

        public SilkyRpcHealthCheck(IServerManager serverManager,
            IServerHealthCheck serverHealthCheck,
            ICurrentRpcToken currentRpcToken)
        {
            _serverManager = serverManager;
            _serverHealthCheck = serverHealthCheck;
            _currentRpcToken = currentRpcToken;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = new CancellationToken())
        {
            var servers = _serverManager.Servers;
            var healthData = new Dictionary<string, object>();
            _currentRpcToken.SetRpcToken();
            foreach (var server in servers)
            {
                foreach (var endpoint in server.Endpoints)
                {
                    if (endpoint.ServiceProtocol == ServiceProtocol.Tcp)
                    {
                        var endpointHealthData = new ServerHealthData()
                        {
                            HostName = server.HostName,
                            Address = endpoint.GetAddress(),
                            ServiceProtocol = endpoint.ServiceProtocol,
                        };
                        bool isHealth;
                        try
                        {
                            isHealth = await _serverHealthCheck.IsHealth(endpoint);
                        }
                        catch (Exception e)
                        {
                            isHealth = false;
                        }

                        endpointHealthData.Health = isHealth;
                        healthData[endpoint.GetAddress()] = endpointHealthData;
                    }
                }
            }

            if (healthData.Values.All(p => ((ServerHealthData)p).Health))
            {
                return HealthCheckResult.Healthy("Silky Rpc Health", healthData);
            }

            if (healthData.Values.All(p => !((ServerHealthData)p).Health))
            {
                return HealthCheckResult.Unhealthy("Silky Rpc Unhealthy", null, healthData);
            }

            return HealthCheckResult.Degraded("Silky Rpc Degraded", null, healthData);
        }
    }
}