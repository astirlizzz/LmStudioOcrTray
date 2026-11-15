using System.Text.Json;

namespace LmStudioOcrTray.Core;

public static class LmStudioProtocol
{
    public static bool TryParseServerStatus(string json, out int port)
    {
        port = 0;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("running", out var runningElement)
                || runningElement.ValueKind != JsonValueKind.True)
            {
                return false;
            }

            if (!root.TryGetProperty("port", out var portElement)
                || !portElement.TryGetInt32(out port)
                || port is < 1 or > 65535)
            {
                port = 0;
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            port = 0;
            return false;
        }
    }

    public static IReadOnlyList<VisionModel> ParseLoadedVisionModels(string json)
    {
        var result = new List<VisionModel>();

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("models", out var modelsElement)
            || modelsElement.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var modelElement in modelsElement.EnumerateArray())
        {
            if (modelElement.TryGetProperty("type", out var typeElement)
                && typeElement.ValueKind == JsonValueKind.String
                && !string.Equals(typeElement.GetString(), "llm", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!modelElement.TryGetProperty("capabilities", out var capabilities)
                || capabilities.ValueKind != JsonValueKind.Object
                || !capabilities.TryGetProperty("vision", out var visionElement)
                || visionElement.ValueKind != JsonValueKind.True)
            {
                continue;
            }

            if (!modelElement.TryGetProperty("loaded_instances", out var loadedInstances)
                || loadedInstances.ValueKind != JsonValueKind.Array
                || loadedInstances.GetArrayLength() == 0)
            {
                continue;
            }

            if (!modelElement.TryGetProperty("key", out var keyElement)
                || keyElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(keyElement.GetString()))
            {
                continue;
            }

            var key = keyElement.GetString()!;
            var displayName = key;
            if (modelElement.TryGetProperty("display_name", out var displayNameElement)
                && displayNameElement.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(displayNameElement.GetString()))
            {
                displayName = displayNameElement.GetString()!;
            }

            var firstInstance = loadedInstances[0];
            var instanceId = key;
            if (firstInstance.ValueKind == JsonValueKind.Object
                && firstInstance.TryGetProperty("id", out var idElement)
                && idElement.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(idElement.GetString()))
            {
                instanceId = idElement.GetString()!;
            }

            var supportsReasoningOff = false;
            if (capabilities.TryGetProperty("reasoning", out var reasoning)
                && reasoning.ValueKind == JsonValueKind.Object
                && reasoning.TryGetProperty("allowed_options", out var allowedOptions)
                && allowedOptions.ValueKind == JsonValueKind.Array)
            {
                supportsReasoningOff = allowedOptions.EnumerateArray().Any(option =>
                    option.ValueKind == JsonValueKind.String
                    && string.Equals(option.GetString(), "off", StringComparison.OrdinalIgnoreCase));
            }

            result.Add(new VisionModel(key, displayName, instanceId, supportsReasoningOff));
        }

        return result;
    }

    public static string? ExtractMessageText(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("output", out var outputElement)
            || outputElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string? lastMessage = null;
        foreach (var item in outputElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("type", out var typeElement)
                || typeElement.ValueKind != JsonValueKind.String
                || !string.Equals(typeElement.GetString(), "message", StringComparison.OrdinalIgnoreCase)
                || !item.TryGetProperty("content", out var contentElement)
                || contentElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var content = contentElement.GetString();
            if (!string.IsNullOrWhiteSpace(content))
            {
                lastMessage = content;
            }
        }

        return lastMessage;
    }
}
