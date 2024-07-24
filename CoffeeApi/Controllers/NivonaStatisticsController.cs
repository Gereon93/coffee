using CoffeeApi.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace CoffeeApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NivonaStatisticsController : ControllerBase
    {
        private readonly ILogger<NivonaStatisticsController> _logger;
        private readonly IMongoCollection<NivonaStatisticsModel> _nivonaStatsCollection;
        private readonly TextAnalyticsClient _textAnalyticsClient;

        public NivonaStatisticsController(IMongoClient mongoClient, ILogger<NivonaStatisticsController> logger, TextAnalyticsClient textAnalyticsClient)
        {
            _logger = logger;
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
                // If the model state is not valid, return a 400 Bad Request with the validation errors
                return BadRequest(ModelState);
            }
        }

        [HttpGet("GetStatisticsById/{id}")]
        public ActionResult<NivonaStatisticsModel> GetStatisticsById(string id)
        {
            // Assuming _nivonaStatsCollection is your MongoDB collection

            // Try to find the NivonaStatisticsModel with the provided id
            var filter = Builders<NivonaStatisticsModel>.Filter.Eq("_id", id);

            // Perform the database query to find the model
            var foundStats = _nivonaStatsCollection.Find(filter).SingleOrDefault();

            if (foundStats == null)
            {
                // If the model is not found, return a 404 Not Found response
                return NotFound();
            }

            // If the model is found, return a 200 OK response with the model data
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
            // Assuming _nivonaStatsCollection is your MongoDB collection

            // Get the historical coffee consumption data
            var historicalData = await _nivonaStatsCollection.Find(Builders<NivonaStatisticsModel>.Filter.Empty).ToListAsync();

            // Prepare the input text for sentiment analysis
            var inputText = string.Join(" ", historicalData);

            // Analyze the sentiment of the input text
            var sentimentResult = await _textAnalyticsClient.AnalyzeSentimentAsync(inputText);

            // Calculate the average sentiment score
            double averageSentimentScore = 0;
            foreach (var document in sentimentResult.Value)
            {
                averageSentimentScore += document.ConfidenceScores.Positive;
            }
            averageSentimentScore /= sentimentResult.Value.Count;

            // Use the average sentiment score to forecast coffee consumption
            double forecastedCoffeeConsumption = averageSentimentScore * 10; // Adjust the multiplier as needed

            return Ok(forecastedCoffeeConsumption);
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

                // Convert the image bytes to JSON and save it to the database
                string json = Convert.ToBase64String(imageBytes);
                await _nivonaStatsCollection.InsertOneAsync(new NivonaStatisticsModel { ImageData = json });

            }

            return Ok();
        }

        public async Task<string> ExtractTextFromImage(string imagePath)
        {
            // Erstellen Sie eine Instanz des FormRecognizerClients mit Ihren Azure Cognitive Services-Anmeldeinformationen
            var formRecognizerClient = new FormRecognizerClient(new Uri("<YOUR_FORM_RECOGNIZER_ENDPOINT>"), new AzureKeyCredential("<YOUR_FORM_RECOGNIZER_API_KEY>"));

            // Laden Sie das Bild in ein MemoryStream-Objekt
            using (var stream = new FileStream(imagePath, FileMode.Open))
            {
                // Extrahieren Sie den Text aus dem Bild
                var recognizeOptions = new RecognizeContentOptions() { ContentType = "image/jpeg" };
                var recognizeResult = await formRecognizerClient.StartRecognizeContentAsync(stream, recognizeOptions).WaitForCompletionAsync();

                // Extrahieren Sie den erkannten Text aus dem Ergebnis
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
            }
        }
    }
}