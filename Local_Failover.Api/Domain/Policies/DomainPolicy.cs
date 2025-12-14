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
        
        // Backoffice: Cloud RW, Local fenced
        if (entity == EntityNames.SalesOrder || entity == EntityNames.CustomerNote)
        {
            if (role == AppRole.Cloud)  return true; 
            if (role == AppRole.Local)  return fence != FenceMode.Fenced; 
        }

        // Floor-ops: Local RW, cloud fenced
        if (entity == EntityNames.StockMovement)
        {
            if (role == AppRole.Cloud)  return fence != FenceMode.Fenced; 
            if (role == AppRole.Local)  return true; 
        }
        return false;
    }
}
 