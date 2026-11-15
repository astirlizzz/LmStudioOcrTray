using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LmStudioOcrTray.Core;

public sealed class LmStudioClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public LmStudioClient(HttpClient? httpClient = null)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    public async Task<IReadOnlyList<VisionModel>> ListLoadedVisionModelsAsync(
        Uri serverUri,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildEndpoint(serverUri, "/api/v1/models"));
        ApplyAuthorization(request, apiKey);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return LmStudioProtocol.ParseLoadedVisionModels(json);
    }

    public async Task<string?> ChatOcrAsync(
        Uri serverUri,
        string? apiKey,
        VisionModel model,
        string prompt,
        string imageDataUrl,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = string.IsNullOrWhiteSpace(model.InstanceId) ? model.Key : model.InstanceId,
            ["input"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "text",
                    ["content"] = prompt,
                },
                new Dictionary<string, object?>
                {
                    ["type"] = "image",
                    ["data_url"] = imageDataUrl,
                },
            },
            ["temperature"] = 0.0,
            ["store"] = false,
        };

        if (model.SupportsReasoningOff)
        {
            payload["reasoning"] = "off";
        }

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(serverUri, "/api/v1/chat"))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        ApplyAuthorization(request, apiKey);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return LmStudioProtocol.ExtractMessageText(responseJson);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static Uri BuildEndpoint(Uri serverUri, string path)
    {
        return new Uri(serverUri, path);
    }

    private static void ApplyAuthorization(HttpRequestMessage request, string? apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        }
    }
}
