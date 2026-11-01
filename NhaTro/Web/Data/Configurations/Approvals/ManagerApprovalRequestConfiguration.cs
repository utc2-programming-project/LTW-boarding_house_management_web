using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Data;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Properties.Entities;

namespace Web.Data.Configurations.Approvals;

/// <summary>
/// VHT-96 — bảng hàng đợi duyệt thao tác Quản lý. Index lọc duy nhất là lớp chặn
/// "hai yêu cầu Chờ duyệt cho cùng một thao tác trên cùng đối tượng"; lớp chặn
/// duyệt trùng nằm ở rowversion + UPDLOCK trong service.
/// </summary>
public class ManagerApprovalRequestConfiguration : IEntityTypeConfiguration<ManagerApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ManagerApprovalRequest> builder)
    {
        builder.ToTable("ManagerApprovalRequests", table =>
        {
            table.HasCheckConstraint(
                "CK_ManagerApprovalRequests_Status",
                $"[Status] IN ({(int)ApprovalRequestStatus.Pending}, {(int)ApprovalRequestStatus.Approved}, {(int)ApprovalRequestStatus.Rejected}, {(int)ApprovalRequestStatus.Cancelled})");
        });

        builder.HasKey(request => request.Id);
        builder.Property(request => request.OperationCode).HasMaxLength(10).IsRequired();
        builder.Property(request => request.TargetType).IsRequired();
        builder.Property(request => request.TargetKey).HasMaxLength(200).IsRequired();
        builder.Property(request => request.PayloadJson).IsRequired();
        builder.Property(request => request.Status).IsRequired();
        builder.Property(request => request.RequestedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(request => request.RequestedAt).IsRequired();
        builder.Property(request => request.DecidedByUserId).HasMaxLength(450);
        builder.Property(request => request.DecisionReason).HasMaxLength(500);
        builder.Property(request => request.RowVersion).IsRowVersion();

        // Một yêu cầu Chờ duyệt cho mỗi (mã SW, loại, khóa đối tượng).
        builder.HasIndex(request => new { request.OperationCode, request.TargetType, request.TargetKey })
            .IsUnique()
            .HasFilter("[Status] = 0")
            .HasDatabaseName("UX_ManagerApprovalRequests_Pending");

        // "Yêu cầu của tôi" và hộp thư Chủ trọ.
        builder.HasIndex(request => new { request.RequestedByUserId, request.Status });
        builder.HasIndex(request => new { request.Status, request.RequestedAt });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(request => request.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(request => request.DecidedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Property>()
            .WithMany()
            .HasForeignKey(request => request.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
