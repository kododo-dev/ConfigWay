using Kododo.Reiho.AspNetCore.API;

namespace Kododo.ConfigWay.FeatureManagement.API.EvaluateFeatureFlag;

/// <summary>Checks a flag for a user, as the application's feature manager does.</summary>
/// <param name="Id">The flag id.</param>
/// <param name="UserId">The user, for targeting; none for an anonymous user.</param>
/// <param name="Groups">The groups of the user, for targeting.</param>
internal record EvaluateFeatureFlag(string Id, string? UserId = null, string[]? Groups = null) : IRequest<FlagEvaluation>;

/// <param name="Enabled">Whether the flag is on for the user.</param>
/// <param name="Variant">The variant the user gets; <c>null</c> for a flag without variants.</param>
internal record FlagEvaluation(bool Enabled, string? Variant);
