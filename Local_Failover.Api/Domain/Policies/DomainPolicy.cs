using Domain.Types;

namespace Domain.Policies;

public interface IDomainPolicy
{
    bool CanWrite(AppRole role, FenceMode fence, string entity);
}

public sealed class DomainPolicy : IDomainPolicy
{
    public bool CanWrite(AppRole role, FenceMode fence, string entity)
    {
        
        // Backoffice: Cloud RW, Local RO
        if (entity == EntityNames.SalesOrder || entity == EntityNames.CustomerNote)
            return role == AppRole.Cloud;

        // Floor-ops
        if (entity == EntityNames.StockMovement)
        {
            if (role == AppRole.Cloud)  return fence != FenceMode.Fenced; // Cloud RO bij fence
            if (role == AppRole.Local)  return fence == FenceMode.Fenced; // Local RW bij fence, anders via command later
        }
        return false;
    }
}
