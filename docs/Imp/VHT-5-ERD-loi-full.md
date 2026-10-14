# VHT-5 — Ke hoach ve ERD loi (self-contained, AI khac doc la ve duoc)

> Doc duy nhat nay la du de ve. Khong can mo source. Moi thong tin duoi day da doi chieu voi migration tren nhanh `dev`.

## 1. Muc tieu

Ve ERD loi gom dung 4 hop chinh:
`AspNetUsers (Identity) + Properties + Rooms + Tenants`.
Ghi day du `PK / FK / quan he / nullable / unique / delete behavior`.
Chi ve bang da co. Khong ve Contract / Invoice / chi phi / hoa don / su co.
Khong tao lai INIT.
Cac bang Identity phu (Roles, Claims, Logins, Tokens...) KHONG ve thanh hop rieng,
chi ghi 1 dong ghi chu: "He thong dung ASP.NET Identity quan ly dang nhap/phan quyen".
Chi khi nhom yeu cau so do tai khoan day du moi ve them.

## 2. Pham vi cam ve them

- Khong ve: Hop dong, Chi phi ky, Hoa don, Thanh toan, Su co, Bao cao, Cong tac, Hang doi duyet, Thung rac (moi chi la cot `IsDeleted`), Nhat ky.
- `Tenant` ve ban nen nhu muc 4. Truong mo rong cho Phat (VHT-23) se lam o migration rieng sau.

## 3. Tong quan 4 hop de ve

```
AspNetUsers 1---N Properties (OwnerId, bat buoc, Restrict)
Properties  1---N Rooms      (PropertyId, bat buoc, Restrict)
AspNetUsers 1---N Tenants    (UserId, tuy chon NULL, Restrict)
```

- Tat ca FK deu `OnDelete Restrict`: khong cho xoa cha khi con ton tai.
- Ca 3 bang loi deu xoa mem bang `IsDeleted` + code tu an (`HasQueryFilter(!IsDeleted)`).

## 4. Chi tiet tung bang (chep nguyen vao hinh)

### 4.1 AspNetUsers (Identity, hop chinh DUY NHAT cua nhom Identity)

PK: `Id nvarchar(450) NOT NULL`.
Nguon chuan: `00000000000000_CreateIdentitySchema.cs:28-51` (cot), `:210-220` (index).

| Cot | Kieu SQL | Null | Ghi chu ve hinh |
|---|---|---|---|
| Id | nvarchar(450) | NOT NULL PK | khoa chinh chuoi |
| UserName | nvarchar(256) | NULL | khong co unique truc tiep |
| NormalizedUserName | nvarchar(256) | NULL | unique `UserNameIndex` filter `[NormalizedUserName] IS NOT NULL` |
| Email | nvarchar(256) | NULL | khong co unique truc tiep |
| NormalizedEmail | nvarchar(256) | NULL | index thuong `EmailIndex` |
| PasswordHash | nvarchar(max) | NULL | |
| SecurityStamp | nvarchar(max) | NULL | |
| ConcurrencyStamp | nvarchar(max) | NULL | |
| PhoneNumber | nvarchar(256) | NULL | dung theo migration `:41` (khong phai max) |
| EmailConfirmed | bit | NOT NULL | |
| PhoneNumberConfirmed | bit | NOT NULL | |
| TwoFactorEnabled | bit | NOT NULL | |
| LockoutEnabled | bit | NOT NULL | |
| LockoutEnd | datetimeoffset | NULL | |
| AccessFailedCount | int | NOT NULL | |

Ghi chu tren hinh (khong ve hop): "Cac bang Identity phu (AspNetRoles, AspNetUserRoles, Claims, Logins, Tokens, Passkeys) nam ngoai pham vi VHT-5. FK cua chung ve Users/Roles la Cascade.".

### 4.2 Properties (Nha tro)

PK: `Id int identity(1,1) NOT NULL`.
FK: `FK_Properties_AspNetUsers_OwnerId: OwnerId -> AspNetUsers.Id, Restrict`.
Index: `IX_Properties_OwnerId`.

| Cot | Kieu SQL | Null | Ghi chu |
|---|---|---|---|
| Id | int identity | NOT NULL PK | |
| Name | nvarchar(200) | NOT NULL | vd "Tro An Binh" |
| Address | nvarchar(500) | NULL | duoc bo trong |
| OwnerId | nvarchar(450) | NOT NULL FK | chu tro, bat buoc |
| IsActive | bit | NOT NULL | default true |
| CreatedAt | datetime2 | NOT NULL | |
| UpdatedAt | datetime2 | NULL | |
| IsDeleted | bit | NOT NULL | xoa mem |

