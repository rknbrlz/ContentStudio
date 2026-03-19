namespace Hgerman.ContentStudio.Application.Services;

public static class JobNumberGenerator
{
    public static string Create() => $"CS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..20];
}
