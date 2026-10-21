using Kododo.Reiho.AspNetCore.API;

namespace Kododo.ConfigWay.FeatureManagement.API.ResetFeatureFlag;

/// <summary>
/// Removes the override of a flag: a flag of the application's configuration is back to how it is configured, and a
/// flag only ConfigWay has is deleted.
/// </summary>
/// <param name="Id">The flag id.</param>
internal record ResetFeatureFlag(string Id) : IRequest;
