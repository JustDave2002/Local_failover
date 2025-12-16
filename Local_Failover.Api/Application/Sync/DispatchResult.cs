namespace Application.Sync;

public sealed record DispatchResult(
    bool Ok,
    int Status,
    string? Message = null,
    object? Body = null
);
