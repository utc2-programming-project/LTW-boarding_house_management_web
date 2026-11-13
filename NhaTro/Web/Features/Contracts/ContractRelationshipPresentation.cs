using Microsoft.Extensions.Localization;

namespace Web.Features.Contracts;

/// <summary>Mã quan hệ lưu trong DB (Partner/Child/…) → nhãn đa ngôn ngữ.</summary>
public static class ContractRelationshipPresentation
{
    public static string LabelKey(string? code) => code switch
    {
        "Partner" => "ContractRelationshipPartner",
        "Child" => "ContractRelationshipChild",
        "Relative" => "ContractRelationshipRelative",
        "Colleague" => "ContractRelationshipColleague",
        "Other" => "ContractRelationshipOther",
        _ => string.Empty,
    };

    public static string Format(IStringLocalizer localizer, string? code) =>
        string.IsNullOrWhiteSpace(code) ? string.Empty : localizer[LabelKey(code)];
}
