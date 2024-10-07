using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace Onec.DebugAdapter.DebugProtocol
{
    public class DebugTargetsRequest : DebugRequestWithResponse<DebugTargetsArguments, DebugTargetsResponse>
    {
        public DebugTargetsRequest() : base("DebugTargetsRequest") { }
    }

    public class DebugTargetsArguments { };

    public class DebugTargetItem
    {
        public string Id { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Seance { get; set; } = string.Empty;
    }

    public class DebugTargetsResponse : ResponseBody
    {
        public DebugTargetItem[] Items { get; set; } = Array.Empty<DebugTargetItem>();
    }
}
