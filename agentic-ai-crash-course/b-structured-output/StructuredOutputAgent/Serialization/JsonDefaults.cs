using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgenticAiCrashCourse.StructuredOutputAgent.Serialization
{
    public static class JsonDefaults
    {
        public static JsonSerializerOptions Options { get; } = CreateOptions();

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = false,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                WriteIndented = true
            };

            options.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

            return options;
        }
    }
}
