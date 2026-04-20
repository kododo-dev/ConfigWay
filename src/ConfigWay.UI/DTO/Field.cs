namespace Kododo.ConfigWay.UI.DTO;

internal record Field(
    string Key,
    string Name,
    FieldType Type,
    string? Value,
    string? DefaultValue,
    bool IsSensitive,
    string? Description,
    EnumOption[]? Options);
