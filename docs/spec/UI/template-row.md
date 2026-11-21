# TEMPLATE ROW — copy/paste

> Dùng kèm [`spec-toan-bo-row.md`](spec-toan-bo-row.md). `= Mxx` nghĩa là lấy mẫu gốc rồi thay/thêm phần dưới.
> `L[...]` = `IStringLocalizer` của trang; thêm key vào **cả vi-VN và en-US**.

## Chỉ có 2 loại tooltip — không trộn

| Loại | Trả lời câu hỏi | Cơ chế | Không dùng cho |
|---|---|---|---|
| **Giải thích** | "Cái này nghĩa là gì / tính thế nào?" | `<HelpTip Text=…>` (nút `?` + bubble) | nội dung đang hiện sẵn trong ô |
| **Đọc đầy đủ** | "Ô bị cắt thì đầy đủ là gì?" | `.hover-tip` + `data-hover-tip` (M03/M04/M05/M06/M14) | giải thích nghiệp vụ, CTA |

`title=""` **không phải** tooltip của dự án: **cấm trên ô dữ liệu** (kể cả mã code — sinh hộp đen của trình duyệt, không theo theme, chồng lên popup). Chỉ còn cho nút/icon hành động và chip có chú thích cực ngắn.

---

## M00 · Hạ tầng — dán 1 lần vào `wwwroot/app.css`

```css
/* Cạnh .cell-link.is-code (dòng ~1056) */
.cell-truncate {
    display: block;
    max-width: 100%;
    min-inline-size: 0;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

/* Xếp 2 dòng trong 1 ô */
.cell-stack {
    display: flex;
    flex-direction: column;
    min-inline-size: 0;
    line-height: 1.35;
}

/* Chép NGUYÊN khối .hover-tip (49-120) từ RoomList.razor.css sang đây.
   Đang là CSS scoped → component khác render .hover-tip sẽ không ăn style. */
```

---

## M01 · Tên + avatar + mã phụ

```csharp
new()
{
    Key = "name",
    Title = L["TenantColumnName"],
    Template = item => @<div class="tenant-name-cell">
        <span class="tenant-avatar" aria-hidden="true">@Initial(item.FullName)</span>
        <span class="tenant-name-text">
            <button type="button" class="tenant-name-button" @onclick="() => OpenDetailsAsync(item.Id)">@item.FullName</button>
            <small>@string.Format(L["TenantRecordNumber"], item.Id)</small>
            @if (item.HasPendingApproval)
            {
                <StatusBadge Text="@L["P2ApprovalPending"]" Tone="@BadgeTone.Warning" />
            }
        </span>
    </div>,
},
```

```css
/* .tenant-name-cell/.tenant-avatar/.tenant-name-text đang scoped trong
   Features/Tenants/Pages/TenantList.razor.css — dùng nơi khác thì chép sang app.css. */
```

---

## M02 · Mã / link cắt sẵn

```csharp
new()
{
    Key = "code",
    Title = L["ContractNumberLabel"],
    Template = item => @<a class="cell-link is-code" href="@AppLinks.Contract(item.Id)" data-hover-tip>
        @item.ContractNumber
        <span class="hover-tip" role="tooltip" aria-hidden="true">
            <span class="hover-tip__line">@item.ContractNumber</span>
        </span>
    </a>,
    Width = "180px",   // bắt buộc: thiếu Width thì table-layout không fixed, ellipsis vô hiệu
},
```

> Mã dài: `is-code` giữ 1 dòng, popup đọc đầy đủ. **Không** đặt `title` — `title` sinh hộp đen của
> trình duyệt, chồng lên popup dự án và không theo dark mode.

---

## M03 · Văn bản dài → 1 dòng + popup mini

```csharp
new()
{
    Key = "address",
    Title = L["P2Address"],
    Template = item => @<span class="cell-truncate" data-hover-tip aria-describedby="tip-@item.Id">
        @item.Address
        <span class="hover-tip" id="tip-@item.Id" role="tooltip">
            <span class="hover-tip__line">@item.Address</span>
        </span>
    </span>,
    Width = "260px",
},
```

