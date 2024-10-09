using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Onec.DebugAdapter.DebugServer;

namespace Onec.DebugAdapter.Services
{
    public interface IDebugTargetsManager
    {
        Task Run(DebugProtocolClient client, CancellationToken cancellationToken);
        DebugTargetId GetTargetId(int threadId);
        DebugTargetId[] GetAttachedDebugTargets();
        Task<DebugTargetId[]> GetDebugTargets();
        Task SetAutoAttachTargetTypes(List<DebugTargetType> types);
        List<DebugTargetType> GetAutoAttachTargetTypes();
        Task AttachDebugTargets(List<DebugTargetId> debugTargets);
        Task DetachDebugTargets(List<DebugTargetIdLight> debugTargets, bool sendDetachRequest);
        ThreadsResponse GetThreads(ThreadsArguments args);
        int GetThreadId(DebugTargetIdLight debugTargetId);
        bool DebugTargetAttached(DebugTargetIdLight debugTarget);
    }
}