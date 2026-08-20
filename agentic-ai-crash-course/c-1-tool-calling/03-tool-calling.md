# Part 3: Tool Calling in .NET

**Status:** In progress
**Builds on:** [Part 1 — Starter agent](../a-starter-agent/01-starter-agent.md), [Part 2 — Structured outputs](../b-1-structured-output/02-structured-outputs.md)

## The problem

A chat model can only answer from what it already knows. It cannot look up today's weather, run a calculation it might get wrong, or call into another part of your application. Tool calling closes that gap: the model is given a list of functions it may request, and instead of guessing an answer it asks the caller to run one of those functions and report back the result.

The Chat Completions API does not execute anything on its own. It only ever returns a *request* to call a named function with some JSON arguments. Running that function, feeding the result back, and deciding when the conversation is actually finished is entirely the caller's responsibility. That loop is what this part builds.

## What we will build

A `Tool Using Agent` with four tools:

- `add_numbers` / `multiply_numbers` — real arithmetic, so the model never has to compute by hand;
- `get_weather` — a mock lookup, standing in for a real external call;
- `convert_temperature` — a small unit-conversion utility.

The agent can chain multiple tool calls in a single turn — for example, asking for a sum and the weather in one message triggers two tool calls before the model produces its final answer.

## Learning objectives

- Describe a function tool's parameters as JSON Schema, the same technique used for structured outputs in Part 2.
- Deserialize a tool call's arguments into a typed C# record.
- Implement the request/execute/respond loop the Chat Completions API expects.
- Keep a single bad tool call from crashing the whole turn.
- Recognize which parts of the OpenAI Agents SDK model (Python) do not map onto the raw Chat Completions API, and why.

## Implementation plan

1. Add a new `ToolUsingAgent` console project.
2. Define a reusable `ToolDefinition` type: schema plus handler.
3. Implement four concrete tools with typed argument records.
4. Implement the tool-calling loop in `ToolAgent`.
5. Wire instructions and tools together in a factory.
6. Connect it to the console.
7. Verify multi-tool turns and failure handling.

## Step 1: Create the project

From the solution root (`agentic-ai-crash-course`, next to `AgenticAiCrashCourse.slnx`):

```bash
dotnet new console --name ToolUsingAgent --output c-1-tool-calling/ToolUsingAgent
dotnet sln add c-1-tool-calling/ToolUsingAgent/ToolUsingAgent.csproj
dotnet add c-1-tool-calling/ToolUsingAgent reference ../../LLMSupport/LLMSupport.csproj

dotnet add c-1-tool-calling/ToolUsingAgent package OpenAI
dotnet add c-1-tool-calling/ToolUsingAgent package Microsoft.Extensions.Configuration
dotnet add c-1-tool-calling/ToolUsingAgent package Microsoft.Extensions.Configuration.Json
dotnet add c-1-tool-calling/ToolUsingAgent package Microsoft.Extensions.Configuration.EnvironmentVariables
dotnet add c-1-tool-calling/ToolUsingAgent package Microsoft.Extensions.Configuration.UserSecrets
dotnet add c-1-tool-calling/ToolUsingAgent package Microsoft.Extensions.Configuration.Binder

dotnet user-secrets init --project c-1-tool-calling/ToolUsingAgent
```

`ToolUsingAgent` sits one directory deeper than `b-1-structured-output` (`c-1-tool-calling/ToolUsingAgent/`), so its reference to the shared library is `..\..\LLMSupport\LLMSupport.csproj` rather than `..\LLMSupport\LLMSupport.csproj`.

Copy `appsettings.json` from `b-1-structured-output` into `c-1-tool-calling/ToolUsingAgent`, keeping the same copy rule:

```xml
<ItemGroup>
  <None Update="appsettings.json">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

Copy the API key you need into this project's own user-secrets store, then confirm the empty project runs:

```bash
dotnet run --project c-1-tool-calling/ToolUsingAgent
```

`LlmOptions` and `LlmClientFactory` are unchanged from Part 1 and Part 2 — provider selection stays configuration-driven, and nothing in this part touches that layer.

## Step 2: A tool as data plus behavior

Python's `agents` SDK derives a tool's JSON Schema from type hints and a docstring via the `@function_tool` decorator. The raw `OpenAI.Chat` SDK has no such decorator: a tool is just a `ChatTool` (the schema the model sees) that has to be paired, by hand, with the C# code that runs when the model asks for it.

Create `Tools/ToolDefinition.cs`:

```csharp
using OpenAI.Chat;