```razor
@* Popup chỉ lặp lại đúng nội dung ô → aria-hidden trên popup, ô vẫn được đọc *@
<span class="cell-truncate" data-hover-tip>
    @item.Reason
    <span class="hover-tip" role="tooltip" aria-hidden="true">
        <span class="hover-tip__line">@item.Reason</span>
    </span>
</span>
```

---

## M04 · Chip giải thích (popup 4 tầng) — `= M03 + __title/__meta/__cta`

```razor
<a class="p2-approval-chip" href="@ApprovalQueueHref(item)" data-hover-tip
   aria-describedby="tip-@item.Id">
    <LucideIcon Name="history" />
    <span>@L["P2ApprovalPending"]</span>
    <span class="hover-tip" id="tip-@item.Id" role="tooltip">
        <span class="hover-tip__title">
            <LucideIcon Name="history" />
            <span>@L["P2ApprovalPendingTipTitle"]</span>
        </span>
        <span class="hover-tip__line">@L["P2ApprovalPendingTipWhat", OperationName(item.OperationCode), item.Code]</span>
        <span class="hover-tip__meta">
            <span><LucideIcon Name="user" />@RequesterName(item)</span>
            <span class="tabular-nums"><LucideIcon Name="calendar" />@DateFormatter.FormatDateTime(item.RequestedAt)</span>
        </span>
        <span class="hover-tip__cta">
            <span>@(_isOwner ? L["P2ApprovalPendingTipCtaOwner"] : L["P2ApprovalPendingTipCtaManager"])</span>
            <LucideIcon Name="arrow-right" />
        </span>
    </span>
</a>
```

---

## M05 · Hai dòng trong một ô — `= M03 + .cell-stack`

```razor
<div class="cell-stack" data-hover-tip aria-describedby="tip-@item.Id">
    <span class="cell-truncate">@item.RoomCode</span>
    <small class="cell-truncate muted">@item.PropertyName · @item.Address</small>
    <span class="hover-tip" id="tip-@item.Id" role="tooltip">
        <span class="hover-tip__line">@item.RoomCode</span>
        <span class="hover-tip__line">@item.PropertyName</span>
        <span class="hover-tip__line">@item.Address</span>
    </span>
</div>
```

---

## M06 · Danh sách tên — `= M03 + tách dòng`

```razor
@if (item.TenantNames.Count == 0)
{
    <span class="muted">@L["NoTenants"]</span>
}
else
{
    <span class="cell-truncate" data-hover-tip aria-describedby="tip-@item.Id">
        @string.Join(", ", item.TenantNames)
        <span class="hover-tip" id="tip-@item.Id" role="tooltip">
            @foreach (var name in item.TenantNames)
            {
                <span class="hover-tip__line">@name</span>
            }
        </span>
    </span>
}
```

---

## M07 · Số / tiền / ngày

```csharp
new() { Key = "total",  Title = L["InvoiceTotal"],  Value = i => MoneyFormatter.Format(i.TotalAmount), IsNumeric = true, Sortable = true },
new() { Key = "due",    Title = L["DueDate"],       Value = i => DateFormatter.Format(i.DueDate),      IsNumeric = true },
new() { Key = "area",   Title = L["P2Area"],        Value = i => NumberFormatter.Trimmed(i.Area),      IsNumeric = true },
```

```razor
@* Bảng <table> thuần *@
<td class="text-right tabular-nums">@MoneyFormatter.Format(item.Amount)</td>
<td class="text-right tabular-nums">@(item.DueDate is null ? "—" : DateFormatter.Format(item.DueDate))</td>
```

---

## M08 · Trạng thái

```razor
@* Neutral | Success | Warning | Error | Info — Text bắt buộc, không dùng màu thay chữ *@
<StatusBadge Text="@(item.IsActive ? L["P2Active"] : L["P2Inactive"])"
             Tone="@(item.IsActive ? BadgeTone.Success : BadgeTone.Neutral)" />

<StatusBadge Text="@L[StatusKey(item.Status)]" Tone="@StatusTone(item.Status)" />
```

