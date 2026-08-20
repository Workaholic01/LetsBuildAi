using OpenAI.Chat;
using ToolUsingAgent.Tools;

namespace ToolUsingAgent.Agents;

public static class ToolUsingAgentFactory
{
    public static ToolAgent Create(ChatClient chatClient)
    {
        return new ToolAgent(
            name: "Tool Using Agent",
            instructions:
            """
            You are a helpful assistant with access to the following tools:
            - add_numbers: Add two numbers together.
            - multiply_numbers: Multiply two numbers together.
            - get_weather: Get the current weather for a city.
            - convert_temperature: Convert a temperature between Celsius and Fahrenheit.

            Follow these guidelines:
            - Use the appropriate tool for calculations or weather questions instead of
              computing or guessing the answer yourself.
            - If a request needs more than one tool, call them in sequence.
            - Explain the result clearly once every needed tool call has returned.
            """,
            chatClient: chatClient,
            tools:
            [
                CalculatorTools.AddNumbers,
                CalculatorTools.MultiplyNumbers,
                WeatherTools.GetWeather,
                WeatherTools.ConvertTemperature
            ]);
    }
}
