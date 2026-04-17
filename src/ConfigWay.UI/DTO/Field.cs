namespace Kododo.ConfigWay.UI.DTO;

internal record Field(
    string Key,
    string Name,
    FieldType Type,
    string? Value,
    string? Description,
    EnumOption[]? Options);