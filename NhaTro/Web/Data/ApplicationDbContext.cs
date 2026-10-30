using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Web.Features.Properties.Entities;
using Web.Features.Contracts.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Settings.Entities;
using Web.Features.Storage.Entities;
using Web.Features.Tenants.Entities;
using Web.Features.Utilities.Entities;

namespace Web.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<Property> Properties => Set<Property>();
        public DbSet<Room> Rooms => Set<Room>();
        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<Contract> Contracts => Set<Contract>();
        public DbSet<ContractMember> ContractMembers => Set<ContractMember>();
        public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
        public DbSet<FileAccessAuditLog> FileAccessAuditLogs => Set<FileAccessAuditLog>();

        // VHT-94: 21 cong tac Quan ly (SW-01..SW-16 + SW-15a..e).
        public DbSet<ManagerToggle> ManagerToggles => Set<ManagerToggle>();
        public DbSet<UtilityTariff> UtilityTariffs => Set<UtilityTariff>();
        public DbSet<UtilityTariffTier> UtilityTariffTiers => Set<UtilityTariffTier>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }
    }
}
