using Domain.Types;
using Ports;

namespace Infrastructure.Fence;

public sealed class AppRoleProvider : IAppRoleProvider
{
    public AppRole Role { get; }
    public AppRoleProvider(AppRole role) => Role = role;
}
