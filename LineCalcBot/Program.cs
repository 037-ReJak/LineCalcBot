using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string ACCESS_TOKEN = "ここにアクセストークン";

app.MapPost("/webhook", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();

    var json = JsonDocument.Parse(body);
    var ev = json.RootElement
        .GetProperty("events")[0];

    var replyToken = ev.GetProperty("replyToken").GetString();
    var text = ev.GetProperty("message").GetProperty("text").GetString();

    string result = Calc(text);

    await Reply(replyToken!, result, ACCESS_TOKEN);

    return Results.Ok();
});

app.Run();

string Calc(string input)
{
    try
    {
        var dt = new System.Data.DataTable();
        var v = dt.Compute(input, "");
        return v.ToString()!;
    }
    catch
    {
        return "計算できません";
    }
}

async Task Reply(string token, string message, string accessToken)
{
    var client = new HttpClient();

    var payload = new
    {
        replyToken = token,
        messages = new[]
        {
            new { type="text", text=message }
        }
    };

    var json = JsonSerializer.Serialize(payload);

    var req = new HttpRequestMessage(
        HttpMethod.Post,
        "https://api.line.me/v2/bot/message/reply"
    );

    req.Headers.Add("Authorization", $"Bearer {accessToken}");
    req.Content = new StringContent(json, Encoding.UTF8, "application/json");

    await client.SendAsync(req);
}
