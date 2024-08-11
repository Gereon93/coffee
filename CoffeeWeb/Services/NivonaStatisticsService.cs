namespace CoffeeWeb.Services
{
    public class NivonaStatisticsService
    {
        private readonly Client _client;

        public NivonaStatisticsService()
        {
#if DEBUG
            _client = new Client("http://localhost:5174/", new HttpClient());
#else
                _client = new Client("http://192.168.2.143:8889/", new HttpClient());
#endif
        }

        public async Task<List<NivonaStatisticsModel>> GetStatisticsAsync()
        {
            var nivonaStats = await _client.GetStatistics2Async();
            return nivonaStats.ToList();
        }
        public async Task<string> GetForecastAsync(DateTime date)
        {
            var forefast = await _client.ForecastCoffeeStatisticsAsync(date);
            return forefast;
        }
    }
}
