using SuperStock.Domain.Entities;

namespace SuperStock.Domain.Interfaces
{
    public interface IJWTService
    {
        string GenerateToken(Usuario usuario);
    }
}
