using System.Net;
using System.Text;
using System.Text.Json;
using LmStudioOcrTray.Core;

namespace LmStudioOcrTray.Tests;

internal static class Program
{
    private static int _passed;
    private static int _failed;

    private static async Task<int> Main()
    {
        await Run("Default settings use Win+Shift+Z and 60 seconds", DefaultSettings);
        await Run("Auto selection returns first loaded vision model", AutoModelSelection);
        await Run("Explicit selection returns only matching loaded vision model", ExplicitModelSelection);
        await Run("Missing explicit model returns null", MissingExplicitModel);
        await Run("Settings normalization repairs invalid persisted values", SettingsNormalization);
        await Run("Hotkey clone is independent", HotkeyCloneIsIndependent);
        await Run("Server status parser returns running port", ServerStatusParser);
        await Run("Model parser returns only loaded vision models", ModelParserFiltersEligibleModels);
        await Run("Chat response parser returns final message and ignores reasoning", ChatResponseParser);
        await Run("Chat request uses Bearer auth and reasoning off when supported", ChatRequestUsesReasoningOff);
        await Run("Chat request omits reasoning when off is unsupported", ChatRequestOmitsUnsupportedReasoning);

        Console.WriteLine($"Passed: {_passed}, Failed: {_failed}");
        return _failed == 0 ? 0 : 1;
    }

    private static Task DefaultSettings()
    {
        var settings = AppSettings.CreateDefault();
        Equal(true, settings.Enabled);
        Equal(true, settings.StartWithWindows);
        Equal(60, settings.TimeoutSeconds);
        Equal(true, settings.Hotkey.Win);
        Equal(true, settings.Hotkey.Shift);
        Equal(false, settings.Hotkey.Ctrl);
        Equal(false, settings.Hotkey.Alt);
        Equal(0x5A, settings.Hotkey.VirtualKey);
        Contains("Transcribe all visible text", settings.Prompt);
        Contains("Do not reason extensively", settings.Prompt);
        return Task.CompletedTask;
    }

    private static Task AutoModelSelection()
    {
        var models = new[]
        {
            new VisionModel("first", "First", "first-instance", SupportsReasoningOff: true),
            new VisionModel("second", "Second", "second-instance", SupportsReasoningOff: false),
        };

        var selected = VisionModelSelector.Select(models, "Auto");
        Equal("first", selected?.Key);
        return Task.CompletedTask;
    }

    private static Task ExplicitModelSelection()
    {
        var models = new[]
        {
            new VisionModel("first", "First", "first-instance", SupportsReasoningOff: true),
            new VisionModel("second", "Second", "second-instance", SupportsReasoningOff: false),
        };

        var selected = VisionModelSelector.Select(models, "second");
        Equal("second", selected?.Key);
        return Task.CompletedTask;
    }

    private static Task MissingExplicitModel()
    {
        var models = new[]
        {
            new VisionModel("first", "First", "first-instance", SupportsReasoningOff: true),
        };

        Equal<VisionModel?>(null, VisionModelSelector.Select(models, "missing"));
        return Task.CompletedTask;
    }

    private static Task SettingsNormalization()
    {
        var settings = new AppSettings
        {
            TimeoutSeconds = 9999,
            Server = "   ",
            VisionModel = "",
            Prompt = " ",
        };

        settings.Normalize();

        Equal(600, settings.TimeoutSeconds);
        Equal("Auto", settings.Server);
        Equal("Auto", settings.VisionModel);
        Equal(AppDefaults.OcrPrompt, settings.Prompt);
        return Task.CompletedTask;
    }

    private static Task HotkeyCloneIsIndependent()
    {
        var original = HotkeySettings.CreateDefault();
        var clone = original.Clone();
        clone.VirtualKey = 0x58;

        Equal(0x5A, original.VirtualKey);
        Equal(0x58, clone.VirtualKey);
        return Task.CompletedTask;
    }

    private static Task ServerStatusParser()
    {
        Equal(true, LmStudioProtocol.TryParseServerStatus("{\"running\":true,\"port\":4321}", out var port));
        Equal(4321, port);
        Equal(false, LmStudioProtocol.TryParseServerStatus("{\"running\":false,\"port\":4321}", out _));
        return Task.CompletedTask;
    }

