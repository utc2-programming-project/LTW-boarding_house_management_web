// Sao chép văn bản có phương án dự phòng: Clipboard API có thể bị chặn
// (quyền trình duyệt, ngữ cảnh nhúng) nên fallback execCommand để nút copy vẫn chạy.
window.vhtCopyText = async function (text) {
    try {
        if (navigator.clipboard && window.isSecureContext) {
            await navigator.clipboard.writeText(text);
            return true;
        }
    } catch (e) {
        // Rơi xuống fallback bên dưới.
    }

    try {
        const area = document.createElement("textarea");
        area.value = text;
        area.setAttribute("readonly", "");
        area.style.position = "fixed";
        area.style.top = "0";
        area.style.opacity = "0";
        document.body.appendChild(area);
        area.select();
        const ok = document.execCommand("copy");
        area.remove();
        return ok;
    } catch (e) {
        return false;
    }
};

// Mở hộp chia sẻ của hệ thống. Trả về false khi trình duyệt không hỗ trợ
// để phía C# chuyển sang sao chép link. Hủy hộp chia sẻ không tính là lỗi.
window.vhtShare = async function (title, url) {
    if (!navigator.share) return false;
    try {
        await navigator.share({ title: title, url: url });
        return true;
    } catch (e) {
        return e && e.name === "AbortError";
    }
};
