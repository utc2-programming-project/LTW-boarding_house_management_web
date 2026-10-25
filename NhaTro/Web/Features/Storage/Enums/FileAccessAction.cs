namespace Web.Features.Storage.Enums;

/// <summary>
/// Hành động truy cập tệp được ghi audit. Chỉ ghi metadata thao tác (ai, làm gì, tệp nào, kết quả),
/// không ghi tên tệp, đường dẫn hay nội dung giấy tờ.
/// </summary>
public enum FileAccessAction
{
    Upload = 0,
    Download = 1,
    List = 2,
    Delete = 3,
    Replace = 4,
}
