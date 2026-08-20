using System.Text;
using System.Text.Json;
using OpenAI.Chat;

namespace ToolUsingAgent.Tools;

public sealed record AddNumbersArgs
{
    public required double A { get; init; }

    public required double B { get; init; }
}

public sealed record MultiplyNumbersArgs
{
    public required double A { get; init; }

    public required double B { get; init; }
}

public static class CalculatorTools
{
    private const string AddNumbersSchema =
        """
        {
          "type": "object",
          "properties": {
            "a": {
              "type": "number",
              "description": "The first number."
            },
            "b": {
              "type": "number",
              "description": "The second number."
            }
          },
          "required": ["a", "b"],
          "additionalProperties": false
        }
        """;

    private const string MultiplyNumbersSchema =
        """
        {
          "type": "object",
          "properties": {
            "a": {
              "type": "number",
              "description": "The first number."
            },
            "b": {
              "type": "number",
              "description": "The second number."
            }
          },
          "required": ["a", "b"],
          "additionalProperties": false
        }
        """;

    public static ToolDefinition AddNumbers { get; } = new()
    {
        Definition = ChatTool.CreateFunctionTool(
            functionName: "add_numbers",
            functionDescription: "Add two numbers together.",
            functionParameters: BinaryData.FromBytes(
                Encoding.UTF8.GetBytes(AddNumbersSchema))),
        InvokeAsync = InvokeAddNumbersAsync
    };

    public static ToolDefinition MultiplyNumbers { get; } = new()
    {
        Definition = ChatTool.CreateFunctionTool(
            functionName: "multiply_numbers",
            functionDescription: "Multiply two numbers together.",
            functionParameters: BinaryData.FromBytes(
                Encoding.UTF8.GetBytes(MultiplyNumbersSchema))),
        InvokeAsync = InvokeMultiplyNumbersAsync
    };

    private static Task<string> InvokeAddNumbersAsync(string argumentsJson)
    {
        AddNumbersArgs args = Deserialize<AddNumbersArgs>(argumentsJson);
        double result = args.A + args.B;

        return Task.FromResult(result.ToString());
    }

    private static Task<string> InvokeMultiplyNumbersAsync(string argumentsJson)
    {
        MultiplyNumbersArgs args = Deserialize<MultiplyNumbersArgs>(argumentsJson);
        double result = args.A * args.B;

        return Task.FromResult(result.ToString());
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
