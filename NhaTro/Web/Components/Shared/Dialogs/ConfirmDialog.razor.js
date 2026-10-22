// Hộp thoại dùng thẻ <dialog> gốc: tự khóa focus trong hộp thoại và trả focus về nút đã mở.

const openTriggers = new WeakMap();
const programmaticCloses = new WeakSet();

function restoreTrigger(dialog) {
    const trigger = openTriggers.get(dialog);
    openTriggers.delete(dialog);
    if (trigger instanceof HTMLElement && trigger.isConnected) trigger.focus();
}

export function register(dialog, dotNetRef) {
    // Esc: không để trình duyệt tự đóng, giao cho .NET quyết định (đang xử lý thì bỏ qua).
    const onCancel = (event) => {
        event.preventDefault();
        dotNetRef.invokeMethodAsync("OnDismissRequested");
    };
    // Trường hợp trình duyệt vẫn đóng được hộp thoại (không qua sự kiện cancel).
    const onClose = () => {
        if (programmaticCloses.delete(dialog)) return;
        restoreTrigger(dialog);
        dotNetRef.invokeMethodAsync("OnDismissRequested");
    };

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
    if (!dialog.open) {
        openTriggers.set(dialog, document.activeElement);
        dialog.showModal();
    }
}

export function close(dialog) {
    if (dialog.open) {
        programmaticCloses.add(dialog);
        dialog.close();
    }
    restoreTrigger(dialog);
}
