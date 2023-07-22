using CoffeeApi.Data;
using CoffeeApi.Models;
using Microsoft.AspNetCore.Mvc;


namespace CoffeeApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NivonaStatisticsController : ControllerBase
    {
        private readonly ILogger<NivonaStatisticsController> _logger;
        public NivonaStatisticsController(ILogger<NivonaStatisticsController> logger)
        {
            _logger = logger;
        }

       
    }
}
