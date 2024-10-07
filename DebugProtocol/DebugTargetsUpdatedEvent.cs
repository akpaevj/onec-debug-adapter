using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace Onec.DebugAdapter.DebugProtocol
{
    class DebugTargetsUpdatedEvent : DebugEvent
    {
        public DebugTargetsUpdatedEvent() : base("DebugTargetsUpdated") { }
    }
}
