using CoffeeApi.Services;

namespace CoffeeTest.Fakes
{
    public class FakeChatCompletionService : IChatCompletionService
    {
        public string NextResponse { get; set; } = string.Empty;

        public Task<string> CompleteAsync(string prompt)
        {
            return Task.FromResult(NextResponse);
        }
    }
}