### 4.3 Rooms (Phong)

PK: `Id int identity NOT NULL`.
FK: `FK_Rooms_Properties_PropertyId: PropertyId -> Properties.Id, Restrict`.
Unique: `(PropertyId, Code)` voi filter `[IsDeleted] = 0`.

| Cot | Kieu SQL | Null | Ghi chu |
|---|---|---|---|
| Id | int identity | NOT NULL PK | |
| PropertyId | int | NOT NULL FK | phong thuoc nha nao, bat buoc |
| Code | nvarchar(50) | NOT NULL | vd P101 |
| Floor | int | NULL | tang, cap 4 thi bo trong |
| AreaM2 | decimal(10,2) | NULL | dien tich |
| StandardPrice | decimal(18,2) | NOT NULL | gia chuan VND |
| Status | int | NOT NULL | 0=Available (Trong), 1=Rented (Dang thue), 2=Maintenance (Bao tri) |
| IsActive | bit | NOT NULL | |
| CreatedAt | datetime2 | NOT NULL | |
| UpdatedAt | datetime2 | NULL | |
| IsDeleted | bit | NOT NULL | |

### 4.4 Tenants (Khach thue)

PK: `Id int identity NOT NULL`.
FK: `FK_Tenants_AspNetUsers_UserId: UserId -> AspNetUsers.Id, Restrict`, nullable.
Unique: `CitizenId` voi filter `[CitizenId] IS NOT NULL AND [IsDeleted] = 0`.
Index: `IX_Tenants_UserId`.

| Cot | Kieu SQL | Null | Ghi chu |
|---|---|---|---|
| Id | int identity | NOT NULL PK | |
| FullName | nvarchar(200) | NOT NULL | vd "Nguyen Van A" |
| Phone | nvarchar(20) | NOT NULL | bat buoc, vd 0901234567 |
| Email | nvarchar(256) | NULL | |
| CitizenId | nvarchar(20) | NULL | CCCD, co thi khong trung, trong thi duoc |
| UserId | nvarchar(450) | NULL FK | co tai khoan thi noi, chua co thi de trong. Index thuong `IX_Tenants_UserId`, KHONG unique -> DB khong cam nhieu ho so cung tro ve 1 tai khoan. Cho Dao/Phat xac nhan: neu nghiep vu yeu cau 1 tai khoan chi 1 ho so thi can rang buoc rieng, khong tu them unique. |
| CreatedAt | datetime2 | NOT NULL | |
| UpdatedAt | datetime2 | NULL | |
| IsDeleted | bit | NOT NULL | |

> Ghi chu nghiep vu chua co: ERD nay CHUA the hien khach nao dang thue phong nao (khong co FK Rooms <-> Tenants). Lien ket thue se co khi nhom chot mo hinh Hop dong / luot thue. Khong tu them quan he.

## 5. Quy uoc ve hinh (bat buoc theo)

- `🔑` = PK. `FK` ghi ben canh cot khoa ngoai + ten FK day du.
- `*` = NOT NULL. `?` = NULL (nullable).
- Ghi `UQ` cho 2 unique: `(PropertyId,Code)` va `CitizenId`.
- Ghi `Restrict` tren ca 3 mui ten quan he.
- Ghi `IsDeleted = thung rac (xoa mem)` trong moi bang loi.
- Bo cuc goi y: `AspNetUsers` o giua trai, `Properties` phai Users, `Rooms` phai Properties, `Tenants` duoi Users.

### 5.1 Phan biet xoa mem va Restrict (de tranh hieu nham)

- `IsDeleted=1` chi la danh dau "bo vao thung rac", ban ghi VAN nam trong DB, app thuong tu an (`HasQueryFilter`).
- `Restrict` ap dung cho xoa THAT (DELETE). VD: phong P101 da xoa mem nhung van tham chieu nha 1, neu co xoa THAT nha 1 thi DB van chan.
- Tren hinh ghi: "IsDeleted = an khoi app. Restrict = cam xoa that cha khi con con (ke ca con da xoa mem)."

### 5.2 Canh bao khoi phuc (restore)

