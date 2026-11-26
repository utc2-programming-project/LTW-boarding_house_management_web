using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.AccessControl;
using Web.Features.AccessControl.Entities;
using Web.Features.AccessControl.Enums;
using Web.Features.AccessControl.Services;
using Web.Features.Contracts.Enums;

namespace Web.Data.Seeding;

/// <summary>
/// Dữ liệu cổng mẫu cho database demo nhỏ (demo-data.json không chứa bảng AccessControl).
/// Chỉ chạy ở Development và database có tên chứa <c>Demo</c>; nhà trọ đã có
/// <see cref="GatePolicy"/> thì bỏ qua nên chạy lại nhiều lần không nhân đôi dữ liệu.
/// </summary>
public static class GateDemoSeeder
{
    /// <summary>Tiền tố token thô của phiếu mẫu, dùng để quẹt thử ở kiosk.</summary>
    public const string TokenPrefix = "DEMO-GATE";

    /// <summary>Token thô của link khách tự điền mẫu (chỉ dùng cho DB demo).</summary>
    public static string InviteToken(int propertyId) => $"{TokenPrefix}-INVITE-{propertyId}";

    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static async Task EnsureAsync(
        IServiceProvider services,
        IHostEnvironment environment,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        var factory = services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var databaseName = db.Database.GetDbConnection().Database;
        if (!databaseName.Contains("Demo", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var propertyIds = await db.Properties.AsNoTracking()
            .Where(property => property.IsActive && !property.IsDeleted)
            .Select(property => property.Id)
            .ToListAsync(cancellationToken);
        if (propertyIds.Count == 0)
        {
            return;
        }

        var seeded = await db.GatePolicies.AsNoTracking()
            .Where(policy => propertyIds.Contains(policy.PropertyId))
            .Select(policy => policy.PropertyId)
            .ToListAsync(cancellationToken);
        var targets = propertyIds.Except(seeded).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var clock = services.GetRequiredService<TimeProvider>();
        var tokens = services.GetRequiredService<IGateTokenService>();
        var now = clock.GetUtcNow();
        var today = now.ToOffset(VietnamOffset).Date;

        // Hợp đồng đang hiệu lực → biết phòng/khách đại diện để gắn phiếu và nhật ký.
        var leases = await (from member in db.ContractMembers.AsNoTracking()
                            join tenant in db.Tenants.AsNoTracking() on member.TenantId equals tenant.Id
                            join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
                            join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
                            where targets.Contains(room.PropertyId)
                                && tenant.UserId != null
                                && contract.Status == ContractStatus.Approved
                                && contract.StartDate <= today
                                && contract.EndDate >= today
                            orderby member.IsRepresentative descending, tenant.Id
                            select new
                            {
                                PropertyId = room.PropertyId,
                                room.Id,
                                ContractId = contract.ContractId,
                                TenantId = tenant.Id,
                                TenantName = tenant.FullName,
                                tenant.UserId,
                            })
            .ToListAsync(cancellationToken);
        var leaseByProperty = leases
            .GroupBy(lease => lease.PropertyId)
            .ToDictionary(group => group.Key, group => group.First());

        var properties = await db.Properties.AsNoTracking()
            .Where(property => targets.Contains(property.Id))
            .ToDictionaryAsync(property => property.Id, cancellationToken);

        var policies = new List<GatePolicy>();
        var devices = new List<GateDevice>();
        var passes = new List<(int PropertyId, int TenantId, GatePass Pass)>();
        var deviceByProperty = new Dictionary<int, GateDevice>();

        foreach (var propertyId in targets)
        {
            var property = properties[propertyId];
            policies.Add(new GatePolicy
            {
                PropertyId = propertyId,
                AuthMode = GateAuthMode.SingleFactor,
                AntiPassbackEnabled = true,
                AllowManualOpen = true,
                AllowManagerApprove = false,
                AutoApproveDayVisit = true,
                AutoApproveOvernight = false,
                AllowTenantIssuePass = true,
                AllowGuestSelfFill = true,
                GuestSelfMaxPassesPerDay = 3,
                TenantMayIssueOvernight = false,
                TenantMaxPassesPerDay = 3,
                TenantMaxTtlMinutes = 60,
                TenantMaxUses = 1,
                PassTtlMinutes = 60,
                DayVisitStartHour = 6,
                DayVisitEndHour = 22,
                RetentionDays = 30,
                OfflineGraceMinutes = 15,
                RequiredGuestFields = "FullName,Phone",
                PreferredStayChannel = StayReportChannel.VNeId,
                UpdatedAt = now,
                UpdatedByUserId = property.OwnerId,
            });

            var mainGate = new GateDevice
            {
                PropertyId = propertyId,
                Name = "Cổng chính",
                Kind = GateDeviceKind.Kiosk,
                Level = GateLevel.A,
                IsOnline = true,
                LastSeenAt = now.AddMinutes(-2),
                Note = "Kiosk đặt tại sảnh, quẹt QR và mã 6 số.",
                CreatedAt = now.AddDays(-30),
            };
            var sideGate = new GateDevice
            {
                PropertyId = propertyId,
                Name = "Cổng hẻm",
                Kind = GateDeviceKind.Reader,
                Level = GateLevel.A,
                IsOnline = false,
                LastSeenAt = now.AddHours(-6),
                Note = "Đầu đọc dự phòng, cần kiểm tra mạng.",
                CreatedAt = now.AddDays(-30),
            };
            devices.Add(mainGate);
            devices.Add(sideGate);
            deviceByProperty[propertyId] = mainGate;

            if (!leaseByProperty.TryGetValue(propertyId, out var lease))
            {
                continue;
            }

            var prefix = $"{TokenPrefix}-{propertyId}";
            string Code(int index) =>
                (900_000 + propertyId * 10 + index).ToString("D6", CultureInfo.InvariantCulture);

            passes.Add((propertyId, lease.TenantId, new GatePass
            {
                PropertyId = propertyId,
                RoomId = lease.Id,
                TenantId = lease.TenantId,
                ContractId = lease.ContractId,
                PassType = GatePassType.Guest,
                StayType = GateStayType.Overnight,
                TokenHash = tokens.Hash($"{prefix}-1"),
                CodeHash = tokens.Hash(Code(1)),
                VisitorName = "Nguyễn Văn Khách",
                VisitorPhone = "0901000001",
                VisitorIdNumber = "079200000001",
                VisitorDob = new DateOnly(1995, 3, 12),
                StayReason = "Bạn của khách thuê ghé ở lại 2 đêm",
                Note = "Chủ trọ đã duyệt, dùng để quẹt thử ở kiosk.",
                Status = GatePassStatus.Approved,
                AllowedMethods = GateMethod.Qr | GateMethod.Code,
                MaxUses = 1,
                UsedCount = 0,
                ExpiresAt = now.AddHours(10),
                IssuerRole = GateIssuerRole.KhachThue,
                IssuedByUserId = lease.UserId!,
                ApprovedByUserId = property.OwnerId,
                ApprovedAt = now.AddMinutes(-30),
                AutoApproved = false,
                CreatedAt = now.AddMinutes(-45),
            }));

            passes.Add((propertyId, lease.TenantId, new GatePass
            {
                PropertyId = propertyId,
                RoomId = lease.Id,
                TenantId = lease.TenantId,
                ContractId = lease.ContractId,
                PassType = GatePassType.Guest,
                StayType = GateStayType.Visit,
                TokenHash = tokens.Hash($"{prefix}-2"),
                CodeHash = tokens.Hash(Code(2)),
                VisitorName = "Trần Thị Ghé Chơi",
                VisitorPhone = "0901000002",
                VisitorIdNumber = "079200000002",
                StayReason = "Ghé thăm buổi chiều",
                Status = GatePassStatus.Approved,
                AllowedMethods = GateMethod.Qr | GateMethod.Code,
                MaxUses = 2,
                UsedCount = 0,
                ExpiresAt = now.AddMinutes(45),
                IssuerRole = GateIssuerRole.KhachThue,
                IssuedByUserId = lease.UserId!,
                ApprovedByUserId = property.OwnerId,
                ApprovedAt = now.AddMinutes(-10),
                AutoApproved = true,
                CreatedAt = now.AddMinutes(-15),
            }));

            passes.Add((propertyId, lease.TenantId, new GatePass
            {
                PropertyId = propertyId,
                RoomId = lease.Id,
                TenantId = lease.TenantId,
                ContractId = lease.ContractId,
                PassType = GatePassType.Guest,
                StayType = GateStayType.Overnight,
                TokenHash = tokens.Hash($"{prefix}-3"),
                CodeHash = tokens.Hash(Code(3)),
                VisitorName = "Lê Văn Qua Đêm",
                VisitorPhone = "0901000003",
                VisitorIdNumber = "079200000003",
                StayReason = "Em họ ở quê lên khám bệnh",
                Status = GatePassStatus.Pending,
                AllowedMethods = GateMethod.Qr | GateMethod.Code,
                MaxUses = 1,
                ExpiresAt = now.AddHours(24),
                IssuerRole = GateIssuerRole.KhachThue,
                IssuedByUserId = lease.UserId!,
                CreatedAt = now.AddMinutes(-20),
            }));

            passes.Add((propertyId, lease.TenantId, new GatePass
            {
                PropertyId = propertyId,
                RoomId = lease.Id,
                TenantId = lease.TenantId,
                ContractId = lease.ContractId,
                PassType = GatePassType.Guest,
                StayType = GateStayType.Visit,
                TokenHash = tokens.Hash($"{prefix}-4"),
                CodeHash = tokens.Hash(Code(4)),
                VisitorName = "Phạm Minh Đã Dùng",
                VisitorPhone = "0901000004",
                StayReason = "Giao hàng",
                Status = GatePassStatus.Used,
                AllowedMethods = GateMethod.Qr | GateMethod.Code,
                MaxUses = 1,
                UsedCount = 1,
                ExpiresAt = now.AddHours(-1),
                IssuerRole = GateIssuerRole.ChuTro,
                IssuedByUserId = property.OwnerId,
                ApprovedByUserId = property.OwnerId,
                ApprovedAt = now.AddHours(-5),
                AutoApproved = false,
                CreatedAt = now.AddHours(-5),
            }));

            passes.Add((propertyId, lease.TenantId, new GatePass
            {
                PropertyId = propertyId,
                RoomId = lease.Id,
                TenantId = lease.TenantId,
                ContractId = lease.ContractId,
                PassType = GatePassType.Guest,
                StayType = GateStayType.Visit,
                TokenHash = tokens.Hash($"{prefix}-5"),
                CodeHash = tokens.Hash(Code(5)),
                VisitorName = "Đỗ Thị Hết Hạn",
                VisitorPhone = "0901000005",
                StayReason = "Tư vấn thuê phòng",
                Status = GatePassStatus.Expired,
                AllowedMethods = GateMethod.Qr,
                MaxUses = 1,
                ExpiresAt = now.AddDays(-2),
                IssuerRole = GateIssuerRole.ChuTro,
                IssuedByUserId = property.OwnerId,
                ApprovedByUserId = property.OwnerId,
                ApprovedAt = now.AddDays(-3),
                AutoApproved = false,
                CreatedAt = now.AddDays(-3),
            }));

            passes.Add((propertyId, lease.TenantId, new GatePass
            {
                PropertyId = propertyId,
                RoomId = lease.Id,
                TenantId = lease.TenantId,
                ContractId = lease.ContractId,
                PassType = GatePassType.Guest,
                StayType = GateStayType.Visit,
                TokenHash = tokens.Hash($"{prefix}-6"),
                CodeHash = tokens.Hash(Code(6)),
                VisitorName = "Bùi Văn Bị Thu Hồi",
                VisitorPhone = "0901000006",
                StayReason = "Bạn tới chơi",
                Status = GatePassStatus.Revoked,
                AllowedMethods = GateMethod.Qr | GateMethod.Code,
                MaxUses = 1,
                ExpiresAt = now.AddHours(2),
                IssuerRole = GateIssuerRole.KhachThue,
                IssuedByUserId = lease.UserId!,
                ApprovedByUserId = property.OwnerId,
                ApprovedAt = now.AddHours(-2),
                AutoApproved = false,
                RevokedAt = now.AddHours(-1),
                RevokedByUserId = property.OwnerId,
                RevokedReason = "Khách huỷ chuyến, chủ trọ thu hồi phiếu.",
                CreatedAt = now.AddHours(-2),
            }));

            passes.Add((propertyId, lease.TenantId, new GatePass
            {
                PropertyId = propertyId,
                RoomId = lease.Id,
                TenantId = lease.TenantId,
                ContractId = lease.ContractId,
                PassType = GatePassType.Guest,
                StayType = GateStayType.Overnight,
                TokenHash = tokens.Hash($"{prefix}-7"),
                CodeHash = tokens.Hash(Code(7)),
                VisitorName = "Hoàng Thị Bị Từ Chối",
                VisitorPhone = "0901000007",
                StayReason = "Ở nhờ một đêm",
                Status = GatePassStatus.Rejected,
                AllowedMethods = GateMethod.Qr | GateMethod.Code,
                MaxUses = 1,
                ExpiresAt = now.AddHours(24),
                IssuerRole = GateIssuerRole.KhachThue,
                IssuedByUserId = lease.UserId!,
                RejectedReason = "Thiếu ảnh CCCD của khách.",
                CreatedAt = now.AddHours(-6),
            }));
        }

        // Link mẫu để thử luồng khách tự điền: token thô cố định, DB chỉ lưu hash.
        var invites = new List<GatePassInvite>();
        foreach (var propertyId in targets.Where(leaseByProperty.ContainsKey))
        {
            var lease = leaseByProperty[propertyId];
            invites.Add(new GatePassInvite
            {
                PropertyId = propertyId,
                RoomId = lease.Id,
                TenantId = lease.TenantId,
                ContractId = lease.ContractId,
                CreatedByUserId = lease.UserId!,
                IssuerRole = GateIssuerRole.KhachThue,
                TokenHash = tokens.Hash(InviteToken(propertyId)),
                ExpiresAt = now.AddHours(24),
                CreatedAt = now.AddMinutes(-5),
            });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.GatePolicies.AddRange(policies);
        db.GateDevices.AddRange(devices);
        db.GatePasses.AddRange(passes.Select(item => item.Pass));
        db.GatePassInvites.AddRange(invites);
        await db.SaveChangesAsync(cancellationToken);

        var reports = new List<StayReport>();
        var logs = new List<GateLog>();
        var enrollments = new List<BiometricEnrollment>();

        foreach (var propertyId in targets)
        {
            var property = properties[propertyId];
            var address = string.IsNullOrWhiteSpace(property.Address) ? property.Name : property.Address;
            var device = deviceByProperty[propertyId];
            var propertyPasses = passes.Where(item => item.PropertyId == propertyId).ToList();
            if (propertyPasses.Count == 0)
            {
                continue;
            }

            // Thứ tự tạo phiếu cố định: 1 qua đêm đã duyệt, 2 khách trong ngày, 3 chờ duyệt,
            // 4 đã dùng, 5 hết hạn, 6 bị thu hồi, 7 bị từ chối.
            var approvedOvernight = propertyPasses[0].Pass;
            var usedPass = propertyPasses[3].Pass;
            var expiredPass = propertyPasses[4].Pass;
            var revokedPass = propertyPasses[5].Pass;
            var lease = leaseByProperty[propertyId];

            var pendingReport = new StayReport
            {
                GatePassId = approvedOvernight.Id,
                PropertyId = propertyId,
                RoomId = approvedOvernight.RoomId,
                ContractId = approvedOvernight.ContractId,
                GuestName = approvedOvernight.VisitorName,
                GuestDob = approvedOvernight.VisitorDob,
                GuestIdNumber = approvedOvernight.VisitorIdNumber,
                StayReason = approvedOvernight.StayReason,
                StayFrom = approvedOvernight.CreatedAt,
                StayTo = now.AddHours(10),
                AddressSnapshot = address,
                Status = StayReportStatus.Pending,
                Channel = StayReportChannel.VNeId,
                DueAt = now.AddHours(12),
                CreatedAt = approvedOvernight.CreatedAt,
            };
            reports.Add(pendingReport);

            reports.Add(new StayReport
            {
                PropertyId = propertyId,
                RoomId = lease.Id,
                ContractId = lease.ContractId,
                GuestName = "Võ Thị Quá Hạn",
                GuestDob = new DateOnly(1998, 7, 1),
                GuestIdNumber = "079200000098",
                StayReason = "Bạn của khách thuê",
                StayFrom = now.AddDays(-2),
                StayTo = now.AddDays(-1),
                AddressSnapshot = address,
                Status = StayReportStatus.Overdue,
                Channel = StayReportChannel.VNeId,
                DueAt = now.AddHours(-6),
                Note = "Quá hạn báo lưu trú, cần xử lý.",
                CreatedAt = now.AddDays(-2),
            });

            reports.Add(new StayReport
            {
                PropertyId = propertyId,
                RoomId = lease.Id,
                ContractId = lease.ContractId,
                GuestName = "Đặng Văn Đã Báo",
                GuestDob = new DateOnly(1990, 1, 20),
                GuestIdNumber = "079200000097",
                StayReason = "Người thân tới thăm",
                StayFrom = now.AddDays(-1).AddHours(-8),
                StayTo = now.AddDays(-1),
                AddressSnapshot = address,
                Status = StayReportStatus.Reported,
                Channel = StayReportChannel.Manual,
                DueAt = now.AddDays(-1).AddHours(4),
                ReportedAt = now.AddDays(-1).AddHours(2),
                ReportedByUserId = property.OwnerId,
                EvidenceRef = "demo-evidence-001",
                Note = "Đã khai báo qua cổng dịch vụ công.",
                CreatedAt = now.AddDays(-1).AddHours(-8),
            });

            logs.Add(new GateLog
            {
                PropertyId = propertyId,
                GateDeviceId = device.Id,
                GatePassId = usedPass.Id,
                Direction = GateDirection.In,
                Method = GateMethod.Qr,
                IsSuccess = true,
                SubjectName = usedPass.VisitorName,
                TenantId = usedPass.TenantId,
                OccurredAt = now.AddHours(-4),
            });
            logs.Add(new GateLog
            {
                PropertyId = propertyId,
                GateDeviceId = device.Id,
                GatePassId = usedPass.Id,
                Direction = GateDirection.Out,
                Method = GateMethod.Qr,
                IsSuccess = true,
                SubjectName = usedPass.VisitorName,
                TenantId = usedPass.TenantId,
                OccurredAt = now.AddHours(-3),
            });
            logs.Add(new GateLog
            {
                PropertyId = propertyId,
                GateDeviceId = device.Id,
                GatePassId = revokedPass.Id,
                Direction = GateDirection.In,
                Method = GateMethod.Qr,
                IsSuccess = false,
                Reason = GateErrorCodes.PassRevoked,
                SubjectName = revokedPass.VisitorName,
                TenantId = revokedPass.TenantId,
                OccurredAt = now.AddMinutes(-50),
            });
            logs.Add(new GateLog
            {
                PropertyId = propertyId,
                GateDeviceId = device.Id,
                GatePassId = expiredPass.Id,
                Direction = GateDirection.In,
                Method = GateMethod.Code,
                IsSuccess = false,
                Reason = GateErrorCodes.PassExpired,
                SubjectName = expiredPass.VisitorName,
                TenantId = expiredPass.TenantId,
                OccurredAt = now.AddDays(-1).AddHours(-2),
            });
            logs.Add(new GateLog
            {
                PropertyId = propertyId,
                GateDeviceId = device.Id,
                Direction = GateDirection.In,
                Method = GateMethod.Qr,
                IsSuccess = false,
                Reason = GateErrorCodes.TokenInvalid,
                OccurredAt = now.AddHours(-2),
            });
            logs.Add(new GateLog
            {
                PropertyId = propertyId,
                GateDeviceId = device.Id,
                Direction = GateDirection.In,
                Method = GateMethod.Manual,
                IsSuccess = true,
                SubjectName = "Mở tay: giao hàng tận cửa",
                OccurredAt = now.AddDays(-3).AddHours(-5),
            });
            logs.Add(new GateLog
            {
                PropertyId = propertyId,
                GateDeviceId = device.Id,
                Direction = GateDirection.In,
                Method = GateMethod.Qr,
                IsSuccess = true,
                SubjectName = lease.TenantName,
                UserId = lease.UserId,
                TenantId = lease.TenantId,
                OccurredAt = now.AddDays(-1).AddHours(-10),
            });
            logs.Add(new GateLog
            {
                PropertyId = propertyId,
                GateDeviceId = device.Id,
                Direction = GateDirection.Out,
                Method = GateMethod.Qr,
                IsSuccess = true,
                SubjectName = lease.TenantName,
                UserId = lease.UserId,
                TenantId = lease.TenantId,
                OccurredAt = now.AddDays(-1).AddHours(-2),
            });

            enrollments.Add(new BiometricEnrollment
            {
                PropertyId = propertyId,
                TenantId = lease.TenantId,
                Method = BiometricMethod.Face,
                TemplateRef = $"demo/templates/face-{lease.TenantId}",
                ConsentAt = now.AddDays(-10),
                EnrolledByUserId = lease.UserId!,
                Status = EnrollmentStatus.Active,
                ReviewedByUserId = property.OwnerId,
                ReviewedAt = now.AddDays(-9),
                RetentionUntil = now.AddDays(355),
                CreatedAt = now.AddDays(-10),
            });
        }

        db.StayReports.AddRange(reports);
        db.GateLogs.AddRange(logs);
        db.BiometricEnrollments.AddRange(enrollments);
        await db.SaveChangesAsync(cancellationToken);

        // Gắn phiếu qua đêm đã duyệt với phiếu báo lưu trú vừa tạo (khoá ngoài một chiều).
        var approvedPassIds = passes
            .Where(item => item.Pass.StayType == GateStayType.Overnight
                && item.Pass.Status == GatePassStatus.Approved)
            .Select(item => item.Pass.Id)
            .ToHashSet();
        foreach (var report in reports.Where(report => report.GatePassId is { } id && approvedPassIds.Contains(id)))
        {
            var pass = passes.Select(item => item.Pass).First(item => item.Id == report.GatePassId);
            pass.StayReportId = report.Id;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "GateDemo: đã thêm dữ liệu cổng cho {Properties} nhà trọ ({Devices} thiết bị, {Passes} phiếu, {Logs} lượt quẹt, {Reports} báo lưu trú).",
            targets.Count,
            devices.Count,
            passes.Count,
            logs.Count,
            reports.Count);

        foreach (var propertyId in targets.Where(leaseByProperty.ContainsKey))
        {
            logger.LogInformation(
                "GateDemo: link khách tự điền mẫu cho nhà {PropertyId}: /guest/{Token}",
                propertyId,
                InviteToken(propertyId));
        }
    }
}
