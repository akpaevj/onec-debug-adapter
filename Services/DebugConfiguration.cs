using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Utilities;
using Newtonsoft.Json.Linq;
using Onec.DebugAdapter.DebugServer;
using Onec.DebugAdapter.Extensions;
using Onec.DebugAdapter.V8;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Onec.DebugAdapter.Services
{
    public class DebugConfiguration : IDebugConfiguration
    {
        public event EventHandler? Initialized;

        public InfoBaseItem InfoBase { get; private set; }
        public string InfoBaseName { get; private set; } = string.Empty;
        public string PlatformBin { get; private set; } = string.Empty;
        public string DebugServerHost { get; private set; } = string.Empty;
        public int DebugServerPort { get; private set; } = 1550;
        public string RootProject { get; private set; } = string.Empty;
        public string DebuggerID { get; private set; } = string.Empty;

        private readonly Dictionary<string, string> _extensions = new();
        public IReadOnlyDictionary<string, string> Extensions => _extensions;
        public DebugTargetType[] InitialTargetTypes { get; private set; } = System.Array.Empty<DebugTargetType>();

        public async Task Init(Dictionary<string, JToken> arguments)
        {
            await InitInfoBase(arguments);
            InitPlatformBin(arguments);
            InitExtension(arguments);
            InitInitialTargetTypes(arguments);

            DebugServerHost = arguments.GetValueAsString("debugServerHost");
            DebugServerPort = arguments.GetValueAsInt("debugServerPort") ?? 1550;
            RootProject = arguments.GetValueAsString("rootProject");
            DebuggerID = Guid.NewGuid().ToString();

            Initialized?.Invoke(this, new EventArgs());
        }

        private async Task InitInfoBase(Dictionary<string, JToken> arguments)
        {
            var infoBase = arguments.GetValueAsString("infoBase") ?? "";
            if (string.IsNullOrEmpty(infoBase))
                throw new System.Exception("Не задано наименование информационной базы");

            var infoBases = await InfoBasesReader.Read();
            var infobaseItem = infoBases.FirstOrDefault(c => c.Name == infoBase);
            if (infobaseItem == null)
                throw new System.Exception($"Информационная база \"{infoBase}\" не найдена в списке баз");

            InfoBase = infobaseItem;

            var connectionString = infobaseItem.Connect;
            if (string.IsNullOrEmpty(connectionString))
                throw new System.Exception($"Не удалось определить строку подключения к информационной базе");

            var reference = Regex.Match(connectionString, "(?<=Ref=\").*?(?=\")", RegexOptions.ExplicitCapture).Value;
            if (string.IsNullOrEmpty(reference))
                throw new System.Exception($"Не удалось определить имя информационной базы по строке подключения {connectionString}");

            InfoBaseName = reference;
        }

        private void InitPlatformBin(Dictionary<string, JToken> arguments)
        {
            if (arguments.GetValueAsString("request") == "launch")
            {
                var platformPath = arguments.GetValueAsString("platformPath");
                if (!Directory.Exists(platformPath))
                    throw new System.Exception("Каталог с версиями платформы 1С не найден");

                var versionFolders = Directory
                    .GetDirectories(platformPath)
                    .Select(c => new { new DirectoryInfo(c).Name, PlatformPath = c })
                    .Where(c => Regex.IsMatch(c.Name, @"^\d+\.\d+\.\d+\.\d+$"))
                    .OrderByDescending(c => c.Name)
                    .ToList();

                if (versionFolders.Count == 0)
                    throw new System.Exception("Не найдены установленные версии платформы");

                var platformVersion = arguments.GetValueAsString("platformVersion") ?? string.Empty;
                if (string.IsNullOrEmpty(platformVersion) || platformVersion.ToUpper() == "LATEST")
                    PlatformBin = Path.Combine(versionFolders.First().PlatformPath, "bin");
                else
                {
                    var versionItem = versionFolders.FirstOrDefault(c => c.Name == platformVersion);
                    if (versionItem == null)
                        throw new System.Exception($"Указанная версия платформы ({platformVersion}) не найдена");
                    else
                        PlatformBin = Path.Combine(versionItem.PlatformPath, "bin");
                }
            }
        }

        private void InitInitialTargetTypes(Dictionary<string, JToken> arguments)
        {
            var types = arguments.GetValueAsStringArray("autoAttachTypes");

            InitialTargetTypes = types switch
            {
                null => System.Array.Empty<DebugTargetType>(),
                _ => types.ToList().Select(c => Enum.Parse<DebugTargetType>(c)).ToArray()
            };
        }

        private void InitExtension(Dictionary<string, JToken> arguments)
        {
            var paths = arguments.GetValueAsStringArray("extensions");

            if (paths != null)
            {
                foreach (var path in paths)
                {
                    var xml = new XmlDocument();
                    xml.Load(Path.Join(path, "Configuration.xml"));

                    var xPath = "/*[local-name()='MetaDataObject']/*[local-name()='Configuration']/*[local-name()='Properties']/*[local-name()='Name']";
                    var extensionName = xml.SelectSingleNode(xPath)?.InnerText;

                    if (!string.IsNullOrEmpty(extensionName))
                        _extensions.Add(extensionName, path);
                }
            }
        }

        public T CreateRequest<T>() where T : RDbgBaseRequest, new()
        {
            var request = new T()
            {
                IdOfDebuggerUi = DebuggerID,
                InfoBaseAlias = InfoBaseName
            };

            return request;
        }

        public T CreateRequest<T>(Action<T> factory) where T : RDbgBaseRequest, new()
        {
            var request = CreateRequest<T>();
            factory.Invoke(request);

            return request;
        }
    }
}
