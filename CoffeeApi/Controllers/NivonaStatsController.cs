using CoffeeApi.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json;
using OpenAI;
using OpenAI.Chat;
using Tesseract;


namespace CoffeeApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NivonaStatsController : ControllerBase
    {
        private readonly IMongoCollection<NivonaStatisticsModel> _nivonaStatsCollection;
        private readonly ChatClient _client;
        public NivonaStatsController(IMongoClient mongoClient)
        {
            var database = mongoClient.GetDatabase("nivona");
            _nivonaStatsCollection = database.GetCollection<NivonaStatisticsModel>("NivonaStatistics");
            var key = Environment.GetEnvironmentVariable("OPEN_AI_KEY") ?? throw new InvalidOperationException("No OpenAI Key");
            OpenAIClient client = new(key);
            _client = client.GetChatClient("gpt-4o");
        }



        [HttpPost("AddStatistics")]
        public async Task<IActionResult> PostNewStatistics(NivonaStatisticsModel stats)
        {
            if (ModelState.IsValid)
            {
                await CheckNullFieldsAndUseLastStats(stats);
                await _nivonaStatsCollection.InsertOneAsync(stats);

                return CreatedAtAction(nameof(GetStatisticsById), new { id = stats.Id }, stats);
            }
            else
            {
                return BadRequest(ModelState);
            }
        }

        private async Task CheckNullFieldsAndUseLastStats(NivonaStatisticsModel stats)
        {
            var newestStatsInDb = await GetNewestStats();
            if (newestStatsInDb == null)
            {
                return;
            }
            if (stats.CappuccinoCount == 0)
            {
                stats.CappuccinoCount = newestStatsInDb.CappuccinoCount;
            }
            if (stats.CoffeeAmericanoCount == 0)
            {
                stats.CoffeeAmericanoCount = newestStatsInDb.CoffeeAmericanoCount;
            }
            if (stats.CoffeeCount == 0)
            {
                stats.CoffeeCount = newestStatsInDb.CoffeeCount;
            }
            if (stats.EspressoCount == 0)
            {
                stats.EspressoCount = newestStatsInDb.EspressoCount;
            }
            if (stats.HotWaterCount == 0)
            {
                stats.HotWaterCount = newestStatsInDb.HotWaterCount;
            }
            if (stats.LatteMacchiatoCount == 0)
            {
                stats.LatteMacchiatoCount = newestStatsInDb.LatteMacchiatoCount;
            }
            if (stats.LungoCount == 0)
            {
                stats.LungoCount = newestStatsInDb.LungoCount;
            }
            if (stats.MilkCount == 0)
            {
                stats.MilkCount = newestStatsInDb.MilkCount;
            }
            if (stats.MyCoffeeCount == 0)
            {
                stats.MyCoffeeCount = newestStatsInDb.MyCoffeeCount;
            }

        }

        [HttpGet("GetStatisticsById/{id}")]
        public ActionResult<NivonaStatisticsModel> GetStatisticsById(string id)
        {
            var filter = Builders<NivonaStatisticsModel>.Filter.Eq("_id", id);
            var foundStats = _nivonaStatsCollection.Find(filter).SingleOrDefault();

            if (foundStats == null)
            {
                return NotFound();
            }

            return Ok(foundStats);
        }

        [HttpGet("GetLastStatistics")]
        public async Task<ActionResult<NivonaStatisticsModel>> GetLastStatisticsAsync()
        {
            NivonaStatisticsModel? foundStats = await GetNewestStats();
            if (foundStats == null)
            {
                return NotFound();
            }

            return Ok(foundStats);
        }

        private async Task<NivonaStatisticsModel?> GetNewestStats()
        {
            var historicalData = await _nivonaStatsCollection.Find(Builders<NivonaStatisticsModel>.Filter.Empty).ToListAsync();
            var orderdHistoricalData = historicalData.OrderBy(x => x.Timestamp).ToList();
            var foundStats = orderdHistoricalData.LastOrDefault();
            return foundStats;
        }

        [HttpGet("GetStatistics")]
        public ActionResult<IEnumerable<NivonaStatisticsModel>> GetStatistics()
        {
            var allStats = _nivonaStatsCollection.Find(Builders<NivonaStatisticsModel>.Filter.Empty).ToList();
            return Ok(allStats);
        }

        [HttpGet("ForecastCoffeeStatistics")]
        public async Task<ActionResult<PredictedStats>> ForecastCoffeeStatisticsAsync(DateTime futureDate)
        {
            var historicalData = await _nivonaStatsCollection.Find(Builders<NivonaStatisticsModel>.Filter.Empty).ToListAsync();
            var orderedHistoricalData = historicalData.OrderBy(x => x.Timestamp).ToList();

            var jsonData = JsonConvert.SerializeObject(orderedHistoricalData);
            var prompt = $"Given the following coffee machine statistics data, predict the coffee statistics for the future date {futureDate.ToString("yyyy-MM-dd")} in the format provided:\n\n" +
                $"Historical Data:\n{jsonData}\n\n" +
                $"Predict the coffee statistics for the date {futureDate.ToString("yyyy-MM-dd")} and provide the response in the following format:\n" +
                "{ \"Timestamp\": \"futureDate\", \"EspressoCount\": 0, \"CoffeeCount\": 0, \"LungoCount\": 0, \"CappuccinoCount\": 0, \"LatteMacchiatoCount\": 0, \"CoffeeAmericanoCount\": 0, \"MilkCount\": 0, \"HotWaterCount\": 0, \"MyCoffeeCount\": 0 }";

            ChatCompletion completion = await _client.CompleteChatAsync(prompt);

            // Assuming the response is a string

            var predicted = new PredictedStats
            {
                Text = completion.Content[0].Text,
                Timestamp = DateTime.Now
            };
            // If needed, you can deserialize it or return directly
            return Ok(predicted);
        }



        [HttpPost("UploadImage")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {


            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded");
            }
            string ocrText = string.Empty;
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                byte[] imageBytes = memoryStream.ToArray();
                ocrText = ExtractTextFromImage(imageBytes);


            }
            ChatMessage text = $"Can you parse for me this data from tesseract {ocrText} to  the corresponde to the json here please only the technicall without comments so i can parse it with dotnet serializer(from the swaggerfile):" + "NivonaStatisticsModel \"{ \\\"Timestamp\\\": \\\"date\\\", \\\"EspressoCount\\\": 0, \\\"CoffeeCount\\\": 0, \\\"LungoCount\\\": 0, \\\"CappuccinoCount\\\": 0, \\\"LatteMacchiatoCount\\\": 0, \\\"CoffeeAmericanoCount\\\": 0, \\\"MilkCount\\\": 0, \\\"HotWaterCount\\\": 0, \\\"MyCoffeeCount\\\": 0 }\"";

            ChatCompletion completion = await _client.CompleteChatAsync(text);

            // Get image description and extract relevant statistics
            var description = completion.Content[0].ToString();

            if (description != null)
            {
                var statistics = ExtractStatisticsFromDescription(description);
                await CheckNullFieldsAndUseLastStats(statistics);
                await _nivonaStatsCollection.InsertOneAsync(statistics);
            }
            else
            {
                Console.WriteLine("No description could be generated.");
            }

            return Ok();
        }
        private static NivonaStatisticsModel ExtractStatisticsFromDescription(string description)
        {
            // Split the description by comma and iterate over each part
            var parts = description.Split("```json");
            var jsonString = parts[1].Split("```")[0];
            jsonString = jsonString.Replace("\n", string.Empty)
                          .Replace("\r", string.Empty)
                          .Replace("\t", string.Empty)
                          .Trim();
            var model = JsonConvert.DeserializeObject<NivonaStatisticsModel>(jsonString) ?? throw new InvalidOperationException("Could not parse the json string");
            model.Timestamp = DateTime.UtcNow;
            return model;
        }


        private static string ExtractTextFromImage(byte[] imageBinary)
        {
            // Tesseract OCR verwenden, um Text aus dem Bild zu extrahieren
            try
            {
                using var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);
                using var img = Pix.LoadFromMemory(imageBinary);
                using var page = engine.Process(img);
                return page.GetText();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during OCR processing: " + ex.Message);
                return string.Empty;
            }
        }
    }

}