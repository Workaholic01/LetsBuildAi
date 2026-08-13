using System.Collections.Generic;

namespace ProductReviewAgent.Models;

public sealed record ProductReview
{
    public required string ProductName { get; init; }

    public required int StarRating { get; init; }

    public required SentimentClassification Sentiment { get; init; }

    public required List<string> PositivePoints { get; init; }

    public required List<string> NegativePoints { get; init; }

    public required bool WouldRecommend { get; init; }

    public required string Summary { get; init; }
}

public enum SentimentClassification
{
    VeryPositive,
    Positive,
    Neutral,
    Negative,
    VeryNegative
}
