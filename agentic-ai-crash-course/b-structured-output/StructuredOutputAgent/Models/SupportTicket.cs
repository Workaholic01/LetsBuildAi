namespace AgenticAiCrashCourse.StructuredOutputAgent.Models;

public sealed record SupportTicket
{
    public required string Title { get; init; }

    public required TicketCategory Category { get; init; }

    public required TicketPriority Priority { get; init; }

    public required string Summary { get; init; }

    public required List<string> SuggestedActions { get; init; }
}

public enum TicketCategory
{
    Account,
    Billing,
    Technical,
    FeatureRequest,
    Other
}

public enum TicketPriority
{
    Low,
    Medium,
    High,
    Critical
}