namespace Web.Features.AccessControl.Enums;

// Trạng thái phiếu khách. Giá trị lưu nguyên vào SQL nên không đổi thứ tự.
public enum GatePassStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Used = 3,
    Expired = 4,
    Revoked = 5,
}

public enum GatePassType
{
    Resident = 0,
    Guest = 1,
    Service = 2,
}

public enum GateStayType
{
    Visit = 0,
    Overnight = 1,
}

// Người cấp phiếu; lưu chuỗi enum để truy vết và thống kê.
public enum GateIssuerRole
{
    ChuTro = 0,
    QuanLy = 1,
    KhachThue = 2,
    System = 3,
}

public enum GateAuthMode
{
    SingleFactor = 0,
    QrPlusBiometric = 1,
    FaceOnly = 2,
}

// Cờ phương thức (dùng cho AllowedMethods).
[Flags]
public enum GateMethod
{
    None = 0,
    Qr = 1,
    Face = 2,
    Fingerprint = 4,
    Passkey = 8,
    Code = 16,
    Manual = 32,
}

public enum GateDirection
{
    In = 0,
    Out = 1,
}

public enum GateSubjectType
{
    Person = 0,
    Vehicle = 1,
}

public enum GateDeviceKind
{
    Kiosk = 0,
    Reader = 1,
    Scanner = 2,
    Relay = 3,
    Camera = 4,
}

public enum GateLevel
{
    A = 0,
    B = 1,
}

public enum StayReportStatus
{
    Pending = 0,
    Reported = 1,
    Overdue = 2,
    Cancelled = 3,
}

public enum StayReportChannel
{
    Asm = 0,
    WebBca = 1,
    VNeId = 2,
    Manual = 3,
}

public enum BiometricMethod
{
    Face = 0,
    Fingerprint = 1,
    Passkey = 2,
}

public enum EnrollmentStatus
{
    Pending = 0,
    Active = 1,
    Rejected = 2,
    Revoked = 3,
}
