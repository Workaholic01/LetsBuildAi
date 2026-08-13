using System;
using System.Collections.Generic;
using System.Text;

namespace StructuredOutputAgent.Models
{
    public class ProductReview
    {
        public string ProductName { get; set; } = string.Empty;
        public int StarRating { get; set; }
        public string Sentiment { get; set; } = string.Empty;
        public List<string> PositivePoints { get; set; } = new List<string>();
        public List<string> NegativePoints { get; set; } = new List<string>();
        public bool WouldRecommend { get; set; }
        public string Summary { get; set; } = string.Empty;
    }

    public enum SentimentClassification
    {
        VeryPositive,
        Positive,
        Neutral,
        Negative,
        VeryNegative
    }
}
