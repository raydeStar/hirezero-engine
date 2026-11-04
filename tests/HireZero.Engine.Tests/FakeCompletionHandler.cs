using System.Net;
using System.Text;
using System.Text.Json;

// Answers each model call in order with the given assistant text.
internal sealed class FakeCompletionHandler(params string[] replies) : HttpMessageHandler
{
    public List<string> Bodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Bodies.Add(await request.Content!.ReadAsStringAsync(token));
        var text = replies[Math.Min(Bodies.Count - 1, replies.Length - 1)];
        var body = JsonSerializer.Serialize(
            new
            {
                id = "cmpl-fictional",
                choices = new[]
                {
                    new { message = new { role = "assistant", content = text }, finish_reason = "stop" },
                },
                usage = new { prompt_tokens = 50, completion_tokens = 20 },
            }
        );
        return new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
