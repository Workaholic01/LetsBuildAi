# Part 1: Build a Provider-Configurable Starter Agent in .NET

**Status:** Complete  
**Series:** [Building AI Agents in .NET](README.md)

> A hands-on starter agent using the official OpenAI .NET client with either OpenAI or OpenRouter.

## What we are building

We will build an AI-agent crash course in C# and .NET. The implementation keeps agent behavior separate from model-provider configuration so the same application can run against OpenAI or OpenRouter without changing its C# code.

The course will progress through:

1. Starter agent and multi-turn conversation
2. Structured outputs
3. Function tools
4. Execution and streaming
5. Context and state
6. Guardrails
7. Persistent sessions
8. Handoffs and delegation
9. Multi-agent orchestration
10. Tracing and observability

This first article focuses only on the starter agent.

## Prerequisites

- A supported .NET SDK
- An OpenAI or OpenRouter API key
- Basic familiarity with C# and `async`/`await`

## 1. Create the solution

```bash
mkdir dotnet-agent-crash-course
cd dotnet-agent-crash-course

dotnet new sln --name DotNetAgentCrashCourse
dotnet new console --name StarterAgent --output src/StarterAgent
dotnet sln add src/StarterAgent/StarterAgent.csproj

dotnet add src/StarterAgent/StarterAgent.csproj package OpenAI
```

Run the generated application before adding anything else:

```bash
dotnet run --project src/StarterAgent
```

## 2. Add provider configuration

Install the configuration packages and initialize user secrets:

```bash
dotnet add src/StarterAgent package Microsoft.Extensions.Configuration.Json
dotnet add src/StarterAgent package Microsoft.Extensions.Configuration.EnvironmentVariables
dotnet add src/StarterAgent package Microsoft.Extensions.Configuration.UserSecrets
dotnet add src/StarterAgent package Microsoft.Extensions.Configuration.Binder
dotnet user-secrets init --project src/StarterAgent
```

Create `src/StarterAgent/appsettings.json`:

```json
{
  "Llm": {
    "Provider": "OpenAI",
    "Providers": {
      "OpenAI": {
        "Endpoint": "https://api.openai.com/v1",
        "Model": "gpt-5.6"
      },
      "OpenRouter": {
        "Endpoint": "https://openrouter.ai/api/v1",
        "Model": "~openai/gpt-latest"
      }
    }
  }
}
```

Configure `StarterAgent.csproj` to copy the settings file:

```xml
<ItemGroup>
  <None Update="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

Create `Configuration/LlmOptions.cs`:

```csharp
namespace StarterAgent.Configuration;

public sealed class LlmOptions
{
    public required string Provider { get; init; }
    public required Dictionary<string, LlmProviderOptions> Providers { get; init; }

    public LlmProviderOptions GetSelectedProvider()
    {
        var match = Providers.FirstOrDefault(pair =>
            string.Equals(pair.Key, Provider, StringComparison.OrdinalIgnoreCase));

        return match.Value
            ?? throw new InvalidOperationException(
                $"LLM provider '{Provider}' is not configured.");
    }
}

public sealed class LlmProviderOptions
{
    public required string Endpoint { get; init; }
    public required string Model { get; init; }
    public string? ApiKey { get; init; }
}
```

Store API keys outside source control:

```bash
dotnet user-secrets set "Llm:Providers:OpenAI:ApiKey" "YOUR_OPENAI_KEY" --project src/StarterAgent
dotnet user-secrets set "Llm:Providers:OpenRouter:ApiKey" "YOUR_OPENROUTER_KEY" --project src/StarterAgent
```

You need to configure only the provider you intend to use.

## 3. Build a provider-neutral client factory

Create `Infrastructure/LlmClientFactory.cs`:

```csharp
using System.ClientModel;
using OpenAI;
using OpenAI.Chat;
using StarterAgent.Configuration;

namespace StarterAgent.Infrastructure;

