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
    x.AddOptions<AppOptions>();
    x.AddOptions<EmailOptions>();
    x.AddUiEditor();
    x.UsePostgreSql(builder.Configuration.GetConnectionString("DemoDB")!);
});

builder.Services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World!");
app.MapGet("/email", (IOptionsSnapshot<EmailOptions> options) => options.Value);

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

app.MapGroup("/internal")
    .UseConfigWay();

app.Run();