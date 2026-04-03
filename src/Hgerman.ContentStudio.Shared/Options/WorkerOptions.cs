namespace Hgerman.ContentStudio.Shared.Options;

public sealed class WorkerOptions
{
    public int IdleDelaySec { get; set; } = 10;
    public int BusyDelaySec { get; set; } = 2;
    public int ErrorDelaySec { get; set; } = 15;
    public int LockMinutes { get; set; } = 10;
    public int HeartbeatSec { get; set; } = 15;
    public int RecoveryLookbackMinutes { get; set; } = 30;
}