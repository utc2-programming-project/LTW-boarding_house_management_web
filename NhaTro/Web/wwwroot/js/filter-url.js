// Đồng bộ trạng thái bộ lọc vào query string mà không điều hướng lại trang.
// Giá trị null/rỗng bị xoá khỏi URL; các tham số khác (returnUrl, deep-link) giữ nguyên.
window.appFilterUrl = {
    replace(values) {
        if (!window.history || typeof window.history.replaceState !== "function") {
            return;
        }

        const url = new URL(window.location.href);
        for (const [key, value] of Object.entries(values || {})) {
            if (value === null || value === undefined || value === "") {
                url.searchParams.delete(key);
            } else {
                url.searchParams.set(key, String(value));
            }
        }

        const query = url.searchParams.toString();
        window.history.replaceState(
            window.history.state,
            "",
            url.pathname + (query ? "?" + query : "") + url.hash);
    },
};
