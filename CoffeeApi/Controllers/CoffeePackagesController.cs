using CoffeeApi.Data;
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

        [HttpPost("RefillNivona")]
        public Task PostRefillNivona()
        {
            return Task.CompletedTask;
        }

        [HttpPost( "NewPackages")]
        public async Task<IActionResult> PostNewPackages(CoffeePackages packages)
        {
            if (ModelState.IsValid)
            {
                // Save the received packages to the database

                await _coffeePackagesCollection.InsertOneAsync(packages);
                
                // Return a 201 Created response with the saved data
                return CreatedAtAction(nameof(PostNewPackages), packages);
            }
            else
            {
                // If the model state is not valid, return a 400 Bad Request with the validation errors
                return BadRequest(ModelState);
            }
        }

        [HttpPut("TakeOnePackage")]
        public Task TakeOnePackage()
        {
            return Task.CompletedTask;
        }
        

    }
}
