using CoffeeApi.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace CoffeeApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CoffeePackagesController : ControllerBase
    {
        private readonly ILogger<CoffeePackagesController> _logger;
        private readonly IMongoCollection<CoffeePackages> _coffeePackagesCollection;

        public CoffeePackagesController(IMongoClient mongoClient, ILogger<CoffeePackagesController> logger)
        {
            _logger = logger;
            var database = mongoClient.GetDatabase("coffee");
            _coffeePackagesCollection = database.GetCollection<CoffeePackages>("CoffeePackages");


        }


        [HttpPost("NewPackages")]
        public async Task<IActionResult> PostNewPackages(CoffeePackages packages)
        {
            if (ModelState.IsValid)
            {
                await _coffeePackagesCollection.InsertOneAsync(packages);
                return Ok("success");
            }
            else
            {
                return BadRequest(ModelState);
            }
        }



        [HttpGet("GetStatistics")]
        public ActionResult<IEnumerable<CoffeePackages>> GetStatistics()
        {
            var allStats = _coffeePackagesCollection.Find(Builders<CoffeePackages>.Filter.Empty).ToList();
            return Ok(allStats);
        }

    }
}
