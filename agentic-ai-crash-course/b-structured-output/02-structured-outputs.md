# Part 2: Type-Safe Structured Outputs in .NET

**Status:** In progress  
**Builds on:** [Part 1 — Starter agent](01-starter-agent.md)

## The problem

Free-form text is useful for conversation, but application code often needs predictable fields. Parsing prose with string operations or hoping the model emits valid JSON is brittle.

Structured Outputs constrains the model response to a JSON Schema. The application can then deserialize that JSON into a C# type.

## What we will build

A support-ticket agent that converts an unstructured customer complaint into:

- a concise title;
- a category;
- a priority;
- a customer-facing summary;
- a list of suggested next actions.

## Learning objectives

- Model the expected result with C# records and enums.
- Express the contract as JSON Schema.
- request strict structured output from a compatible model;
- detect refusals and invalid responses;
- deserialize safely with `System.Text.Json`;
- keep OpenAI/OpenRouter selection configuration-driven.

## Implementation plan

1. Add a new `StructuredOutputAgent` console project.
2. Reuse the provider configuration shape from Part 1.
3. Define the `SupportTicket` domain model.
4. Define its strict JSON Schema.
5. Send the schema as the response format.
6. Deserialize and display the typed result.
7. Verify representative and malformed inputs.

## Step 1: Create the project

From the solution root:

```bash
dotnet new console --name StructuredOutputAgent --output src/StructuredOutputAgent
dotnet sln add src/StructuredOutputAgent/StructuredOutputAgent.csproj

dotnet add src/StructuredOutputAgent package OpenAI
dotnet add src/StructuredOutputAgent package Microsoft.Extensions.Configuration.Json
dotnet add src/StructuredOutputAgent package Microsoft.Extensions.Configuration.EnvironmentVariables
dotnet add src/StructuredOutputAgent package Microsoft.Extensions.Configuration.UserSecrets
dotnet add src/StructuredOutputAgent package Microsoft.Extensions.Configuration.Binder

dotnet user-secrets init --project src/StructuredOutputAgent
```

Copy these provider-layer files from `StarterAgent` into the same relative locations under `StructuredOutputAgent`:

```text
Configuration/LlmOptions.cs
Infrastructure/LlmClientFactory.cs
appsettings.json
```

Change their namespaces from `StarterAgent` to `StructuredOutputAgent`, then add the same `appsettings.json` copy rule to the new project file.

Copy only the API key you need into this project's user-secrets store. User secrets are project-specific.

Run the empty project before continuing:

```bash
dotnet run --project src/StructuredOutputAgent
```

## Design note: copy now, extract later

At this early stage, duplicating two small provider-layer files keeps each tutorial runnable on its own. After the patterns stabilize, the series can extract them into a shared class library without obscuring the learning objective of Part 2.

## Step 2: Define the typed output contract

Before writing a prompt or JSON Schema, define the shape the rest of the application needs. This keeps the design application-first: the model must adapt to our contract, rather than application code adapting to arbitrary model prose.

Create `Models/SupportTicket.cs`:

```csharp
namespace StructuredOutputAgent.Models;

public sealed record SupportTicket
{
    public required string Title { get; init; }

    public required TicketCategory Category { get; init; }

    public required TicketPriority Priority { get; init; }

    public required string Summary { get; init; }

    public required List<string> SuggestedActions { get; init; }
}

public enum TicketCategory
{
    Account,
    Billing,
    Technical,
    FeatureRequest,
    Other
}

public enum TicketPriority
{
    Low,
    Medium,
    High,
    Critical
}
```

This model deliberately uses:

- `required` properties so callers cannot accidentally construct incomplete tickets;
- enums to limit category and priority to known application values;
- a list because one complaint may require several follow-up actions;
- a record because this type represents data rather than an object with mutable behavior.

At this point, do not add JSON-specific attributes or prompt instructions to the domain model. The next layer will map this C# contract to JSON Schema and configure `System.Text.Json` explicitly.

Verify that the project compiles:

```bash
dotnet build src/StructuredOutputAgent/StructuredOutputAgent.csproj
```

The project will still print `Hello, World!`. That is expected—we have defined the output contract but have not called the model yet.

## Step 3: Define a strict JSON Schema

The C# type is the contract used inside the application. The model needs the equivalent contract expressed as JSON Schema.

Create `Schemas/SupportTicketSchema.cs`:

