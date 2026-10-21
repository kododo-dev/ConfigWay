using Kododo.ConfigWay.FeatureManagement.Model;
using Kododo.Reiho.AspNetCore.API;

namespace Kododo.ConfigWay.FeatureManagement.API.SaveFeatureFlag;

/// <param name="Flag">The flag to save as an override.</param>
/// <param name="IsNew">Whether this adds a flag, which must not exist yet; otherwise it changes an existing flag.</param>
internal record SaveFeatureFlag(FeatureFlag Flag, bool IsNew = false) : IRequest;
