using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Kododo.ConfigWay.Tests;

internal class EmailOptions        { public string Server { get; set; } = string.Empty; }
internal class Options             { public string Value  { get; set; } = string.Empty; }
internal class AppSettings        { public string Value  { get; set; } = string.Empty; }

internal class AlwaysFailValidator : IValidateOptions<SimpleOptions>
{
    public static readonly string ErrorMessage = "SimpleOptions is always invalid.";

    public ValidateOptionsResult Validate(string? name, SimpleOptions options) =>
        ValidateOptionsResult.Fail(ErrorMessage);
}

internal class ConditionalValidator : IValidateOptions<SimpleOptions>
{
    public static readonly string ErrorMessage = "Name must not be empty.";

    public ValidateOptionsResult Validate(string? name, SimpleOptions options) =>
        string.IsNullOrEmpty(options.Name)
            ? ValidateOptionsResult.Fail(ErrorMessage)
            : ValidateOptionsResult.Success;
}

internal class SimpleOptions
{
    public string Name  { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

internal class NestedOptions
{
    public string       TopLevel { get; set; } = string.Empty;
    public ChildOptions Child    { get; set; } = new();
}

internal class ChildOptions
{
    public string ChildProp { get; set; } = string.Empty;
}

[Display(Name = "Custom Section Name", Description = "Section description")]
internal class AnnotatedOptions
{
    [Display(Name = "Custom Field", Description = "Field description")]
    public string Field { get; set; } = string.Empty;

    [Display(Name = "Nested Section")]
    public AnnotatedChild Nested { get; set; } = new();
}

internal class AnnotatedChild
{
    [Display(Name = "Child Field", Description = "Child field description")]
    public string ChildField { get; set; } = string.Empty;
}

internal enum Severity
{
    [Display(Name = "Low priority")]
    Low,
    Medium,
    [Display(Name = "High priority")]
    High,
}

internal class TypedOptions
{
    public bool     Enabled  { get; set; }
    public int      Count    { get; set; }
    public Severity Level    { get; set; }
    public string   Label    { get; set; } = string.Empty;
    public bool?    Optional { get; set; }
}

internal class SimpleArrayOptions
{
    public string[] Tags  { get; set; } = [];
    public int[]    Ports { get; set; } = [];
}

internal enum ItemCategory
{
    [Display(Name = "Primary")]
    Primary,
    Secondary,
}

internal class ItemOptions
{
    public string       Name     { get; set; } = string.Empty;
    public int          Priority { get; set; }
    public ItemCategory Category { get; set; }
}

internal class ComplexArrayOptions
{
    public ItemOptions[] Items { get; set; } = [];
}

internal class SensitiveOptions
{
    public string Username { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    public string ApiKey { get; set; } = string.Empty;
}

internal class NestedSensitiveOptions
{
    public string Name { get; set; } = string.Empty;
    public SensitiveCredentials Credentials { get; set; } = new();
}

internal class SensitiveCredentials
{
    [DataType(DataType.Password)]
    public string Secret { get; set; } = string.Empty;
}
