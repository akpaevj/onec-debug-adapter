using Newtonsoft.Json.Linq;
using Onec.DebugAdapter.DebugServer;

namespace Onec.DebugAdapter.Services
{
    public interface IDebugConfiguration
    {
        event EventHandler? Initialized;

        string DebuggerID { get; }
        string DebugServerHost { get; }
        int DebugServerPort { get; }
        string Infobase { get; }
        string RootProject { get; }
        IReadOnlyDictionary<string, string> Extensions { get; }

        T CreateRequest<T>() where T : RDbgBaseRequest, new();
        T CreateRequest<T>(Action<T> factory) where T : RDbgBaseRequest, new();
        void Init(Dictionary<string, JToken> arguments);
    }
}