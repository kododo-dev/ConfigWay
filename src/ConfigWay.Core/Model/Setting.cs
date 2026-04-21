namespace Kododo.ConfigWay.Core.Model;

/// <summary>
/// A single configuration override stored by ConfigWay.
/// </summary>
/// <param name="Key">
/// The fully-qualified configuration key using the colon-separated convention,
/// e.g. <c>Smtp:Host</c> or <c>Webhooks:Endpoints:0:Url</c>.
/// </param>
/// <param name="Value">
/// The override value, or <see langword="null"/> to explicitly clear the setting.
/// </param>
public record Setting(string Key, string? Value);
