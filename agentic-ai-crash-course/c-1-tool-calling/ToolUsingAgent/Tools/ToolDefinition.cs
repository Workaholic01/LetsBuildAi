using OpenAI.Chat;

namespace ToolUsingAgent.Tools;

public sealed record ToolDefinition
{
    public required ChatTool Definition { get; init; }

    public required Func<string, Task<string>> InvokeAsync { get; init; }

    public string Name => Definition.FunctionName;
}
