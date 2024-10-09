using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;

namespace Onec.DebugAdapter.DebugServer
{
    public interface IDebugServerListener
    {
        event EventHandler<CallStackFormedEventArgs>? CallStackFormed;
        event EventHandler<DebugTargetEventArgs>? DebugTargetEvent;
        event EventHandler<ExpressionEvaluatedEventArgs>? ExpressionEvaluated;
        event EventHandler<RuntimeExceptionArgs>? RuntimeException;
        event EventHandler<SetForegroundHelperArgs>? SetForegroundHelper;
        event EventHandler<ForegroundHelperRequestArgs>? ForegroundHelperRequested;
        event EventHandler<ProcessForegroundHelperArgs>? ProcessForegroundHelper;
        event EventHandler<ShowMetadataObjectArgs>? ShowMetadataObject;

        void Run(DebugProtocolClient debugProtocolClient, CancellationToken cancellationToken);
    }
}