namespace ToolUsingAgent.Tools;

public sealed record ToolDefinition
{
    public required ChatTool Definition { get; init; }

    public required Func<string, Task<string>> InvokeAsync { get; init; }

    public string Name => Definition.FunctionName;
}
```

- `Definition` is what gets added to `ChatCompletionOptions.Tools` and sent to the model.
- `InvokeAsync` takes the raw JSON arguments string the model returned and produces the plain-text result the model will read back. Unlike Part 2's structured output, a tool result is just text — the model is free to summarize or reformat it in its final answer.
- `Name` reads `Definition.FunctionName` so callers do not have to repeat the tool's name as a separate string when building a dispatch table.
- Both properties are `required init`, the same pattern used for `SupportTicket` in Part 2: a `ToolDefinition` cannot be constructed half-finished.

## Step 3: Implement the tools

Each tool needs three things: a typed record for its arguments, a hand-written JSON Schema describing those arguments (the same technique as `SupportTicketSchema` in Part 2), and a handler that deserializes the arguments and runs the real logic.

Create `Tools/CalculatorTools.cs`:

```csharp
using System.Text;
using System.Text.Json;
using OpenAI.Chat;

namespace ToolUsingAgent.Tools;

public sealed record AddNumbersArgs
{
    public required double A { get; init; }

    public required double B { get; init; }
}

public sealed record MultiplyNumbersArgs
{
    public required double A { get; init; }

    public required double B { get; init; }
}

public static class CalculatorTools
{
    private const string AddNumbersSchema =
        """
        {
          "type": "object",
          "properties": {
            "a": { "type": "number", "description": "The first number." },
            "b": { "type": "number", "description": "The second number." }
          },
          "required": ["a", "b"],
          "additionalProperties": false
        }
        """;

    private const string MultiplyNumbersSchema =
        """
        {
          "type": "object",
          "properties": {
            "a": { "type": "number", "description": "The first number." },
            "b": { "type": "number", "description": "The second number." }
          },
          "required": ["a", "b"],
          "additionalProperties": false
        }
        """;

    public static ToolDefinition AddNumbers { get; } = new()
    {
        Definition = ChatTool.CreateFunctionTool(
            functionName: "add_numbers",
            functionDescription: "Add two numbers together.",
            functionParameters: BinaryData.FromBytes(
                Encoding.UTF8.GetBytes(AddNumbersSchema))),
        InvokeAsync = InvokeAddNumbersAsync
    };

    public static ToolDefinition MultiplyNumbers { get; } = new()
    {
        Definition = ChatTool.CreateFunctionTool(
            functionName: "multiply_numbers",
            functionDescription: "Multiply two numbers together.",
            functionParameters: BinaryData.FromBytes(
                Encoding.UTF8.GetBytes(MultiplyNumbersSchema))),
        InvokeAsync = InvokeMultiplyNumbersAsync
    };

    private static Task<string> InvokeAddNumbersAsync(string argumentsJson)
    {
        AddNumbersArgs args = Deserialize<AddNumbersArgs>(argumentsJson);
        double result = args.A + args.B;

        return Task.FromResult(result.ToString());
    }

    private static Task<string> InvokeMultiplyNumbersAsync(string argumentsJson)
    {
        MultiplyNumbersArgs args = Deserialize<MultiplyNumbersArgs>(argumentsJson);
        double result = args.A * args.B;

        return Task.FromResult(result.ToString());
    }

    private static readonly JsonSerializerOptions ArgumentsOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static T Deserialize<T>(string argumentsJson)
    {
        return JsonSerializer.Deserialize<T>(argumentsJson, ArgumentsOptions)
            ?? throw new JsonException(
                $"The arguments for {typeof(T).Name} were empty.");
    }
}
```

The `PropertyNameCaseInsensitive = true` option matters here. The model sends camelCase JSON (`"a"`, `"fromUnit"`), while the args records use PascalCase C# properties (`A`, `FromUnit`). `System.Text.Json` is case-sensitive by default — without this option, `"a"` silently fails to bind to `A` and the property is left at its default value instead of throwing, which is a worse failure than an exception because it looks like success.

Create `Tools/WeatherTools.cs` the same way, with `get_weather` (a mock lookup) and `convert_temperature`:

```csharp
using System.Text;
using System.Text.Json;
using OpenAI.Chat;

