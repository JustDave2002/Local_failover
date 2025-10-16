using Domain.Types;

namespace Ports;

public interface IAppRoleProvider
{
    AppRole Role { get; }
}
