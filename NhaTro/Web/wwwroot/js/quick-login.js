/* Lọc panel "đăng nhập nhanh" theo tên/email; ẩn nhóm không còn dòng khớp.
   Progressive enhancement: không có JS thì panel vẫn bấm đăng nhập được. */
(() => {
    function init() {
        document.querySelectorAll("[data-quick-login]").forEach(panel => {
            if (panel.dataset.ready === "true") return;
            panel.dataset.ready = "true";

            const search = panel.querySelector("[data-quick-login-search]");
            const rows = [...panel.querySelectorAll("[data-quick-login-row]")];
            const groups = [...panel.querySelectorAll("[data-quick-login-group]")];
            const empty = panel.querySelector("[data-quick-login-empty]");

            const apply = () => {
                const query = (search?.value || "").trim().toLowerCase();
                let visible = 0;

                rows.forEach(row => {
                    const hit = query.length === 0 || (row.dataset.search || "").includes(query);
                    row.hidden = !hit;
                    if (hit) visible++;
                });

                groups.forEach(group => {
                    const hasRows = group.querySelector("[data-quick-login-row]:not([hidden])");
                    group.hidden = !hasRows;
                    if (query.length > 0 && hasRows) {
                        group.open = true;
                    }
                });

                if (empty) empty.hidden = visible !== 0;
            };

            search?.addEventListener("input", apply);
            apply();
        });
    }

    document.addEventListener("DOMContentLoaded", init, { once: true });
    document.addEventListener("DOMContentLoaded", () => {
        window.Blazor?.addEventListener("enhancedload", init);
    }, { once: true });
})();
