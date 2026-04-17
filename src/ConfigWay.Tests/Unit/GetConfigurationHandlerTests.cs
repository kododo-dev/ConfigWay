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
    private readonly IConfiguration _appConfig = Substitute.For<IConfiguration>();

    // ── Section structure ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_FlatType_ProducesFieldsOnly()
    {
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var section = sections.Should().ContainSingle().Subject;
        section.Fields.Should().HaveCount(2);
        section.Sections.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NestedType_ProducesSubSection()
    {
        var handler = CreateHandler(new CoreOptions("Root", typeof(NestedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var root = sections.Single();
        root.Fields.Should().ContainSingle(f => f.Key == "Root:TopLevel");
        root.Sections.Should().ContainSingle(s => s.Key == "Root:Child");
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

    // ── Key generation ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_GeneratesCorrectNestedKeys()
    {
        var handler = CreateHandler(new CoreOptions("Root", typeof(NestedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var root = sections.Single();
        var child = root.Sections.Single();
        var childProp = child.Fields.Single();

        root.Key.Should().Be("Root");
        child.Key.Should().Be("Root:Child");
        childProp.Key.Should().Be("Root:Child:ChildProp");
    }

    // ── Values from IConfiguration ────────────────────────────────────────────

    [Fact]
    public async Task Handle_FillsValueFromAppConfiguration()
    {
        _appConfig["Simple:Name"].Returns("Alice");
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Fields
            .Single(f => f.Key == "Simple:Name").Value
            .Should().Be("Alice");
    }

    [Fact]
    public async Task Handle_MissingKeyInAppConfiguration_ValueIsNull()
    {
        _appConfig["Simple:Name"].Returns((string?)null);
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        sections.Single().Fields
            .Single(f => f.Key == "Simple:Name").Value
            .Should().BeNull();
    }

    // ── Display names ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutDisplayAttribute_UsesPropNameAsLabel()
    {
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var field = sections.Single().Fields.Single(f => f.Key == "Simple:Name");
        field.Name.Should().Be("Name");
    }

    [Fact]
    public async Task Handle_WithDisplayNameOnProperty_UsesDisplayName()
    {
        var handler = CreateHandler(new CoreOptions("Ann", typeof(AnnotatedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var field = sections.Single().Fields.Single(f => f.Key == "Ann:Field");
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

    // ── Descriptions ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithDisplayDescriptionOnProperty_PopulatesFieldDescription()
    {
        var handler = CreateHandler(new CoreOptions("Ann", typeof(AnnotatedOptions)));

        var sections = await handler.HandleAsync(new GetConfiguration(), default);

        var field = sections.Single().Fields.Single(f => f.Key == "Ann:Field");
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

    // ── Field types ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_StringProperty_MapsToStringType()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        sections.Single().Fields.Single(f => f.Key == "T:Label").Type
            .Should().Be(Kododo.ConfigWay.UI.DTO.FieldType.String);
    }

    [Fact]
    public async Task Handle_BoolProperty_MapsToBoolType()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        sections.Single().Fields.Single(f => f.Key == "T:Enabled").Type
            .Should().Be(Kododo.ConfigWay.UI.DTO.FieldType.Bool);
    }

    [Fact]
    public async Task Handle_NullableBoolProperty_MapsToBoolType()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        sections.Single().Fields.Single(f => f.Key == "T:Optional").Type
            .Should().Be(Kododo.ConfigWay.UI.DTO.FieldType.Bool);
    }

    [Fact]
    public async Task Handle_IntProperty_MapsToNumberType()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        sections.Single().Fields.Single(f => f.Key == "T:Count").Type
            .Should().Be(Kododo.ConfigWay.UI.DTO.FieldType.Number);
    }

    [Fact]
    public async Task Handle_EnumProperty_MapsToEnumType()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        sections.Single().Fields.Single(f => f.Key == "T:Level").Type
            .Should().Be(Kododo.ConfigWay.UI.DTO.FieldType.Enum);
    }

    [Fact]
    public async Task Handle_NonEnumFields_HaveNullOptions()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        sections.Single().Fields
            .Where(f => f.Key != "T:Level")
            .Should().AllSatisfy(f => f.Options.Should().BeNull());
    }

    [Fact]
    public async Task Handle_EnumField_PopulatesAllMemberValues()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        var options = sections.Single().Fields.Single(f => f.Key == "T:Level").Options;
        options.Should().NotBeNull();
        options!.Select(o => o.Value).Should().BeEquivalentTo("Low", "Medium", "High");
    }

    [Fact]
    public async Task Handle_EnumField_DisplayAttributeOnMember_UsesDisplayName()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        var options = sections.Single().Fields.Single(f => f.Key == "T:Level").Options!;
        options.Single(o => o.Value == "Low").Label.Should().Be("Low priority");
        options.Single(o => o.Value == "High").Label.Should().Be("High priority");
    }

    [Fact]
    public async Task Handle_EnumField_MemberWithoutDisplayAttribute_UsesMemberName()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var sections = await handler.HandleAsync(new GetConfiguration(), default);
        var options = sections.Single().Fields.Single(f => f.Key == "T:Level").Options!;
        options.Single(o => o.Value == "Medium").Label.Should().Be("Medium");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private GetConfigurationHandler CreateHandler(params CoreOptions[] options)
    {
        var config = new Configuration(_store, options);

        return new GetConfigurationHandler(config, _appConfig);
    }
}