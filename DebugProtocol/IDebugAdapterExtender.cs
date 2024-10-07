using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;

namespace Onec.DebugAdapter.DebugProtocol
{
    public interface IDebugAdapterExtender
    {
        void Init(DebugProtocolClient debugProtocolClient, CancellationToken cancellationToken);
    }
}