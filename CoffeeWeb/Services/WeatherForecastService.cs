namespace CoffeeWeb.Data
{
    public class WeatherForecastService
    {

        public async Task<List<WeatherForecast>> GetForecastAsync(DateOnly startDate)
        {
            var client = new Client("http://192.168.2.143:8889/", new HttpClient());
            var data = await client.GetWeatherForecastAsync();
            return data.ToList();
        }
    }
}