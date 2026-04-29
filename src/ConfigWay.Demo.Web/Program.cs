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

var pathBase = builder.Configuration["ASPNETCORE_PATHBASE"];
if (!string.IsNullOrEmpty(pathBase))
    app.UsePathBase(pathBase);

app.MapGet("/preview/branding", (IOptionsSnapshot<BrandingOptions> opts) => opts.Value);
app.MapGet("/preview/smtp", (IOptionsSnapshot<SmtpOptions> opts) => opts.Value);
app.MapGet("/preview/identity", (IOptionsSnapshot<IdentityOptions> opts) => opts.Value);
app.MapGet("/preview/storage", (IOptionsSnapshot<StorageOptions> opts) => opts.Value);
app.MapGet("/preview/webhooks", (IOptionsSnapshot<WebhooksOptions> opts) => opts.Value);
app.MapGet("/preview/feature-flags", (IOptionsSnapshot<FeatureFlags> opts) => opts.Value);

app.UseConfigWay("/");

app.Run();