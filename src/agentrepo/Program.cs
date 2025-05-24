using Microsoft.AspNetCore.Mvc;
using Octokit;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

string yourGitHubAppName = "agentrepo";
string githubCopilotCompletionsUrl =
    "https://api.githubcopilot.com/chat/completions";

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Logging middleware to log every request to a file
app.Use(async (context, next) =>
{
    var logPath = Path.Combine(AppContext.BaseDirectory, "requests.log");
    var logEntry = new StringBuilder();
    logEntry.AppendLine($"[{DateTime.UtcNow:O}] {context.Request.Method} {context.Request.Path}");
    logEntry.AppendLine("=== REQUEST HEADERS ===");
    foreach (var header in context.Request.Headers)
    {
        logEntry.AppendLine($"  {header.Key}: {header.Value}");
    }
    logEntry.AppendLine();
    await File.AppendAllTextAsync(logPath, logEntry.ToString());
    await next();
});

app.MapGet("/callback", () => Results.BadRequest("Use POST for this endpoint."));

app.MapPost("/callback", async (
    [FromHeader(Name = "X-GitHub-Token")] string githubToken,
    [FromBody] Request userRequest) =>
{
    string logPath = Path.Combine(AppContext.BaseDirectory, "agent_logs.log");
    var logEntry = new StringBuilder();
    
    // Log request
    logEntry.AppendLine($"[{DateTime.UtcNow:O}] New Agent Request");
    logEntry.AppendLine("=== REQUEST ===");
    logEntry.AppendLine(JsonSerializer.Serialize(userRequest, new JsonSerializerOptions { WriteIndented = true }));
    logEntry.AppendLine();
    
    // Initialize Octokit GitHub client with app name and credentials
    var octokitClient = new GitHubClient(new Octokit.ProductHeaderValue("agentrepo"))
    {
        Credentials = new Credentials(githubToken)
    };

    // Fetch current GitHub user
    var user = await octokitClient.User.Current();

    // Insert system prompts
    userRequest.Messages.Insert(0, new Message
    {
        Role = "system",
        Content = $"Start every response with the user's name, which is @{user.Login}"
    });
    userRequest.Messages.Insert(0, new Message
    {
        Role = "system",
        Content = "You are a helpful assistant that replies to user messages as if you were Blackbeard the Pirate."
    });

    // Prepare HTTP client
    var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", githubToken);

    userRequest.Stream = true;

    // Call GitHub Copilot API
    var copilotLLMResponse = await httpClient.PostAsJsonAsync(
        githubCopilotCompletionsUrl, userRequest);

    var responseStream = await copilotLLMResponse.Content.ReadAsStreamAsync();
    
    // Create a copy of the response stream for logging
    var memoryStream = new MemoryStream();
    await responseStream.CopyToAsync(memoryStream);
    memoryStream.Position = 0; // Reset position to start of stream
    
    // Log response
    using (var reader = new StreamReader(new MemoryStream(memoryStream.ToArray())))
    {
        logEntry.AppendLine("=== RESPONSE ===");
        logEntry.AppendLine(await reader.ReadToEndAsync());
        logEntry.AppendLine();
        logEntry.AppendLine("==============================");
        
        // Write logs to file
        await File.AppendAllTextAsync(logPath, logEntry.ToString());
    }
    
    // Reset memory stream position for the actual response
    memoryStream.Position = 0;
    
    return Results.Stream(memoryStream, "application/json");
});

app.MapGet("/agent", () => "You may close this tab and " +
    "return to GitHub.com (where you should refresh the page " +
    "and start a fresh chat). If you're using VS Code or " +
    "Visual Studio, return there.");


app.MapGet("/", () => "Hello Copilot!");

app.Run();