namespace ToolUsingAgent.Tools;

public sealed record GetWeatherArgs
{
    public required string City { get; init; }
}

public sealed record ConvertTemperatureArgs
{
    public required double Temperature { get; init; }

    public required string FromUnit { get; init; }

    public required string ToUnit { get; init; }
}

public static class WeatherTools
{
    private const string GetWeatherSchema =
        """
        {
          "type": "object",
          "properties": {
            "city": { "type": "string", "description": "The city to get the weather for." }
          },
          "required": ["city"],
          "additionalProperties": false
        }
        """;

    private const string ConvertTemperatureSchema =
        """
        {
          "type": "object",
          "properties": {
            "temperature": { "type": "number", "description": "The temperature value to convert." },
            "fromUnit": {
              "type": "string",
              "description": "The unit of the input temperature.",
              "enum": ["celsius", "fahrenheit"]
            },
            "toUnit": {
              "type": "string",
              "description": "The unit to convert the temperature to.",
              "enum": ["celsius", "fahrenheit"]
            }
          },
          "required": ["temperature", "fromUnit", "toUnit"],
          "additionalProperties": false
        }
        """;

    public static ToolDefinition GetWeather { get; } = new()
    {
        Definition = ChatTool.CreateFunctionTool(
            functionName: "get_weather",
            functionDescription: "Get the current weather for a city (mock implementation).",
            functionParameters: BinaryData.FromBytes(
                Encoding.UTF8.GetBytes(GetWeatherSchema))),
        InvokeAsync = InvokeGetWeatherAsync
    };

    public static ToolDefinition ConvertTemperature { get; } = new()
    {
        Definition = ChatTool.CreateFunctionTool(
            functionName: "convert_temperature",
            functionDescription: "Convert a temperature between Celsius and Fahrenheit.",
            functionParameters: BinaryData.FromBytes(
                Encoding.UTF8.GetBytes(ConvertTemperatureSchema))),
        InvokeAsync = InvokeConvertTemperatureAsync
    };

    private static Task<string> InvokeGetWeatherAsync(string argumentsJson)
    {
        GetWeatherArgs args = Deserialize<GetWeatherArgs>(argumentsJson);

        return Task.FromResult(
            $"The weather in {args.City} is sunny with 72°F.");
    }

    private static Task<string> InvokeConvertTemperatureAsync(string argumentsJson)
    {
        ConvertTemperatureArgs args = Deserialize<ConvertTemperatureArgs>(argumentsJson);

        string fromUnit = args.FromUnit.Trim().ToLowerInvariant();
        string toUnit = args.ToUnit.Trim().ToLowerInvariant();

        if (fromUnit == "celsius" && toUnit == "fahrenheit")
        {
            double result = (args.Temperature * 9 / 5) + 32;
            return Task.FromResult($"{args.Temperature}°C = {result:F1}°F");
        }

        if (fromUnit == "fahrenheit" && toUnit == "celsius")
        {
            double result = (args.Temperature - 32) * 5 / 9;
            return Task.FromResult($"{args.Temperature}°F = {result:F1}°C");
        }

        return Task.FromResult("Unsupported temperature conversion.");
    }

    private static readonly JsonSerializerOptions ArgumentsOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static T Deserialize<T>(string argumentsJson)
    {
        return JsonSerializer.Deserialize<T>(argumentsJson, ArgumentsOptions)
            ?? throw new JsonException(
                $"The arguments for {typeof(T).Name} were empty.");
    }
}
```

Build the project to confirm both files compile before moving on:

```bash
dotnet build c-1-tool-calling/ToolUsingAgent/ToolUsingAgent.csproj
```

## Step 4: The tool-calling loop

This is the part the Chat Completions API leaves entirely to the caller. A single request either finishes with an answer or asks for one or more tool calls — and after those tool calls are answered, the *next* request might ask for more. The loop has to keep going until the model actually stops.

Create `Agents/ToolAgent.cs`:

```csharp
using OpenAI.Chat;
using ToolUsingAgent.Tools;

namespace ToolUsingAgent.Agents;

public sealed class ToolAgent
{
    private const int MaxToolCallRounds = 8;

    private readonly ChatClient _chatClient;
    private readonly ChatCompletionOptions _completionOptions;
    private readonly Dictionary<string, ToolDefinition> _toolsByName;
    private readonly List<ChatMessage> _history;

