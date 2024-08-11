using CoffeeApi.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using OpenAI;
using OpenAI.Chat;


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
                await _nivonaStatsCollection.InsertOneAsync(stats);

                return CreatedAtAction(nameof(GetStatisticsById), new { id = stats.Id }, stats);
            }
            else
            {
                return BadRequest(ModelState);
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

        [HttpGet("GetStatistics")]
        public ActionResult<IEnumerable<NivonaStatisticsModel>> GetStatistics()
        {
            var allStats = _nivonaStatsCollection.Find(Builders<NivonaStatisticsModel>.Filter.Empty).ToList();
            return Ok(allStats);
        }

        [HttpGet("ForecastCoffeeStatistics")]
        public async Task<string> ForecastCoffeeStatisticsAsync(DateTime futureDate)
        {
            var historicalData = await _nivonaStatsCollection.Find(Builders<NivonaStatisticsModel>.Filter.Empty).ToListAsync();
            var orderdHistoricalData = historicalData.OrderBy(x => x.Timestamp).ToList();

            var jsonData = Newtonsoft.Json.JsonConvert.SerializeObject(orderdHistoricalData);
            var prompt = $"Given the following coffee machine statistics data, predict the coffee statistics for the future date {futureDate.ToString("yyyy-MM-dd")} in the format provided:\n\n" +
                $"Historical Data:\n{jsonData}\n\n" +
                $"Predict the coffee statistics for the date {futureDate.ToString("yyyy-MM-dd")} and provide the response in the following format:\n" +
                "{ \"Timestamp\": \"futureDate\", \"EspressoCount\": 0, \"CoffeeCount\": 0, \"LungoCount\": 0, \"CappuccinoCount\": 0, \"LatteMacchiatoCount\": 0, \"CoffeeAmericanoCount\": 0, \"MilkCount\": 0, \"HotWaterCount\": 0, \"MyCoffeeCount\": 0 }";



            ChatCompletion completion = await _client.CompleteChatAsync(prompt);

            return completion.Content.First().ToString();
        }

        [HttpPost("UploadImage")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded");
            }

            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                byte[] imageBytes = memoryStream.ToArray();

            }

            return Ok();
        }
        /*
                public string ExtractTextFromImage(string imagePath)
                {
                    var formRecognizerClient = new FormRecognizerClient(new Uri("<YOUR_FORM_RECOGNIZER_ENDPOINT>"), new AzureKeyCredential("<YOUR_FORM_RECOGNIZER_API_KEY>"));

                    using (var stream = new FileStream(imagePath, FileMode.Open))
                    {
                        var recognizeOptions = new RecognizeContentOptions() { ContentType = "image/jpeg" };
                        var recognizeResult = await formRecognizerClient.StartRecognizeContentAsync(stream, recognizeOptions).WaitForCompletionAsync();
                        string extractedText = "";

                        foreach (var page in recognizeResult.Value)
                        {
                            foreach (var line in page.Lines)
                            {
                                foreach (var word in line.Words)
                                {
                                    extractedText += word.Text + " ";
                                }
                            }
                        }
                        return extractedText.Trim();
                    return "Hello World";
                }*/
    }

}