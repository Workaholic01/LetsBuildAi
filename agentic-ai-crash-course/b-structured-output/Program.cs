using AgenticAiCrashCourse.Configuration;
using AgenticAiCrashCourse.Infrastructure;
using AgenticAiCrashCourse.StructuredOutputAgent.Agents;
using AgenticAiCrashCourse.StructuredOutputAgent.Models;
using AgenticAiCrashCourse.StructuredOutputAgent.Serialization;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using System.Text.Json;

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

SupportTicket ticket = await agent.AnalyzeAsync(complaint);

Console.WriteLine("\nStructured ticket:");
Console.WriteLine(
    JsonSerializer.Serialize(ticket, JsonDefaults.Options));