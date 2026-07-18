using EcoMyceliumTracker.Models;
namespace EcoMyceliumTracker.Repositories;

public interface IMyceliumRepository
{
    Task<IEnumerable<MyceliumNetwork>> GetAllAsync();
    Task<Guid> CreateAsync(MyceliumNetwork network);
}