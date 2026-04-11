using Microsoft.Extensions.Options;

namespace Kododo.ConfigWay.Demo.Web;

public class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        if (string.IsNullOrEmpty(options.SmtpServer) && !string.IsNullOrEmpty(options.SenderEmail))
        {
            return ValidateOptionsResult
                .Fail(new[] { "SmtpServer is required if SenderEmail is provided.", "Sample exception message" });
        }
        
        return ValidateOptionsResult.Success;
    }
}

public class EmailOptions
{
    public string SmtpServer { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public Credentials Credentials { get; set; } = new();
}

public class Credentials
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AppOptions
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    
    public EmailOptions EmailOptions { get; set; } = new();
    public EmailOptions EmailOptions2 { get; set; } = new();
    public EmailOptions EmailOptions3 { get; set; } = new();
    public EmailOptions EmailOptions4 { get; set; } = new();
}