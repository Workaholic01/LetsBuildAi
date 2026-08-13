using System;
using System.Collections.Generic;
using System.Text;

namespace StructuredOutputAgent.Agents
{
    public abstract class SupportTicketAgentException : Exception
    {
        protected SupportTicketAgentException(string message)
            : base(message)
        {
        }

        protected SupportTicketAgentException(
            string message,
            Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class ModelRefusalException
        : SupportTicketAgentException
    {
        public ModelRefusalException(string refusal)
            : base("The model refused to create a support ticket.")
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(refusal);
            Refusal = refusal;
        }

        public string Refusal { get; }
    }

    public sealed class InvalidModelResponseException
        : SupportTicketAgentException
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
