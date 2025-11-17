namespace Infrastructure.Data;

public class AppliedEvent
{
    public Guid Id { get; set; }           // EventId
    public DateTime SeenAtUtc { get; set; }
}