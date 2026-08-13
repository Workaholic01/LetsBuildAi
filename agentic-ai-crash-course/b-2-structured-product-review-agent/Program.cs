using LLMSupport.Configuration;
using LLMSupport.Infrastructure;
using ProductReviewAgent.Agents;
using OpenAI.Chat;
using ProductReviewAgent.Models;
using ProductReviewAgent.Serialization;
using System.Text.Json;
using System.ClientModel;
using Microsoft.Extensions.Configuration;

IConfiguration configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var llmOptions = configuration
    .GetRequiredSection("Llm")
    .Get<LlmOptions>()
    ?? throw new InvalidOperationException(
        "LLM configuration is missing.");

LlmProviderOptions provider = llmOptions.GetSelectedProvider();
ChatClient chatClient = LlmClientFactory.Create(llmOptions);
var agent = new ProductReviewAgent.Agents.ProductReviewAgent(chatClient);

Console.WriteLine("Product Review Agent");
Console.WriteLine($"Provider: {llmOptions.Provider}");
Console.WriteLine($"Model:    {provider.Model}");
Console.Write("\nCustomer complaint: ");

string? complaint = Console.ReadLine();

if (string.IsNullOrWhiteSpace(complaint))
{
    Console.WriteLine("A customer complaint is required.");
    return;
}

try
{
    ProductReview review = await agent.AnalyzeAsync(complaint);

    Console.WriteLine("\nStructured review:");
    Console.WriteLine(
        JsonSerializer.Serialize(review, JsonDefaults.Options));
}
catch (ModelRefusalException exception)
{
    Console.Error.WriteLine(
        $"\nThe request was refused: {exception.Refusal}");
}
catch (InvalidModelResponseException exception)
{
    Console.Error.WriteLine(
        $"\nThe model returned an unusable response: " +
        $"{exception.Message}");

    if (exception.InnerException is not null)
    {
        Console.Error.WriteLine(
            $"Cause: {exception.InnerException.Message}");
    }
}
catch (ClientResultException exception)
{
    Console.Error.WriteLine(
        $"\nProvider request failed (HTTP {exception.Status}).");
    Console.Error.WriteLine(exception.Message);
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("\nThe request was cancelled.");
}
