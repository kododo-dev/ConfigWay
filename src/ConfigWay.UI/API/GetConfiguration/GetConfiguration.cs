using Kododo.ConfigWay.UI.DTO;
using Kododo.Reiho.AspNetCore.API;

namespace Kododo.ConfigWay.UI.API.GetConfiguration;

internal record GetConfiguration : IRequest<Section[]>;