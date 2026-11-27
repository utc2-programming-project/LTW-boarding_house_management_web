window.globalSearch = (() => {
    const listeners = new WeakMap();
    return {
        register(input, root) {
            const shortcut = event => {
                if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
                    event.preventDefault(); input.focus();
                }
            };
            const outside = event => { if (!root.contains(event.target)) input.blur(); };
            document.addEventListener('keydown', shortcut);
            document.addEventListener('pointerdown', outside);
            listeners.set(root, { shortcut, outside });
        },
        unregister(root) {
            const item = listeners.get(root);
            if (!item) return;
            document.removeEventListener('keydown', item.shortcut);
            document.removeEventListener('pointerdown', item.outside);
            listeners.delete(root);
        }
    };
})();
