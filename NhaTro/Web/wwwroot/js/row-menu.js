// Menu thao tác theo hàng của bảng: định vị bằng position: fixed để không bị khung cuộn
// của bảng cắt mất, và đóng khi bấm ra ngoài, nhấn Escape hoặc cuộn trang.
(() => {
    const selector = '[data-row-menu]';
    const pad = 8;
    const gap = 6;
    let current = null;

    const triggerOf = (menu) => menu.parentElement?.querySelector('.p2-menu-trigger') ?? null;

    const place = (menu) => {
        const trigger = triggerOf(menu);
        if (!trigger) return;

        const anchor = trigger.getBoundingClientRect();
        const box = menu.getBoundingClientRect();

        let top = anchor.bottom + gap;
        if (top + box.height > window.innerHeight - pad && anchor.top - gap - box.height >= pad) {
            top = anchor.top - gap - box.height;
        }
        top = Math.max(pad, Math.min(top, window.innerHeight - pad - box.height));

        let left = anchor.right - box.width;
        left = Math.max(pad, Math.min(left, window.innerWidth - pad - box.width));

        menu.style.position = 'fixed';
        menu.style.top = `${top}px`;
        menu.style.left = `${left}px`;
        menu.style.right = 'auto';
    };

    const close = (menu) => {
        if (!menu || menu.dataset.closing) return;
        menu.dataset.closing = 'true';
        triggerOf(menu)?.click();
    };

    const sync = () => {
        const menu = document.querySelector(selector);
        if (menu === current) {
            if (menu) place(menu);
            return;
        }

        current = menu;
        if (!menu) return;

        place(menu);
        const first = menu.querySelector('[role="menuitem"]');
        if (first) first.focus({ preventScroll: true });
    };

    document.addEventListener('click', (event) => {
        if (!current) return;
        if (current.contains(event.target)) return;
        if (triggerOf(current)?.contains(event.target)) return;
        close(current);
    }, true);

    document.addEventListener('keydown', (event) => {
        if (event.key !== 'Escape' || !current) return;
        event.preventDefault();
        const trigger = triggerOf(current);
        close(current);
        trigger?.focus({ preventScroll: true });
    }, true);

    window.addEventListener('scroll', () => {
        if (current) close(current);
    }, true);

    window.addEventListener('resize', () => {
        if (current) place(current);
    });

    const start = () => new MutationObserver(sync).observe(document.body, { childList: true, subtree: true });

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start, { once: true });
    } else {
        start();
    }
})();
