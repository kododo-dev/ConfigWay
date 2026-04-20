namespace Kododo.ConfigWay.UI.DTO;

internal record Section(
    string Key,
    string Name,
    Section[] Sections,
    Field[] Fields,
    ArrayField[] Arrays,
    string? Description);