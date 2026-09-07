using OpenAI.Chat;
using AgentsAsToolsAgent.Tools;

namespace AgentsAsToolsAgent.Agents;

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
                AgentTool.Create(
                    "translate_to_spanish",
                    "Translate text to Spanish.",
                    spanishAgent),
                AgentTool.Create(
                    "translate_to_french",
                    "Translate text to French.",
                    frenchAgent),
                AgentTool.Create(
                    "translate_to_german",
                    "Translate text to German.",
                    germanAgent)
            ]);
    }
}
