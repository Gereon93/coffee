using CoffeeApi.Controllers;
using CoffeeApi.Models;
using CoffeeApi.Services;
using CoffeeTest.Fakes;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace CoffeeTest.Controllers
{
    public class NivonaStatsControllerTests
    {
        [Fact]
        public async Task PostNewStatistics_FillsMissingCounts_FromLatest()
        {
            var repo = new FakeNivonaStatsRepository();
            repo.Items.Add(new NivonaStatisticsModel
            {
                Id = ObjectId.GenerateNewId(),
                Timestamp = new DateTime(2024, 1, 1),
                CoffeeCount = 10,
                EspressoCount = 5,
                LatteMacchiatoCount = 3
            });
            var chat = new FakeChatCompletionService();
            var merger = new NivonaStatsMerger();
            var controller = new NivonaStatsController(repo, chat, merger);

            var stats = new NivonaStatisticsModel
            {
                Timestamp = new DateTime(2024, 1, 2),
                CoffeeCount = 0,
                EspressoCount = 2,
                LatteMacchiatoCount = 0
            };

            var result = await controller.PostNewStatistics(stats);

            Assert.IsType<CreatedAtActionResult>(result);
            Assert.Single(repo.Items);
            Assert.Equal(10, stats.CoffeeCount);
            Assert.Equal(2, stats.EspressoCount);
            Assert.Equal(3, stats.LatteMacchiatoCount);
        }

        [Fact]
        public async Task GetStatisticsById_ReturnsNotFound_WhenMissing()
        {
            var controller = new NivonaStatsController(
                new FakeNivonaStatsRepository(),
                new FakeChatCompletionService(),
                new NivonaStatsMerger());

            var result = await controller.GetStatisticsById("missing");

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetStatisticsById_ReturnsOk_WhenFound()
        {
            var repo = new FakeNivonaStatsRepository();
            var model = new NivonaStatisticsModel
            {
                Id = ObjectId.GenerateNewId(),
                Timestamp = new DateTime(2024, 1, 1),
                CoffeeCount = 7
            };
            repo.Items.Add(model);
            var controller = new NivonaStatsController(
                repo,
                new FakeChatCompletionService(),
                new NivonaStatsMerger());

            var result = await controller.GetStatisticsById(model.Id.ToString());

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(model, ok.Value);
        }

        [Fact]
        public async Task GetLastStatistics_ReturnsNotFound_WhenEmpty()
        {
            var controller = new NivonaStatsController(
                new FakeNivonaStatsRepository(),
                new FakeChatCompletionService(),
                new NivonaStatsMerger());

            var result = await controller.GetLastStatisticsAsync();

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetLastStatistics_ReturnsLatest()
        {
            var repo = new FakeNivonaStatsRepository();
            var older = new NivonaStatisticsModel { Timestamp = new DateTime(2024, 1, 1) };
            var newer = new NivonaStatisticsModel { Timestamp = new DateTime(2024, 1, 2) };
            repo.Items.Add(older);
            repo.Items.Add(newer);
            var controller = new NivonaStatsController(
                repo,
                new FakeChatCompletionService(),
                new NivonaStatsMerger());

            var result = await controller.GetLastStatisticsAsync();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(newer, ok.Value);
        }

        [Fact]
        public async Task GetStatistics_ReturnsAll()
        {
            var repo = new FakeNivonaStatsRepository();
            repo.Items.Add(new NivonaStatisticsModel { Timestamp = new DateTime(2024, 1, 1) });
            repo.Items.Add(new NivonaStatisticsModel { Timestamp = new DateTime(2024, 1, 2) });
            var controller = new NivonaStatsController(
                repo,
                new FakeChatCompletionService(),
                new NivonaStatsMerger());

            var result = await controller.GetStatistics();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var values = Assert.IsAssignableFrom<IEnumerable<NivonaStatisticsModel>>(ok.Value);
            Assert.Equal(2, values.Count());
        }

        [Fact]
        public async Task ForecastCoffeeStatistics_ReturnsPredictedText()
        {
            var repo = new FakeNivonaStatsRepository();
            repo.Items.Add(new NivonaStatisticsModel { Timestamp = new DateTime(2024, 1, 1) });
            var chat = new FakeChatCompletionService { NextResponse = "predicted" };
            var controller = new NivonaStatsController(repo, chat, new NivonaStatsMerger());

            var result = await controller.ForecastCoffeeStatisticsAsync(new DateTime(2024, 2, 1));

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var predicted = Assert.IsType<PredictedStats>(ok.Value);
            Assert.Equal("predicted", predicted.Text);
        }

        [Fact]
        public async Task UploadImage_ReturnsBadRequest_WhenNoFile()
        {
            var controller = new NivonaStatsController(
                new FakeNivonaStatsRepository(),
                new FakeChatCompletionService(),
                new NivonaStatsMerger());

            var result = await controller.UploadImage(null!);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("No file uploaded", badRequest.Value);
        }
    }
}
