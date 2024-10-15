using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;

namespace Onec.DebugAdapter.V8
{
    public interface IMetadataProvider
    {
        Task Init(DebugProtocolClient client, CancellationToken cancellationToken = default);

        string ModulePathByInfo(string extension, string objectId, string propertyId, CancellationToken cancellationToken = default);

        (string Extension, string ObjectId, string PropertyId) ModuleInfoByPath(string path, CancellationToken cancellationToken = default);
    }
}
