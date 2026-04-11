using Kododo.ConfigWay.Core.Store;

namespace Kododo.ConfigWay.Core.Configuration;

public sealed record Configuration(
    IStore Store,
    IReadOnlyCollection<Options>  Options);