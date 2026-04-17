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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private GetConfigurationHandler CreateHandler(params CoreOptions[] options)
    {
        var config = new Configuration(_store, options);

        return new GetConfigurationHandler(config, _appConfig);
    }
}