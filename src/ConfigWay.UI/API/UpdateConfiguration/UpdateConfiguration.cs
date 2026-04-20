using Kododo.ConfigWay.Core.Model;
using Kododo.Reiho.AspNetCore.API;

namespace Kododo.ConfigWay.UI.API.UpdateConfiguration;

internal record UpdateConfiguration(Setting[] Settings) : IRequest<string[]>
{
    public string[] KeysToDelete { get; init; } = [];
}