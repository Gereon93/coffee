namespace CoffeeWeb.Data
{
    public class WeatherForecastService
    {

        public async Task<List<WeatherForecast>> GetForecastAsync()
        {
            var client = new Client("http://192.168.2.143:8889/", new HttpClient());
            var data = await client.GetRandomForecastAsync();
            return data.ToList();
        }
    }
}