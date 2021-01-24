using System.Reflection;
using Lms.Core.DependencyInjection;

namespace Lms.Rpc.Ids
{
    public interface IServiceIdGenerator : ITransientDependency
    {
        string GenerateServiceId(MethodInfo method);
    }
}