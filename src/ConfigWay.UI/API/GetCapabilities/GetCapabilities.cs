using Kododo.Reiho.AspNetCore.API;

namespace Kododo.ConfigWay.UI.API.GetCapabilities;

internal record GetCapabilities : IRequest<Capabilities>;

/// <param name="Extensions">The ids of the editor extensions added by extension packages, e.g. <c>flags</c>.</param>
internal record Capabilities(string[] Extensions);
