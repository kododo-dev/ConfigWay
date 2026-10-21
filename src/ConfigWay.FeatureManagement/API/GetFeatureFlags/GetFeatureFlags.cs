using Kododo.ConfigWay.FeatureManagement.Model;
using Kododo.Reiho.AspNetCore.API;

namespace Kododo.ConfigWay.FeatureManagement.API.GetFeatureFlags;

internal record GetFeatureFlags : IRequest<FeatureFlags>;

/// <param name="IsFeatureManagementRegistered">
/// Whether the application registered feature management (<c>services.AddFeatureManagement()</c>); without it the
/// flags are stored but nothing evaluates them.
/// </param>
/// <param name="Filters">The aliases of the feature filters the application registered.</param>
/// <param name="Flags">The flags, by id.</param>
internal record FeatureFlags(bool IsFeatureManagementRegistered, string[] Filters, FeatureFlagItem[] Flags);

/// <param name="Id">The flag id.</param>
/// <param name="Flag">The flag in effect: the override when there is one, otherwise the configured flag.</param>
/// <param name="Configured">The flag as the application's configuration defines it; <c>null</c> when only ConfigWay has it.</param>
/// <param name="HasOverride">Whether ConfigWay keeps an override of the flag.</param>
/// <param name="IsDotnetSchema">Whether the configuration defines the flag in the older .NET schema.</param>
/// <param name="HasVariants">Whether the flag has variants, which are kept but not edited.</param>
/// <param name="Error">Why the stored override cannot be read; resetting the flag removes it.</param>
internal record FeatureFlagItem(
    string Id,
    FeatureFlag Flag,
    FeatureFlag? Configured,
    bool HasOverride,
    bool IsDotnetSchema,
    bool HasVariants,
    string? Error);
