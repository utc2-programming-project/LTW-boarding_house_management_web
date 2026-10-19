// Xử lý sự kiện dùng chung cho Identity static SSR và điều hướng Blazor.
(() => {
    const root = document.documentElement;
    const storageKey = "nhatro.theme";
    let receiver = null;
    let returnFocus = null;
    const mobile = window.matchMedia("(max-width: 767px)");
    const desktop = window.matchMedia("(min-width: 1200px)");
    let initialTheme = "light";
    try { initialTheme = localStorage.getItem(storageKey) === "dark" ? "dark" : "light"; } catch { }
    root.dataset.theme = initialTheme;

    function syncControls() {
        if (!root.dataset.theme) root.dataset.theme = initialTheme;
        document.querySelectorAll("[data-theme-toggle]").forEach(button => button.setAttribute("aria-pressed", String(root.dataset.theme === "dark")));
        const expanded = mobile.matches ? root.dataset.drawerOpen === "true" : window.innerWidth >= 1200 && root.dataset.sidebarCollapsed !== "true";
        document.querySelectorAll("[data-drawer-toggle]").forEach(button => button.setAttribute("aria-expanded", String(expanded)));
    }
    function closeDrawer(restoreFocus = true) {
        const wasOpen = root.dataset.drawerOpen === "true";
        root.dataset.drawerOpen = "false";
        document.querySelectorAll("[data-drawer-overlay]").forEach(overlay => overlay.hidden = true);
        const sidebar = document.getElementById("app-sidebar");
        if (sidebar) {
            sidebar.removeAttribute("role");
            sidebar.removeAttribute("aria-modal");
        }
        document.querySelector(".app-workspace")?.removeAttribute("inert");
        document.querySelector(".skip-link")?.removeAttribute("inert");
        if (wasOpen && restoreFocus && returnFocus?.isConnected) returnFocus.focus();
        syncControls();
    }
    function openDrawer(button) {
        returnFocus = button;
        root.dataset.drawerOpen = "true";
        document.querySelectorAll("[data-drawer-overlay]").forEach(overlay => overlay.hidden = false);
        const sidebar = document.getElementById("app-sidebar");
        if (sidebar) {
            sidebar.setAttribute("role", "dialog");
            sidebar.setAttribute("aria-modal", "true");
            sidebar.querySelector("[data-drawer-close]")?.focus();
        }
        document.querySelector(".app-workspace")?.setAttribute("inert", "");
        document.querySelector(".skip-link")?.setAttribute("inert", "");
        syncControls();
    }
    async function setCulture(button) {
        const control = button.closest(".culture-control");
        if (!control || control.getAttribute("aria-busy") === "true" || button.getAttribute("aria-pressed") === "true") return;
        const error = control.querySelector("[data-culture-error]");
        const status = control.querySelector("[data-culture-status]");
        error.hidden = true;
        control.setAttribute("aria-busy", "true");
        control.querySelectorAll("button").forEach(item => item.disabled = true);
        status.textContent = status.dataset.loadingLabel;
        try {
            const token = control.querySelector('input[name="__RequestVerificationToken"]')?.value;
            if (!token) throw new Error("Missing antiforgery token");
            const response = await fetch(`/culture/set?culture=${encodeURIComponent(button.dataset.culture)}`, {
                method: "POST",
                credentials: "same-origin",
                headers: { RequestVerificationToken: token }
            });
            if (!response.ok) throw new Error("Culture update failed");
            window.location.reload();
        } catch {
            error.hidden = false;
            status.textContent = "";
            control.removeAttribute("aria-busy");
            control.querySelectorAll("button").forEach(item => item.disabled = false);
        }
    }
    document.addEventListener("click", event => {
        if (!(event.target instanceof Element)) return;
        const button = event.target.closest("button");
        if (button?.matches("[data-theme-toggle]")) {
            root.dataset.theme = root.dataset.theme === "dark" ? "light" : "dark";
            initialTheme = root.dataset.theme;
            try { localStorage.setItem(storageKey, root.dataset.theme); } catch { }
            syncControls();
            receiver?.invokeMethodAsync("SetTheme", root.dataset.theme === "dark").catch(() => { });
        } else if (button?.matches("[data-drawer-toggle]")) {
            if (mobile.matches) {
                if (root.dataset.drawerOpen === "true") closeDrawer();
                else openDrawer(button);
            } else if (window.innerWidth >= 1200) {
                root.dataset.sidebarCollapsed = root.dataset.sidebarCollapsed === "true" ? "false" : "true";
                syncControls();
            }
        } else if (button?.matches("[data-drawer-close]") || event.target.closest("[data-drawer-overlay]")) {
            closeDrawer();
        } else if (button?.matches("[data-culture]")) {
            void setCulture(button);
        }
        if (event.target.closest(".sidebar-menu a,.app-brand")) closeDrawer(false);
        if (event.target.closest("#blazor-error-ui .dismiss")) document.getElementById("blazor-error-ui").style.display = "none";
    });
    document.addEventListener("keydown", event => {
        if (!mobile.matches || root.dataset.drawerOpen !== "true") return;
        if (event.key === "Escape") { event.preventDefault(); closeDrawer(); return; }
        if (event.key !== "Tab") return;
        const sidebar = document.getElementById("app-sidebar");
        const controls = [...(sidebar?.querySelectorAll('a[href],button:not([disabled]),[tabindex="0"]') ?? [])].filter(item => item.getClientRects().length);
        if (!controls.length) { event.preventDefault(); sidebar?.focus(); return; }
        const first = controls[0], last = controls[controls.length - 1];
        if (event.shiftKey && (document.activeElement === first || document.activeElement === sidebar)) { event.preventDefault(); last.focus(); }
        else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    });
    // Các listener chỉ đăng ký một lần; enhanced navigation dùng lại document.
    document.addEventListener("DOMContentLoaded", syncControls, { once: true });
    mobile.addEventListener("change", () => closeDrawer());
    desktop.addEventListener("change", syncControls);
    const observer = new MutationObserver(() => {
        syncControls();
        if (root.dataset.drawerOpen === "true" && !document.getElementById("app-sidebar")) closeDrawer(false);
    });
    document.addEventListener("DOMContentLoaded", () => observer.observe(document.body, { childList: true, subtree: true }), { once: true });
    window.appLayout = {
        connect: reference => { receiver = reference; syncControls(); return root.dataset.theme === "dark"; },
        disconnect: () => { receiver = null; closeDrawer(false); }
    };
})();
