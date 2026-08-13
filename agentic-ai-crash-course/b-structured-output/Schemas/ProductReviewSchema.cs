using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace StructuredOutputAgent.Schemas
{
    internal class ProductReviewSchema
    {
        public const string Name = "ProductReview";
        public const string Json = """
        {
            "type": "object",
            "properties": {
                "ProductName": { "type": "string" },
                "StarRating": { "type": "integer", "minimum": 1, "maximum": 5 },
                "Sentiment": { "type": "string", "enum": ["VeryPositive", "Positive", "Neutral", "Negative", "VeryNegative"] },
                "PositivePoints": { "type": "array", "items": { "type": "string" } },
                "NegativePoints": { "type": "array", "items": { "type": "string" } },
                "WouldRecommend": { "type": "boolean" },
                "Summary": { "type": "string" }
            },
            "required": ["ProductName", "StarRating", "Sentiment", "PositivePoints", "NegativePoints", "WouldRecommend", "Summary"]
        }
        """;

        public static JsonDocument Parse()
        {
            return JsonDocument.Parse(Json);
        }

    }


}
