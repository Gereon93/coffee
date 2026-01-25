using CoffeeApi.Models;
using CoffeeApi.Repositories;

namespace CoffeeTest.Fakes
{
    public class FakeCoffeePackagesRepository : ICoffeePackagesRepository
    {
        public List<CoffeePackages> Items { get; } = new();

        public Task<List<CoffeePackages>> GetAllAsync()
        {
            return Task.FromResult(Items.ToList());
        }

        public Task InsertAsync(CoffeePackages packages)
        {
            Items.Add(packages);
            return Task.CompletedTask;
        }
    }
}
