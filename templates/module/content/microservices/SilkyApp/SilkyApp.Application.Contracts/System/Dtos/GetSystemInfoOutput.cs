namespace SilkyApp.Application.Contracts.System.Dtos
{
    public class GetSystemInfoOutput
    {
        /// <summary>
        /// 主机名称
        /// </summary>
        public string HostName { get; set; }
        
        /// <summary>
        /// 运行环境
        /// </summary>
        public string Environment { get; set; }
        
        /// <summary>
        /// 应用名称
        /// </summary>
        public string AppName { get; set; }
    }
}