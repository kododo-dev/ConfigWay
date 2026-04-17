using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Kododo.ConfigWay.Demo.Web;

// NOTE: ConfigWay currently surfaces only `string` properties as editable fields
// (see Kododo.ConfigWay.ConfigurationProvider.IsLeaf). Values that semantically
// represent bools/ints/URIs are therefore modeled as strings and parsed by the
// consuming subsystem. Validators below demonstrate how to enforce those
// contracts at save time without requiring the library to understand new types.

// ── Branding / public contact ────────────────────────────────────────────────

[Display(
    Name = "Branding",
    Description = "Public-facing product metadata: used on invoices, transactional emails and the marketing site.")]
public class BrandingOptions
{
    [Display(Name = "Company Name", Description = "Legal name of the operating entity.")]
    public string CompanyName { get; set; } = string.Empty;

    [Display(Name = "Support Email", Description = "Address advertised to end users for support requests.")]
    public string SupportEmail { get; set; } = string.Empty;

    [Display(Name = "Support Phone", Description = "E.164-formatted phone number shown in the help center.")]
    public string SupportPhone { get; set; } = string.Empty;

    [Display(Name = "Marketing Site Url", Description = "Canonical URL of the marketing site (https://...).")]
    public string MarketingSiteUrl { get; set; } = string.Empty;
}

// ── SMTP ─────────────────────────────────────────────────────────────────────

public class SmtpCredentials
{
    [Display(Name = "Username", Description = "SMTP account login.")]
    public string Username { get; set; } = string.Empty;

    [Display(
        Name = "Password",
        Description = "SMTP account password. In production, inject via a secret manager and leave this blank.")]
    public string Password { get; set; } = string.Empty;
}

[Display(
    Name = "Smtp",
    Description = "Outbound SMTP relay used for transactional email (receipts, alerts, password resets).")]
public class SmtpOptions
{
    [Display(Name = "Host", Description = "Hostname of the SMTP relay, e.g. 'smtp.sendgrid.net'.")]
    public string Host { get; set; } = string.Empty;

    [Display(Name = "Port", Description = "TCP port. Common values: 25, 465, 587.")]
    public string Port { get; set; } = "587";

    [Display(Name = "Use Ssl", Description = "Whether STARTTLS/SSL should be negotiated. Accepts 'true' or 'false'.")]
    public string UseSsl { get; set; } = "true";

    [Display(Name = "From Address", Description = "Envelope sender address (RFC 5321 MAIL FROM).")]
    public string FromAddress { get; set; } = string.Empty;

    [Display(Name = "From Name", Description = "Display name attached to the From header.")]
    public string FromName { get; set; } = string.Empty;

    [Display(Name = "Credentials", Description = "Authentication credentials for the SMTP relay.")]
    public SmtpCredentials Credentials { get; set; } = new();
}

