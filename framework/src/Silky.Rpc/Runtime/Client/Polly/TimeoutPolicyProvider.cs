using System;
using Polly;
using Silky.Rpc.Runtime.Server;

namespace Silky.Rpc.Runtime.Client
{
    public class TimeoutPolicyProvider : IPolicyProvider
    {
        public IAsyncPolicy Create(ServiceEntry serviceEntry, object[] parameters)
        {
            if (serviceEntry.GovernanceOptions.TimeoutMillSeconds > 0)
            {
                return Policy.TimeoutAsync(
                    TimeSpan.FromMilliseconds(serviceEntry.GovernanceOptions.TimeoutMillSeconds));
            }

            return null;
        }
    }
}