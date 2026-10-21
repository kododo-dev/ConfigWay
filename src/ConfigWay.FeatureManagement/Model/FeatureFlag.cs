using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;

namespace Kododo.ConfigWay.FeatureManagement.Model;

/// <summary>A feature flag as the editor shows and edits it.</summary>
/// <param name="Id">The flag name used by the application, e.g. in <c>IsEnabledAsync("Beta")</c>.</param>
/// <param name="Enabled">Off when <c>false</c>; when <c>true</c>, on if the filters allow it (or there are none).</param>
/// <param name="RequirementType">Whether any or all of the filters must allow the flag.</param>
/// <param name="Filters">The conditions; an empty list means the flag is simply on or off.</param>
/// <param name="Description">An optional note on what the flag is for.</param>
internal sealed record FeatureFlag(
    string Id,
    bool Enabled,
    RequirementType RequirementType,
    FeatureFilter[] Filters,
    string? Description = null) : IValidatableObject
{
    /// <summary>Longest id: the store key, with its prefix, must fit the SQL Server key column.</summary>
    public const int MaxIdLength = 200;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Id))
            yield return new ValidationResult("The id is required.", [nameof(Id)]);
        else if (Id.Trim() != Id)
            yield return new ValidationResult("The id cannot start or end with a space.", [nameof(Id)]);
        else if (Id.Contains(':'))
            yield return new ValidationResult("The id cannot contain ':'.", [nameof(Id)]);
        else if (Id.Length > MaxIdLength)
            yield return new ValidationResult($"The id can have at most {MaxIdLength} characters.", [nameof(Id)]);
    }
}

/// <summary>Whether any or all of a flag's filters must allow it.</summary>
internal enum RequirementType
{
    Any,
    All,
}

/// <summary>A condition of a flag: a feature filter and its parameters.</summary>
/// <param name="Name">The filter alias, e.g. <c>Microsoft.Percentage</c>.</param>
/// <param name="Parameters">The filter's settings; their shape depends on the filter.</param>
internal sealed record FeatureFilter(string Name, JsonObject? Parameters = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            yield return new ValidationResult("The filter name is required.", [nameof(Name)]);
            yield break;
        }

        foreach (var error in KnownFilters.Validate(Name, Parameters))
            yield return new ValidationResult(error, [nameof(Parameters)]);
    }
}
