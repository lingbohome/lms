using System.Collections.Generic;

namespace Silky.Http.Dashboard.AppService.Dtos
{
    public class GetGatewayOutput
    {
        public string HostName { get; set; }

        public int InstanceCount { get; set; }

        public int SupportServiceCount { get; set; }

        public int SupportServiceEntryCount { get; set; }

        public bool ExistWebSocketService { get; set; }
        
        public IEnumerable<string> SupportServices { get; set; }
    }
}