using System;
using System.Collections.Generic;
using System.Text;

namespace ProductReviewAgent.Agents
{
    public abstract class ProductReviewAgentException : Exception
    {
        protected ProductReviewAgentException(string message)
            : base(message)
        {
        }

        protected ProductReviewAgentException(
            string message,
            Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class ModelRefusalException
        : ProductReviewAgentException
    {
        public ModelRefusalException(string refusal)
            : base("The model refused to create a product review.")
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(refusal);
            Refusal = refusal;
        }

        public string Refusal { get; }
    }

    public sealed class InvalidModelResponseException
        : ProductReviewAgentException
    {
        public InvalidModelResponseException(string message)
            : base(message)
        {
        }

        public InvalidModelResponseException(
            string message,
            Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
