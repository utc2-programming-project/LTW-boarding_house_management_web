using Microsoft.Extensions.Localization;
using Web.Features.Utilities;

namespace NhaTro.IntegrationTests.Infrastructure;

/// <summary>Localizer that returns the resource key, so tests assert on keys instead of translated text.</summary>
public sealed class UtilityKeyLocalizer : IStringLocalizer<UtilityResources>
{
    public LocalizedString this[string name] => new(name, name);

    public LocalizedString this[string name, params object[] arguments] => new(name, name);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}
