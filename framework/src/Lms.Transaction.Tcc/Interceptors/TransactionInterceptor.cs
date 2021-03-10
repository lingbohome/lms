﻿using System.Diagnostics;
using System.Threading.Tasks;
using Lms.Core.DependencyInjection;
using Lms.Core.DynamicProxy;
using Lms.Rpc.Runtime.Server;
using Lms.Rpc.Transaction;
using Lms.Rpc.Transport;

namespace Lms.Transaction.Tcc.Interceptors
{
    public class TransactionInterceptor : LmsInterceptor, ITransientDependency
    {
        public async override Task InterceptAsync(ILmsMethodInvocation invocation)
        {
            var argumentsDictionary = invocation.ArgumentsDictionary;
            var serviceEntry = argumentsDictionary["serviceEntry"] as ServiceEntry;
            Debug.Assert(serviceEntry != null);
            if (!serviceEntry.IsTransactionServiceEntry())
            {
                await invocation.ProceedAsync();
            }
            else
            {
                var transactionContext =
                    RpcContext.GetContext().GetAttachment("transactionContext") as TransactionContext;
                await TransactionAspectInvoker.GetInstance().Invoke(transactionContext, invocation);
            }
        }
    }
}