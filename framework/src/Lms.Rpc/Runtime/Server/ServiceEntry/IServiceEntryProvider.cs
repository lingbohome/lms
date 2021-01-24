using System.Collections.Generic;
using Lms.Core.DependencyInjection;

namespace Lms.Rpc.Runtime.Server.ServiceEntry
{
    public interface IServiceEntryProvider : ITransientDependency
    {
        IReadOnlyList<ServiceEntry> GetEntries();
    }
}