// Bật/tắt đúng dòng giải thích của nút, dùng được cả trên trang static SSR.
(() => {
    function setExpanded(button, expanded) {
        const description = document.getElementById(button.getAttribute("aria-controls"));
        if (!description) return;
        description.hidden = !expanded;
        button.setAttribute("aria-expanded", String(expanded));
    }

    document.addEventListener("click", event => {
        const button = event.target instanceof Element
            ? event.target.closest(".page-help-button[aria-controls]")
            : null;
        if (button) setExpanded(button, button.getAttribute("aria-expanded") !== "true");
    });

    // Escape đóng phần giải thích; Enter/Space dùng hành vi click tự nhiên của button.
    document.addEventListener("keydown", event => {
        if (event.key !== "Escape") return;
        document.querySelectorAll('.page-help-button[aria-expanded="true"]')
            .forEach(button => setExpanded(button, false));
    });
})();
