using CoffeeApi.Data;
using CoffeeApi.Models;
using Microsoft.AspNetCore.Mvc;


namespace CoffeeApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CoffeePackagesController : ControllerBase
    {
        private readonly ILogger<CoffeePackagesController> _logger;
        public CoffeePackagesController(ILogger<CoffeePackagesController> logger)
        {
            _logger = logger;
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
                /* _dbContext.CoffeePackages.Add(packages);
                 await _dbContext.SaveChangesAsync();
                */
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
