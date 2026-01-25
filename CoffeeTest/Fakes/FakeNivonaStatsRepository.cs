using CoffeeApi.Models;
using CoffeeApi.Repositories;
using MongoDB.Bson;

namespace CoffeeTest.Fakes
{
    public class FakeNivonaStatsRepository : INivonaStatsRepository
    {
        public List<NivonaStatisticsModel> Items { get; } = new();

        public Task<List<NivonaStatisticsModel>> GetAllAsync()
        {
            return Task.FromResult(Items.ToList());
        }

        public Task<NivonaStatisticsModel?> GetByIdAsync(string id)
        {
            var found = Items.FirstOrDefault(item => item.Id.ToString() == id);
            return Task.FromResult<NivonaStatisticsModel?>(found);
        }

        public Task<NivonaStatisticsModel?> GetNewestAsync()
        {
            var found = Items.OrderBy(item => item.Timestamp).LastOrDefault();
            return Task.FromResult<NivonaStatisticsModel?>(found);
        }

        public Task InsertAsync(NivonaStatisticsModel stats)
        {
            if (stats.Id == ObjectId.Empty)
            {
                stats.Id = ObjectId.GenerateNewId();
            }

            Items.Add(stats);
            return Task.CompletedTask;
        }
    }
}
