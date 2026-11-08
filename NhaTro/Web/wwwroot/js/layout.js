// Xử lý sự kiện dùng chung cho Identity static SSR và điều hướng Blazor.
(() => {
    const root = document.documentElement;
    const storageKey = "nhatro.theme";
    const sidebarWidthKey = "nhatro.sidebar-width";
    const sidebarCollapsedKey = "nhatro.sidebar-collapsed";
    const sidebarWidthMin = 200;
    const sidebarWidthMax = 340;
    const sidebarCollapseAt = 140;
    const contractFormStateKey = "nhatro.contract-create.preview";
    let receiver = null;
    let returnFocus = null;
    const mobile = window.matchMedia("(max-width: 767px)");
    const desktop = window.matchMedia("(min-width: 1200px)");
    let initialTheme = "light";
    try { initialTheme = localStorage.getItem(storageKey) === "dark" ? "dark" : "light"; } catch { }
    root.dataset.theme = initialTheme;
    // Khôi phục trạng thái thu gọn + bề rộng trước lần vẽ đầu tiên để sidebar
    // giống nhau ở mọi lần tải trang / tab mới.
    function restoreSidebarState() {
        try {
            const stored = localStorage.getItem(sidebarCollapsedKey);
            if (root.dataset.sidebarCollapsed !== "true" && root.dataset.sidebarCollapsed !== "false" && stored === "true") {
                root.dataset.sidebarCollapsed = "true";
            }
        } catch { }
        try {
            if (!root.style.getPropertyValue("--sidebar-user-width")) {
                const storedWidth = Number(localStorage.getItem(sidebarWidthKey));
                if (Number.isFinite(storedWidth) && storedWidth > 0) {
                    root.style.setProperty("--sidebar-user-width", Math.min(sidebarWidthMax, Math.max(sidebarWidthMin, storedWidth)) + "px");
                }
            }
        } catch { }
    }
    function persistSidebarCollapsed(collapsed) {
        try { localStorage.setItem(sidebarCollapsedKey, String(collapsed)); } catch { }
    }
    restoreSidebarState();

    // Keep this preview form's values when the VI/EN selector reloads the page.
    window.contractFormPreview = {
        take() {
            try {
                const state = sessionStorage.getItem(contractFormStateKey);
                sessionStorage.removeItem(contractFormStateKey);
                return state;
            } catch {
                return null;
            }
        },
        store() {
            const form = document.querySelector("form.contract-form");
            if (!form) return;
            const value = id => form.querySelector(`#${id}`)?.value ?? "";
            const number = id => value(id) === "" ? null : Number(value(id));
            const members = Array.from(form.querySelectorAll(".member-row"), row => ({
                FullName: row.querySelector(".member-name")?.value ?? "",
                PhoneNumber: row.querySelector(".member-phone")?.value ?? "",
                Relationship: row.querySelector(".member-relationship")?.value ?? "Other"
            }));
            try {
                sessionStorage.setItem(contractFormStateKey, JSON.stringify({
                    PropertyId: number("contract-property"),
                    RoomId: number("contract-room"),
                    RepresentativeTenantId: number("contract-tenant"),
                    StartDate: value("contract-start") || null,
                    EndDate: value("contract-end") || null,
                    MonthlyRentVnd: number("contract-rent"),
                    DepositVnd: number("contract-deposit"),
                    PaymentCycleMonths: number("contract-cycle"),
                    MeterReadingDay: number("contract-meter-day"),
                    Notes: value("contract-notes"),
                    Members: members
                }));
            } catch { }
        }
    };

    function syncControls() {
        if (!root.dataset.theme) root.dataset.theme = initialTheme;
        document.querySelectorAll("[data-theme-toggle]").forEach(button => button.setAttribute("aria-pressed", String(root.dataset.theme === "dark")));
        const expanded = mobile.matches ? root.dataset.drawerOpen === "true" : true;
        document.querySelectorAll("[data-drawer-toggle]").forEach(button => button.setAttribute("aria-expanded", String(expanded)));
        // Desktop: logo thương hiệu đóng vai nút thu gọn/mở rộng sidebar.
        const desktopCollapse = window.innerWidth >= 1200;
        document.querySelectorAll(".app-brand").forEach(brand => {
            const label = desktopCollapse ? brand.dataset.toggleLabel : brand.dataset.label;
            if (label) brand.setAttribute("aria-label", label);
            if (desktopCollapse) brand.setAttribute("aria-expanded", String(root.dataset.sidebarCollapsed !== "true"));
            else brand.removeAttribute("aria-expanded");
        });
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
    // Popover tài khoản ở footer sidebar (Hồ sơ / Cài đặt / Đăng xuất).
    function setAccountMenu(open) {
        const menu = document.getElementById("account-menu");
        if (!menu) return;
        menu.hidden = !open;
        document.querySelector("[data-account-menu-toggle]")?.setAttribute("aria-expanded", String(open));
    }
    // Sidebar được giữ nguyên qua enhanced navigation (<aside data-permanent>), nên
    // tự đánh dấu mục đang mở theo URL hiện tại thay vì chờ server render lại.
    function syncActiveNav() {
        const path = location.pathname.replace(/\/+$/, "") || "/";
        const links = Array.from(document.querySelectorAll(".sidebar-menu .nav-item[href]"));
        // URL có thể khớp nhiều mục (cha + con); chỉ mục có href dài nhất được active.
        let active = null;
        let activeLength = -1;
        links.forEach(link => {
            const href = link.getAttribute("href");
            if (!href) return;
            const target = new URL(href, location.origin).pathname.replace(/\/+$/, "") || "/";
            const matches = target === "/" ? path === "/" : path === target || path.startsWith(target + "/");
            if (!matches || target.length <= activeLength) return;
            active = link;
            activeLength = target.length;
        });
        links.forEach(link => {
            const isActive = link === active;
            link.classList.toggle("active", isActive);
            if (isActive) link.setAttribute("aria-current", "page");
            else link.removeAttribute("aria-current");
        });
    }
    // Chạy lại animation nội dung mỗi lần đổi trang cho chuyển cảnh đồng nhất.
    function animatePage() {
        const surface = document.querySelector(".app-content > .app-surface");
        if (!surface) return;
        surface.classList.remove("page-enter");
        void surface.offsetWidth;
        surface.classList.add("page-enter");
    }
    function onNavigated() {
        setAccountMenu(false);
        syncActiveNav();
        animatePage();
    }
    // VHT-8: nút hiện/ẩn mật khẩu ở trang đăng nhập (static SSR nên xử lý bằng JS thuần).
    function togglePassword(button) {
        const input = document.getElementById(button.dataset.passwordToggle);
        if (!input) return;
        const show = input.type === "password";
        input.type = show ? "text" : "password";
        button.setAttribute("aria-pressed", String(show));
        const label = show ? button.dataset.hideLabel : button.dataset.showLabel;
        if (label) button.setAttribute("aria-label", label);
    }
    async function setCulture(button) {        const control = button.closest(".culture-control");
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
            window.contractFormPreview?.store();
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
            }
        } else if (button?.matches("[data-drawer-close]") || event.target.closest("[data-drawer-overlay]")) {
            closeDrawer();
        } else if (event.target.closest(".app-brand") && window.innerWidth >= 1200) {
            // Desktop: logo thu gọn/mở rộng sidebar thay vì điều hướng trang chủ.
            event.preventDefault();
            const expanding = root.dataset.sidebarCollapsed === "true";
            root.dataset.sidebarCollapsed = expanding ? "false" : "true";
            if (expanding) {
                // Bề rộng đang lưu có thể dưới min (vừa kéo từ rail) -> mở về đúng mức min.
                const width = Number.parseFloat(root.style.getPropertyValue("--sidebar-user-width"));
                if (!Number.isFinite(width) || width < sidebarWidthMin) applySidebarWidth(sidebarWidthMin);
            }
            persistSidebarCollapsed(root.dataset.sidebarCollapsed === "true");
            syncControls();
        } else if (event.target.closest("[data-account-menu-toggle]")) {
            event.preventDefault();
            setAccountMenu(document.getElementById("account-menu")?.hidden ?? false);
        } else if (button?.matches("[data-culture]")) {
            void setCulture(button);
        } else if (button?.matches("[data-password-toggle]")) {
            togglePassword(button);
        }
        if (!event.target.closest(".sidebar-account-wrap")) setAccountMenu(false);
        if (event.target.closest(".sidebar-menu a,.sidebar-footer a,.app-brand")) closeDrawer(false);
        if (event.target.closest("#blazor-error-ui .dismiss")) document.getElementById("blazor-error-ui").style.display = "none";
    });
    // Kéo tay resizer: bám chuột có giảm tốc, thả ra thì snap mượt về rail hoặc trong khoảng min-max.
    let resizeState = null;
    const applySidebarWidth = width => root.style.setProperty("--sidebar-user-width", Math.round(width) + "px");
    const setSidebarNarrow = width => root.dataset.sidebarNarrow = String(width < sidebarWidthMin);
    const runResizeFrame = () => {
        if (!resizeState) return;
        const diff = resizeState.target - resizeState.current;
        const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        if (reduceMotion || Math.abs(diff) < 0.5) {
            resizeState.current = resizeState.target;
        } else {
            resizeState.current += diff * 0.28;
        }
        applySidebarWidth(resizeState.current);
        setSidebarNarrow(resizeState.current);
        resizeState.raf = Math.abs(diff) < 0.5 ? 0 : requestAnimationFrame(runResizeFrame);
    };
    document.addEventListener("pointerdown", event => {
        if (!(event.target instanceof Element) || event.button !== 0) return;
        const handle = event.target.closest("[data-sidebar-resizer]");
        if (!handle || window.innerWidth < 1200) return;
        event.preventDefault();
        const startWidth = document.getElementById("app-sidebar")?.getBoundingClientRect().width ?? 240;
        applySidebarWidth(startWidth);
        root.dataset.sidebarCollapsed = "false";
        setSidebarNarrow(startWidth);
        resizeState = { pointerId: event.pointerId, target: Math.min(sidebarWidthMax, Math.max(72, event.clientX)), current: startWidth, raf: 0 };
        handle.setPointerCapture(event.pointerId);
        handle.dataset.dragging = "true";
        root.dataset.sidebarResizing = "true";
        syncControls();
        resizeState.raf = requestAnimationFrame(runResizeFrame);
    });
    document.addEventListener("pointermove", event => {
        if (!resizeState || event.pointerId !== resizeState.pointerId) return;
        resizeState.target = Math.min(sidebarWidthMax, Math.max(72, event.clientX));
        if (!resizeState.raf) resizeState.raf = requestAnimationFrame(runResizeFrame);
    });
    const endSidebarResize = event => {
        if (!resizeState || event.pointerId !== resizeState.pointerId) return;
        if (resizeState.raf) cancelAnimationFrame(resizeState.raf);
        const finalWidth = resizeState.current;
        resizeState = null;
        document.querySelectorAll("[data-sidebar-resizer]").forEach(handle => delete handle.dataset.dragging);
        // Bật lại transition trước khi snap để hiệu ứng thu gọn/mở rộng chạy mượt.
        root.removeAttribute("data-sidebar-resizing");
        root.removeAttribute("data-sidebar-narrow");
        if (finalWidth < sidebarCollapseAt) {
            root.dataset.sidebarCollapsed = "true";
            persistSidebarCollapsed(true);
        } else {
            root.dataset.sidebarCollapsed = "false";
            persistSidebarCollapsed(false);
            const width = Math.min(sidebarWidthMax, Math.max(sidebarWidthMin, finalWidth));
            applySidebarWidth(width);
            try { localStorage.setItem(sidebarWidthKey, String(width)); } catch { }
        }
        syncControls();
    };
    document.addEventListener("pointerup", endSidebarResize);
    document.addEventListener("pointercancel", endSidebarResize);
    document.addEventListener("keydown", event => {
        if (event.key === "Escape") setAccountMenu(false);
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
    document.addEventListener("animationend", event => {
        if (event.animationName === "page-in" && event.target instanceof Element) event.target.classList.remove("page-enter");
    });
    // Enhanced navigation thay DOM nhưng dùng lại <html>: khôi phục trạng thái sidebar
    // và đồng bộ mục active sau mỗi lần điều hướng bằng trang SSR tĩnh.
    document.addEventListener("DOMContentLoaded", () => {
        window.Blazor?.addEventListener("enhancedload", () => {
            restoreSidebarState();
            syncControls();
            if (root.dataset.drawerOpen === "true") closeDrawer(false);
            onNavigated();
        });
    }, { once: true });
    // Quay lại từ bfcache: DOM cũ hiện lại nên trạng thái điều khiển cần được đồng bộ.
    window.addEventListener("pageshow", event => {
        if (!event.persisted) return;
        restoreSidebarState();
        syncControls();
        onNavigated();
    });
    window.appLayout = {
        connect: reference => { receiver = reference; syncControls(); return root.dataset.theme === "dark"; },
        disconnect: () => { receiver = null; closeDrawer(false); },
        onNavigated
    };
})();