- Unique co dieu kien cho phep dung lai ma sau xoa mem: nha A xoa P101 roi tao lai P101 moi thi duoc. Nhung neu khoi phuc P101 cu thi TRUNG ma -> loi.
- Tuong tu CCCD khach: dung lai CCCD cua ho so da xoa mem thi duoc, khoi phuc ho so cu co the trung.
- Tren hinh / Word ghi: "Khoi phuc phai kiem tra trung Code phong / CCCD truoc."

## 6. Vi du de hieu (giai thich tieng nguoi)

- Nha `Id=1 "Tro An Binh" + OwnerId=abc` nghia la tai khoan `abc` dung ten khu nha do. Xoa `abc` luc nha 1 con ton tai thi DB chan.
- Phong `PropertyId=1 + Code=P101` nghia la phong P101 cua nha 1. Nha 1 da co P101 thi khong them P101 nua. Nha 2 van duoc dat P101. Phong da xoa mem thi duoc dung lai ma.
- Khach "Ba Nam chi ghi so tay SDT" thi `UserId=NULL` van luu duoc. Khi cap tai khoan dang nhap thi dien `UserId` vao de noi ho so voi login. Khach chua co CCCD van luu duoc, co roi thi khong trung ai.
- `Restrict` giong "khong cho dot so do khi nha con nguoi o": con phong thi khong xoa nha, con nha/ho so thi khong xoa tai khoan.
- Xoa mem giong "bo vao thung rac": tick `IsDeleted=1`, app tu an di, sau nay VHT-98/99 lam nut Khoi phuc.

## 7. Dau vao, phu thuoc, ban giao, tieu chi

- Dau vao: migration `00000000000000_CreateIdentitySchema` + `20261008142750_CoreSchema` + snapshot + 3 entity + 3 configuration tren nhanh `dev`. Tai lieu nay da tong hop het, khong can mo lai.
- Phu thuoc VHT-23 (Phat): cho DTO truong ho so khach truoc khi mo rong `Tenant`. Ve nen truoc, va sau.
- Ban giao: file ERD `png + svg + file nguon` dat vao `docs/design/database/`, gui Phuc ghep Word, nho Dao/Phat review FK.
- Tieu chi xong: PK/FK/nullable/truong ho so khop 100% muc 4; chay `dotnet ef migrations list` + `dotnet ef database update` duoc tren DB sach va DB co du lieu; khong sua migration cu, chi them migration moi khi mo rong Tenant.
- Tham chieu goc: `docs/task/cong_viec_theo_tuan.md` (VHT-5), `docs/task/timeline_song_song.md` (nhip 1A).

## 8. Checklist truoc khi nop

- [ ] Du 4 hop chinh, bang Identity phu chi la ghi chu.
- [ ] Du PK/FK/nullable/UQ/Restrict nhu muc 4-5.
- [ ] Ten FK giong migration. `UserNameIndex` nam tren `NormalizedUserName`, `EmailIndex` tren `NormalizedEmail`, `PhoneNumber nvarchar(256)` theo migration.
- [ ] Status Room ghi du 3 gia tri.
- [ ] Ghi chu xoa mem vs xoa that + canh bao trung khi khoi phuc.
- [ ] Ghi chu UserId khong unique, cho Dao/Phat xac nhan 1 user - may ho so.
- [ ] Ghi chu chua co quan he thue Rooms-Tenants, doi mo hinh Hop dong.
- [ ] Da gui Phuc + Dao/Phat review.

## 9. Phu luc: code hien tai tren nhanh `dev` (nguon doi chieu)

> AI chi can doc muc 4 de ve. Muc 9 la bang chung khop source, khong can mo them file.

### 9.1 `NhaTro/Web/Data/ApplicationDbContext.cs`

```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Tenants.Entities;

namespace Web.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<Property> Properties => Set<Property>();
        public DbSet<Room> Rooms => Set<Room>();
        public DbSet<Tenant> Tenants => Set<Tenant>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }
    }
}
```

### 9.2 `NhaTro/Web/Data/ApplicationUser.cs`

```csharp
using Microsoft.AspNetCore.Identity;

namespace Web.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
    }
}
```

### 9.3 `NhaTro/Web/Features/Properties/Entities/Property.cs`

```csharp
namespace Web.Features.Properties.Entities;

public class Property
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
```

### 9.4 `NhaTro/Web/Features/Rooms/Entities/Room.cs` + `Enums/RoomStatus.cs`

```csharp
using Web.Features.Rooms.Enums;

namespace Web.Features.Rooms.Entities;

public class Room
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public int? Floor { get; set; }
    public decimal? AreaM2 { get; set; }
    public decimal StandardPrice { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
```

