using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FCZ.Core.Models;

namespace FCZ.Scenarios
{
    public static class ScenarioSerializer
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new StepJsonConverter() }
        };

        public static Scenario Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Scenario file not found: {path}");
            }

            string json = File.ReadAllText(path);
            var scenario = JsonSerializer.Deserialize<Scenario>(json, Options);
            
            if (scenario == null)
            {
                throw new InvalidOperationException("Failed to deserialize scenario");
            }

            return scenario;
        }

        public static void Save(string path, Scenario scenario)
        {
            string directory = Path.GetDirectoryName(path) ?? string.Empty;
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(scenario, Options);
            File.WriteAllText(path, json);
        }

        public static IEnumerable<Scenario> LoadAll(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return Enumerable.Empty<Scenario>();
            }

            var scenarios = new List<Scenario>();
            foreach (var file in Directory.GetFiles(folder, "*.json"))
            {
                try
                {
                    scenarios.Add(Load(file));
                }
                catch
                {
                    // Skip invalid files
                }
            }

            return scenarios;
        }
    }

    public class StepJsonConverter : JsonConverter<Step>
    {
        public override Step? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeElement))
            {
                throw new JsonException("Step must have a 'type' property");
            }

            string type = typeElement.GetString() ?? string.Empty;

            return type switch
            {
                "waitForImageThenClick" => JsonSerializer.Deserialize<WaitForImageThenClickStep>(root.GetRawText(), options),
                "waitForImage" => JsonSerializer.Deserialize<WaitForImageStep>(root.GetRawText(), options),
                "clickTemplate" => JsonSerializer.Deserialize<ClickTemplateStep>(root.GetRawText(), options),
                "clickPoint" => JsonSerializer.Deserialize<ClickPointStep>(root.GetRawText(), options),
                "typeText" => JsonSerializer.Deserialize<TypeTextStep>(root.GetRawText(), options),
                "wait" => JsonSerializer.Deserialize<WaitStep>(root.GetRawText(), options),
                "conditionalBlock" => JsonSerializer.Deserialize<ConditionalBlockStep>(root.GetRawText(), options),
                "loop" => JsonSerializer.Deserialize<LoopStep>(root.GetRawText(), options),
                "log" => JsonSerializer.Deserialize<LogStep>(root.GetRawText(), options),
                _ => throw new JsonException($"Unknown step type: {type}")
            };
        }

        public override void Write(Utf8JsonWriter writer, Step value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}

