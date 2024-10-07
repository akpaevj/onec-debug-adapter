namespace Onec.DebugAdapter.Metadata
{
    public interface IMetadataProvider
    {
        Task Init(CancellationToken cancellationToken = default);

        string ModulePathByInfo(string extension, string objectId, string propertyId, CancellationToken cancellationToken = default);

        (string Extension, string ObjectId, string PropertyId) ModuleInfoByPath(string path, CancellationToken cancellationToken = default);
    }
}
