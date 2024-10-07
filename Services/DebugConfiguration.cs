using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Utilities;
using Newtonsoft.Json.Linq;
using Onec.DebugAdapter.DebugServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Onec.DebugAdapter.Services
{
    public class DebugConfiguration : IDebugConfiguration
    {
        public event EventHandler? Initialized;

        public string DebugServerHost { get; private set; } = string.Empty;
        public int DebugServerPort { get; private set; } = 1550;
        public string Infobase { get; private set; } = string.Empty;
        public string RootProject { get; private set; } = string.Empty;
        public string DebuggerID { get; private set; } = string.Empty;

        private readonly Dictionary<string, string> _extensions = new();
        public IReadOnlyDictionary<string, string> Extensions => _extensions;

        public void Init(Dictionary<string, JToken> arguments)
        {
            DebugServerHost = arguments.GetValueAsString("debugServerHost");
            DebugServerPort = arguments.GetValueAsInt("debugServerPort") ?? 1550;
            Infobase = arguments.GetValueAsString("infoBase");
            RootProject = arguments.GetValueAsString("rootProject");
            DebuggerID = Guid.NewGuid().ToString();

            if (arguments.ContainsKey("extensions"))
            {
                var extensionsValue = arguments["extensions"];

                if (extensionsValue is JArray extensionsArray)
                {
                    foreach (var pathToken in extensionsArray)
                    {
                        if (pathToken.Type == JTokenType.String)
                        {
                            var path = pathToken.ToString();

                            var xml = new XmlDocument();
                            xml.Load(Path.Join(path, "Configuration.xml"));

                            var xPath = "/*[local-name()='MetaDataObject']/*[local-name()='Configuration']/*[local-name()='Properties']/*[local-name()='Name']";
                            var extensionName = xml.SelectSingleNode(xPath)?.InnerText;

                            if (!string.IsNullOrEmpty(extensionName))
                                _extensions.Add(extensionName, path);
                        }
                    }
                }
            }

            Initialized?.Invoke(this, new EventArgs());
        }

        public T CreateRequest<T>() where T : RDbgBaseRequest, new()
        {
            var request = new T()
            {
                IdOfDebuggerUi = DebuggerID,
                InfoBaseAlias = Infobase
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
