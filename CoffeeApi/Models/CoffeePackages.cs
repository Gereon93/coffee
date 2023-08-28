using MongoDB.Bson;

namespace CoffeeApi.Models
{
    public class CoffeePackages
    {
        public ObjectId Id { get; set; }
        public string CoffeeName { set; get; }
        public DateTime DateTimeStamp { set; get; }
        public int Count { set; get; }
    }
}
