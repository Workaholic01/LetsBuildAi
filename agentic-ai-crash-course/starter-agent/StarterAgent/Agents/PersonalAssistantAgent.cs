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