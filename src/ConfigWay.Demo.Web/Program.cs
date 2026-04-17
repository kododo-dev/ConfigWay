using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Kododo.ConfigWay;
using Kododo.ConfigWay.Demo.Web;
using Kododo.ConfigWay.UI;
using Kododo.ConfigWay.PostgreSQL;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.AddConfigWay(x =>
{
    x.AddOptions<BrandingOptions>("Branding");
    x.AddOptions<SmtpOptions>("Smtp");
    x.AddOptions<IdentityOptions>("Identity");
    x.AddOptions<StorageOptions>("Storage");
    x.AddOptions<FeatureFlags>("FeatureFlags");
    x.AddUiEditor();
    x.UsePostgreSql(builder.Configuration.GetConnectionString("DemoDB")!);
});

builder.Services.AddSingleton<IValidateOptions<SmtpOptions>, SmtpOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<IdentityOptions>, IdentityOptionsValidator>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World!");

// Expose current live values of every section so you can verify that
// "Save" in the ConfigWay UI is reflected without restarting the app.
// These live under /preview to avoid colliding with UseConfigWay() mounted at /config.
app.MapGet("/preview/branding", (IOptionsSnapshot<BrandingOptions> opts) => opts.Value);
app.MapGet("/preview/smtp", (IOptionsSnapshot<SmtpOptions> opts) => opts.Value);
app.MapGet("/preview/identity", (IOptionsSnapshot<IdentityOptions> opts) => opts.Value);
app.MapGet("/preview/storage", (IOptionsSnapshot<StorageOptions> opts) => opts.Value);
app.MapGet("/preview/feature-flags", (IOptionsSnapshot<FeatureFlags> opts) => opts.Value);

app.MapGet("/login", async (HttpContext ctx) =>
{
    var claims = new[] { new Claim(ClaimTypes.Name, "admin"), new Claim(ClaimTypes.Role, "Admin") };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(new ClaimsPrincipal(identity));
    return Results.Ok("Logged in as admin");
});

app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync();
    return Results.Ok("Logged out");
});

app.UseConfigWay()
    .RequireAuthorization(policy => policy.RequireRole("Admin"));

app.MapGroup("/public")
    .UseConfigWay();

app.Run();