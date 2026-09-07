using OpenAI.Chat;

namespace AgentsAsToolsAgent.Agents;

public static class TranslationAgentFactory
{
    public static ToolAgent Create(ChatClient chatClient, string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

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
