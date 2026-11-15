namespace LmStudioOcrTray.Core;

public static class VisionModelSelector
{
    public static VisionModel? Select(IReadOnlyList<VisionModel> models, string? configuredModel)
    {
        if (models.Count == 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(configuredModel)
            || configuredModel.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            return models[0];
        }

        return models.FirstOrDefault(model =>
            model.Key.Equals(configuredModel, StringComparison.OrdinalIgnoreCase)
            || model.InstanceId.Equals(configuredModel, StringComparison.OrdinalIgnoreCase));
    }
}
