using OpenAI.Chat;
using AgentsAsToolsAgent.Tools;

namespace AgentsAsToolsAgent.Agents;

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
