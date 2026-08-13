using OpenAI.Chat;
using StructuredOutputAgent.Schemas;
using System;
using System.Collections.Generic;
using System.Text;

namespace StructuredOutputAgent.Agents
{
    internal class ProductReviewAgent
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
    }
}
