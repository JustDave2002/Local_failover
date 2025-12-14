namespace Domain.Entities;

public class SalesOrder {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Customer { get; set; } = "";
    public decimal Total { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    // public byte[] RowVersion { get; set; } = default!;
}

public class CustomerNote {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Customer { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    // public byte[] RowVersion { get; set; } = default!;
}

public class StockMovement {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Product { get; set; } = "";
    public decimal Qty { get; set; }
    public string Location { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    // public byte[] RowVersion { get; set; } = default!;
}

public class Outbox {
    public Guid Id { get; set; } = Guid.NewGuid(); // OpId
    public string Direction { get; set; } = "";    // "toCloud" | "toLocal"
    public string Entity { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; set; }
}

public class OpApplied {
    public Guid OpId { get; set; }     // primary key
    public DateTime AppliedAtUtc { get; set; } = DateTime.UtcNow;
}

public class Lease {
    public string TenantId { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime LastBeatAtUtc { get; set; }
}