---

## M09 · Dữ liệu che (D9) — `= M03 + rule`

```razor
@* .tenant-id-mask đang scoped trong TenantList.razor.css — dùng nơi khác thì chép sang app.css *@
<span class="tenant-id-mask" title="@L["TenantIdMaskedHint"]">@(string.IsNullOrWhiteSpace(item.IdNumberMasked) ? L["NoData"] : item.IdNumberMasked)</span>
```

---

## M10 · Menu ⋯ + phân quyền

```razor
<div class="p2-row-actions">
    <button type="button" class="p2-menu-trigger @(IsMenuOpen(item.Id) ? "is-open" : null)"
            title="@L["P2Actions"]" aria-label="@($"{L["P2Actions"]}: {item.FullName}")"
            aria-haspopup="menu" aria-expanded="@(IsMenuOpen(item.Id) ? "true" : "false")"
            aria-controls="row-menu-@item.Id"
            @onclick="() => ToggleRowMenu(item.Id)">
        <LucideIcon Name="ellipsis" />
    </button>
    @if (IsMenuOpen(item.Id))
    {
        <div id="row-menu-@item.Id" class="p2-row-menu" role="menu" aria-label="@L["P2Actions"]" data-row-menu>
            <button class="p2-row-menu-item" type="button" role="menuitem" @onclick="() => MenuDetailsAsync(item)">
                <LucideIcon Name="eye" /><span>@L["P2ViewDetails"]</span>
            </button>
            <button class="p2-row-menu-item" type="button" role="menuitem" @onclick="() => MenuEditAsync(item)">
                <LucideIcon Name="pen" /><span>@L["P2Edit"]</span>
            </button>
            @if (_isOwner)
            {
                <div class="p2-row-menu-sep" role="separator"></div>
                <button class="p2-row-menu-item danger" type="button" role="menuitem" @onclick="() => MenuDeleteAsync(item)">
                    <LucideIcon Name="trash" /><span>@L["TenantDelete"]</span>
                </button>
            }
        </div>
    }
</div>
```

```csharp
new()
{
    Key = "actions",
    Title = L["P2Actions"],
    Template = actions,          // markup ở khối trên
    CellClass = "cell-actions",
    Width = "120px",
},
```

> **Căn cột thao tác phải đều với tiêu đề**: `th.cell-actions` và `td.cell-actions` cùng căn giữa.
> `DataTable.razor.css` chỉ căn được `.p2-row-actions` do chính nó render — phần này nằm trong
> `RenderFragment` của trang nên không mang scope của `DataTable`. Thêm vào CSS trang:
>
> ```css
> .trang ::deep .table-card th.cell-actions,
> .trang ::deep .table-card td.cell-actions { text-align: center; }
> .trang ::deep .table-card td.cell-actions .p2-row-actions { justify-content: center; }
> ```
>
> Không có 2 rule này, nút `⋯` dạt phải còn chữ "Thao tác" căn giữa → lệch trục.

---

## M11 · Thùng rác — `= M10 + TrashColumns`

```csharp
new() { Key = "name", Title = L["TenantColumnName"], Value = i => i.Name, Width = "240px" },

TrashColumns.DeletedAt<T>(L["TrashDeletedAt"], i => i.DeletedAt),   // Value + 170px
TrashColumns.Actions<T>(L["P2Actions"], actions),                  // Template + 120px + cell-actions
```

```razor
@* TrashRowMenu: 2 mục Restore/Purge, dùng chung mọi thùng rác *@
<TrashRowMenu RestoreText="@L["Restore"]" PurgeText="@L["Purge"]"
              OnRestore="() => RequestRestore(item)" OnPurge="() => RequestPurge(item)" />
```

