# NhaTro

**Boarding house management web app** — rooms, tenants, contracts, utilities, invoices, payments, maintenance and reports for small rental businesses. NhaTro is a server-rendered Blazor app for owners, property managers and tenants: staff run day-to-day operations, tenants get a self-service portal for their contract, room, invoices and service registrations.

> Academic project for **Web Programming** at **UTC2** (University of Transport and Communications — Ho Chi Minh City Campus), 2026. Documentation under [`docs/`](docs/) is in Vietnamese.

## Demo

No public deployment yet; screenshots will be added later. The app runs locally on Windows with .NET 10 and SQL Server LocalDB, and includes an optional ~60 MB demo dataset (400 properties, thousands of tenants). Development builds seed `admin@nhatro.local` (`DevAdmin!234`, Admin) plus `chutro@nhatro.local`, `quanly@nhatro.local` and `khachthue@nhatro.local` (`DevAccounts!234`, Owner/Manager/Tenant); the `/login` page also shows a Quick login panel.

<!-- Replace each path below with ![...](path) when the screenshot is captured. -->

| Landing page | Owner dashboard | Contracts | Invoices & payments |
| --- | --- | --- | --- |
| `docs/image/demo/landing.png` | `docs/image/demo/dashboard.png` | `docs/image/demo/contracts.png` | `docs/image/demo/invoices.png` |

## Features

- **Accounts & access** — owner self-registration, email OTP, TOTP 2FA, passkeys, CAPTCHA and per-IP rate limiting, sessions, multi-role switching, email/QR invitations.
- **Properties & rooms** — property setup wizard, Vietnamese address catalogue, manager assignment per building, manager changes to room info/price/status routed through approval, soft delete with trash.
- **Tenants** — profiles, identity documents, invitations and a tenant portal (`/my/room`, `/my/invoices`, `/my/service-registrations`).
- **Contracts** — create/edit, tenant review and signing, deposit payment/refund/reminders, renewals, pending room assignments, printable view.
- **Utilities** — service catalogue and price history, period meter readings with immutable audit and tariff snapshots, monthly cost calculation, service registrations.
- **Invoices & payments** — invoices from locked period costs, revision history, per-owner VietQR, proof upload, payment review queue.
- **Maintenance & approvals** — incident reports with photos and status workflow; 21 manager switches, with manager actions (SW-01…SW-16) queued for owner approval.
- **Reports, dashboard & admin** — revenue/debt, rooms, tenants, contracts, costs and incidents; Excel export behind approval; role-scoped KPIs and charts; user/owner management, owner suspension and audit log viewer.
- **Platform** — local file storage with limits and access audit, in-app notifications, VI/EN UI, light/dark theme, `/health/live` and `/health/ready`, Serilog.

## Tech stack

| Layer | Technology |
| --- | --- |
| Runtime | .NET 10 (`net10.0`), ASP.NET Core 10 |
| Web UI | Blazor Web App (Interactive Server), Razor, MudBlazor 9.11, custom CSS theme |
| Auth | ASP.NET Core Identity (cookie, roles, 2FA, passkeys), email OTP |
| Data & files | EF Core 10.0.12 + SQL Server/LocalDB; local file storage |
| Libraries | Serilog, QRCoder, ClosedXML, SkiaSharp |
| Tests & CI | xUnit, bUnit, `Mvc.Testing`, Playwright; GitHub Actions (Windows) |

## Local setup

Requires .NET SDK 10 and SQL Server LocalDB (Windows) or another SQL Server. Open `NhaTro/NhaTro.slnx` in Visual Studio 2022+ or any IDE with .NET 10 support, set `Web` as the startup project and run; Development applies pending migrations and seeds roles, the first Admin and the sample accounts automatically. Passwords, SMTP credentials and other overrides live in User Secrets — `appsettings.json` contains no secrets.

## Documentation

[System design & business flows](docs/thiet_ke_he_thong.md) · [Tech stack & conventions](docs/tech-stack/cong_nghe_su_dung.md) · [UI style guide](docs/design/phong_cach_giao_dien.md) · [Test checklist](docs/kiem_thu_toan_bo_chuc_nang.md) · [IIS deployment](docs/operations/iis-deployment.md) · [Workflow diagrams P1–P8](docs/image/README.md) · [Demo dataset spec](docs/spec/demo-data-spec.md)

## Team

Coursework for **Web Programming** at **UTC2 — University of Transport and Communications, Ho Chi Minh City Campus**, 2026.

**Team:** Cao Trung Tinh (`TinhCao`), Đinh Hoàng Phúc (`dinhhoangphuc123`), Hình Vĩnh Phát (`V.Phat`), Mai Thị Hồng Đào (`MaiThiHongDao`), Phú (`Piu-01`).

## License

No license file is included. This repository is academic coursework; all rights reserved by the authors. Contact the team before reusing any part of it. Third-party libraries remain under their own licenses.
