using System;
using Silky.Core.Rpc;
using Silky.Core.Serialization;
using Silky.Rpc.Diagnostics;
using Silky.SkyApm.Diagnostics.Abstraction.Factory;
using SkyApm;
using SkyApm.Config;
using SkyApm.Diagnostics;
using SkyApm.Tracing;
using SkyApm.Tracing.Segments;

namespace Silky.SkyApm.Diagnostics.Rpc.Client
{
    public class RpcClientTracingDiagnosticProcessor : ITracingDiagnosticProcessor
    {
        public string ListenerName { get; } = RpcDiagnosticListenerNames.DiagnosticClientListenerName;

        private readonly TracingConfig _tracingConfig;
        private readonly ISerializer _serializer;
        private readonly ISilkySegmentContextFactory _silkySegmentContextFactory;

        public RpcClientTracingDiagnosticProcessor(IConfigAccessor configAccessor,
            ISerializer serializer,
            ISilkySegmentContextFactory silkySegmentContextFactory)
        {
            _serializer = serializer;
            _silkySegmentContextFactory = silkySegmentContextFactory;
            _tracingConfig = configAccessor.Get<TracingConfig>();
        }

        [DiagnosticName(RpcDiagnosticListenerNames.BeginRpcRequest)]
        public void BeginRequest([Object] RpcInvokeEventData eventData)
        {
            var clientAddress = RpcContext.Context.GetClientAddress();
            var serverAddress = RpcContext.Context.GetServerAddress();
            var serviceKey = RpcContext.Context.GetServerKey();
            var context = _silkySegmentContextFactory.GetExitSContext(eventData.ServiceEntryId);
            context.Span.AddLog(
                LogEvent.Event("Rpc Client Begin Invoke"),
                LogEvent.Message($"Rpc Client Invoke {Environment.NewLine}" +
                                 $"--> ServiceEntryId:{eventData.ServiceEntryId}.{Environment.NewLine}" +
                                 $"--> ServiceKey:{serviceKey}{Environment.NewLine}" +
                                 $"--> MessageId:{eventData.MessageId}.{Environment.NewLine}" +
                                 $"--> Parameters:{_serializer.Serialize(eventData.Message.Parameters)}.{Environment.NewLine}" +
                                 $"--> Attachments:{_serializer.Serialize(eventData.Message.Attachments)}"));

            context.Span.AddTag(SilkyTags.RPC_SERVICEENTRYID, eventData.ServiceEntryId.ToString());
            context.Span.AddTag(SilkyTags.SERVICEKEY, serviceKey);
            context.Span.AddTag(SilkyTags.RPC_CLIENT_ADDRESS, clientAddress);
            context.Span.AddTag(SilkyTags.RPC_SERVER_ADDRESS, serverAddress);
            context.Span.AddTag(SilkyTags.ISGATEWAY, RpcContext.Context.IsGateway());
        }

        [DiagnosticName(RpcDiagnosticListenerNames.EndRpcRequest)]
        public void EndRequest([Object] RpcInvokeResultEventData eventData)
        {
            var context = _silkySegmentContextFactory.GetExitSContext(eventData.ServiceEntryId);
            context.Span.AddLog(LogEvent.Event("Rpc Client Invoke End"),
                LogEvent.Message(
                    $"Rpc Invoke Succeeded!{Environment.NewLine}" +
                    $"--> Spend Time: {eventData.ElapsedTimeMs}ms.{Environment.NewLine}" +
                    $"--> ServiceEntryId: {eventData.ServiceEntryId}.{Environment.NewLine}" +
                    $"--> MessageId: {eventData.MessageId}.{Environment.NewLine}" +
                    $"--> Result: {_serializer.Serialize(eventData.Result)}"));

            context.Span.AddTag(SilkyTags.ELAPSED_TIME, $"{eventData.ElapsedTimeMs}");
            context.Span.AddTag(SilkyTags.RPC_STATUSCODE, $"{eventData.StatusCode}");
            _silkySegmentContextFactory.ReleaseContext(context);
        }

        [DiagnosticName(RpcDiagnosticListenerNames.ErrorRpcRequest)]
        public void RpcError([Object] RpcInvokeExceptionEventData eventData)
        {
            var context = _silkySegmentContextFactory.GetExitSContext(eventData.ServiceEntryId);
            context.Span?.AddTag(SilkyTags.RPC_STATUSCODE, $"{eventData.StatusCode}");
            context.Span?.ErrorOccurred(eventData.Exception, _tracingConfig);
            _silkySegmentContextFactory.ReleaseContext(context);
        }
    }
}