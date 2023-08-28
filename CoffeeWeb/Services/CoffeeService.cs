namespace CoffeeWeb.Services
{
    public class CoffeeService
    {
        private readonly Client _client;
        public CoffeeService() {
            _client = new Client("http://192.168.2.143:8889/", new HttpClient());
        }
        public async Task AddCoffeePackageAsync(CoffeePackages newPackage)
        {
            await _client.NewPackagesAsync(newPackage);
        }
        public async Task<List<CoffeePackages>> GetStatisticsAsync()
        {
            var stats = await _client.GetStatisticsAsync();
            return stats.ToList();
        }
    }
}
