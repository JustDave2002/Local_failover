
public record CreateSalesOrderRequest(string Customer, decimal Total);
public record CreateSalesOrderResponse(Guid Id, string Customer, decimal Total, DateTime CreatedAtUtc);

public record PostStockMovementRequest(string Product, decimal Qty, string Location);
public record PostStockMovementResponse(Guid Id, string Product, decimal Qty, string Location, DateTime CreatedAtUtc);