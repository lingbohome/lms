using System.Collections.Generic;
using System.Threading.Tasks;
using Castle.Core.Internal;
using Microsoft.Extensions.Logging;
using Silky.Core;
using Silky.Core.DynamicProxy;
using Silky.Rpc.Runtime;
using Silky.Rpc.Runtime.Server;
using Silky.Rpc.Runtime.Server.Parameter;
using Silky.Rpc.Transport;
using Silky.Transaction.Repository;
using Silky.Transaction.Abstraction;
using Silky.Transaction.Abstraction.Participant;
using Silky.Transaction.Tcc.Executor;

namespace Silky.Transaction.Tcc
{
    public static class ParticipantExtensions
    {
        public static ILogger<TccTransactionExecutor> Logger =
            EngineContext.Current.Resolve<ILogger<TccTransactionExecutor>>();

        public static async Task Executor(this IParticipant participant, ActionStage stage,
            ISilkyMethodInvocation invocation = null)

        {
            SetContext(stage, participant);
            var serviceEntryLocator = EngineContext.Current.Resolve<IServiceEntryLocator>();
            var serviceEntry = serviceEntryLocator.GetServiceEntryById(participant.ServiceId);

            async Task LocalExecutor(ISilkyMethodInvocation localInvocation, IParticipant localParticipant,
                TccMethodType methodType)
            {
                if (localInvocation != null)
                {
                    await localInvocation.ExcuteTccMethod(methodType, RpcContext.GetContext().GetTransactionContext());
                }
                else if (localParticipant.Invocation != null)
                {
                    await localParticipant.Invocation.ExcuteTccMethod(methodType,
                        RpcContext.GetContext().GetTransactionContext());
                }
                else
                {
                    var (excutor, instance) = serviceEntry.GetTccExcutorInfo(localParticipant.ServiceKey, methodType);
                    if (excutor != null && instance != null)
                    {
                        var actualParameters = new List<object>();
                        var i = 0;
                        if (!serviceEntry.ParameterDescriptors.IsNullOrEmpty())
                        {
                            foreach (var parameterDescriptor in serviceEntry.ParameterDescriptors)
                            {
                                actualParameters.Add(
                                    parameterDescriptor.GetActualParameter(localParticipant.Parameters[i]));
                            }
                        }

                        await excutor?.ExecuteTccMethodAsync(instance, actualParameters.ToArray());
                    }
                }
            }

            if (serviceEntry.IsLocal)
            {
                participant.Status = stage;
                await TransRepositoryStore.UpdateParticipantStatus(participant);

                if (stage == ActionStage.Confirming)
                {
                    await LocalExecutor(invocation, participant, TccMethodType.Confirm);
                }
                else
                {
                    await LocalExecutor(invocation, participant, TccMethodType.Cancel);
                }

                await TransRepositoryStore.RemoveParticipant(participant);
            }
            else
            {
                RpcContext.GetContext().SetTransactionContext(SilkyTransactionContextHolder.Instance.Get());
                var serviceExecutor = EngineContext.Current.Resolve<IServiceExecutor>();
                await serviceExecutor.Execute(serviceEntry, participant.Parameters, participant.ServiceKey);
            }
        }


        private static void SetContext(ActionStage action, IParticipant participant)
        {
            var context = new TransactionContext()
            {
                Action = action,
                TransId = participant.TransId,
                ParticipantId = participant.ParticipantId,
                TransactionRole = participant.Role,
                TransType = TransactionType.Tcc
            };

            SilkyTransactionContextHolder.Instance.Set(context);
        }
    }
}