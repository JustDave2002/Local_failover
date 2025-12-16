namespace Infrastructure.Data;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = "T1";
    public string Direction { get; set; } = "toCloud";
    public string Entity { get; set; } = default!;
    public string Action { get; set; } = default!;
    public string PayloadJson { get; set; } = default!;
    public DateTime CreatedUtc { get; set; }
    public DateTime? SentUtc { get; set; }
    public DateTime? AckedUtc { get; set; }
}
