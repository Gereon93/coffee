namespace CoffeeWeb.Services
{
    public class NivonaStatisticsService
    {

        private readonly Client _client;
        public NivonaStatisticsService()
        {
            _client = new Client("http://192.168.2.143:8889/", new HttpClient());
        }

        public async Task<ICollection<NivonaStatisticsModel>> GetStatisticsAsync()
        {
            return await _client.GetStatistics2Async();
        }
        public async Task AddCoffeePackageAsync(NivonaStatisticsModel newStatistics)
        {
            await _client.AddStatisticsAsync(newStatistics);
        }

    }
}
