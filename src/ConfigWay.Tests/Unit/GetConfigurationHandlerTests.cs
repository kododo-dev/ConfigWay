using FluentAssertions;
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

        var section = sections.Should().ContainSingle().Subject;
        section.Fields.Should().HaveCount(2);
        section.Sections.Should().BeEmpty();
        section.Arrays.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NestedType_ProducesSubSection()
    {
        var handler = CreateHandler(new CoreOptions("Root", typeof(NestedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var root = sections.Single();
        root.Fields.Should().ContainSingle(f => f.Key == "TopLevel");
        root.Sections.Should().ContainSingle(s => s.Key == "Child");
    }

    [Fact]
    public async Task Handle_MultipleRegisteredOptions_ReturnsMultipleSections()
    {
        var handler = CreateHandler(
            new CoreOptions("Simple", typeof(SimpleOptions)),
            new CoreOptions("Nested", typeof(NestedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Should().HaveCount(2);
        sections.Select(s => s.Key).Should().BeEquivalentTo("Simple", "Nested");
    }

    [Fact]
    public async Task Handle_GeneratesCorrectNestedKeys()
    {
        var handler = CreateHandler(new CoreOptions("Root", typeof(NestedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var root      = sections.Single();
        var child     = root.Sections.Single();
        var childProp = child.Fields.Single();

        root.Key.Should().Be("Root");
        child.Key.Should().Be("Child");
        childProp.Key.Should().Be("ChildProp");
    }

    [Fact]
    public async Task Handle_FillsValueFromAppConfiguration()
    {
        var handler = CreateHandlerWithData(
            new() { ["Simple:Name"] = "Alice" },
            new CoreOptions("Simple", typeof(SimpleOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Fields
            .Single(f => f.Key == "Name").Value
            .Should().Be("Alice");
    }

    [Fact]
    public async Task Handle_MissingKeyInAppConfiguration_ValueIsNull()
    {
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Fields
            .Single(f => f.Key == "Name").Value
            .Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithoutDisplayAttribute_UsesPropNameAsLabel()
    {
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var field = sections.Single().Fields.Single(f => f.Key == "Name");
        field.Name.Should().Be("Name");
    }

    [Fact]
    public async Task Handle_WithDisplayNameOnProperty_UsesDisplayName()
    {
        var handler = CreateHandler(new CoreOptions("Ann", typeof(AnnotatedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var field = sections.Single().Fields.Single(f => f.Key == "Field");
        field.Name.Should().Be("Custom Field");
    }

    [Fact]
    public async Task Handle_WithDisplayNameOnClass_UsesSectionDisplayName()
    {
        var handler = CreateHandler(new CoreOptions("Ann", typeof(AnnotatedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Name.Should().Be("Custom Section Name");
    }

    [Fact]
    public async Task Handle_WithoutDisplayNameOnClass_UsesKeyAsSectionName()
    {
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Name.Should().Be("Simple");
    }

    [Fact]
    public async Task Handle_WithDisplayNameOnNestedType_PropagatesName()
    {
        var handler = CreateHandler(new CoreOptions("Ann", typeof(AnnotatedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var nested = sections.Single().Sections.Single();
        nested.Name.Should().Be("Nested Section");
    }

    [Fact]
    public async Task Handle_WithDisplayDescriptionOnProperty_PopulatesFieldDescription()
    {
        var handler = CreateHandler(new CoreOptions("Ann", typeof(AnnotatedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var field = sections.Single().Fields.Single(f => f.Key == "Field");
        field.Description.Should().Be("Field description");
    }

    [Fact]
    public async Task Handle_WithDisplayDescriptionOnClass_PopulatesSectionDescription()
    {
        var handler = CreateHandler(new CoreOptions("Ann", typeof(AnnotatedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Description.Should().Be("Section description");
    }

    [Fact]
    public async Task Handle_WithoutDisplayDescription_DescriptionIsNull()
    {
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Fields.Should().AllSatisfy(f => f.Description.Should().BeNull());
        sections.Single().Description.Should().BeNull();
    }

    [Fact]
    public async Task Handle_StringProperty_MapsToStringType()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        sections.Single().Fields.Single(f => f.Key == "Label").Type
            .Should().Be(Kododo.ConfigWay.UI.DTO.FieldType.String);
    }

    [Fact]
    public async Task Handle_BoolProperty_MapsToBoolType()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        sections.Single().Fields.Single(f => f.Key == "Enabled").Type
            .Should().Be(Kododo.ConfigWay.UI.DTO.FieldType.Bool);
    }

    [Fact]
    public async Task Handle_NullableBoolProperty_MapsToBoolType()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        sections.Single().Fields.Single(f => f.Key == "Optional").Type
            .Should().Be(Kododo.ConfigWay.UI.DTO.FieldType.Bool);
    }

    [Fact]
    public async Task Handle_IntProperty_MapsToNumberType()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        sections.Single().Fields.Single(f => f.Key == "Count").Type
            .Should().Be(Kododo.ConfigWay.UI.DTO.FieldType.Number);
    }

    [Fact]
    public async Task Handle_EnumProperty_MapsToEnumType()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        sections.Single().Fields.Single(f => f.Key == "Level").Type
            .Should().Be(Kododo.ConfigWay.UI.DTO.FieldType.Enum);
    }

    [Fact]
    public async Task Handle_NonEnumFields_HaveNullOptions()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        sections.Single().Fields
            .Where(f => f.Key != "Level")
            .Should().AllSatisfy(f => f.Options.Should().BeNull());
    }

    [Fact]
    public async Task Handle_EnumField_PopulatesAllMemberValues()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        var options = sections.Single().Fields.Single(f => f.Key == "Level").Options;
        options.Should().NotBeNull();
        options!.Select(o => o.Value).Should().BeEquivalentTo("Low", "Medium", "High");
    }

    [Fact]
    public async Task Handle_EnumField_DisplayAttributeOnMember_UsesDisplayName()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        var options = sections.Single().Fields.Single(f => f.Key == "Level").Options!;
        options.Single(o => o.Value == "Low").Label.Should().Be("Low priority");
        options.Single(o => o.Value == "High").Label.Should().Be("High priority");
    }

    [Fact]
    public async Task Handle_EnumField_MemberWithoutDisplayAttribute_UsesMemberName()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        var options = sections.Single().Fields.Single(f => f.Key == "Level").Options!;
        options.Single(o => o.Value == "Medium").Label.Should().Be("Medium");
    }

    [Fact]
    public async Task Handle_SimpleStringArray_AppearsInArraysNotFields()
    {
        var handler = CreateHandler(new CoreOptions("A", typeof(SimpleArrayOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var section = sections.Single();
        section.Arrays.Should().ContainSingle(a => a.Key == "Tags");
        section.Fields.Should().NotContain(f => f.Key == "Tags");
    }

    [Fact]
    public async Task Handle_SimpleStringArray_IsSimpleTrue()
    {
        var handler = CreateHandler(new CoreOptions("A", typeof(SimpleArrayOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Arrays.Single(a => a.Key == "Tags").IsSimple
            .Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SimpleStringArray_TemplateHasStringType()
    {
        var handler = CreateHandler(new CoreOptions("A", typeof(SimpleArrayOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var template = sections.Single().Arrays.Single(a => a.Key == "Tags").Template;
        template.Type.Should().Be(Kododo.ConfigWay.UI.DTO.FieldType.String);
    }

    [Fact]
    public async Task Handle_SimpleIntArray_TemplateHasNumberType()
    {
        var handler = CreateHandler(new CoreOptions("A", typeof(SimpleArrayOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var template = sections.Single().Arrays.Single(a => a.Key == "Ports").Template;
        template.Type.Should().Be(Kododo.ConfigWay.UI.DTO.FieldType.Number);
    }

    [Fact]
    public async Task Handle_ComplexArray_IsSimpleFalse()
    {
        var handler = CreateHandler(new CoreOptions("C", typeof(ComplexArrayOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Arrays.Single(a => a.Key == "Items").IsSimple
            .Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ComplexArray_TemplateHasFields()
    {
        var handler = CreateHandler(new CoreOptions("C", typeof(ComplexArrayOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var template = sections.Single().Arrays.Single(a => a.Key == "Items").Template;
        template.Fields.Should().NotBeEmpty();
        template.Fields.Select(f => f.Key).Should().BeEquivalentTo("Name", "Priority", "Category");
    }

    [Fact]
    public async Task Handle_ComplexArray_TemplateEnumField_HasOptions()
    {
        var handler = CreateHandler(new CoreOptions("C", typeof(ComplexArrayOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var template = sections.Single().Arrays.Single(a => a.Key == "Items").Template;
        var categoryField = template.Fields.Single(f => f.Key == "Category");
        categoryField.Options.Should().NotBeNull();
        categoryField.Options!.Select(o => o.Value).Should().BeEquivalentTo("Primary", "Secondary");
    }

    [Fact]
    public async Task Handle_ArrayWithNoItemsInConfig_HasEmptyItems()
    {
        var handler = CreateHandler(new CoreOptions("A", typeof(SimpleArrayOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Arrays.Single(a => a.Key == "Tags").Items
            .Should().BeEmpty();
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

        sections.Single().Arrays.Single(a => a.Key == "Tags").Items
            .Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_ArrayItemFromConfig_HasCorrectValue()
    {
        var handler = CreateHandlerWithData(
            new() { ["A:Tags:0"] = "hello", ["A:Tags:1"] = "world" },
            new CoreOptions("A", typeof(SimpleArrayOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var items = sections.Single().Arrays.Single(a => a.Key == "Tags").Items;
        items.Single(i => i.Index == 0).Value.Should().Be("hello");
        items.Single(i => i.Index == 1).Value.Should().Be("world");
    }

    [Fact]
    public async Task Handle_ArrayItemFromBaseConfig_IsNotDeletable()
    {
        var handler = CreateHandlerWithData(
            new() { ["A:Tags:0"] = "alpha" },
            new CoreOptions("A", typeof(SimpleArrayOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Arrays.Single(a => a.Key == "Tags")
            .Items.Single().IsDeletable
            .Should().BeFalse();
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
        item.Fields.Should().NotBeEmpty();
        item.Fields.Single(f => f.Key == "Name").Value.Should().Be("First");
    }

    [Fact]
    public async Task Handle_Field_DefaultValueNullWhenNotInBaseConfig()
    {
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Fields
            .Single(f => f.Key == "Name").DefaultValue
            .Should().BeNull();
    }

    [Fact]
    public async Task Handle_Field_DefaultValuePopulatedFromBaseConfig()
    {
        var handler = CreateHandlerWithData(
            new() { ["Simple:Name"] = "Alice" },
            new CoreOptions("Simple", typeof(SimpleOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Fields
            .Single(f => f.Key == "Name").DefaultValue
            .Should().Be("Alice");
    }

    [Fact]
    public async Task Handle_NestedField_DefaultValuePopulatedFromBaseConfig()
    {
        var handler = CreateHandlerWithData(
            new() { ["Root:Child:ChildProp"] = "deep-default" },
            new CoreOptions("Root", typeof(NestedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var child = sections.Single().Sections.Single();
        child.Fields.Single(f => f.Key == "ChildProp").DefaultValue
            .Should().Be("deep-default");
    }

    [Fact]
    public async Task Handle_SimpleArrayItem_DefaultValueFromBaseConfig()
    {
        var handler = CreateHandlerWithData(
            new() { ["A:Tags:0"] = "alpha", ["A:Tags:1"] = "beta" },
            new CoreOptions("A", typeof(SimpleArrayOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var items = sections.Single().Arrays.Single(a => a.Key == "Tags").Items;
        items.Single(i => i.Index == 0).DefaultValue.Should().Be("alpha");
        items.Single(i => i.Index == 1).DefaultValue.Should().Be("beta");
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
        item.DefaultValue.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ArrayTemplate_DefaultValueIsNull()
    {
        var handler = CreateHandler(new CoreOptions("A", typeof(SimpleArrayOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var template = sections.Single().Arrays.Single(a => a.Key == "Tags").Template;
        template.DefaultValue.Should().BeNull();
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
        item.Fields.Single(f => f.Key == "Name").DefaultValue.Should().Be("First");
        item.Fields.Single(f => f.Key == "Priority").DefaultValue.Should().Be("5");
    }

    [Fact]
    public async Task Handle_NonPasswordProperty_IsSensitiveFalse()
    {
        var handler = CreateHandler(new CoreOptions("S", typeof(SensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Fields.Single(f => f.Key == "Username").IsSensitive
            .Should().BeFalse();
    }

    [Fact]
    public async Task Handle_PasswordProperty_IsSensitiveTrue()
    {
        var handler = CreateHandler(new CoreOptions("S", typeof(SensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Fields.Single(f => f.Key == "Password").IsSensitive
            .Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SensitiveField_NoStoredValue_ValueIsNull()
    {
        var handler = CreateHandler(new CoreOptions("S", typeof(SensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Fields.Single(f => f.Key == "Password").Value
            .Should().BeNull();
    }

    [Fact]
    public async Task Handle_SensitiveField_WithStoredValue_ValueIsMasked()
    {
        var handler = CreateHandlerWithData(
            new() { ["S:Password"] = "secret123" },
            new CoreOptions("S", typeof(SensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Fields.Single(f => f.Key == "Password").Value
            .Should().Be("***");
    }

    [Fact]
    public async Task Handle_SensitiveField_ActualValueNeverReturned()
    {
        var handler = CreateHandlerWithData(
            new() { ["S:ApiKey"] = "my-actual-key" },
            new CoreOptions("S", typeof(SensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var field = sections.Single().Fields.Single(f => f.Key == "ApiKey");
        field.Value.Should().NotBe("my-actual-key");
    }

    [Fact]
    public async Task Handle_SensitiveField_WithDefaultValue_DefaultValueIsMasked()
    {
        var handler = CreateHandlerWithData(
            new() { ["S:Password"] = "base-secret" },
            new CoreOptions("S", typeof(SensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Fields.Single(f => f.Key == "Password").DefaultValue
            .Should().Be("***");
    }

    [Fact]
    public async Task Handle_SensitiveField_NoDefaultValue_DefaultValueIsNull()
    {
        var handler = CreateHandler(new CoreOptions("S", typeof(SensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Fields.Single(f => f.Key == "Password").DefaultValue
            .Should().BeNull();
    }

    [Fact]
    public async Task Handle_NestedSensitiveField_IsSensitiveTrue()
    {
        var handler = CreateHandler(new CoreOptions("NS", typeof(NestedSensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var creds = sections.Single().Sections.Single(s => s.Key == "Credentials");
        creds.Fields.Single(f => f.Key == "Secret").IsSensitive
            .Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SensitiveField_NoOverride_HasOverrideFalse()
    {
        var handler = CreateHandler(new CoreOptions("S", typeof(SensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Fields.Single(f => f.Key == "Password").HasOverride
            .Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonSensitiveField_NoOverride_HasOverrideFalse()
    {
        var handler = CreateHandler(new CoreOptions("S", typeof(SensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Fields.Single(f => f.Key == "Username").HasOverride
            .Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Field_BaseConfigAndAppConfigMatch_HasOverrideFalse()
    {
        var handler = CreateHandlerWithData(
            new() { ["S:Username"] = "base-user" },
            new CoreOptions("S", typeof(SensitiveOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Fields.Single(f => f.Key == "Username").HasOverride
            .Should().BeFalse();
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
