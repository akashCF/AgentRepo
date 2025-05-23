using Microsoft.AspNetCore.Mvc;
using Octokit;
using System.Net.Http.Json;
using System.Net.Http.Headers;

string yourGitHubAppName = "agentrepo";
string githubCopilotCompletionsUrl =
    "https://api.githubcopilot.com/chat/completions";

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapPost("/agent", async (
    [FromHeader(Name = "X-GitHub-Token")] string githubToken,
    [FromBody] Request userRequest) =>
{
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

    return Results.Stream(responseStream, "application/json");
});

app.MapGet("/callback", () => "You may close this tab and " +
    "return to GitHub.com (where you should refresh the page " +
    "and start a fresh chat). If you're using VS Code or " +
    "Visual Studio, return there.");

app.MapGet("/", () => "Hello Copilot!");

app.Run();


