namespace Kododo.ConfigWay.UI.DTO;

internal record ArrayItem(
    int Index,
    bool IsDeletable,
    string? Value,
    string? DefaultValue,
    FieldType? Type,
    EnumOption[]? Options,
    Field[] Fields,
    Section[] Sections,
    ArrayField[] Arrays);