public class SmtpOptionsValidator : IValidateOptions<SmtpOptions>
{
    public ValidateOptionsResult Validate(string? name, SmtpOptions options)
    {
        var failures = new List<string>();

        if (!string.IsNullOrEmpty(options.FromAddress) && string.IsNullOrEmpty(options.Host))
            failures.Add("Smtp.Host is required when Smtp.FromAddress is configured.");

        if (!string.IsNullOrEmpty(options.Port))
        {
            if (!int.TryParse(options.Port, out var port))
                failures.Add($"Smtp.Port '{options.Port}' is not a valid integer.");
            else if (port is <= 0 or > 65535)
                failures.Add($"Smtp.Port must be between 1 and 65535 (got {port}).");
        }

        if (!string.IsNullOrEmpty(options.UseSsl) && options.UseSsl is not ("true" or "false"))
            failures.Add("Smtp.UseSsl must be 'true' or 'false'.");

        if (!string.IsNullOrEmpty(options.FromAddress) && !options.FromAddress.Contains('@'))
            failures.Add($"Smtp.FromAddress '{options.FromAddress}' is not a valid email address.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

// ── Identity / OpenID Connect ────────────────────────────────────────────────

[Display(
    Name = "Identity",
    Description = "OpenID Connect / OAuth2 settings for authenticating end users against the corporate IdP.")]
public class IdentityOptions
{
    [Display(Name = "Authority", Description = "OIDC authority (issuer) URL, e.g. 'https://login.example.com'.")]
    public string Authority { get; set; } = string.Empty;

    [Display(Name = "Audience", Description = "Expected 'aud' claim on incoming access tokens.")]
    public string Audience { get; set; } = string.Empty;

    [Display(Name = "Client Id", Description = "OIDC client identifier registered with the IdP.")]
    public string ClientId { get; set; } = string.Empty;

    [Display(Name = "Client Secret", Description = "OIDC client secret. Rotate at least every 90 days.")]
    public string ClientSecret { get; set; } = string.Empty;

    [Display(Name = "Access Token Lifetime (minutes)", Description = "Target access token lifetime in minutes.")]
    public string AccessTokenLifetimeMinutes { get; set; } = "60";

    [Display(Name = "Require Https Metadata", Description = "'true' in every environment except local dev.")]
    public string RequireHttpsMetadata { get; set; } = "true";
}

public class IdentityOptionsValidator : IValidateOptions<IdentityOptions>
{
    public ValidateOptionsResult Validate(string? name, IdentityOptions options)
    {
        var failures = new List<string>();

        if (!string.IsNullOrEmpty(options.Authority)
            && !Uri.TryCreate(options.Authority, UriKind.Absolute, out _))
        {
            failures.Add($"Identity.Authority '{options.Authority}' is not a valid absolute URI.");
        }

        if (!string.IsNullOrEmpty(options.AccessTokenLifetimeMinutes)
            && (!int.TryParse(options.AccessTokenLifetimeMinutes, out var lifetime) || lifetime <= 0))
        {
            failures.Add("Identity.AccessTokenLifetimeMinutes must be a positive integer.");
        }

        if (!string.IsNullOrEmpty(options.RequireHttpsMetadata)
            && options.RequireHttpsMetadata is not ("true" or "false"))
        {
            failures.Add("Identity.RequireHttpsMetadata must be 'true' or 'false'.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

// ── Object storage (S3-compatible) ───────────────────────────────────────────

public class StorageCredentials
{
    [Display(Name = "Access Key Id", Description = "IAM access key identifier.")]
    public string AccessKeyId { get; set; } = string.Empty;

    [Display(Name = "Secret Access Key", Description = "IAM secret; prefer IRSA/managed identities in production.")]
    public string SecretAccessKey { get; set; } = string.Empty;
}

[Display(
    Name = "Storage",
    Description = "Object storage backend used for user uploads, backups and static assets.")]
public class StorageOptions
{
    [Display(Name = "Provider", Description = "Storage provider identifier. Supported: 's3', 'azure-blob', 'gcs'.")]
    public string Provider { get; set; } = "s3";

    [Display(Name = "Endpoint", Description = "Service endpoint; leave blank to use the provider default.")]
    public string Endpoint { get; set; } = string.Empty;

    [Display(Name = "Region", Description = "Region/location code, e.g. 'eu-central-1'.")]
    public string Region { get; set; } = string.Empty;

    [Display(Name = "Bucket Name", Description = "Bucket/container that holds application assets.")]
    public string BucketName { get; set; } = string.Empty;

    [Display(Name = "Presigned Url Ttl (seconds)", Description = "Default lifetime of generated presigned URLs.")]
    public string PresignedUrlTtlSeconds { get; set; } = "900";

    [Display(Name = "Credentials", Description = "Access credentials for the storage backend.")]
    public StorageCredentials Credentials { get; set; } = new();
}

// ── Feature flags ────────────────────────────────────────────────────────────

[Display(
    Name = "FeatureFlags",
    Description = "Runtime toggles for shipping work-in-progress behavior. Values must be 'true' or 'false'.")]
public class FeatureFlags
{
    [Display(Name = "Enable New Checkout", Description = "Redirects users to the redesigned checkout flow.")]
    public string EnableNewCheckout { get; set; } = "false";

    [Display(Name = "Enable Beta Banner", Description = "Shows the 'beta' banner on top of the SPA.")]
    public string EnableBetaBanner { get; set; } = "false";

    [Display(Name = "Maintenance Mode", Description = "Short-circuits the site into a maintenance page.")]
    public string MaintenanceMode { get; set; } = "false";

    [Display(Name = "Read-Only Mode", Description = "Disables writes while keeping reads available.")]
    public string ReadOnlyMode { get; set; } = "false";
}
