using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace Onec.DebugAdapter.DebugProtocol
{
    class RaiseSetAutoAttachTargetTypesEvent : DebugEvent
    {
        public RaiseSetAutoAttachTargetTypesEvent() : base("RaiseSetAutoAttachTargetTypes") { }
    }
}
