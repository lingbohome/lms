using Polly;
using Silky.Core.DependencyInjection;
using Silky.Rpc.Runtime.Server;

namespace Silky.Rpc.Runtime.Client
{
    public interface IInvokePolicyProvider : IScopedDependency
    {
        IAsyncPolicy Create(ServiceEntry serviceEntry, object[] parameters);
    }
}