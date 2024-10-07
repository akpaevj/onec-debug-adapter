using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;

namespace Onec.DebugAdapter.DebugServer
{
    public interface IDebugServerListener
    {
        event EventHandler<CallStackFormedEventArgs>? CallStackFormed;
        event EventHandler<DebugTargetEventArgs>? DebugTargetEvent;
        event EventHandler<ExpressionEvaluatedEventArgs>? ExpressionEvaluated;
        event EventHandler<RuntimeExceptionArgs>? RuntimeException;

        void Run(DebugProtocolClient debugProtocolClient, CancellationToken cancellationToken);
    }
}