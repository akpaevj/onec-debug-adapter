using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json;

namespace Onec.DebugAdapter.DebugProtocol
{
    public class SetAutoAttachTargetTypesRequest : DebugRequest<SetAutoAttachTargetTypesArguments>
    {
        public SetAutoAttachTargetTypesRequest() : base("SetAutoAttachTargetTypesRequest") { }
    }

    public class SetAutoAttachTargetTypesArguments 
    {
        [JsonProperty("types")]
        public string[] Types { get; set; } = Array.Empty<string>();
    };
}
