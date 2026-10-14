namespace Web.Features.Accounts;

// Doc tu User Secrets (dev) hoac bien moi truong (prod):
//   dotnet user-secrets set "AdminSeed:Email" "<email>" --project NhaTro/Web
//   dotnet user-secrets set "AdminSeed:Password" "<mat-khau-manh>" --project NhaTro/Web
// Prod: dat bien moi truong AdminSeed__Email / AdminSeed__Password.
public sealed class AdminSeedOptions
{
    public string? Email { get; set; }
    public string? Password { get; set; }
}
