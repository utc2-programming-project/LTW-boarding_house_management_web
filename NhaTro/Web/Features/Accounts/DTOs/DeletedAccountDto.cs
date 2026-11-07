namespace Web.Features.Accounts.DTOs;

/// <summary>Tài khoản trong thùng rác (xoá mềm), dùng cho màn khôi phục.</summary>
public sealed record DeletedAccountDto(
    string Id,
    string OriginalEmail,
    string? FullName,
    DateTime? DeletedAt);
