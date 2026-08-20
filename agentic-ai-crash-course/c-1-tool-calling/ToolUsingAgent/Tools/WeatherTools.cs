using System.Text;
using System.Text.Json;
using OpenAI.Chat;

namespace ToolUsingAgent.Tools;

public sealed record GetWeatherArgs
{
    public required string City { get; init; }
}

public sealed record ConvertTemperatureArgs
{
    public required double Temperature { get; init; }

    public required string FromUnit { get; init; }

    public required string ToUnit { get; init; }
}

public static class WeatherTools
{
    private const string GetWeatherSchema =
        """
        {
          "type": "object",
          "properties": {
            "city": {
              "type": "string",
              "description": "The city to get the weather for."
            }
          },
          "required": ["city"],
          "additionalProperties": false
        }
        """;

    private const string ConvertTemperatureSchema =
        """
        {
          "type": "object",
          "properties": {
            "temperature": {
              "type": "number",
              "description": "The temperature value to convert."
            },
            "fromUnit": {
              "type": "string",
              "description": "The unit of the input temperature.",
              "enum": ["celsius", "fahrenheit"]
            },
            "toUnit": {
              "type": "string",
              "description": "The unit to convert the temperature to.",
              "enum": ["celsius", "fahrenheit"]
            }
          },
          "required": ["temperature", "fromUnit", "toUnit"],
          "additionalProperties": false
        }
        """;

    public static ToolDefinition GetWeather { get; } = new()
    {
        Definition = ChatTool.CreateFunctionTool(
            functionName: "get_weather",
            functionDescription: "Get the current weather for a city (mock implementation).",
            functionParameters: BinaryData.FromBytes(
                Encoding.UTF8.GetBytes(GetWeatherSchema))),
        InvokeAsync = InvokeGetWeatherAsync
    };

    public static ToolDefinition ConvertTemperature { get; } = new()
    {
        Definition = ChatTool.CreateFunctionTool(
            functionName: "convert_temperature",
            functionDescription: "Convert a temperature between Celsius and Fahrenheit.",
            functionParameters: BinaryData.FromBytes(
                Encoding.UTF8.GetBytes(ConvertTemperatureSchema))),
        InvokeAsync = InvokeConvertTemperatureAsync
    };

    private static Task<string> InvokeGetWeatherAsync(string argumentsJson)
    {
        GetWeatherArgs args = Deserialize<GetWeatherArgs>(argumentsJson);

        return Task.FromResult(
            $"The weather in {args.City} is sunny with 72°F.");
    }

    private static Task<string> InvokeConvertTemperatureAsync(string argumentsJson)
    {
        ConvertTemperatureArgs args = Deserialize<ConvertTemperatureArgs>(argumentsJson);

        string fromUnit = args.FromUnit.Trim().ToLowerInvariant();
        string toUnit = args.ToUnit.Trim().ToLowerInvariant();

        if (fromUnit == "celsius" && toUnit == "fahrenheit")
        {
            double result = (args.Temperature * 9 / 5) + 32;
            return Task.FromResult(
                $"{args.Temperature}°C = {result:F1}°F");
        }

        if (fromUnit == "fahrenheit" && toUnit == "celsius")
        {
            double result = (args.Temperature - 32) * 5 / 9;
            return Task.FromResult(
                $"{args.Temperature}°F = {result:F1}°C");
        }

        return Task.FromResult("Unsupported temperature conversion.");
    }

    private static readonly JsonSerializerOptions ArgumentsOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static T Deserialize<T>(string argumentsJson)
    {
        return JsonSerializer.Deserialize<T>(argumentsJson, ArgumentsOptions)
            ?? throw new JsonException(
                $"The arguments for {typeof(T).Name} were empty.");
    }
}
