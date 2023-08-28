namespace CoffeeWeb.Services
{
    public class CoffeeService
    {
        private readonly Client _client;
        public CoffeeService() {
            _client = new Client("http://192.168.2.143:8889/", new HttpClient());
        }


    }
}