    private static Task ModelParserFiltersEligibleModels()
    {
        const string json = """
        {
          "models": [
            {
              "type": "llm",
              "key": "vision-loaded",
              "display_name": "Vision Loaded",
              "loaded_instances": [{ "id": "vision-loaded:1", "config": {} }],
              "capabilities": {
                "vision": true,
                "reasoning": { "allowed_options": ["off", "on"], "default": "on" }
              }
            },
            {
              "type": "llm",
              "key": "vision-unloaded",
              "display_name": "Vision Unloaded",
              "loaded_instances": [],
              "capabilities": { "vision": true }
            },
            {
              "type": "llm",
              "key": "text-loaded",
              "display_name": "Text Loaded",
              "loaded_instances": [{ "id": "text-loaded" }],
              "capabilities": { "vision": false }
            }
          ]
        }
        """;

        var models = LmStudioProtocol.ParseLoadedVisionModels(json);
        Equal(1, models.Count);
        Equal("vision-loaded", models[0].Key);
        Equal("vision-loaded:1", models[0].InstanceId);
        Equal(true, models[0].SupportsReasoningOff);
        return Task.CompletedTask;
    }

    private static Task ChatResponseParser()
    {
        const string json = """
        {
          "output": [
            { "type": "reasoning", "content": "thinking" },
            { "type": "message", "content": "first" },
            { "type": "message", "content": "final OCR text" }
          ]
        }
        """;

        Equal("final OCR text", LmStudioProtocol.ExtractMessageText(json));
        return Task.CompletedTask;
    }

    private static async Task ChatRequestUsesReasoningOff()
    {
        var handler = new RecordingHandler("{\"output\":[{\"type\":\"message\",\"content\":\"ok\"}]}");
        using var http = new HttpClient(handler);
        using var client = new LmStudioClient(http);
        var model = new VisionModel("model-key", "Model", "instance-1", SupportsReasoningOff: true);

        var result = await client.ChatOcrAsync(
            new Uri("http://127.0.0.1:1234"),
            "secret-token",
            model,
            "OCR now",
            "data:image/png;base64,AAAA",
            CancellationToken.None);

        Equal("ok", result);
        Equal("Bearer", handler.AuthorizationScheme);
        Equal("secret-token", handler.AuthorizationParameter);

        using var body = JsonDocument.Parse(handler.Body ?? throw new InvalidOperationException("Missing request body."));
        Equal("instance-1", body.RootElement.GetProperty("model").GetString());
        Equal("off", body.RootElement.GetProperty("reasoning").GetString());
        Equal(false, body.RootElement.GetProperty("store").GetBoolean());
        Equal(0.0, body.RootElement.GetProperty("temperature").GetDouble());
        Equal("image", body.RootElement.GetProperty("input")[1].GetProperty("type").GetString());
    }

    private static async Task ChatRequestOmitsUnsupportedReasoning()
    {
        var handler = new RecordingHandler("{\"output\":[{\"type\":\"message\",\"content\":\"ok\"}]}");
        using var http = new HttpClient(handler);
        using var client = new LmStudioClient(http);
        var model = new VisionModel("model-key", "Model", "instance-1", SupportsReasoningOff: false);

        await client.ChatOcrAsync(
            new Uri("http://127.0.0.1:1234"),
            null,
            model,
            "OCR now",
            "data:image/png;base64,AAAA",
            CancellationToken.None);

        using var body = JsonDocument.Parse(handler.Body ?? throw new InvalidOperationException("Missing request body."));
        Equal(false, body.RootElement.TryGetProperty("reasoning", out _));
    }

    private static async Task Run(string name, Func<Task> test)
    {
        try
        {
            await test();
            _passed++;
            Console.WriteLine($"PASS  {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            Console.Error.WriteLine($"FAIL  {name}: {ex.Message}");
        }
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }

    private static void Contains(string expectedSubstring, string actual)
    {
        if (!actual.Contains(expectedSubstring, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected string to contain '{expectedSubstring}'.");
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly string _responseJson;

        public RecordingHandler(string responseJson)
        {
            _responseJson = responseJson;
        }

        public string? Body { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }
}
