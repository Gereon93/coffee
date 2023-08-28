using MongoDB.Bson;

namespace CoffeeApi.Models
{
    public class NivonaStatisticsModel
    {
        public ObjectId Id { get; set; }
        public DateTime Timestamp { get; set; }
        public int EspressoCount { get; set; }
        public int CoffeeCount { get; set; }
        public int LungoCount { get; set; }
        public int CappuccinoCount { get; set; }
        public int LatteMacchiatoCount  { get; set; }
        public int CoffeeAmericanoCount { get; set; }
        public int MilkCount { get; set; }
        public int HotWaterCount { get; set; }
        public int MyCoffeeCount { get; set; }

    }
}
