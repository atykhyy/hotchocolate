using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading;
using HotChocolate.Execution.Processing.Plan;

namespace HotChocolate.Execution.Processing
{
    internal class DeferredTaskExecutor : IAsyncEnumerable<IQueryResult>
    {
        private readonly IOperationContextOwner _operationContextOwner;

        public DeferredTaskExecutor(IOperationContextOwner operationContextOwner)
        {
            _operationContextOwner = operationContextOwner ??
                throw new ArgumentNullException(nameof(operationContextOwner));
        }

        public async IAsyncEnumerator<IQueryResult> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
        {
            try
            {
                IOperationContext context = _operationContextOwner.OperationContext;
                QueryPlan rootQueryPlan = context.QueryPlan;

                while (context.Scheduler.DeferredWork.TryTake(
                    out IDeferredExecutionTask? deferredTask))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    context.Result.Clear();

                    IQueryResult result =
                        await deferredTask.ExecuteAsync(context).ConfigureAwait(false);

                    if (!deferredTask.IsCompleted)
                    {
                        context.Scheduler.DeferredWork.Register(deferredTask);
                    }

                    yield return result;
                }
            }
            finally
            {
                _operationContextOwner.Dispose();
            }
        }
    }
}
