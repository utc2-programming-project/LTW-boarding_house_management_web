---
name: Property Management System
colors:
  surface: '#f8f9ff'
  surface-dim: '#cbdbf5'
  surface-bright: '#f8f9ff'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#eff4ff'
  surface-container: '#e5eeff'
  surface-container-high: '#dce9ff'
  surface-container-highest: '#d3e4fe'
  on-surface: '#0b1c30'
  on-surface-variant: '#434655'
  inverse-surface: '#213145'
  inverse-on-surface: '#eaf1ff'
  outline: '#737686'
  outline-variant: '#c3c6d7'
  surface-tint: '#0053db'
  primary: '#004ac6'
  on-primary: '#ffffff'
  primary-container: '#2563eb'
  on-primary-container: '#eeefff'
  inverse-primary: '#b4c5ff'
  secondary: '#1d4ed8'
  on-secondary: '#ffffff'
  secondary-container: '#4069f2'
  on-secondary-container: '#fffbff'
  tertiary: '#005a82'
  on-tertiary: '#ffffff'
  tertiary-container: '#0074a6'
  on-tertiary-container: '#e4f2ff'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#dbe1ff'
  primary-fixed-dim: '#b4c5ff'
  on-primary-fixed: '#00174b'
  on-primary-fixed-variant: '#003ea8'
  secondary-fixed: '#dce1ff'
  secondary-fixed-dim: '#b7c4ff'
  on-secondary-fixed: '#001551'
  on-secondary-fixed-variant: '#0039b5'
  tertiary-fixed: '#c9e6ff'
  tertiary-fixed-dim: '#89ceff'
  on-tertiary-fixed: '#001e2f'
  on-tertiary-fixed-variant: '#004c6e'
  background: '#f8f9ff'
  on-background: '#0b1c30'
  surface-variant: '#d3e4fe'
typography:
  headline-xl:
    fontFamily: Inter
    fontSize: 32px
    fontWeight: '700'
    lineHeight: 40px
  headline-xl-mobile:
    fontFamily: Inter
    fontSize: 24px
    fontWeight: '700'
    lineHeight: 32px
  headline-lg:
    fontFamily: Inter
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
  headline-md:
    fontFamily: Inter
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
  title-md:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '600'
    lineHeight: 24px
  body-lg:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  body-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  body-sm:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 16px
  label-md:
    fontFamily: Inter
    fontSize: 13px
    fontWeight: '500'
    lineHeight: 18px
  label-sm:
    fontFamily: Inter
    fontSize: 11px
    fontWeight: '600'
    lineHeight: 14px
    letterSpacing: 0.05em
  metric-num:
    fontFamily: Inter
    fontSize: 28px
    fontWeight: '700'
    lineHeight: 34px
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  gutter: 1rem
  gutter-desktop: 1.5rem
  margin: 1rem
  margin-desktop: 2rem
  space-xs: 0.25rem
  space-sm: 0.5rem
  space-md: 1rem
  space-lg: 1.5rem
  space-xl: 2rem
---

## Brand & Style

This design system targets landlords, property operations managers, and leasing agents handling residential rentals, boarding houses, and serviced apartments. It delivers an operational atmosphere centered on clarity, predictability, and effortless data digestion.

The aesthetic follows a modern corporate and clean utilitarian approach:
- High data density balanced by disciplined white space.
- Crisp structural framing using delicate borders rather than heavy drop shadows.
- Visual confidence achieved through purposeful, balanced royal blues against soft cool-slate backgrounds.
- High-contrast legibility for occupancy metrics, financial balances, tenant lists, and meter readings.

## Colors

The palette establishes an authoritative, reliable environment with distinct functional roles:

- **Primary (`#2563EB`)**: Key actions, navigation highlights, active tabs, and primary metrics.
- **Secondary / Brand Dark (`#1D4ED8`)**: Hover states, active pressed states, and primary button hover rings.
- **Tertiary (`#0EA5E9`)**: Informational indicators, utilities tracking (electricity/water counters), and ancillary highlights.
- **Neutral Canvas & Surfaces**:
  - App Background: `#F8FAFC` (Slate 50)
  - Card/Container Surface: `#FFFFFF` (White)
  - Secondary/Sidebar Surface: `#F1F5F9` (Slate 100)
  - Subtle Border/Divider: `#E2E8F0` (Slate 200)
  - Strong Border/Focus: `#CBD5E1` (Slate 300)
  - Text Primary: `#0F172A` (Slate 900)
  - Text Muted/Secondary: `#64748B` (Slate 500)
- **Functional & Alert Semantics**:
  - Success (Paid, Occupied, Verified): Background `#F0FDF4`, Border `#BBF7D0`, Text `#15803D`.
  - Warning/Pending (Expiring Lease, Overdue Alert, Deposit Due): Background `#FEFCE8`, Border `#FEF08A`, Text `#A16207`.
  - Critical/Danger (Overdue Rent, Vacant, Maintenance Urgency): Background `#FEF2F2`, Border `#FECACA`, Text `#B91C1C`.
  - Info (Contract Renewal, General Note): Background `#EFF6FF`, Border `#BFDBFE`, Text `#1D4ED8`.

## Typography

The type system prioritizes high-density data parsing, tabular figures for currency/measurements, and instant hierarchical recognition:

