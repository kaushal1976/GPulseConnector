namespace GPulseConnector.Services.Sync
{
    public interface ISyncEntity
    {
        long Id { get; set; }
        string ComputeHash();
    }
}