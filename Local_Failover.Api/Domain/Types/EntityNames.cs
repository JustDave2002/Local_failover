namespace Domain.Types; 

public static class EntityNames
{
    public const string SalesOrder = "salesorder";
    public const string CustomerNote = "customernote";
    public const string StockMovement = "stockmovement";
    
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        [SalesOrder]    = SalesOrder,    ["salesorders"]    = SalesOrder,
        [CustomerNote]  = CustomerNote,  ["customernotes"]  = CustomerNote,
        [StockMovement] = StockMovement, ["stockmovements"] = StockMovement,
    };

    public static bool TryNormalize(string raw, out string canonical)
        => Map.TryGetValue(raw ?? string.Empty, out canonical);
}