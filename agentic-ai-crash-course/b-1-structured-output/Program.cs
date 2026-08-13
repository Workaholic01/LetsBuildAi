using LLMSupport.Configuration;
using LLMSupport.Infrastructure;
using StructuredOutputAgent.Agents;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using StructuredOutputAgent.Models;
using StructuredOutputAgent.Serialization;
using System.Text.Json;
using System.ClientModel;

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
var agent = new SupportTicketAgent(chatClient);

Console.WriteLine("Support Ticket Agent");
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
    SupportTicket ticket = await agent.AnalyzeAsync(complaint);

    Console.WriteLine("\nStructured ticket:");
    Console.WriteLine(
        JsonSerializer.Serialize(ticket, JsonDefaults.Options));
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
