using Newtonsoft.Json.Linq;
using Onec.DebugAdapter.DebugServer;
using Onec.DebugAdapter.V8;

namespace Onec.DebugAdapter.Services
{
    public interface IDebugConfiguration
    {
        Task Initialization { get; }

		InfoBaseItem InfoBase { get; }
        bool IsFileInfoBase { get; }
        string InfoBaseName { get; }
        string PlatformBin { get; }
        string DebuggerID { get; }
        string DebugServerHost { get; }
        int DebugServerPort { get; }
        string RootProject { get; }
        IReadOnlyDictionary<string, string> Extensions { get; }
        DebugTargetType[] InitialTargetTypes { get; }

        // Отладочный порт файловой информационной базы выбирается на лету, поэтому требуется инжект в конфигурацию отладки
        void SetDebugServerPort(int port);

        T CreateRequest<T>() where T : RDbgBaseRequest, new();
        T CreateRequest<T>(Action<T> factory) where T : RDbgBaseRequest, new();
        Task Init(Dictionary<string, JToken> arguments);
    }
}