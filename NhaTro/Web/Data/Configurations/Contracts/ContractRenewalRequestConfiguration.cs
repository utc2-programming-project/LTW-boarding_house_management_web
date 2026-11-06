using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Rules;
using Web.Features.Tenants.Entities;

namespace Web.Data.Configurations.Contracts;

/// <summary>
/// Bảng yêu cầu gia hạn hợp đồng của khách thuê. Index lọc duy nhất chặn
/// "hai yêu cầu còn Chờ xử lý trên cùng một hợp đồng"; các yêu cầu đã xử lý vẫn được
/// giữ làm lịch sử.
/// </summary>
public sealed class ContractRenewalRequestConfiguration : IEntityTypeConfiguration<ContractRenewalRequest>
{
    public void Configure(EntityTypeBuilder<ContractRenewalRequest> builder)
    {
        builder.ToTable("ContractRenewalRequests", table =>
        {
            table.HasCheckConstraint(
                "CK_ContractRenewalRequests_Status",
                $"[Status] IN ({(int)RenewalRequestStatus.Pending}, {(int)RenewalRequestStatus.Approved}, {(int)RenewalRequestStatus.Rejected})");
            table.HasCheckConstraint(
                "CK_ContractRenewalRequests_Months",
                $"[RequestedMonths] BETWEEN {ContractRenewalRules.MinRequestedMonths} AND {ContractRenewalRules.MaxRequestedMonths}");
        });

        builder.HasKey(request => request.Id);

        builder.Property(request => request.Note).HasMaxLength(ContractRenewalRules.MaxNoteLength);
        builder.Property(request => request.RejectReason).HasMaxLength(ContractRenewalRules.MaxNoteLength);
        builder.Property(request => request.DecidedByUserId).HasMaxLength(450);
        builder.Property(request => request.Status).IsRequired();
        builder.Property(request => request.RequestedAt).IsRequired();
        builder.Property(request => request.RowVersion).IsRowVersion();

        builder.HasOne<Contract>()
            .WithMany()
            .HasForeignKey(request => request.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(request => request.RequestedByTenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(request => request.DecidedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Một hợp đồng chỉ có một yêu cầu đang chờ.
        builder.HasIndex(request => request.ContractId)
            .IsUnique()
            .HasFilter("[Status] = 0")
            .HasDatabaseName("UX_ContractRenewalRequests_Pending");

        builder.HasIndex(request => new { request.RequestedByTenantId, request.Status });
        builder.HasIndex(request => new { request.Status, request.RequestedAt });
    }
}
