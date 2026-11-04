// Escape + khóa focus + hoàn tiêu điểm cho các hộp thoại overlay (role="dialog" + aria-modal="true").
// Hộp thoại dùng thẻ <dialog> gốc (TenantDialogShell, ConfirmDialog) đã có hành vi này nên không nằm ở đây.
// Lớp phủ đóng hộp thoại được đánh dấu data-dialog-dismiss: cùng phần tử xử lý @onclick mà người dùng vẫn bấm.
(() => {
    const selector = '[role="dialog"][aria-modal="true"]';
    const focusableSelector = [
        'a[href]', 'button:not([disabled])', 'input:not([disabled])',
        'select:not([disabled])', 'textarea:not([disabled])', '[tabindex]:not([tabindex="-1"])'
    ].join(',');

    const stack = [];
    const tracked = new WeakSet();

    const focusableIn = (root) =>
        [...root.querySelectorAll(focusableSelector)].filter((item) => item.getClientRects().length);

    const dismissTarget = (dialog) => dialog.closest('[data-dialog-dismiss]') ?? dialog;

    const open = (dialog) => {
        if (stack.some((entry) => entry.dialog === dialog)) return;
        stack.push({ dialog, trigger: document.activeElement });
        const items = focusableIn(dialog);
        if (items.length) items[0].focus();
    };

    const closed = (dialog) => {
        const index = stack.findIndex((entry) => entry.dialog === dialog);
        if (index < 0) return;
        const [entry] = stack.splice(index, 1);
        const trigger = entry.trigger;
        if (trigger instanceof HTMLElement && trigger.isConnected && !trigger.contains(document.activeElement)) {
            trigger.focus();
        }
    };

    document.addEventListener('keydown', (event) => {
        const top = stack[stack.length - 1];
        if (!top) return;

        if (event.key === 'Escape') {
            event.preventDefault();
            dismissTarget(top.dialog).click();
            return;
        }

        if (event.key !== 'Tab') return;

        const items = focusableIn(top.dialog);
        if (!items.length) {
            event.preventDefault();
            return;
        }

        const first = items[0];
        const last = items[items.length - 1];
        const active = document.activeElement;

        if (!top.dialog.contains(active)) {
            event.preventDefault();
            (event.shiftKey ? last : first).focus();
        } else if (event.shiftKey && active === first) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && active === last) {
            event.preventDefault();
            first.focus();
        }
    }, true);

    const sync = () => {
        document.querySelectorAll(selector).forEach((dialog) => {
            if (tracked.has(dialog)) return;
            tracked.add(dialog);
            open(dialog);
        });
        stack.filter((entry) => !entry.dialog.isConnected)
            .forEach((entry) => closed(entry.dialog));
    };

    const start = () => {
        new MutationObserver(sync).observe(document.body, { childList: true, subtree: true });
        sync();
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start, { once: true });
    } else {
        start();
    }
})();
