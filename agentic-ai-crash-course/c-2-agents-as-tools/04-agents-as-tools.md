# Part 4: Agents as Tools in .NET

**Status:** Complete
**Builds on:** [Part 3 — Tool calling](../c-1-tool-calling/03-tool-calling.md)

## The problem

Part 3 built a single agent that calls plain C# functions as tools — a calculator, a mock weather lookup, a unit converter. Those tools are stateless utilities: given arguments, they compute a result and return.

Some capabilities are not simple functions, though. Translating text well, for a specific target language, benefits from a model call with its own focused system prompt rather than a hand-written function. Rather than inventing a new abstraction for "an agent that another agent can call," this part shows that none is needed: an agent can be wrapped as a `ToolDefinition` exactly like a calculator function, because from the calling agent's perspective a tool is just "a name, a JSON argument schema, and an async function that returns a string." What runs inside that function — arithmetic or a whole model conversation — is invisible to the caller.

## What this project builds

An `Agents As Tools Agent` (`Translation Orchestrator`) that does not translate anything itself. Instead, it delegates to three specialized sub-agents, each exposed to the orchestrator as a tool:

- `translate_to_spanish` — delegates to a Spanish `Translation Agent`;
- `translate_to_french` — delegates to a French `Translation Agent`;
- `translate_to_german` — delegates to a German `Translation Agent`.

If a request needs more than one language, the orchestrator calls the relevant tools once each and presents every translation labeled with its language, using the exact same tool-calling loop from Part 3.

## Learning objectives

- Recognize that a `ToolDefinition`'s `InvokeAsync` can run an entire sub-agent conversation instead of a plain function.
- Wrap an agent as a tool without changing the tool-calling loop or the `ToolDefinition` contract from Part 3.
- Give a sub-agent a narrow, single-purpose system prompt instead of reusing the orchestrator's instructions.
- Reset a sub-agent between calls so each delegation is an independent request, not a continuation of a previous one.
- Let an orchestrator fan out to multiple specialized agents in the same turn.

## Project structure

```text
c-2-agents-as-tools/
├── Agents/
│   ├── ToolAgent.cs
│   ├── TranslationAgent.cs
│   └── TranslationOrchestratorAgent.cs
├── Tools/
│   ├── AgentTool.cs
│   └── ToolDefinition.cs
├── appsettings.json
├── Program.cs
└── AgentsAsToolsAgent.csproj
```

## Key files and classes

### [`Tools/ToolDefinition.cs`](Tools/ToolDefinition.cs:1)

Unchanged from Part 3: a `ChatTool` (the schema the model sees) paired with an `InvokeAsync` function that turns raw JSON arguments into a plain-text result.

```csharp
public sealed record ToolDefinition
{
    public required ChatTool Definition { get; init; }

    public required Func<string, Task<string>> InvokeAsync { get; init; }

    public string Name => Definition.FunctionName;
}
```

### [`Agents/ToolAgent.cs`](Agents/ToolAgent.cs:1)

The same tool-calling loop introduced in Part 3, byte-for-byte: it sends the conversation history plus the configured tools, loops on `ChatFinishReason.ToolCalls` by invoking each requested tool and appending a `ToolChatMessage` with the result, returns the final text on `ChatFinishReason.Stop`, caps the loop at `MaxToolCallRounds` (8), catches a failing tool's exception and turns it into an `Error: ...` string instead of crashing the turn, and rolls back the history if the turn ultimately fails. `ToolAgent` has no idea whether a given tool is a calculator or another agent — that distinction lives entirely in how the tool's `ToolDefinition` is constructed.

### [`Agents/TranslationAgent.cs`](Agents/TranslationAgent.cs:1)

A factory that builds one narrowly scoped `ToolAgent` per target language, with no tools of its own:

```csharp
public static class TranslationAgentFactory
{
    public static ToolAgent Create(ChatClient chatClient, string language)
    {
        return new ToolAgent(
            name: $"{language} Translation Agent",
            instructions:
            $"""
            You translate the user's message to {language}.
            Respond with only the translation, with no extra commentary.
            """,
            chatClient: chatClient,
            tools: []);
    }
}
```

Each translation agent is a plain `ToolAgent` — the same class the orchestrator itself is built from — configured with an empty tool list and a single-purpose instruction. Nothing about `ToolAgent` needed to change to support being used this way.

### [`Tools/AgentTool.cs`](Tools/AgentTool.cs:1)

The piece that makes agents-as-tools work: a factory that wraps a `ToolAgent` in a `ToolDefinition`. The tool's JSON Schema takes a single `text` argument:

```csharp
private const string InvokeAgentSchema =
    """
    {
      "type": "object",
      "properties": {
        "text": {
          "type": "string",
          "description": "The text to pass to the specialized agent."
        }
      },
      "required": ["text"],
      "additionalProperties": false
    }
    """;

public static ToolDefinition Create(
    string toolName,
    string toolDescription,
    ToolAgent agent)
{
    return new ToolDefinition
    {
        Definition = ChatTool.CreateFunctionTool(
            functionName: toolName,
            functionDescription: toolDescription,
            functionParameters: BinaryData.FromBytes(
                Encoding.UTF8.GetBytes(InvokeAgentSchema))),
        InvokeAsync = argumentsJson => InvokeAsync(argumentsJson, agent)
    };
}

private static async Task<string> InvokeAsync(
    string argumentsJson,
    ToolAgent agent)
{
    InvokeAgentArgs args = Deserialize<InvokeAgentArgs>(argumentsJson);

    try
    {
        return await agent.RunAsync(args.Text);
    }
    finally
    {
        // Each call is an independent request to the sub-agent, not a
        // continuation of a previous one.
        agent.Reset();
    }
}
```

