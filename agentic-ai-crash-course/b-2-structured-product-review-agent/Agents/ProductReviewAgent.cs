using OpenAI.Chat;
using ProductReviewAgent.Models;
using ProductReviewAgent.Schemas;
using ProductReviewAgent.Serialization;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace ProductReviewAgent.Agents
{
    public class ProductReviewAgent
    {
        private const string Instructions =
       """
        You are a product review analysis expert that extracts structured data 
           from customer product reviews.

           Analyze the review text and extract:
           - Product name if mentioned
           - Star rating (1-5) based on review tone
           - Sentiment classification (very_positive to very_negative)
           - Main positive and negative points
           - Whether they would recommend (if stated or implied)
           - Brief summary

           RATING GUIDELINES:
           - 5 stars: Excellent, highly satisfied, "amazing", "perfect"
           - 4 stars: Good, satisfied, minor issues
           - 3 stars: Okay, mixed feelings, "decent"
           - 2 stars: Poor, unsatisfied, significant issues
           - 1 star: Terrible, very unsatisfied, "worst"

           IMPORTANT: Response must be valid JSON matching the ProductReview schema.
        """;

        private readonly ChatClient _chatClient;
        private readonly ChatCompletionOptions _completionOptions;

        public ProductReviewAgent(ChatClient chatClient)
        {
            ArgumentNullException.ThrowIfNull(chatClient);

            _chatClient = chatClient;
            _completionOptions = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: ProductReviewSchema.Name,
                    jsonSchema: BinaryData.FromBytes(
                        Encoding.UTF8.GetBytes(ProductReviewSchema.Json)),
                    jsonSchemaIsStrict: true)
            };
        }


        public async Task<ProductReview> AnalyzeAsync(
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

            if (!string.IsNullOrWhiteSpace(completion.Refusal))
            {
                throw new ModelRefusalException(completion.Refusal);
            }

            if (completion.FinishReason != ChatFinishReason.Stop)
            {
                string message = completion.FinishReason switch
                {
                    ChatFinishReason.Length =>
                        "The model response ended before the JSON was complete.",

                    ChatFinishReason.ContentFilter =>
                        "The model response was stopped by a content filter.",

                    ChatFinishReason.ToolCalls =>
                        "The model returned unexpected tool calls.",

                    ChatFinishReason.FunctionCall =>
                        "The model returned an obsolete function call.",

                    _ =>
                        $"The model stopped for an unexpected reason: " +
                        $"{completion.FinishReason}."
                };

                throw new InvalidModelResponseException(message);
            }

            string json = string.Concat(
                completion.Content
                    .Where(part =>
                        part.Kind == ChatMessageContentPartKind.Text)
                    .Select(part => part.Text));

            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidModelResponseException(
                    "The provider returned no structured content.");
            }

            try
            {
                return JsonSerializer.Deserialize<ProductReview>(
                    json,
                    JsonDefaults.Options)
                    ?? throw new JsonException(
                        "The structured response was empty.");
            }
            catch (JsonException exception)
            {
                throw new InvalidModelResponseException(
                    "The response did not match the ProductReview contract.",
                    exception);
            }
        }
    }
}
