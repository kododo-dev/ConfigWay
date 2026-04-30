using Microsoft.Extensions.Options;
using Kododo.ConfigWay;
using Kododo.ConfigWay.Demo.Web;
using Kododo.ConfigWay.UI;
using Kododo.ConfigWay.PostgreSQL;

var builder = WebApplication.CreateBuilder(args);

builder.AddConfigWay(x =>
{
    x.AddOptions<BrandingOptions>("Branding");
    x.AddOptions<SmtpOptions>("Smtp");
    x.AddOptions<IdentityOptions>("Identity");
    x.AddOptions<StorageOptions>("Storage");
    x.AddOptions<WebhooksOptions>("Webhooks");
    x.AddOptions<FeatureFlags>("FeatureFlags");
    x.AddUiEditor();
    x.UsePostgreSql(builder.Configuration.GetConnectionString("DemoDB")!);
});

builder.Services.AddSingleton<IValidateOptions<SmtpOptions>, SmtpOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<IdentityOptions>, IdentityOptionsValidator>();

var app = builder.Build();

var pathBase = builder.Configuration["PathBase"] ?? "/";
if(!pathBase.StartsWith("/")) pathBase = "/" + pathBase;
if(!pathBase.EndsWith("/")) pathBase += "/";

app.MapGet("/", (
    IOptionsSnapshot<BrandingOptions> branding,
    IOptionsSnapshot<SmtpOptions> smtp,
    IOptionsSnapshot<IdentityOptions> identity,
    IOptionsSnapshot<StorageOptions> storage,
    IOptionsSnapshot<WebhooksOptions> webhooks,
    IOptionsSnapshot<FeatureFlags> flags) =>
{
    var html = HomeView.Render(
        pathBase,
        branding.Value, smtp.Value, identity.Value,
        storage.Value, webhooks.Value, flags.Value);

    return Results.Content(html, "text/html");
});

app.UseConfigWay();

app.Run();
