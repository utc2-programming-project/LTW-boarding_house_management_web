window.globalSearch = (() => {
    const listeners = new WeakMap();
    return {
        register(input, root) {
            const shortcut = event => {
                if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
                    event.preventDefault();
                    input.focus();
                }
            };
            const outside = event => { if (!root.contains(event.target)) input.blur(); };
            // C05: chỉ chặn mặc định của phím mũi tên khi dropdown đang mở,
            // để việc sửa con trỏ trong ô tìm vẫn bình thường khi không có kết quả.
            const keys = event => {
                const expanded = input.getAttribute('aria-expanded');
                if (expanded === null || expanded === 'false') return;
                if (event.key === 'ArrowDown' || event.key === 'ArrowUp') event.preventDefault();
            };
            document.addEventListener('keydown', shortcut);
            document.addEventListener('pointerdown', outside);
            input.addEventListener('keydown', keys);
            listeners.set(root, { shortcut, outside, keys, input });
        },
        unregister(root) {
            const item = listeners.get(root);
            if (!item) return;
            document.removeEventListener('keydown', item.shortcut);
            document.removeEventListener('pointerdown', item.outside);
            if (item.input) item.input.removeEventListener('keydown', item.keys);
            listeners.delete(root);
        },
        scrollActive(id) {
            if (!id) return;
            const element = document.getElementById(id);
            if (element) element.scrollIntoView({ block: 'nearest' });
        }
    };
})();