- Use tabular numerals (`font-feature-settings: 'tnum' 1`) across all metric, billing, and balance tables.
- `label-sm` utilizes uppercase casing with `0.05em` letter-spacing strictly for category tags, table column headers, and apartment status pill headers.
- Metric values in analytical widgets must pair `metric-num` with a secondary `label-md` unit specifier (e.g., `₫`, `m²`, `%`).
- Text weights are restricted to 400 (regular body), 500 (labels and metadata), 600 (card titles and navigation), and 700 (totals and primary KPI headers).

## Layout & Spacing

The layout is built around a flexible 12-column dashboard grid optimized for variable-aspect data visualization:

- **Desktop (1200px+)**: Sidebar navigation at a fixed 260px; primary content fluid with 24px (`space-lg`) section gaps, 24px column gutters (`gutter-desktop`), and 32px canvas margins (`margin-desktop`).
- **Tablet (768px - 1199px)**: Collapsed icon sidebar (64px); 16px grid gutters (`gutter`), 16px margins (`margin`). Metric cards shift to a 2-up layout.
- **Mobile (< 768px)**: Single-column stacked layout; off-canvas drawer navigation; card margins set to 16px (`margin`), vertical spacing tightened to 12px-16px.

All interior card layouts use an 8px base increment: 16px (`space-md`) internal card padding for summary cards, 24px (`space-lg`) for dense data sheets and form wizards.

## Elevation & Depth

This design system favors border-driven definition and micro-elevation over dramatic shadows, keeping data sharp and printable:

- **Level 0 (Flat / Canvas)**: Background `#F8FAFC`. Zero elevation.
- **Level 1 (Card / Container Default)**: Pure white `#FFFFFF` surface resting on `#E2E8F0` hairline 1px border. Shadow: `0 1px 2px 0 rgba(15, 23, 42, 0.05)`.
- **Level 2 (Hover / Active Unit Card)**: Elevated room tiles and selectable invoices. Shadow: `0 4px 6px -1px rgba(15, 23, 42, 0.08), 0 2px 4px -2px rgba(15, 23, 42, 0.04)`. Border shifts to `#CBD5E1`.
- **Level 3 (Popovers / Dropdowns / Datepickers)**: Shadow: `0 10px 15px -3px rgba(15, 23, 42, 0.1), 0 4px 6px -4px rgba(15, 23, 42, 0.05)`.
- **Level 4 (Modals & Slide-over Sheets)**: Shadow: `0 20px 25px -5px rgba(15, 23, 42, 0.12), 0 8px 10px -6px rgba(15, 23, 42, 0.05)`, backed by a neutral backdrop overlay `rgba(15, 23, 42, 0.4)`.

## Shapes

With `roundedness: 1`, shapes maintain clean, soft-cornered geometry that reinforces operational stability and order:

- Base inputs, buttons, standard badges, and data cells use `0.25rem` (4px).
- Cards, panels, modal dialogs, and table shells use `rounded-lg` at `0.5rem` (8px).
- Outer dashboard layout blocks and slide-over panels use `rounded-xl` at `0.75rem` (12px).
- Badges and status pills strictly retain full rounded treatment (`rounded-full` / 9999px) to contrast against squared container architecture.

## Components

### Buttons
- **Primary**: Background `#2563EB`, text `#FFFFFF`, border none. Hover state `#1D4ED8`. Active state `#1E40AF`. Focus ring 2px `#2563EB` with 2px offset.
- **Secondary**: Background `#FFFFFF`, text `#0F172A`, border 1px solid `#E2E8F0`. Hover state `#F1F5F9`.
- **Danger**: Background `#FEF2F2`, text `#B91C1C`, border 1px solid `#FECACA`. Hover state `#FEE2E2`.
- **Sizes**: Standard form height 38px (padding 8px 16px); compact table-action height 30px (padding 4px 10px).

### Input Fields & Selects
- Height 38px, border 1px solid `#E2E8F0`, background `#FFFFFF`, border-radius 4px.
- Placeholder text `#94A3B8`.
- Focus state: Border color `#2563EB`, box-shadow `0 0 0 1px #2563EB`.
- Error state: Border color `#EF4444`, focus ring `#EF4444`, error text below input in 12px `#B91C1C`.
- Support integrated prefix/suffix units for rent inputs (`₫ / tháng`, `kWh`, `m³`).

### Status Badges & Chips
- Padding: 2px 8px, border-radius 9999px, font size 11px, weight 600.
- **Occupied / Đã thuê**: Background `#DCFCE7`, text `#15803D`.
- **Vacant / Còn trống**: Background `#EFF6FF`, text `#1D4ED8`.
- **Overdue / Quá hạn**: Background `#FEE2E2`, text `#B91C1C`.
- **Maintenance / Đang sửa chữa**: Background `#FEF9C3`, text `#A16207`.

### Data Cards (Room & Apartment Units)
- Border 1px solid `#E2E8F0`, background `#FFFFFF`, border-radius 8px, padding 16px.
- Structure: Room Number & Floor (Top left, bold), Status Badge (Top right), Tenant info (middle), Utilities / Balance indicator (bottom bar separated by 1px `#F1F5F9` divider).

### Tables (Lease & Invoicing Grids)
- Header row: Background `#F8FAFC`, border-bottom 1px solid `#E2E8F0`, text `label-sm` in `#64748B`.
- Row height: 48px regular, 40px compact. Zebra striping disabled; hover row state `#F8FAFC`.
- Numbers and status columns must be right-aligned; room IDs and tenant names left-aligned.

### Utility Counter Meters
- Specific compound component for electricity/water entry: previous reading (read-only grayed text), current reading (editable numeric field), dynamic calculated consumption difference pill displayed inline.