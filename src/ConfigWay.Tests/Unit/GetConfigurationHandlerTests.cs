using Kododo.ConfigWay.Core.Configuration;
using Kododo.ConfigWay.Core.Store;
using Kododo.ConfigWay.UI.API.GetConfiguration;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;
using CoreOptions = Kododo.ConfigWay.Core.Configuration.Options;

namespace Kododo.ConfigWay.Tests.Unit;

public class GetConfigurationHandlerTests
{
    private readonly IStore _store = Substitute.For<IStore>();

    [Fact]
    public async Task Handle_FlatType_ProducesFieldsOnly()
    {
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var section = Assert.Single(sections);
        Assert.Equal(2, section.Fields.Length);
        Assert.Empty(section.Sections);
        Assert.Empty(section.Arrays);
    }

    [Fact]
    public async Task Handle_NestedType_ProducesSubSection()
    {
        var handler = CreateHandler(new CoreOptions("Root", typeof(NestedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var root = sections.Single();
        Assert.Single(root.Fields, f => f.Key == "TopLevel");
        Assert.Single(root.Sections, s => s.Key == "Child");
    }

    [Fact]
    public async Task Handle_MultipleRegisteredOptions_ReturnsMultipleSections()
    {
        var handler = CreateHandler(
            new CoreOptions("Simple", typeof(SimpleOptions)),
            new CoreOptions("Nested", typeof(NestedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.Equal(2, sections.Length);
        Assert.Equivalent(new[] { "Simple", "Nested" }, sections.Select(s => s.Key), strict: true);
    }

    [Fact]
    public async Task Handle_GeneratesCorrectNestedKeys()
    {
        var handler = CreateHandler(new CoreOptions("Root", typeof(NestedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var root      = sections.Single();
        var child     = root.Sections.Single();
        var childProp = child.Fields.Single();

        Assert.Equal("Root", root.Key);
        Assert.Equal("Child", child.Key);
        Assert.Equal("ChildProp", childProp.Key);
    }

    [Fact]
    public async Task Handle_FillsValueFromAppConfiguration()
    {
        var handler = CreateHandlerWithData(
            new() { ["Simple:Name"] = "Alice" },
            new CoreOptions("Simple", typeof(SimpleOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.Equal("Alice", sections.Single().Fields.Single(f => f.Key == "Name").Value);
    }

    [Fact]
    public async Task Handle_MissingKeyInAppConfiguration_ValueIsNull()
    {
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.Null(sections.Single().Fields.Single(f => f.Key == "Name").Value);
    }

    [Fact]
    public async Task Handle_WithoutDisplayAttribute_UsesPropNameAsLabel()
    {
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var field = sections.Single().Fields.Single(f => f.Key == "Name");
        Assert.Equal("Name", field.Name);
    }

    [Fact]
    public async Task Handle_WithDisplayNameOnProperty_UsesDisplayName()
    {
        var handler = CreateHandler(new CoreOptions("Ann", typeof(AnnotatedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var field = sections.Single().Fields.Single(f => f.Key == "Field");
        Assert.Equal("Custom Field", field.Name);
    }

    [Fact]
    public async Task Handle_WithDisplayNameOnClass_UsesSectionDisplayName()
    {
        var handler = CreateHandler(new CoreOptions("Ann", typeof(AnnotatedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.Equal("Custom Section Name", sections.Single().Name);
    }

    [Fact]
    public async Task Handle_WithoutDisplayNameOnClass_UsesKeyAsSectionName()
    {
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.Equal("Simple", sections.Single().Name);
    }

    [Fact]
    public async Task Handle_WithDisplayNameOnNestedType_PropagatesName()
    {
        var handler = CreateHandler(new CoreOptions("Ann", typeof(AnnotatedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var nested = sections.Single().Sections.Single();
        Assert.Equal("Nested Section", nested.Name);
    }

    [Fact]
    public async Task Handle_WithDisplayDescriptionOnProperty_PopulatesFieldDescription()
    {
        var handler = CreateHandler(new CoreOptions("Ann", typeof(AnnotatedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var field = sections.Single().Fields.Single(f => f.Key == "Field");
        Assert.Equal("Field description", field.Description);
    }

    [Fact]
    public async Task Handle_WithDisplayDescriptionOnClass_PopulatesSectionDescription()
    {
        var handler = CreateHandler(new CoreOptions("Ann", typeof(AnnotatedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.Equal("Section description", sections.Single().Description);
    }

    [Fact]
    public async Task Handle_WithoutDisplayDescription_DescriptionIsNull()
    {
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.All(sections.Single().Fields, f => Assert.Null(f.Description));
        Assert.Null(sections.Single().Description);
    }

    [Fact]
    public async Task Handle_StringProperty_MapsToStringType()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        Assert.Equal(
            Kododo.ConfigWay.UI.DTO.FieldType.String,
            sections.Single().Fields.Single(f => f.Key == "Label").Type);
    }

    [Fact]
    public async Task Handle_BoolProperty_MapsToBoolType()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        Assert.Equal(
            Kododo.ConfigWay.UI.DTO.FieldType.Bool,
            sections.Single().Fields.Single(f => f.Key == "Enabled").Type);
    }

    [Fact]
    public async Task Handle_NullableBoolProperty_MapsToBoolType()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        Assert.Equal(
            Kododo.ConfigWay.UI.DTO.FieldType.Bool,
            sections.Single().Fields.Single(f => f.Key == "Optional").Type);
    }

    [Fact]
    public async Task Handle_IntProperty_MapsToNumberType()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        Assert.Equal(
            Kododo.ConfigWay.UI.DTO.FieldType.Number,
            sections.Single().Fields.Single(f => f.Key == "Count").Type);
    }

    [Fact]
    public async Task Handle_EnumProperty_MapsToEnumType()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        Assert.Equal(
            Kododo.ConfigWay.UI.DTO.FieldType.Enum,
            sections.Single().Fields.Single(f => f.Key == "Level").Type);
    }

    [Fact]
    public async Task Handle_NonEnumFields_HaveNullOptions()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        Assert.All(sections.Single().Fields.Where(f => f.Key != "Level"), f => Assert.Null(f.Options));
    }

    [Fact]
    public async Task Handle_EnumField_PopulatesAllMemberValues()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        var options = sections.Single().Fields.Single(f => f.Key == "Level").Options;
        Assert.NotNull(options);
        Assert.Equivalent(new[] { "Low", "Medium", "High" }, options!.Select(o => o.Value), strict: true);
    }

    [Fact]
    public async Task Handle_EnumField_DisplayAttributeOnMember_UsesDisplayName()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        var options = sections.Single().Fields.Single(f => f.Key == "Level").Options!;
        Assert.Equal("Low priority", options.Single(o => o.Value == "Low").Label);
        Assert.Equal("High priority", options.Single(o => o.Value == "High").Label);
    }

    [Fact]
    public async Task Handle_EnumField_MemberWithoutDisplayAttribute_UsesMemberName()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        var options = sections.Single().Fields.Single(f => f.Key == "Level").Options!;
        Assert.Equal("Medium", options.Single(o => o.Value == "Medium").Label);
    }

    [Fact]
    public async Task Handle_SimpleStringArray_AppearsInArraysNotFields()
    {
        var handler = CreateHandler(new CoreOptions("A", typeof(SimpleArrayOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var section = sections.Single();
        Assert.Single(section.Arrays, a => a.Key == "Tags");
        Assert.DoesNotContain(section.Fields, f => f.Key == "Tags");
    }

    [Fact]
    public async Task Handle_SimpleStringArray_IsSimpleTrue()
    {
        var handler = CreateHandler(new CoreOptions("A", typeof(SimpleArrayOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.True(sections.Single().Arrays.Single(a => a.Key == "Tags").IsSimple);
    }

    [Fact]
    public async Task Handle_SimpleStringArray_TemplateHasStringType()
    {
        var handler = CreateHandler(new CoreOptions("A", typeof(SimpleArrayOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var template = sections.Single().Arrays.Single(a => a.Key == "Tags").Template;
        Assert.Equal(Kododo.ConfigWay.UI.DTO.FieldType.String, template.Type);
    }

    [Fact]
    public async Task Handle_SimpleIntArray_TemplateHasNumberType()
    {
        var handler = CreateHandler(new CoreOptions("A", typeof(SimpleArrayOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var template = sections.Single().Arrays.Single(a => a.Key == "Ports").Template;
        Assert.Equal(Kododo.ConfigWay.UI.DTO.FieldType.Number, template.Type);
    }

    [Fact]
    public async Task Handle_ComplexArray_IsSimpleFalse()
    {
        var handler = CreateHandler(new CoreOptions("C", typeof(ComplexArrayOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.False(sections.Single().Arrays.Single(a => a.Key == "Items").IsSimple);
    }

    [Fact]
    public async Task Handle_ComplexArray_TemplateHasFields()
    {
        var handler = CreateHandler(new CoreOptions("C", typeof(ComplexArrayOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var template = sections.Single().Arrays.Single(a => a.Key == "Items").Template;
        Assert.NotEmpty(template.Fields);
        Assert.Equivalent(new[] { "Name", "Priority", "Category" }, template.Fields.Select(f => f.Key), strict: true);
    }

    [Fact]
    public async Task Handle_ComplexArray_TemplateEnumField_HasOptions()
    {
        var handler = CreateHandler(new CoreOptions("C", typeof(ComplexArrayOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var template = sections.Single().Arrays.Single(a => a.Key == "Items").Template;
        var categoryField = template.Fields.Single(f => f.Key == "Category");
        Assert.NotNull(categoryField.Options);
        Assert.Equivalent(new[] { "Primary", "Secondary" }, categoryField.Options!.Select(o => o.Value), strict: true);
    }

    [Fact]
    public async Task Handle_ArrayWithNoItemsInConfig_HasEmptyItems()
    {
        var handler = CreateHandler(new CoreOptions("A", typeof(SimpleArrayOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.Empty(sections.Single().Arrays.Single(a => a.Key == "Tags").Items);
    }

    [Fact]
    public async Task Handle_ArrayWithItemsInConfig_DetectsIndices()
    {
        var handler = CreateHandlerWithData(
            new()
            {
                ["A:Tags:0"] = "alpha",
                ["A:Tags:1"] = "beta",
                ["A:Tags:2"] = "gamma",
            },
            new CoreOptions("A", typeof(SimpleArrayOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.Equal(3, sections.Single().Arrays.Single(a => a.Key == "Tags").Items.Length);
    }

    [Fact]
    public async Task Handle_ArrayItemFromConfig_HasCorrectValue()
    {
        var handler = CreateHandlerWithData(
            new() { ["A:Tags:0"] = "hello", ["A:Tags:1"] = "world" },
            new CoreOptions("A", typeof(SimpleArrayOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var items = sections.Single().Arrays.Single(a => a.Key == "Tags").Items;
        Assert.Equal("hello", items.Single(i => i.Index == 0).Value);
        Assert.Equal("world", items.Single(i => i.Index == 1).Value);
    }

    [Fact]
    public async Task Handle_ArrayItemFromBaseConfig_IsNotDeletable()
    {
        var handler = CreateHandlerWithData(
            new() { ["A:Tags:0"] = "alpha" },
            new CoreOptions("A", typeof(SimpleArrayOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.False(sections.Single().Arrays.Single(a => a.Key == "Tags").Items.Single().IsDeletable);
    }

    [Fact]
    public async Task Handle_ComplexArrayItemFromConfig_HasFields()
    {
        var handler = CreateHandlerWithData(
            new()
            {
                ["C:Items:0:Name"]     = "First",
                ["C:Items:0:Priority"] = "1",
            },
            new CoreOptions("C", typeof(ComplexArrayOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var item = sections.Single().Arrays.Single(a => a.Key == "Items").Items.Single();
        Assert.NotEmpty(item.Fields);
        Assert.Equal("First", item.Fields.Single(f => f.Key == "Name").Value);
    }

    [Fact]
    public async Task Handle_Field_DefaultValueNullWhenNotInBaseConfig()
    {
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.Null(sections.Single().Fields.Single(f => f.Key == "Name").DefaultValue);
    }

    [Fact]
    public async Task Handle_Field_DefaultValuePopulatedFromBaseConfig()
    {
        var handler = CreateHandlerWithData(
            new() { ["Simple:Name"] = "Alice" },
            new CoreOptions("Simple", typeof(SimpleOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.Equal("Alice", sections.Single().Fields.Single(f => f.Key == "Name").DefaultValue);
    }

    [Fact]
    public async Task Handle_NestedField_DefaultValuePopulatedFromBaseConfig()
    {
        var handler = CreateHandlerWithData(
            new() { ["Root:Child:ChildProp"] = "deep-default" },
            new CoreOptions("Root", typeof(NestedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var child = sections.Single().Sections.Single();
        Assert.Equal("deep-default", child.Fields.Single(f => f.Key == "ChildProp").DefaultValue);
    }

    [Fact]
    public async Task Handle_SimpleArrayItem_DefaultValueFromBaseConfig()
    {
        var handler = CreateHandlerWithData(
            new() { ["A:Tags:0"] = "alpha", ["A:Tags:1"] = "beta" },
            new CoreOptions("A", typeof(SimpleArrayOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var items = sections.Single().Arrays.Single(a => a.Key == "Tags").Items;
        Assert.Equal("alpha", items.Single(i => i.Index == 0).DefaultValue);
        Assert.Equal("beta", items.Single(i => i.Index == 1).DefaultValue);
    }

    [Fact]
    public async Task Handle_ComplexArrayItem_DefaultValueIsNull()
    {
        var handler = CreateHandlerWithData(
            new()
            {
                ["C:Items:0:Name"]     = "First",
                ["C:Items:0:Priority"] = "1",
            },
            new CoreOptions("C", typeof(ComplexArrayOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var item = sections.Single().Arrays.Single(a => a.Key == "Items").Items.Single();
        Assert.Null(item.DefaultValue);
    }

    [Fact]
    public async Task Handle_ArrayTemplate_DefaultValueIsNull()
    {
        var handler = CreateHandler(new CoreOptions("A", typeof(SimpleArrayOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var template = sections.Single().Arrays.Single(a => a.Key == "Tags").Template;
        Assert.Null(template.DefaultValue);
    }

    [Fact]
    public async Task Handle_ComplexArrayItemField_DefaultValueFromBaseConfig()
    {
        var handler = CreateHandlerWithData(
            new()
            {
                ["C:Items:0:Name"]     = "First",
                ["C:Items:0:Priority"] = "5",
            },
            new CoreOptions("C", typeof(ComplexArrayOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var item = sections.Single().Arrays.Single(a => a.Key == "Items").Items.Single();
        Assert.Equal("First", item.Fields.Single(f => f.Key == "Name").DefaultValue);
        Assert.Equal("5", item.Fields.Single(f => f.Key == "Priority").DefaultValue);
    }

    [Fact]
    public async Task Handle_NonPasswordProperty_IsSensitiveFalse()
    {
        var handler = CreateHandler(new CoreOptions("S", typeof(SensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.False(sections.Single().Fields.Single(f => f.Key == "Username").IsSensitive);
    }

    [Fact]
    public async Task Handle_PasswordProperty_IsSensitiveTrue()
    {
        var handler = CreateHandler(new CoreOptions("S", typeof(SensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.True(sections.Single().Fields.Single(f => f.Key == "Password").IsSensitive);
    }

    [Fact]
    public async Task Handle_SensitiveField_NoStoredValue_ValueIsNull()
    {
        var handler = CreateHandler(new CoreOptions("S", typeof(SensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.Null(sections.Single().Fields.Single(f => f.Key == "Password").Value);
    }

    [Fact]
    public async Task Handle_SensitiveField_WithStoredValue_ValueIsMasked()
    {
        var handler = CreateHandlerWithData(
            new() { ["S:Password"] = "secret123" },
            new CoreOptions("S", typeof(SensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.Equal("***", sections.Single().Fields.Single(f => f.Key == "Password").Value);
    }

    [Fact]
    public async Task Handle_SensitiveField_ActualValueNeverReturned()
    {
        var handler = CreateHandlerWithData(
            new() { ["S:ApiKey"] = "my-actual-key" },
            new CoreOptions("S", typeof(SensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var field = sections.Single().Fields.Single(f => f.Key == "ApiKey");
        Assert.NotEqual("my-actual-key", field.Value);
    }

    [Fact]
    public async Task Handle_SensitiveField_WithDefaultValue_DefaultValueIsMasked()
    {
        var handler = CreateHandlerWithData(
            new() { ["S:Password"] = "base-secret" },
            new CoreOptions("S", typeof(SensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.Equal("***", sections.Single().Fields.Single(f => f.Key == "Password").DefaultValue);
    }

    [Fact]
    public async Task Handle_SensitiveField_NoDefaultValue_DefaultValueIsNull()
    {
        var handler = CreateHandler(new CoreOptions("S", typeof(SensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.Null(sections.Single().Fields.Single(f => f.Key == "Password").DefaultValue);
    }

    [Fact]
    public async Task Handle_NestedSensitiveField_IsSensitiveTrue()
    {
        var handler = CreateHandler(new CoreOptions("NS", typeof(NestedSensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var creds = sections.Single().Sections.Single(s => s.Key == "Credentials");
        Assert.True(creds.Fields.Single(f => f.Key == "Secret").IsSensitive);
    }

    [Fact]
    public async Task Handle_SensitiveField_NoOverride_HasOverrideFalse()
    {
        var handler = CreateHandler(new CoreOptions("S", typeof(SensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.False(sections.Single().Fields.Single(f => f.Key == "Password").HasOverride);
    }

    [Fact]
    public async Task Handle_NonSensitiveField_NoOverride_HasOverrideFalse()
    {
        var handler = CreateHandler(new CoreOptions("S", typeof(SensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.False(sections.Single().Fields.Single(f => f.Key == "Username").HasOverride);
    }

    [Fact]
    public async Task Handle_Field_BaseConfigAndAppConfigMatch_HasOverrideFalse()
    {
        var handler = CreateHandlerWithData(
            new() { ["S:Username"] = "base-user" },
            new CoreOptions("S", typeof(SensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        Assert.False(sections.Single().Fields.Single(f => f.Key == "Username").HasOverride);
    }

    private GetConfigurationHandler CreateHandler(params CoreOptions[] options) =>
        CreateHandlerWithData(new Dictionary<string, string?>(), options);

    private GetConfigurationHandler CreateHandlerWithData(
        Dictionary<string, string?> configData,
        params CoreOptions[] options)
    {
        var appConfig = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
        return new GetConfigurationHandler(new Configuration(_store, options), appConfig);
    }
}
