using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Web.Data.Configurations.Accounts;

// VHT-8: cau hinh cot FullName them vao AspNetUsers.
// Ghi chu lech ERD VHT-5 (nguon chuan schema): ERD khong co cot nay,
// them theo dac ta VHT-8 muc "Ho so ca nhan: Ho ten".
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FullName).HasMaxLength(200);
    }
}
