using CoffeeApi.Controllers;
using CoffeeApi.Models;
using CoffeeTest.Fakes;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeTest.Controllers
{
    public class CoffeePackagesControllerTests
    {
        [Fact]
        public async Task PostNewPackages_InsertsAndReturnsOk()
        {
            var repo = new FakeCoffeePackagesRepository();
            var controller = new CoffeePackagesController(repo, new TestLogger<CoffeePackagesController>());
            var packages = new CoffeePackages();

            var result = await controller.PostNewPackages(packages);

            Assert.IsType<OkObjectResult>(result);
            Assert.Single(repo.Items);
        }

        [Fact]
        public async Task GetStatistics_ReturnsAll()
        {
            var repo = new FakeCoffeePackagesRepository();
            repo.Items.Add(new CoffeePackages());
            repo.Items.Add(new CoffeePackages());
            var controller = new CoffeePackagesController(repo, new TestLogger<CoffeePackagesController>());

            var result = await controller.GetStatistics();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var values = Assert.IsAssignableFrom<IEnumerable<CoffeePackages>>(ok.Value);
            Assert.Equal(2, values.Count());
        }
    }
}
