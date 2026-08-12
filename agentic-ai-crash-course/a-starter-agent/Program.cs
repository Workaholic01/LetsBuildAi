using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using StarterAgent.Agents;
using LLMSupport.Configuration;
using LLMSupport.Infrastructure;

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
Agent agent = PersonalAssistantAgent.Create(chatClient);

Console.WriteLine(agent.Name);
Console.WriteLine($"Provider: {llmOptions.Provider}");
Console.WriteLine($"Model:    {provider.Model}");
Console.WriteLine("Commands: /reset, /exit");

while (true)
{
    Console.Write("\nYou: ");
    string? input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input))
    {
        continue;
    }

    if (input.Equals(
        "/exit",
        StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    if (input.Equals(
        "/reset",
        StringComparison.OrdinalIgnoreCase))
    {
        agent.Reset();
        Console.WriteLine("Conversation reset.");
        continue;
    }

    try
    {
        string response = await agent.RunAsync(input);

        Console.WriteLine();
        Console.WriteLine($"{agent.Name}: {response}");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("The request was cancelled.");
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(
            $"Agent request failed: {exception.Message}");
    }
}