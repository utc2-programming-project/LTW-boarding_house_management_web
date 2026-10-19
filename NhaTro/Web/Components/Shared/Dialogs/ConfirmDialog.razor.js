// Hộp thoại dùng thẻ <dialog> gốc: tự khóa focus trong hộp thoại và trả focus về nút đã mở.

export function register(dialog, dotNetRef) {
    // Esc: không để trình duyệt tự đóng, giao cho .NET quyết định (đang xử lý thì bỏ qua).
    const onCancel = (event) => {
        event.preventDefault();
        dotNetRef.invokeMethodAsync("OnDismissRequested");
    };
    // Trường hợp trình duyệt vẫn đóng được hộp thoại (không qua sự kiện cancel).
    const onClose = () => dotNetRef.invokeMethodAsync("OnDismissRequested");

    dialog.addEventListener("cancel", onCancel);
    dialog.addEventListener("close", onClose);

    return {
        dispose: () => {
            dialog.removeEventListener("cancel", onCancel);
            dialog.removeEventListener("close", onClose);
        }
    };
}

export function open(dialog) {
    if (!dialog.open) dialog.showModal();
}

export function close(dialog) {
    if (dialog.open) dialog.close();
}
