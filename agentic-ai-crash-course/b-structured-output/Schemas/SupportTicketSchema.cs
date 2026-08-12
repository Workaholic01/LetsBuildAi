using System.Text.Json;

namespace StructuredOutputAgent.Schemas;

public static class SupportTicketSchema
{
    public const string Name = "support_ticket";

    public const string Json =
        """
        {
          "type": "object",
          "properties": {
            "title": {
              "type": "string",
              "description": "A concise title describing the customer issue."
            },
            "category": {
              "type": "string",
              "description": "The application category for the issue.",
              "enum": [
                "account",
                "billing",
                "technical",
                "featureRequest",
                "other"
              ]
            },
            "priority": {
              "type": "string",
              "description": "The urgency of the support request.",
              "enum": [
                "low",
                "medium",
                "high",
                "critical"
              ]
            },
            "summary": {
              "type": "string",
              "description": "A clear customer-facing summary of the issue."
            },
            "suggestedActions": {
              "type": "array",
              "description": "Practical next actions for resolving the issue.",
              "items": {
                "type": "string"
              }
            }
          },
          "required": [
            "title",
            "category",
            "priority",
            "summary",
            "suggestedActions"
          ],
          "additionalProperties": false
        }
        """;

    public static JsonDocument Parse()
    {
        return JsonDocument.Parse(Json);
    }
}