using StructuredOutputAgent.Models;
using StructuredOutputAgent.Schemas;
using StructuredOutputAgent.Serialization;
using OpenAI.Chat;
using System.Text;
using System.Text.Json;

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