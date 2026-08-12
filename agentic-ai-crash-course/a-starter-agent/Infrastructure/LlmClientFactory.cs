using System.ClientModel;
using OpenAI;
using OpenAI.Chat;
using StarterAgent.Configuration;

namespace StarterAgent.Infrastructure;

public static class LlmClientFactory
{
    public static ChatClient Create(LlmOptions options)
    {
        LlmProviderOptions provider = options.GetSelectedProvider();

        if (string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            throw new InvalidOperationException(
                $"No API key is configured for '{options.Provider}'.");
        }

        if (!Uri.TryCreate(provider.Endpoint, UriKind.Absolute, out Uri? endpoint))
        {
            throw new InvalidOperationException(
                $"The endpoint '{provider.Endpoint}' is invalid.");
        }

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = endpoint
        };

        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(provider.ApiKey),
            clientOptions);

        return openAiClient.GetChatClient(provider.Model);
    }
}