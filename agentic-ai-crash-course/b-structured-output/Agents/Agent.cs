using OpenAI.Chat;

namespace AgenticAiCrashCourse.Agents;

public sealed class Agent
{
    private readonly ChatClient _chatClient;
    private readonly List<ChatMessage> _history;

    public Agent(
        string name,
        string instructions,
        ChatClient chatClient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);
        ArgumentNullException.ThrowIfNull(chatClient);

        Name = name;
        Instructions = instructions;
        _chatClient = chatClient;

        _history =
        [
            new SystemChatMessage(instructions)
        ];
    }

    public string Name { get; }

    public string Instructions { get; }

    // Excludes the system instructions.
    public int MessageCount => _history.Count - 1;

    public async Task<string> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        _history.Add(new UserChatMessage(input));

        try
        {
            ChatCompletion completion =
                await _chatClient.CompleteChatAsync(
                    _history,
                    cancellationToken: cancellationToken);

            string response = string.Concat(
                completion.Content.Select(part => part.Text));

            _history.Add(new AssistantChatMessage(response));

            return response;
        }
        catch
        {
            // Do not retain a user message whose request failed.
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