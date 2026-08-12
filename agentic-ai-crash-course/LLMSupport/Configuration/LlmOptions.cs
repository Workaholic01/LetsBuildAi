namespace LLMSupport.Configuration;

public sealed class LlmOptions
{
    public required string Provider { get; init; }

    public required Dictionary<string, LlmProviderOptions> Providers { get; init; }

    public LlmProviderOptions GetSelectedProvider()
    {
        var match = Providers.FirstOrDefault(pair =>
            string.Equals(pair.Key, Provider, StringComparison.OrdinalIgnoreCase));

        return match.Value
            ?? throw new InvalidOperationException(
                $"LLM provider '{Provider}' is not configured.");
    }
}

public sealed class LlmProviderOptions
{
    public required string Endpoint { get; init; }

    public required string Model { get; init; }

    public string? ApiKey { get; init; }
}