    public ToolAgent(
        string name,
        string instructions,
        ChatClient chatClient,
        IReadOnlyList<ToolDefinition> tools)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(tools);

        Name = name;
        Instructions = instructions;
        _chatClient = chatClient;

        _toolsByName = tools.ToDictionary(tool => tool.Name);

        _completionOptions = new ChatCompletionOptions();

        foreach (ToolDefinition tool in tools)
        {
            _completionOptions.Tools.Add(tool.Definition);
        }

        _history =
        [
            new SystemChatMessage(instructions)
        ];
    }

    public string Name { get; }

    public string Instructions { get; }

    public async Task<string> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        int checkpoint = _history.Count;
        _history.Add(new UserChatMessage(input));

        try
        {
            for (int round = 0; round < MaxToolCallRounds; round++)
            {
                ChatCompletion completion =
                    await _chatClient.CompleteChatAsync(
                        _history,
                        _completionOptions,
                        cancellationToken);

                if (completion.FinishReason == ChatFinishReason.ToolCalls)
                {
                    _history.Add(new AssistantChatMessage(completion));

                    foreach (ChatToolCall toolCall in completion.ToolCalls)
                    {
                        string result = await InvokeToolAsync(
                            toolCall,
                            cancellationToken);

                        _history.Add(new ToolChatMessage(toolCall.Id, result));
                    }

                    continue;
                }

                if (completion.FinishReason == ChatFinishReason.Stop)
                {
                    string response = string.Concat(
                        completion.Content.Select(part => part.Text));

                    _history.Add(new AssistantChatMessage(response));

                    return response;
                }

                throw new InvalidOperationException(
                    $"The model stopped for an unexpected reason: " +
                    $"{completion.FinishReason}.");
            }

            throw new InvalidOperationException(
                $"The agent exceeded {MaxToolCallRounds} tool-call rounds " +
                "without producing a final answer.");
        }
        catch
        {
            // Do not retain a turn whose request failed partway through.
            _history.RemoveRange(checkpoint, _history.Count - checkpoint);
            throw;
        }
    }

    private async Task<string> InvokeToolAsync(
        ChatToolCall toolCall,
        CancellationToken cancellationToken)
    {
        if (!_toolsByName.TryGetValue(toolCall.FunctionName, out ToolDefinition? tool))
        {
            return $"Error: no tool named '{toolCall.FunctionName}' is available.";
        }

        try
        {
            return await tool.InvokeAsync(toolCall.FunctionArguments.ToString());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return $"Error: the '{tool.Name}' tool failed: {exception.Message}";
        }
    }

    public void Reset()
    {
        _history.Clear();
        _history.Add(new SystemChatMessage(Instructions));
    }
}
```

Four decisions are worth calling out:

1. **The loop only exits through `ChatFinishReason.Stop`.** Every other reason either means "call more tools" (`ToolCalls`, handled and looped) or "something went wrong" (`Length`, `ContentFilter`, and so on, which throw immediately — retrying the same request would not fix a content-filter stop).
2. **A capped round count (`MaxToolCallRounds`).** A model that keeps requesting tool calls without ever answering would otherwise loop forever. Eight rounds is generous for a learning example; production code would likely make this configurable.
3. **A failing tool never throws out of the loop.** `InvokeToolAsync` catches both an unknown tool name and any exception the handler raises, and turns each into an `Error: ...` string that becomes the tool's result. The model sees the failure in the conversation and can decide how to respond — retry with different arguments, apologize, or try a different approach — instead of the whole turn crashing on one bad call.
4. **Failed-turn rollback.** If the loop ultimately throws (an unrecoverable finish reason, or exceeding the round cap), everything appended since the turn started — the user message and any partial assistant/tool messages — is removed. This mirrors the rollback in Part 1's `Agent.RunAsync`: a failed turn should not leave the conversation history in a half-finished state for the next call.

## Step 5: Wire instructions and tools together

Create `Agents/ToolUsingAgent.cs`:

```csharp
using OpenAI.Chat;
using ToolUsingAgent.Tools;

namespace ToolUsingAgent.Agents;

