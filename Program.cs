using System.Text.Json;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using OpenAI.Chat;

var model = "gpt-4o";

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var keyVaultUri = Environment.GetEnvironmentVariable("KEY_VAULT_URI");
var secretName = Environment.GetEnvironmentVariable("SECRET_NAME");

if (string.IsNullOrEmpty(keyVaultUri))
{
  throw new InvalidOperationException("Environment variable 'KEY_VAULT_URI' is missing.");
}

if (string.IsNullOrEmpty(secretName))
{
  throw new InvalidOperationException("Environment variable 'SECRET_NAME' is missing.");
}

var keyVaultClient = new SecretClient(new Uri(keyVaultUri), new DefaultAzureCredential());

var secret = await keyVaultClient.GetSecretAsync(secretName);
var apiKey = secret.Value.Value; // do more research here

string schema = File.ReadAllText("schema.json");
ChatClient client = new(model, apiKey: apiKey);

app.MapGet("/advisor", async (HttpRequest request) =>
{
  var city = request.Query["city"].ToString();
  var days = request.Query["days"].ToString();

  if (string.IsNullOrEmpty(city) || string.IsNullOrEmpty(days))
  {
    return Results.Problem("Query parameters 'city' and 'days' are required.");
  }

  List<ChatMessage> messages =
  [
      new UserChatMessage($"I would like to travel in {city} for {days} day(s). Give me some advises"),
  ];

  ChatCompletionOptions opts = new()
  {
    ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
      jsonSchemaFormatName: "travel_route_plan",
      jsonSchema: BinaryData.FromString(schema),
      jsonSchemaIsStrict: true
    )
  };

  ChatCompletion completion = await client.CompleteChatAsync(messages, opts);

  JsonDocument structuredJson = JsonDocument.Parse(completion.Content[0].Text);

  return Results.Json(new { results = structuredJson });
});

app.Run();
