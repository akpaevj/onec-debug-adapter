using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Onec.DebugAdapter.Services;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks.Dataflow;
using System.Linq.Expressions;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Onec.DebugAdapter.V8
{
    public class MetadataProvider : IMetadataProvider
    {
        private readonly IDebugConfiguration _configuration;
        private readonly ConcurrentDictionary<string, (string Extension, string ObjectId, string PropertyId)> _modulesInfoByPath = new();
        private readonly ConcurrentDictionary<(string Extension, string ObjectId, string PropertyId), string> _pathsByModuleInfo = new();

        public MetadataProvider(IDebugConfiguration debugConfiguration)
        {
            _configuration = debugConfiguration;
        }

        public async Task Init(CancellationToken cancellationToken)
            => await FillMetadataCache(cancellationToken);

        public string ModulePathByInfo(string extension, string objectId, string propertyId, CancellationToken cancellationToken = default)
            => _pathsByModuleInfo[(extension, objectId, propertyId)];

        public (string Extension, string ObjectId, string PropertyId) ModuleInfoByPath(string path, CancellationToken cancellationToken = default)
            => _modulesInfoByPath[path];

        private static string GetPropertyId(string mdType, string moduleName)
        {
            return mdType switch
            {
                "CommonModules" or "WebServices" or "HTTPServices" => "d5963243-262e-4398-b4d7-fb16d06484f6",
                _ => moduleName switch
                {
                    "Module" => "32e087ab-1491-49b6-aba7-43571b41ac2b",
                    "CommandModule" => "078a6af8-d22c-4248-9c33-7e90075a3d2c",
                    "ObjectModule" => "a637f77f-3840-441d-a1c3-699c8c5cb7e0",
                    "ManagerModule" => "d1b64a2c-8078-4982-8190-8f81aefda192",
                    "RecordSetModule" => "9f36fd70-4bf4-47f6-b235-935f73aab43f",
                    "ValueManagerModule" => "3e58c91f-9aaa-4f42-8999-4baf33907b75",
                    "ManagedApplicationModule" => "d22e852a-cf8a-4f77-8ccb-3548e7792bea",
                    "SessionModule" => "9b7bbbae-9771-46f2-9e4d-2489e0ffc702",
                    "ExternalConnectionModule" => "a4a9c1e2-1e54-4c7f-af06-4ca341198fac",
                    "OrdinaryApplicationModule" => "a78d9ce3-4e0c-48d5-9863-ae7342eedf94",
                    _ => throw new NotImplementedException($"{mdType}\\{moduleName} is unknown module type")
                }
            };
        }

        private static string GetObjectId(string path)
        {
            var xml = new XmlDocument();
            xml.Load(path);

            var xPath = "/*[local-name()='MetaDataObject']";
            var typedNode = xml.SelectSingleNode(xPath)?.FirstChild;

            return typedNode!.Attributes!.GetNamedItem("uuid")!.Value!;
        }

        private async Task FillMetadataCache(CancellationToken cancellationToken)
        {
            var blockOptions = new ExecutionDataflowBlockOptions()
            {
                CancellationToken = cancellationToken,
                EnsureOrdered = false,
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                BoundedCapacity = DataflowBlockOptions.Unbounded
            };

            var mdReaderBlock = new ActionBlock<(string Extension, string Path)>(args =>
            {
                var mdName = Path.GetFileNameWithoutExtension(args.Path);
                var mdPath = Path.Combine(Path.GetDirectoryName(args.Path)!, mdName);
                var mdType = Directory.GetParent(mdPath)!.Name;

                var mdXml = new XmlDocument();
                mdXml.Load(args.Path);

                var typedNode = mdXml.SelectSingleNode("/*[local-name()='MetaDataObject']")!.FirstChild!;
                var objectId = typedNode.Attributes!.GetNamedItem("uuid")!.Value!;

                var extPath = Path.Combine(mdPath, "Ext");
                if (Directory.Exists(extPath))
                    foreach (var moduleFile in Directory.EnumerateFiles(extPath, "*.bsl", SearchOption.AllDirectories))
                    {
                        var propertyId = GetPropertyId(mdType, Path.GetFileNameWithoutExtension(moduleFile));
                        CacheModule(moduleFile, args.Extension, objectId, propertyId);
                    }

                var formsPath = Path.Combine(mdPath, "Forms");
                if (Directory.Exists(formsPath))
                    foreach (var formXmlFile in Directory.EnumerateFiles(formsPath, "*.xml"))
                    {
                        var formPath = Path.Combine(formsPath, Path.GetFileNameWithoutExtension(formXmlFile));
                        if (Directory.Exists(formPath))
                        {
                            var formModuleFile = Directory.EnumerateFiles(formPath, "*.bsl", SearchOption.AllDirectories).FirstOrDefault();
                            if (formModuleFile != null)
                            {
                                var propertyId = GetPropertyId(mdType, Path.GetFileNameWithoutExtension(formModuleFile));
                                CacheModule(formModuleFile, args.Extension, GetObjectId(formXmlFile), propertyId);
                            }
                        }
                    }

                var commandsPath = Path.Combine(mdPath, "Commands");
                if (Directory.Exists(commandsPath))
                {
                    var commandNodes = typedNode.SelectNodes("./*[local-name()='ChildObjects']/*[local-name()='Command']")!;
                    foreach (XmlNode commandNode in commandNodes)
                    {
                        var commandObjectId = commandNode.Attributes!.GetNamedItem("uuid")!.Value!;
                        var commandName = commandNode.SelectSingleNode("./*[local-name()='Properties']/*[local-name()='Name']")!.InnerText;
                        var commandPath = Path.Combine(commandsPath, commandName);
                        if (Directory.Exists(commandPath))
                        {
                            var commandModuleFile = Directory.EnumerateFiles(commandPath, "*.bsl", SearchOption.AllDirectories).FirstOrDefault();
                            if (commandModuleFile != null)
                                // Захардкоженный идентификатор типа модуля формы
                                CacheModule(commandModuleFile, args.Extension, commandObjectId, GetPropertyId("", Path.GetFileNameWithoutExtension(commandModuleFile)));
                        }
                    }
                }
            }, blockOptions);

            var rootReaderBlock = new ActionBlock<(string Extension, string Path)>(async args =>
            {
                var mdXml = new XmlDocument();
                mdXml.Load(Path.Combine(args.Path, "Configuration.xml"));

                var typedNode = mdXml.SelectSingleNode("/*[local-name()='MetaDataObject']")!.FirstChild!;
                var objectId = typedNode.Attributes!.GetNamedItem("uuid")!.Value!;

                var extPath = Path.Combine(args.Path, "Ext");
                if (Directory.Exists(extPath))
                    foreach (var moduleFile in Directory.EnumerateFiles(extPath, "*.bsl"))
                    {
                        var propertyId = GetPropertyId("", Path.GetFileNameWithoutExtension(moduleFile));
                        CacheModule(moduleFile, args.Extension, objectId, propertyId);
                    }

                var rootMdfolders = Directory.GetDirectories(args.Path);

                foreach (var rootMdFolder in rootMdfolders)
                {
                    if (new DirectoryInfo(rootMdFolder).Name == "Ext")
                        continue;

                    var xmlFiles = Directory.GetFiles(rootMdFolder, "*.xml");

                    foreach (var xmlFile in xmlFiles)
                        await mdReaderBlock.SendAsync((args.Extension, xmlFile), cancellationToken).ConfigureAwait(false);
                }
            }, blockOptions);

            _ = rootReaderBlock.Completion.ContinueWith(delegate { mdReaderBlock.Complete(); }, cancellationToken).ConfigureAwait(false);

            await rootReaderBlock.SendAsync((string.Empty, _configuration.RootProject));
            foreach(var kv in _configuration.Extensions)
                await rootReaderBlock.SendAsync((kv.Key, kv.Value));

            rootReaderBlock.Complete();

            await mdReaderBlock.Completion;
        }

        private void CacheModule(string path, string extension, string objectId, string propertyId)
        {
            _modulesInfoByPath.TryAdd(path, (extension, objectId, propertyId));
            _pathsByModuleInfo.TryAdd((extension, objectId, propertyId), path);
        }
    }
}