```csharp
namespace Web.Features.Rooms.Enums;

public enum RoomStatus
{
    Available = 0,
    Rented = 1,
    Maintenance = 2
}
```

### 9.5 `NhaTro/Web/Features/Tenants/Entities/Tenant.cs`

```csharp
namespace Web.Features.Tenants.Entities;

public class Tenant
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? CitizenId { get; set; }
    public string? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
```

### 9.6 `Data/Configurations/Properties/PropertyConfiguration.cs`

```csharp
builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
builder.Property(p => p.Address).HasMaxLength(500);
builder.Property(p => p.OwnerId).HasMaxLength(450).IsRequired();
builder.HasIndex(p => p.OwnerId);
builder.HasQueryFilter(p => !p.IsDeleted);
builder.HasOne<Data.ApplicationUser>().WithMany()
    .HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Restrict);
```

### 9.7 `Data/Configurations/Rooms/RoomConfiguration.cs`

```csharp
builder.Property(r => r.Code).HasMaxLength(50).IsRequired();
builder.Property(r => r.StandardPrice).HasPrecision(18, 2);
builder.Property(r => r.AreaM2).HasPrecision(10, 2);
builder.HasIndex(r => new { r.PropertyId, r.Code }).IsUnique()
    .HasFilter("[IsDeleted] = 0");
builder.HasQueryFilter(r => !r.IsDeleted);
builder.HasOne<Features.Properties.Entities.Property>().WithMany()
    .HasForeignKey(r => r.PropertyId).OnDelete(DeleteBehavior.Restrict);
```

### 9.8 `Data/Configurations/Tenants/TenantConfiguration.cs`

```csharp
builder.Property(t => t.FullName).HasMaxLength(200).IsRequired();
builder.Property(t => t.Phone).HasMaxLength(20).IsRequired();
builder.Property(t => t.Email).HasMaxLength(256);
builder.Property(t => t.CitizenId).HasMaxLength(20);
builder.HasIndex(t => t.CitizenId).IsUnique()
    .HasFilter("[CitizenId] IS NOT NULL AND [IsDeleted] = 0");
builder.HasQueryFilter(t => !t.IsDeleted);
builder.HasOne<Data.ApplicationUser>().WithMany()
    .HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
```

### 9.9 `Data/Migrations/20261008142750_CoreSchema.cs` (Up, trich nguyen van)

```csharp
// Properties
Id = int identity PK,
Name = nvarchar(200) NOT NULL,
Address = nvarchar(500) NULL,
OwnerId = nvarchar(450) NOT NULL FK FK_Properties_AspNetUsers_OwnerId -> AspNetUsers.Id Restrict,
IsActive = bit NOT NULL, CreatedAt = datetime2 NOT NULL,
UpdatedAt = datetime2 NULL, IsDeleted = bit NOT NULL

// Tenants
Id = int identity PK,
FullName = nvarchar(200) NOT NULL, Phone = nvarchar(20) NOT NULL,
Email = nvarchar(256) NULL, CitizenId = nvarchar(20) NULL,
UserId = nvarchar(450) NULL FK FK_Tenants_AspNetUsers_UserId -> AspNetUsers.Id Restrict,
CreatedAt NOT NULL, UpdatedAt NULL, IsDeleted NOT NULL

// Rooms
Id = int identity PK,
PropertyId = int NOT NULL FK FK_Rooms_Properties_PropertyId -> Properties.Id Restrict,
Code = nvarchar(50) NOT NULL, Floor = int NULL,
AreaM2 = decimal(10,2) NULL, StandardPrice = decimal(18,2) NOT NULL,
Status = int NOT NULL, IsActive = bit NOT NULL,
CreatedAt NOT NULL, UpdatedAt NULL, IsDeleted NOT NULL

// Index
IX_Properties_OwnerId,
IX_Rooms_PropertyId_Code unique filter [IsDeleted] = 0,
IX_Tenants_CitizenId unique filter [CitizenId] IS NOT NULL AND [IsDeleted] = 0,
IX_Tenants_UserId
```

File goc: `NhaTro/Web/Data/Migrations/20261008142750_CoreSchema.cs:14-116`.
Snapshot doi chieu: `NhaTro/Web/Data/Migrations/ApplicationDbContextModelSnapshot.cs`.
Identity goc: `NhaTro/Web/Data/Migrations/00000000000000_CreateIdentitySchema.cs`.
