using Azure.AI.TextAnalytics;
using CoffeeApi.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;


namespace CoffeeApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NivonaStatsController : ControllerBase
    {
        private readonly IMongoCollection<NivonaStatisticsModel> _nivonaStatsCollection;
        private readonly TextAnalyticsClient _textAnalyticsClient;

        public NivonaStatsController(IMongoClient mongoClient, TextAnalyticsClient textAnalyticsClient)
        {
            var database = mongoClient.GetDatabase("nivona");
            _nivonaStatsCollection = database.GetCollection<NivonaStatisticsModel>("NivonaStatistics");
            _textAnalyticsClient = textAnalyticsClient;
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

        [HttpGet("ForecastCoffeeConsumption")]
        public async Task<ActionResult<double>> ForecastCoffeeConsumption(DateTime futureDate)
        {
            var historicalData = await _nivonaStatsCollection.Find(Builders<NivonaStatisticsModel>.Filter.Empty).ToListAsync();
            var inputText = string.Join(" ", historicalData);
            var sentimentResult = await _textAnalyticsClient.AnalyzeSentimentAsync(inputText);
            /*
             * double averageSentimentScore = 0;

              foreach (var document in sentimentResult.Value)
              {
                  averageSentimentScore += document.ConfidenceScores.Positive;
              }

              averageSentimentScore /= sentimentResult.Value.Count;
              double forecastedCoffeeConsumption = averageSentimentScore * 10;
            */
            return Ok(3);
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