```razor
@* Purge bắt gõ đúng tên/mã *@
<PurgeConfirmDialog Visible="_purgeVisible" Title="@L["PurgeTitle"]"
                    Message="@string.Format(L["PurgeMessage"], _purgeItem?.Name ?? string.Empty)"
                    ExpectedText="@(_purgeItem?.Name ?? string.Empty)"
                    PromptText="@string.Format(L["PurgeTypePrompt"], _purgeItem?.Name ?? string.Empty)"
                    MismatchText="@L["PurgeMismatch"]"
                    ConfirmText="@L["Purge"]" CancelText="@L["Cancel"]"
                    OnConfirm="PurgeAsync" OnCancel="ClosePurge" />
```

```razor
<TablePager Page="_page" TotalPages="PageCount" From="FirstItem" To="LastItem" Total="_total"
            PageChanged="GoToPageAsync" />
```

---

## M12 · Bảng con dịch vụ (3–4 cột)

```razor
<table class="contract-detail-table">
    <thead>
        <tr>
            <th scope="col">@L["ContractServiceName"]</th>
            <th scope="col">@L["ContractServiceUnit"]</th>
            <th scope="col">@L["ContractServiceRate"]</th>
            <th scope="col">@L["ContractStatusLabel"]</th>
        </tr>
    </thead>
    <tbody>
    @foreach (var service in item.Services)
    {
        <tr>
            <td><a class="cell-link" href="/services">@service.Name</a></td>
            <td>@service.Unit</td>
            <td class="tabular-nums">@MoneyFormatter.Format(service.UnitPrice)</td>
            <td>
                <span class="contract-detail-tag @(service.IsRegistered ? "is-applied" : "is-muted")">
                    @L[service.IsRegistered ? "ContractServiceApplied" : "ContractServiceUnregistered"]
                </span>
            </td>
        </tr>
    }
    </tbody>
</table>
```

---

## M13 · Link chéo / prefill

```razor
<a class="cell-link is-quiet" href="@AppLinks.RoomsByCode(item.RoomName)">@item.RoomName</a>
<a class="cell-link is-quiet" href="@AppLinks.Property(item.PropertyId)">@item.PropertyName</a>
<a class="cell-link is-quiet" href="@AppLinks.TenantsByKeyword(name)">@name</a>
<a class="cell-link" href="@AppLinks.RoomsOfProperty(item.PropertyId)">@item.RoomCount</a>
```

> Link chéo trong ô mang chữ sẵn nên **không** cần `title`; ô dài thì bọc `cell-truncate` + popup (M03).

```csharp
[SupplyParameterFromQuery(Name = "q")] public string? Q { get; set; }
[SupplyParameterFromQuery(Name = "tenantId")] public int? TenantId { get; set; }

protected override async Task OnParametersSetAsync()
{
    _search = Q;
    await LoadAsync();          // Blazor reuse trang: nạp lại theo query mỗi lần
}

// Fallback: [SupplyParameterFromQuery] không phải lúc nào cũng có giá trị ở interactive circuit
var query = QueryHelpers.ParseQuery(new Uri(Navigation.Uri).Query);
```

---

## M14 · Hàng dạng thẻ / `<dl>` (không có `<table>`)

```razor
<ul class="account-list">
    @foreach (var row in _rows)
    {
        <li class="account-list-item">
            <div class="account-session">
                <strong class="cell-truncate" data-hover-tip aria-describedby="tip-@row.Id">
                    @DeviceLabel(row.UserAgent)
                    <span class="hover-tip" id="tip-@row.Id" role="tooltip">
                        <span class="hover-tip__line">@row.UserAgent</span>
                        <span class="hover-tip__meta">
                            <span><LucideIcon Name="calendar" />@row.LastSeenAt.ToLocalTime().ToString("HH:mm dd/MM/yyyy")</span>
                        </span>
                    </span>
                </strong>
                <span class="account-meta">@row.IpAddress</span>
            </div>
        </li>
    }
</ul>
```

```razor
<dl class="tenant-profile-details">
    <div>
        <dt>@L["TenantFullName"]</dt>
        <dd class="cell-truncate" data-hover-tip aria-describedby="tip-fullname">
            @_profile.FullName
            <span class="hover-tip" id="tip-fullname" role="tooltip">
                <span class="hover-tip__line">@_profile.FullName</span>
            </span>
        </dd>
    </div>
</dl>
```