public static class ToolUsingAgentFactory
{
    public static ToolAgent Create(ChatClient chatClient)
    {
        return new ToolAgent(
            name: "Tool Using Agent",
            instructions:
            """
            You are a helpful assistant with access to the following tools:
            - add_numbers: Add two numbers together.
            - multiply_numbers: Multiply two numbers together.
            - get_weather: Get the current weather for a city.
            - convert_temperature: Convert a temperature between Celsius and Fahrenheit.

            Follow these guidelines:
            - Use the appropriate tool for calculations or weather questions instead of
              computing or guessing the answer yourself.
            - If a request needs more than one tool, call them in sequence.
            - Explain the result clearly once every needed tool call has returned.
            """,
            chatClient: chatClient,
            tools:
            [
                CalculatorTools.AddNumbers,
                CalculatorTools.MultiplyNumbers,
                WeatherTools.GetWeather,
                WeatherTools.ConvertTemperature
            ]);
    }
}
```

The factory class is named `ToolUsingAgentFactory` rather than `ToolUsingAgent`. The project's own root namespace is already `ToolUsingAgent`, and a class sharing that exact name would collide with the namespace segment wherever other code writes a fully-qualified reference like `ToolUsingAgent.Agents.ToolAgent`.

## Step 6: Connect it to the console

Replace `Program.cs` with the same REPL shape used in Part 1 and Part 2, calling into `ToolAgent` instead:

```csharp
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using ToolUsingAgent.Agents;
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
ToolAgent agent = ToolUsingAgentFactory.Create(chatClient);

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
```

Build the whole solution to confirm the project reference and package set are correct:

```bash
dotnet build c-1-tool-calling/ToolUsingAgent/ToolUsingAgent.csproj
```

Expected output ends with:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Step 7: Verify multi-tool turns and failure handling

Run the project:

```bash
dotnet run --project c-1-tool-calling/ToolUsingAgent
```

Try a prompt that needs two different tools in one turn:

```text
What's 12 times 7, and what's the weather in Berlin?
```

The model should request `multiply_numbers` and `get_weather` — either in the same round or across two rounds of the loop — and the final answer should report both results correctly (84, and the mock Berlin weather).

Then try a prompt that chains a calculation into a conversion:

```text
Convert 12 plus 20 degrees Celsius to Fahrenheit.
```

This requires the model to call `add_numbers` first, read the result, and then call `convert_temperature` with that result as input — a genuine two-round trip through the loop, not just two calls in parallel.

To see the failure path without touching real code, ask something a tool cannot do, for example:

```text
What's the weather on the moon?
```

`get_weather` has no special handling for this — it will return its mock string regardless of city — which is a good moment to notice that a mock tool cannot demonstrate real-world failure. To actually exercise `InvokeToolAsync`'s error handling, temporarily throw inside `InvokeGetWeatherAsync` (for example, `throw new InvalidOperationException("simulated failure");`) and rerun the same prompt. The console should still print a coherent final answer — built from the model reacting to an `Error: ...` tool result — instead of crashing. Remove the temporary throw afterward.

## What does not carry over from the Python SDK

The Python `agents` SDK groups tool calling into three ideas: custom function tools, OpenAI-hosted built-in tools (`WebSearchTool`, `CodeInterpreterTool`), and agents exposed as tools to an orchestrator. Only the first maps cleanly onto the raw Chat Completions API used here:

- **Built-in tools are out of scope for this part.** `WebSearchTool` and `CodeInterpreterTool` are OpenAI-hosted tools that belong to the newer *Responses API*, not Chat Completions. There is no `ChatTool` equivalent that invokes them — they are a different request shape entirely. Reaching them from `.NET` would mean switching API surfaces, which is a separate lesson from tool calling itself.
- **Agents-as-tools does map, and reuses everything built here.** An agent used as a tool is just a `ToolDefinition` whose `InvokeAsync` happens to run another `ChatClient` conversation instead of a calculator or lookup, and returns that conversation's final text. No new abstraction is needed — `ToolAgent`'s loop, `ChatCompletionOptions.Tools`, and the `ToolDefinition` record all stay exactly as they are.

## Next implementation step

Add an agents-as-tools example: a small orchestrator whose tool list includes a `ToolDefinition` wrapping a call into a second, specialized agent (for example, a translator), demonstrating that the tool abstraction built in this part composes without modification.

## Reference

- [Official OpenAI .NET SDK tool-calling example](https://github.com/openai/openai-dotnet#how-to-use-chat-completions-with-tools)
- [OpenAI function calling guide](https://developers.openai.com/api/docs/guides/function-calling)
- [OpenRouter tool-calling guide](https://openrouter.ai/docs/guides/features/tool-calling)
