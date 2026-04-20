namespace Kododo.ConfigWay.UI.DTO;

internal record ArrayField(
    string Key,
    string Name,
    string? Description,
    bool IsSimple,
    ArrayItem[] Items,
    ArrayItem Template);
