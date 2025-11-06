namespace Tracker.Models;

public class NewTrackingRecord
{
    public int OrderId { get; set; }
    public string Via { get; set; } = string.Empty;
    public string Track { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
}