```csharp
using System.Text.Json;

namespace StructuredOutputAgent.Schemas;

public static class SupportTicketSchema
{
    public const string Name = "support_ticket";

    public const string Json =
        """
        {
          "type": "object",
          "properties": {
            "title": {
              "type": "string",
              "description": "A concise title describing the customer issue."
            },
            "category": {
              "type": "string",
              "description": "The application category for the issue.",
              "enum": [
                "account",
                "billing",
                "technical",
                "featureRequest",
                "other"
              ]
            },
            "priority": {
              "type": "string",
              "description": "The urgency of the support request.",
              "enum": [
                "low",
                "medium",
                "high",
                "critical"
              ]
            },
            "summary": {
              "type": "string",
              "description": "A clear customer-facing summary of the issue."
            },
            "suggestedActions": {
              "type": "array",
              "description": "Practical next actions for resolving the issue.",
              "items": {
                "type": "string"
              }
            }
          },
          "required": [
            "title",
            "category",
            "priority",
            "summary",
            "suggestedActions"
          ],
          "additionalProperties": false
        }
        """;

    public static JsonDocument Parse()
    {
        return JsonDocument.Parse(Json);
    }
}
```

The schema mirrors the C# contract exactly:

| C# member | JSON property | JSON type |
| --- | --- | --- |
| `Title` | `title` | string |
| `Category` | `category` | constrained string |
| `Priority` | `priority` | constrained string |
| `Summary` | `summary` | string |
| `SuggestedActions` | `suggestedActions` | array of strings |

Three details make the schema strict:

1. Every property appears in `required`.
2. Both enums list their permitted serialized values.
3. `additionalProperties: false` prevents the model from inventing fields the application does not understand.

The schema uses camel-case JSON names and enum values. In the next step, `System.Text.Json` will be configured with the same naming policy so deserialization matches this contract.

### Validate the schema's JSON syntax

Temporarily replace `Program.cs` with:

```csharp
using System.Text.Json;
using StructuredOutputAgent.Schemas;

using JsonDocument schema = SupportTicketSchema.Parse();

Console.WriteLine(
    $"Schema '{SupportTicketSchema.Name}' is valid JSON.");
```

Run the project:

```bash
dotnet run --project src/StructuredOutputAgent
```

Expected output:

```text
Schema 'support_ticket' is valid JSON.
```

This confirms that the schema document is syntactically valid JSON. The selected provider will validate whether it supports the schema and Structured Outputs when we send the first request.

## Step 4: Align `System.Text.Json` with the schema

The schema now defines the provider-facing contract. Next, configure the .NET serializer to use that same contract:

- property names are camel case;
- enum values are camel-case strings;
- unknown properties are rejected;
- formatted JSON is easier to inspect while learning.

Create `Serialization/JsonDefaults.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StructuredOutputAgent.Serialization;

public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = true
        };

        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        return options;
    }
}
```

`JsonUnmappedMemberHandling.Disallow` requires .NET 8 or later. It makes deserialization fail when the response contains fields outside the schema instead of silently ignoring them.

For this step, replace `Program.cs` with a local round-trip test:

```csharp
using System.Text.Json;
using StructuredOutputAgent.Models;
using StructuredOutputAgent.Serialization;

const string json =
    """
    {
      "title": "Unable to access account",
      "category": "account",
      "priority": "high",
      "summary": "The customer cannot sign in after resetting their password.",
      "suggestedActions": [
        "Verify the customer identity.",
        "Check whether the account is locked.",
        "Send a new password reset link."
      ]
    }
    """;

SupportTicket ticket =
    JsonSerializer.Deserialize<SupportTicket>(
        json,
        JsonDefaults.Options)
    ?? throw new JsonException("The support ticket was empty.");

Console.WriteLine($"{ticket.Category} / {ticket.Priority}");
Console.WriteLine(
    JsonSerializer.Serialize(ticket, JsonDefaults.Options));
```

Run it:

```bash
dotnet run --project src/StructuredOutputAgent
```

The first line should be:

```text
Account / High
```

The remaining output should contain camel-case property names and camel-case string enum values such as `"category": "account"` and `"priority": "high"`.

This proves that the same JSON shape can cross the boundary in both directions. If this local test fails, fix the contract before making a remote model request.

## Step 5: Send the first structured-output request

Now connect the pieces:

