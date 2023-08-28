namespace CoffeeWeb.Services
{
    public class NivonaStatisticsService
    {

        private readonly Client _client;
        public NivonaStatisticsService()
        {
            _client = new Client("http://192.168.2.143:8889/", new HttpClient());
        }

        public async Task<List<NivonaStatisticsModel>> GetStatisticsAsync()
        {
            var nivonaStats =  await _client.GetStatistics2Async();
            return nivonaStats.ToList();
        }


    }
}
