
public class CreateSalesOrderRequest
{
    public Guid? Id { get; set; }
    public string Customer { get; set; } = "";
    public decimal Total { get; set; }
    public DateTime? CreatedAtUtc { get; set; }
}

public record CreateSalesOrderResponse(Guid Id, string Customer, decimal Total, DateTime CreatedAtUtc);

public class PostStockMovementRequest
{
    public Guid? Id { get; set; }
    public string Product { get; set; } = "";
    public int Qty { get; set; }
    public string Location { get; set; } = "";
    public DateTime? CreatedAtUtc { get; set; }  // wordt alleen gebruikt bij resync
}
public record PostStockMovementResponse(Guid Id, string Product, decimal Qty, string Location, DateTime CreatedAtUtc);