using System.Text.Json;

namespace ProductReviewAgent.Schemas;

public static class ProductReviewSchema
{
    public const string Name = "product_review";

    public const string Json =
        """
        {
          "type": "object",
          "properties": {
            "productName": {
              "type": "string",
              "description": "The name of the product being reviewed, if mentioned."
            },
            "starRating": {
              "type": "integer",
              "description": "A star rating from 1 to 5 based on the review's tone.",
              "minimum": 1,
              "maximum": 5
            },
            "sentiment": {
              "type": "string",
              "description": "The overall sentiment expressed in the review.",
              "enum": [
                "veryPositive",
                "positive",
                "neutral",
                "negative",
                "veryNegative"
              ]
            },
            "positivePoints": {
              "type": "array",
              "description": "The main positive points mentioned in the review.",
              "items": {
                "type": "string"
              }
            },
            "negativePoints": {
              "type": "array",
              "description": "The main negative points mentioned in the review.",
              "items": {
                "type": "string"
              }
            },
            "wouldRecommend": {
              "type": "boolean",
              "description": "Whether the reviewer would recommend the product, stated or implied."
            },
            "summary": {
              "type": "string",
              "description": "A brief summary of the review."
            }
          },
          "required": [
            "productName",
            "starRating",
            "sentiment",
            "positivePoints",
            "negativePoints",
            "wouldRecommend",
            "summary"
          ],
          "additionalProperties": false
        }
        """;

    public static JsonDocument Parse()
    {
        return JsonDocument.Parse(Json);
    }
}