1. the configured provider creates the `ChatClient`;
2. the JSON Schema becomes the request's response format;
3. the model returns JSON constrained by that schema;
4. `System.Text.Json` converts the response into `SupportTicket`.

### Check model compatibility first

The selected model must support strict structured outputs.

- For OpenAI, check that the model lists Structured Outputs as supported.
- For OpenRouter, check the model's supported parameters for `structured_outputs` or `response_format`.

If the model does not support this feature, the provider should reject the request. Changing providers still requires only a configuration change; the agent code below remains the same.

### Create the support-ticket agent

Create `Agents/SupportTicketAgent.cs`:

```csharp
using System.Text;
using System.Text.Json;
using OpenAI.Chat;
using StructuredOutputAgent.Models;
using StructuredOutputAgent.Schemas;
using StructuredOutputAgent.Serialization;

namespace StructuredOutputAgent.Agents;

public sealed class SupportTicketAgent
{
    private const string Instructions =
        """
        You convert customer complaints into support tickets.

        Follow these rules:
        - Use only information present in the complaint.
        - Choose the closest available category.
        - Base priority on urgency and customer impact.
        - Use critical only for severe, time-sensitive impact.
        - Suggest practical actions without inventing account details.
        """;

    private readonly ChatClient _chatClient;
    private readonly ChatCompletionOptions _completionOptions;

    public SupportTicketAgent(ChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);

        _chatClient = chatClient;
        _completionOptions = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: SupportTicketSchema.Name,
                jsonSchema: BinaryData.FromBytes(
                    Encoding.UTF8.GetBytes(SupportTicketSchema.Json)),
                jsonSchemaIsStrict: true)
        };
    }

    public async Task<SupportTicket> AnalyzeAsync(
        string complaint,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(complaint);

        List<ChatMessage> messages =
        [
            new SystemChatMessage(Instructions),
            new UserChatMessage(complaint)
        ];

        ChatCompletion completion = await _chatClient.CompleteChatAsync(
            messages,
            _completionOptions,
            cancellationToken);

        string json = string.Concat(
            completion.Content.Select(part => part.Text));

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException(
                "The provider returned no structured content.");
        }

        try
        {
            return JsonSerializer.Deserialize<SupportTicket>(
                json,
                JsonDefaults.Options)
                ?? throw new JsonException(
                    "The structured response was empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "The response did not match the SupportTicket contract.",
                exception);
        }
    }
}
```

There is no OpenAI/OpenRouter branch in this class. `ChatResponseFormat.CreateJsonSchemaFormat` produces the OpenAI-compatible structured-output request, while `LlmClientFactory` still controls the endpoint, model, and credential.

The system prompt describes classification behavior, not JSON formatting. The schema is responsible for the output shape.

### Call the agent from `Program.cs`

Replace the local serializer test in `Program.cs` with:

```csharp
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using StructuredOutputAgent.Agents;
using StructuredOutputAgent.Configuration;
using StructuredOutputAgent.Infrastructure;
using StructuredOutputAgent.Models;
using StructuredOutputAgent.Serialization;

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
```

Run the application:

```bash
dotnet run --project src/StructuredOutputAgent
```

Try this input:

```text
I was charged twice for my subscription this morning. I need the duplicate charge reversed before my rent payment is processed tomorrow.
```

The exact wording may vary, but the output must contain exactly the five schema fields, with a valid category and priority:

```json
{
  "title": "Duplicate subscription charge",
  "category": "billing",
  "priority": "high",
  "summary": "The customer was charged twice for a subscription and needs the duplicate charge reversed quickly.",
  "suggestedActions": [
    "Verify both subscription charges.",
    "Reverse the duplicate charge if confirmed.",
    "Tell the customer when the refund should appear."
  ]
}
```

This is the first remote call in Part 2. If it succeeds, the complete path is working: provider configuration, strict JSON Schema, model response, and typed C# deserialization.

## Next implementation step

Harden the agent for refusal, truncated output, invalid JSON, unsupported models, and provider errors without losing the original diagnostic context.

## Reference

- [Official OpenAI Structured Outputs guide](https://developers.openai.com/api/docs/guides/structured-outputs)
- [Official OpenAI .NET SDK structured-output example](https://github.com/openai/openai-dotnet#how-to-use-chat-completions-with-structured-outputs)
- [OpenRouter Structured Outputs guide](https://openrouter.ai/docs/guides/features/structured-outputs)
