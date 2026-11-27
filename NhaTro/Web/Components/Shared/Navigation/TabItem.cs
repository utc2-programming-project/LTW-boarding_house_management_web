namespace Web.Components.Shared.Navigation;

/// <summary>Một mục tab: có Href là link (static SSR), không có là nút (interactive).</summary>
public sealed record TabItem(
    string Key,
    string Label,
    string? Href = null,
    string? Icon = null,
    bool Disabled = false,
    string? Id = null,
    string? Controls = null);
