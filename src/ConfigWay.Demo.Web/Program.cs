using Microsoft.Extensions.Options;
using Kododo.ConfigWay;
using Kododo.ConfigWay.Demo.Web;
using Kododo.ConfigWay.UI;
using Kododo.ConfigWay.PostgreSQL;
using Kododo.ConfigWay.FeatureManagement;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

var builder = WebApplication.CreateBuilder(args);

builder.AddConfigWay(x =>
{
    x.AddOptions<BrandingOptions>("Branding");
    x.AddOptions<SmtpOptions>("Smtp");
    x.AddOptions<IdentityOptions>("Identity");
    x.AddOptions<StorageOptions>("Storage");
    x.AddOptions<WebhooksOptions>("Webhooks");
    x.AddUiEditor();
    x.AddFeatureFlags();
    x.UsePostgreSql(builder.Configuration.GetConnectionString("DemoDB")!);
});

builder.Services.AddSingleton<IValidateOptions<SmtpOptions>, SmtpOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<IdentityOptions>, IdentityOptionsValidator>();
builder.Services.AddFeatureManagement();

var app = builder.Build();

var pathBase = builder.Configuration["PathBase"] ?? "/";
if(!pathBase.StartsWith("/")) pathBase = "/" + pathBase;
if(!pathBase.EndsWith("/")) pathBase += "/";

app.MapGet("/", async (
    string? user,
    IOptionsSnapshot<BrandingOptions> branding,
    IOptionsSnapshot<SmtpOptions> smtp,
    IOptionsSnapshot<IdentityOptions> identity,
    IOptionsSnapshot<StorageOptions> storage,
    IOptionsSnapshot<WebhooksOptions> webhooks,
    IVariantFeatureManager featureManager,
    CancellationToken cancellationToken) =>
{
    // The demo's visitors: pick one with ?user= to see the flags as that user sees them.
    var visitor = HomeView.Visitors.FirstOrDefault(v => v.Id == user) ?? HomeView.Visitors[0];
    var context = new TargetingContext { UserId = visitor.Id, Groups = visitor.Groups };

    var flags = new List<HomeView.FlagState>();
    await foreach (var name in featureManager.GetFeatureNamesAsync(cancellationToken))
    {
        var enabled = await featureManager.IsEnabledAsync(name, context, cancellationToken);
        var variant = await featureManager.GetVariantAsync(name, context, cancellationToken);
        flags.Add(new HomeView.FlagState(name, enabled, variant?.Name));
    }

    var html = HomeView.Render(
        pathBase,
        branding.Value, smtp.Value, identity.Value,
        storage.Value, webhooks.Value,
        visitor, flags.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToArray());

    return Results.Content(html, "text/html");
});

// The public demo: anyone may edit its settings.
app.UseConfigWay().AllowAnonymous();

app.Run();
