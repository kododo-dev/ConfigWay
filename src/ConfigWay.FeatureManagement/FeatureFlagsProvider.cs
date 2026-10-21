using System.Globalization;
using Kododo.ConfigWay.Core.Model;

namespace Kododo.ConfigWay.FeatureManagement;

/// <summary>
/// Exposes the flags kept in the ConfigWay store as <c>feature_management:feature_flags</c>, where
/// Microsoft.FeatureManagement reads them.
/// </summary>
internal sealed class FeatureFlagsProvider : StoreConfigurationProvider
{
    /// <summary>
    /// The index of the first flag. Feature management is set up to merge flags by id, where the indexes do not
    /// matter; should it merge by index instead (it does when the app has a provider it cannot inspect), indexes
    /// past those of the app's own flags still let the flags of the store win.
    /// </summary>
    public const int FirstIndex = 100_000;

    protected override IEnumerable<KeyValuePair<string, string?>> Select(IReadOnlyList<Setting> settings)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var index = FirstIndex;
        foreach (var flag in StoredFlags.Read(settings))
        {
            if (flag.Json is null)
                continue;
            ConfigurationJson.Flatten(
                flag.Json,
                $"feature_management:feature_flags:{index.ToString(CultureInfo.InvariantCulture)}",
                data);
            index++;
        }

        return data;
    }
}
