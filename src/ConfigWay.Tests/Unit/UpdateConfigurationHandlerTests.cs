using FluentAssertions;
using Kododo.ConfigWay.Core.Configuration;
using Kododo.ConfigWay.Core.Model;
using Kododo.ConfigWay.Core.Store;
using Kododo.ConfigWay.UI.API.UpdateConfiguration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using CoreOptions = Kododo.ConfigWay.Core.Configuration.Options;

namespace Kododo.ConfigWay.Tests.Unit;

public class UpdateConfigurationHandlerTests
{
    private readonly IStore _store = Substitute.For<IStore>();
    private readonly IConfigurationEditor _editor = Substitute.For<IConfigurationEditor>();
    private readonly IConfiguration _appConfig = Substitute.For<IConfiguration>();
    private ServiceCollection _serviceCollection = new ServiceCollection();
    private ServiceProvider? _serviceProvider;

    public UpdateConfigurationHandlerTests()
    {
        // By default, no validators registered.
        BuildServiceProvider();
    }

    // ── HandleAsync — happy path ───────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ValidSettings_ReturnsEmptyErrors()
    {
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));
        var settings = new[] { new Setting("Simple:Name", "Alice") };

        var errors = await handler.HandleAsync(new UpdateConfiguration(settings), default);

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ValidSettings_PersistsToStore()
    {
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));
        var settings = new[] { new Setting("Simple:Name", "Alice") };

        await handler.HandleAsync(new UpdateConfiguration(settings), default);

        await _store.Received(1).SetAsync(
            Arg.Is<IReadOnlyCollection<Setting>>(s => s.Any(x => x.Key == "Simple:Name")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ValidSettings_TriggersReload()
    {
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));
        var settings = new[] { new Setting("Simple:Name", "Alice") };

        await handler.HandleAsync(new UpdateConfiguration(settings), default);

        await _editor.Received(1).ReloadAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NullValue_Accepted()
    {
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));
        var settings = new[] { new Setting("Simple:Name", null) };

        var errors = await handler.HandleAsync(new UpdateConfiguration(settings), default);

        errors.Should().BeEmpty();
    }

    // ── Validation failures ───────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_WhenValidatorFails_ReturnsErrors()
    {
        RegisterValidator(new AlwaysFailValidator());
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));
        var settings = new[] { new Setting("Simple:Name", "X") };

        var errors = await handler.HandleAsync(new UpdateConfiguration(settings), default);

        errors.Should().ContainSingle(e => e == AlwaysFailValidator.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_WhenValidatorFails_DoesNotPersistToStore()
    {
        RegisterValidator(new AlwaysFailValidator());
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));
        var settings = new[] { new Setting("Simple:Name", "X") };

        await handler.HandleAsync(new UpdateConfiguration(settings), default);

        await _store.DidNotReceive().SetAsync(Arg.Any<IReadOnlyCollection<Setting>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenValidatorFails_DoesNotTriggerReload()
    {
        RegisterValidator(new AlwaysFailValidator());
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));
        var settings = new[] { new Setting("Simple:Name", "X") };

        await handler.HandleAsync(new UpdateConfiguration(settings), default);

        await _editor.DidNotReceive().ReloadAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ConditionalValidator_FailsWhenNameEmpty()
    {
        RegisterValidator(new ConditionalValidator());
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));
        var settings = new[] { new Setting("Simple:Name", "") };

        var errors = await handler.HandleAsync(new UpdateConfiguration(settings), default);

        errors.Should().ContainSingle(e => e == ConditionalValidator.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_ConditionalValidator_PassesWhenNameProvided()
    {
        RegisterValidator(new ConditionalValidator());
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));
        var settings = new[] { new Setting("Simple:Name", "Alice") };

        var errors = await handler.HandleAsync(new UpdateConfiguration(settings), default);

        errors.Should().BeEmpty();
    }

    // ── Key relevance filtering ───────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_SettingsForDifferentSection_DoesNotRunValidator()
    {
        RegisterValidator(new AlwaysFailValidator());
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));

        // Settings for a completely different section — validator should not be triggered
        var settings = new[] { new Setting("Other:SomeKey", "value") };

        var errors = await handler.HandleAsync(new UpdateConfiguration(settings), default);

        errors.Should().BeEmpty();
    }

    // ── Typed values — bool, number, enum ────────────────────────────────────

    [Theory]
    [InlineData("True")]
    [InlineData("False")]
    [InlineData("true")]
    [InlineData("false")]
    public async Task HandleAsync_BoolValue_Accepted(string value)
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var settings = new[] { new Setting("T:Enabled", value) };

        var errors = await handler.HandleAsync(new UpdateConfiguration(settings), default);

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_InvalidBoolValue_ReturnsError()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var settings = new[] { new Setting("T:Enabled", "yes") };

        var errors = await handler.HandleAsync(new UpdateConfiguration(settings), default);

        errors.Should().ContainSingle(e => e.Contains("T:Enabled"));
    }

    [Fact]
    public async Task HandleAsync_IntValue_Accepted()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var settings = new[] { new Setting("T:Count", "42") };

        var errors = await handler.HandleAsync(new UpdateConfiguration(settings), default);

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_InvalidIntValue_ReturnsError()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var settings = new[] { new Setting("T:Count", "not-a-number") };

        var errors = await handler.HandleAsync(new UpdateConfiguration(settings), default);

        errors.Should().ContainSingle(e => e.Contains("T:Count"));
    }

    [Theory]
    [InlineData("Low")]
    [InlineData("Medium")]
    [InlineData("High")]
    [InlineData("low")]
    [InlineData("HIGH")]
    public async Task HandleAsync_EnumValue_Accepted(string value)
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var settings = new[] { new Setting("T:Level", value) };

        var errors = await handler.HandleAsync(new UpdateConfiguration(settings), default);

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_InvalidEnumValue_ReturnsError()
    {
        var handler = CreateHandler(new CoreOptions("T", typeof(TypedOptions)));
        var settings = new[] { new Setting("T:Level", "Critical") };

        var errors = await handler.HandleAsync(new UpdateConfiguration(settings), default);

        errors.Should().ContainSingle(e => e.Contains("T:Level"));
    }

    // ── Nested properties ─────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NestedProperty_ValidatesCorrectly()
    {
        var handler = CreateHandler(new CoreOptions("Root", typeof(NestedOptions)));
        var settings = new[] { new Setting("Root:Child:ChildProp", "value") };

        var errors = await handler.HandleAsync(new UpdateConfiguration(settings), default);

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_UnknownProperty_ReturnsError()
    {
        var handler = CreateHandler(new CoreOptions("Simple", typeof(SimpleOptions)));
        var settings = new[] { new Setting("Simple:DoesNotExist", "value") };

        var errors = await handler.HandleAsync(new UpdateConfiguration(settings), default);

        errors.Should().ContainSingle(e => e.Contains("Simple:DoesNotExist"));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private UpdateConfigurationHandler CreateHandler(params CoreOptions[] options)
    {
        var config = new Configuration(_store, options);
        return new UpdateConfigurationHandler(config, _editor, _appConfig, _serviceProvider!);
    }

    private void RegisterValidator<T>(IValidateOptions<T> validator) where T : class
    {
        _serviceCollection.AddSingleton(validator);
        BuildServiceProvider();
    }

    private void BuildServiceProvider()
    {
        _serviceProvider = _serviceCollection.BuildServiceProvider();
    }
}