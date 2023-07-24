using CoffeeApi.Data;
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

        public NivonaStatisticsController(IMongoClient mongoClient, ILogger<NivonaStatisticsController> logger)
        {
            _logger = logger;
            var database = mongoClient.GetDatabase("nivona");
            _nivonaStatsCollection = database.GetCollection<NivonaStatisticsModel>("NivonaStatistics");
        }
        [HttpPost("AddStatistics")]
        public async Task<IActionResult> PostNewStatistics(NivonaStatisticsModel stats)
        {
            if (ModelState.IsValid)
            {
                await _nivonaStatsCollection.InsertOneAsync(stats);

                // Return a 201 Created response with the URL of the newly created resource
                // Pass the name of the action ("GetStatisticsById") and route values (the "id" of the newly created resource)
                return CreatedAtAction(nameof(GetStatisticsById), new { id = stats.Id }, stats);
            }
            else
            {
                // If the model state is not valid, return a 400 Bad Request with the validation errors
                return BadRequest(ModelState);
            }
        }

        [HttpGet("GetStatisticsById/{id}")]
        public IActionResult GetStatisticsById(string id)
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
        public IActionResult GetStatistics()
        {
            // Assuming _nivonaStatsCollection is your MongoDB collection

            // Perform the database query to get all the NivonaStatisticsModel records
            var allStats = _nivonaStatsCollection.Find(Builders<NivonaStatisticsModel>.Filter.Empty).ToList();

            // Return a 200 OK response with the list of NivonaStatisticsModel records
            return Ok(allStats);
        }

    }
}