`InvokeAsync` deserializes the `text` argument, calls `agent.RunAsync(args.Text)` on the wrapped sub-agent, and returns that sub-agent's final answer as the tool's plain-text result — exactly the shape `ToolAgent`'s loop expects from any tool. The `finally` block calls `agent.Reset()` after every invocation: without it, the sub-agent's in-memory history would keep growing across unrelated orchestrator turns, and later delegations would (incorrectly) receive earlier translation requests as prior conversation context.

### [`Agents/TranslationOrchestratorAgent.cs`](Agents/TranslationOrchestratorAgent.cs:1)

The factory that assembles the whole example. It creates three `TranslationAgent` instances, wraps each with `AgentTool.Create`, and hands the resulting `ToolDefinition`s to a `ToolAgent` the same way Part 3 handed it calculator and weather tools:

```csharp
public static class TranslationOrchestratorAgentFactory
{
    public static ToolAgent Create(ChatClient chatClient)
    {
        ToolAgent spanishAgent = TranslationAgentFactory.Create(chatClient, "Spanish");
        ToolAgent frenchAgent = TranslationAgentFactory.Create(chatClient, "French");
        ToolAgent germanAgent = TranslationAgentFactory.Create(chatClient, "German");

        return new ToolAgent(
            name: "Translation Orchestrator",
            instructions:
            """
            You are a translation orchestrator. You do not translate text yourself;
            you delegate to specialized translation agents through tools.

            Available tools:
            - translate_to_spanish: Translate text to Spanish.
            - translate_to_french: Translate text to French.
            - translate_to_german: Translate text to German.

            If a request needs more than one language, call the relevant tools once each.
            Present every translation clearly labeled with its language.
            """,
            chatClient: chatClient,
            tools:
            [
                AgentTool.Create("translate_to_spanish", "Translate text to Spanish.", spanishAgent),
                AgentTool.Create("translate_to_french", "Translate text to French.", frenchAgent),
                AgentTool.Create("translate_to_german", "Translate text to German.", germanAgent)
            ]);
    }
}
```

All four `ToolAgent` instances — the orchestrator and its three sub-agents — share the same `ChatClient`, so they all go through the same configured provider, endpoint, and model. Only the instructions and tool list differ between them.

## How the delegation flows

1. The console sends user input to the orchestrator's `RunAsync`.
2. The model decides the request needs translation and returns `ChatFinishReason.ToolCalls` requesting, for example, `translate_to_spanish` and `translate_to_french` in the same round.
3. `ToolAgent.InvokeToolAsync` looks up each tool by name and calls its `InvokeAsync`, which is `AgentTool`'s wrapper for that language's sub-agent.
4. The wrapper deserializes the tool call's `text` argument, runs it through the sub-agent's own single-turn conversation (its own system prompt, its own `ChatClient` call), and returns the sub-agent's plain-text translation as the tool result — then resets the sub-agent.
5. The orchestrator appends each result as a `ToolChatMessage` and loops again. Once every requested tool has answered, the model produces the final, labeled response and the loop exits on `ChatFinishReason.Stop`.

From the orchestrator's point of view, this is indistinguishable from Part 3's calculator and weather tools: request tool, get a string back, keep going. The only difference is what happens inside `InvokeAsync`.

## Run the project

```bash
dotnet run --project c-2-agents-as-tools
```

Try a request that needs a single language:

```text
Translate "Good morning, how are you?" to Spanish.
```

Then try one that needs more than one tool call in the same turn:

```text
Translate "Where is the train station?" into French and German.
```

The orchestrator should call `translate_to_french` and `translate_to_german`, and the final answer should present both translations, each clearly labeled with its language.

## Provider configuration

[`appsettings.json`](appsettings.json:1) uses the same `Llm` configuration shape as every other part in the series:

```json
{
  "Llm": {
    "Provider": "OpenRouter",
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

This project defaults `Llm:Provider` to `OpenRouter` rather than `OpenAI`. Switching providers still only requires changing this value (or the `Llm__Provider` environment variable) — nothing about `ToolAgent`, `TranslationAgentFactory`, or `AgentTool` is provider-specific. As with earlier parts, store the API key for whichever provider you use with `dotnet user-secrets set`, scoped to this project.

## What this project demonstrates

- The `ToolDefinition` abstraction from Part 3 needed no changes to support agents-as-tools — it was already general enough.
- An agent exposed as a tool is just a function whose implementation happens to run another model conversation.
- Sub-agents should be reset after each invocation so unrelated delegations do not leak conversation history into each other.
- An orchestrator can fan out to several specialized agents within a single turn, using the same tool-calling loop that drives plain function tools.
- Composition, not a new framework concept, is what turns single-purpose agents into a multi-agent system.

## Reference

- [Official OpenAI .NET SDK tool-calling example](https://github.com/openai/openai-dotnet#how-to-use-chat-completions-with-tools)
- [OpenAI function calling guide](https://developers.openai.com/api/docs/guides/function-calling)
- [OpenRouter tool-calling guide](https://openrouter.ai/docs/guides/features/tool-calling)