public static class LlmClientFactory
{
    public static ChatClient Create(LlmOptions options)
    {
        LlmProviderOptions provider = options.GetSelectedProvider();

        if (string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            throw new InvalidOperationException(
                $"No API key is configured for '{options.Provider}'.");
        }

        if (!Uri.TryCreate(provider.Endpoint, UriKind.Absolute, out Uri? endpoint))
        {
            throw new InvalidOperationException(
                $"The endpoint '{provider.Endpoint}' is invalid.");
        }

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = endpoint
        };

        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(provider.ApiKey),
            clientOptions);

        return openAiClient.GetChatClient(provider.Model);
    }
}
```

The factory is the provider boundary. Everything above it depends on `ChatClient`, while endpoint, model, and credentials remain configuration concerns.

## 4. Define an agent

For this course, the starter agent consists of a name, instructions, a model client, and its in-memory conversation history.

Create `Agents/Agent.cs`:

```csharp
using OpenAI.Chat;

namespace StarterAgent.Agents;

public sealed class Agent
{
    private readonly ChatClient _chatClient;
    private readonly List<ChatMessage> _history;

    public Agent(string name, string instructions, ChatClient chatClient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);
        ArgumentNullException.ThrowIfNull(chatClient);

        Name = name;
        Instructions = instructions;
        _chatClient = chatClient;
        _history = [new SystemChatMessage(instructions)];
    }

    public string Name { get; }
    public string Instructions { get; }
    public int MessageCount => _history.Count - 1;

    public async Task<string> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        _history.Add(new UserChatMessage(input));

        try
        {
            ChatCompletion completion = await _chatClient.CompleteChatAsync(
                _history,
                cancellationToken: cancellationToken);

            string response = string.Concat(
                completion.Content.Select(part => part.Text));

            _history.Add(new AssistantChatMessage(response));
            return response;
        }
        catch
        {
            _history.RemoveAt(_history.Count - 1);
            throw;
        }
    }

    public void Reset()
    {
        _history.Clear();
        _history.Add(new SystemChatMessage(Instructions));
    }
}
```

Create `Agents/PersonalAssistantAgent.cs`:

```csharp
using OpenAI.Chat;

namespace StarterAgent.Agents;

public static class PersonalAssistantAgent
{
    public static Agent Create(ChatClient chatClient)
    {
        return new Agent(
            name: "Personal Assistant",
            instructions:
            """
            You are a helpful personal assistant.

            Follow these guidelines:
            - Give clear and concise answers.
            - Prefer practical recommendations.
            - Ask for clarification when the request is ambiguous.
            - Do not invent facts when you are uncertain.
            """,
            chatClient: chatClient);
    }
}
```

## 5. Run a multi-turn console conversation

The console application creates one agent instance and reuses it for the entire loop. Because the agent retains user and assistant messages, later questions can refer to earlier turns.

Useful commands:

- `/reset` clears the current in-memory conversation.
- `/exit` ends the application.

The complete `Program.cs` appears in the accompanying implementation step.

```csharp
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using StarterAgent.Agents;
using StarterAgent.Configuration;
using StarterAgent.Infrastructure;

IConfiguration configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var llmOptions = configuration
    .GetRequiredSection("Llm")
    .Get<LlmOptions>()
    ?? throw new InvalidOperationException("LLM configuration is missing.");

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

    if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    if (input.Equals("/reset", StringComparison.OrdinalIgnoreCase))
    {
        agent.Reset();
        Console.WriteLine("Conversation reset.");
        continue;
    }

    try
    {
        string response = await agent.RunAsync(input);
        Console.WriteLine($"\n{agent.Name}: {response}");
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Agent request failed: {exception.Message}");
    }
}
```

Try this sequence:

```text
You: My name is Sam and I am learning ASP.NET Core.
You: What am I learning?
```

The second answer should mention ASP.NET Core because both turns are submitted as conversation history.

## What we learned

- Provider selection belongs in configuration, not agent code.
- Secrets should not be committed to `appsettings.json`.
- An agent combines stable instructions with a model and an execution loop.
- Multi-turn memory is conversation history supplied with each model request.
- In-memory history disappears when the process exits; persistent sessions are a later concern.

## Next milestone

[Part 2](02-structured-outputs.md) replaces free-form answers with a strict JSON Schema and a type-safe C# support-ticket model. Streaming stays in Part 4, where it can cover both text and tool-execution events.

## References

- [OpenAI SDKs and CLI](https://developers.openai.com/api/docs/libraries)
- [OpenAI Chat API reference](https://developers.openai.com/api/reference/resources/chat)
- [OpenRouter quickstart](https://openrouter.ai/docs/quickstart)
