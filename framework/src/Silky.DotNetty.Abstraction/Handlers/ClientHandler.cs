using System.Net;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Silky.Rpc.Endpoint.Monitor;
using Silky.Rpc.Runtime;
using Silky.Rpc.Transport.Messages;

namespace Silky.DotNetty.Handlers
{
    public class ClientHandler : ChannelHandlerAdapter
    {
        private readonly IMessageListener _messageListener;
        private readonly IRpcEndpointMonitor _rpcEndpointMonitor;

        public ClientHandler(IMessageListener messageListener, IRpcEndpointMonitor rpcEndpointMonitor)
        {
            _messageListener = messageListener;
            _rpcEndpointMonitor = rpcEndpointMonitor;
        }

        public async override void ChannelRead(IChannelHandlerContext context, object message)
        {
            var transportMessage = (TransportMessage)message;
            await _messageListener.OnReceived(null, transportMessage);
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            var remoteAddress = context.Channel.RemoteAddress as IPEndPoint;
            if (remoteAddress != null)
            {
                _rpcEndpointMonitor.RemoveRpcEndpoint(remoteAddress.Address.MapToIPv4(), remoteAddress.Port);
            }
        }

        public async override Task CloseAsync(IChannelHandlerContext context)
        {
            var remoteAddress = context.Channel.RemoteAddress as IPEndPoint;
            if (remoteAddress != null)
            {
                _rpcEndpointMonitor.RemoveRpcEndpoint(remoteAddress.Address.MapToIPv4(), remoteAddress.Port);
            }

            await base.CloseAsync(context);
        }
    }
}