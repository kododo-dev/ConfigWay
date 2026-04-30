using System.Net;

namespace Kododo.ConfigWay.Demo.Web;

static class HomeView
{
    public static string Render(
      string basePath,
        BrandingOptions b,
        SmtpOptions s,
        IdentityOptions id,
        StorageOptions st,
        WebhooksOptions wh,
        FeatureFlags ff)
    {
        static string Flag(bool on) => on
            ? "<span style='color:#16a34a;font-weight:600'>✓ on</span>"
            : "<span style='color:#9ca3af'>✗ off</span>";

        static string Row(string label, string value) =>
            $"<tr><td style='color:#6b7280;padding:4px 12px 4px 0;white-space:nowrap'>{label}</td><td style='font-weight:500'>{WebUtility.HtmlEncode(value)}</td></tr>";

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="UTF-8"/>
              <meta name="viewport" content="width=device-width,initial-scale=1"/>
              <title>ConfigWay Demo</title>
              <style>
                *{box-sizing:border-box;margin:0;padding:0}
                body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;background:#f9fafb;color:#111827;min-height:100vh}
                .hero{background:#fff;border-bottom:1px solid #e5e7eb;padding:48px 24px 40px;text-align:center}
                .hero h1{font-size:2rem;font-weight:700;margin-bottom:12px}
                .hero p{color:#6b7280;font-size:1.05rem;max-width:560px;margin:0 auto 28px}
                .btn{display:inline-flex;align-items:center;gap:8px;background:#2563eb;color:#fff;font-weight:600;font-size:1rem;padding:13px 28px;border-radius:8px;text-decoration:none;transition:background .15s}
                .btn:hover{background:#1d4ed8}
                .btn svg{flex-shrink:0}
                .note{margin-top:14px;font-size:.85rem;color:#9ca3af}
                .grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(320px,1fr));gap:20px;padding:32px 24px;max-width:1200px;margin:0 auto}
                .card{background:#fff;border:1px solid #e5e7eb;border-radius:10px;padding:20px 22px}
                .card h2{font-size:.85rem;font-weight:600;text-transform:uppercase;letter-spacing:.06em;color:#6b7280;margin-bottom:14px}
                table{width:100%;border-collapse:collapse;font-size:.9rem}
                .footer{text-align:center;padding:24px;color:#9ca3af;font-size:.82rem;border-top:1px solid #e5e7eb;margin-top:8px}
              </style>
            </head>
            <body>
              <div class="hero">
                <h1>ConfigWay Demo</h1>
                <p>This app demonstrates ConfigWay — a runtime configuration editor for ASP.NET Core.
                   All settings below are live and can be changed without restarting the application.</p>
                <a class="btn" href="{{basePath}}config">
                  <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                    <path d="M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z"/>
                    <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/>
                  </svg>
                  Open Configuration Editor
                </a>
                <p class="note">Changes take effect immediately — no restart needed.</p>
              </div>

              <div class="grid">
                <div class="card">
                  <h2>Branding</h2>
                  <table>
                    {{Row("Company", b.CompanyName)}}
                    {{Row("Support email", b.SupportEmail)}}
                    {{Row("Support phone", b.SupportPhone)}}
                    {{Row("Marketing site", b.MarketingSiteUrl)}}
                  </table>
                </div>

                <div class="card">
                  <h2>SMTP</h2>
                  <table>
                    {{Row("Host", s.Host)}}
                    {{Row("Port", s.Port.ToString())}}
                    {{Row("SSL", s.UseSsl ? "yes" : "no")}}
                    {{Row("From address", s.FromAddress)}}
                    {{Row("From name", s.FromName)}}
                  </table>
                </div>

                <div class="card">
                  <h2>Identity</h2>
                  <table>
                    {{Row("Authority", id.Authority)}}
                    {{Row("Audience", id.Audience)}}
                    {{Row("Client ID", id.ClientId)}}
                    {{Row("Token lifetime", $"{id.AccessTokenLifetimeMinutes} min")}}
                    {{Row("Require HTTPS", id.RequireHttpsMetadata ? "yes" : "no")}}
                  </table>
                </div>

                <div class="card">
                  <h2>Storage</h2>
                  <table>
                    {{Row("Provider", st.Provider.ToString())}}
                    {{Row("Region", st.Region)}}
                    {{Row("Bucket", st.BucketName)}}
                    {{Row("Presigned URL TTL", $"{st.PresignedUrlTtlSeconds} s")}}
                  </table>
                </div>

                <div class="card">
                  <h2>Webhooks</h2>
                  <table>
                    {{Row("Endpoints", wh.Endpoints.Length.ToString())}}
                    {{Row("Allowed origins", wh.AllowedOrigins.Length.ToString())}}
                    {{Row("Max retries", wh.MaxRetries.ToString())}}
                  </table>
                </div>

                <div class="card">
                  <h2>Feature Flags</h2>
                  <table>
                    <tr><td style='color:#6b7280;padding:4px 12px 4px 0'>New Checkout</td><td>{{Flag(ff.EnableNewCheckout)}}</td></tr>
                    <tr><td style='color:#6b7280;padding:4px 12px 4px 0'>Beta Banner</td><td>{{Flag(ff.EnableBetaBanner)}}</td></tr>
                    <tr><td style='color:#6b7280;padding:4px 12px 4px 0'>Maintenance Mode</td><td>{{Flag(ff.MaintenanceMode)}}</td></tr>
                    <tr><td style='color:#6b7280;padding:4px 12px 4px 0'>Read-Only Mode</td><td>{{Flag(ff.ReadOnlyMode)}}</td></tr>
                  </table>
                </div>
              </div>

              <div class="footer">ConfigWay Demo · settings are read live via IOptionsSnapshot</div>
            </body>
            </html>
            """;
    }
}
