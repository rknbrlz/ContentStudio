namespace Hgerman.ContentStudio.Shared.Options;

public class StorageOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "content-studio";
    public string LocalRootPath { get; set; } = "storage";
    public string PublicBaseUrl { get; set; } = string.Empty;
}
