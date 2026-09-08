using System.Text;
using System.Text.Json;
using OpenAI.Chat;
using AgentsAsToolsAgent.Agents;

namespace AgentsAsToolsAgent.Tools;

public sealed record InvokeAgentArgs
{
    public required string Text { get; init; }
}

public static class AgentTool
{
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
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolDescription);
        ArgumentNullException.ThrowIfNull(agent);

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
        Console.WriteLine($"Invoking agent: {agent.Name}");
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
