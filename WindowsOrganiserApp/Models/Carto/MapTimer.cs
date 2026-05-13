namespace WindowsOrganiserApp.Models.Carto;

public sealed class MapTimer
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Label { get; set; } = string.Empty;
    public double MapX { get; set; }
    public double MapY { get; set; }
    public int DurationSeconds { get; set; } = 900;
    public DateTime? StartedAt { get; set; }
    public bool IsRunning { get; set; }
    public int? PausedRemainingSeconds { get; set; }

    public TimeSpan Elapsed => IsRunning && StartedAt.HasValue
        ? DateTime.Now - StartedAt.Value
        : TimeSpan.Zero;

    public TimeSpan Remaining
    {
        get
        {
            if (!IsRunning)
                return TimeSpan.FromSeconds(PausedRemainingSeconds ?? DurationSeconds);
            if (!StartedAt.HasValue)
                return TimeSpan.FromSeconds(DurationSeconds);
            var r = TimeSpan.FromSeconds(DurationSeconds) - Elapsed;
            return r < TimeSpan.Zero ? TimeSpan.Zero : r;
        }
    }

    public bool IsExpired =>
        (IsRunning && StartedAt.HasValue && Remaining <= TimeSpan.Zero) ||
        (!IsRunning && StartedAt.HasValue && (PausedRemainingSeconds == null || PausedRemainingSeconds <= 0));

    public bool IsPaused => !IsRunning && PausedRemainingSeconds.HasValue && PausedRemainingSeconds > 0;
}
