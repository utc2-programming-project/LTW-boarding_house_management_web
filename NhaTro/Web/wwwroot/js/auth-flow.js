/* Luồng tài khoản: hướng chuyển tab, OTP nhiều ô và đếm ngược gửi lại.
   Tất cả đều là progressive enhancement; form static SSR vẫn chạy khi không có JS. */
(() => {
    const directionKey = "nhatro.auth.direction";
    const fromIndexKey = "nhatro.auth.from";
    const heightKey = "nhatro.auth.height";

    /** Ghi nhớ hướng chuyển (tab nào -> tab nào) để chạy animation đúng chiều. */
    function rememberDirection(tab) {
        const current = document.querySelector("[data-auth-tab][aria-current='page']");
        const from = current ? Number(current.dataset.authIndex) : 0;
        const to = Number(tab.dataset.authIndex);
        if (!Number.isFinite(to) || to < 0) return null;

        const direction = to >= from ? "forward" : "back";
        try {
            sessionStorage.setItem(directionKey, direction);
            sessionStorage.setItem(fromIndexKey, String(Number.isFinite(from) ? from : 0));
        } catch {
            // sessionStorage có thể bị chặn; bỏ animation là đủ.
        }
        return direction;
    }

    /** Nội dung trượt ra ngay khi bấm tab, chạy song song trong lúc chờ trang mới. */
    function playLeave(direction) {
        const card = document.querySelector(".auth-card");
        if (!card) return;

        card.classList.remove(
            "auth-enter-forward", "auth-enter-back",
            "auth-leave-forward", "auth-leave-back");
        void card.offsetWidth;
        card.classList.add(`auth-leave-${direction}`);

        // Trang mới thường tới trước mốc này. Nếu không (điều hướng bị huỷ, lỗi mạng)
        // thì trả nội dung về chỗ cũ thay vì để card trống.
        setTimeout(() => {
            card.classList.remove("auth-leave-forward", "auth-leave-back");
        }, 1500);
    }

    /** Ghi nhớ chiều cao card hiện tại để trang mới co giãn từ đúng mốc đó. */
    function rememberHeight() {
        const card = document.querySelector(".auth-card");
        if (!card) return;
        try {
            sessionStorage.setItem(heightKey, String(card.offsetHeight));
        } catch {
            // Không lưu được thì bỏ animation chiều cao.
        }
    }

    /** Card 3 tab cao thấp khác nhau; chạy từ chiều cao cũ sang chiều cao mới
        thay vì để mép dưới nhảy một nhịp. */
    function animateCardHeight() {
        const card = document.querySelector(".auth-card");
        if (!card) return;

        let previous = 0;
        try {
            previous = Number(sessionStorage.getItem(heightKey)) || 0;
            sessionStorage.removeItem(heightKey);
        } catch {
            return;
        }

        const next = card.offsetHeight;
        if (!previous || !next || Math.abs(previous - next) < 2) return;

        card.style.height = `${previous}px`;
        void card.offsetWidth;
        card.classList.add("auth-resize");
        card.style.height = `${next}px`;

        const clear = () => {
            card.classList.remove("auth-resize");
            card.style.removeProperty("height");
        };
        card.addEventListener("transitionend", clear, { once: true });
        setTimeout(clear, 400);
    }

    function animateAuthCard() {
        const card = document.querySelector(".auth-card");
        if (!card) return;
        let direction = null;
        try {
            direction = sessionStorage.getItem(directionKey);
            sessionStorage.removeItem(directionKey);
        } catch {
            return;
        }

        if (!direction) return;
        card.classList.remove(
            "auth-enter-forward", "auth-enter-back",
            "auth-leave-forward", "auth-leave-back");
        void card.offsetWidth;
        card.classList.add(`auth-enter-${direction}`);
        const clear = () => card.classList.remove("auth-enter-forward", "auth-enter-back");
        card.addEventListener("animationend", clear, { once: true });
        setTimeout(clear, 500);
    }

    /** Thanh tab là khối mới sau mỗi lần đổi trang, nên cho indicator chạy lại từ vị trí cũ
        để ô sáng trượt sang tab mới thay vì nhảy thẳng. */
    function animateIndicator() {
        const indicator = document.querySelector(".auth-tabs-indicator");
        if (!indicator) return;

        let from = null;
        try {
            from = sessionStorage.getItem(fromIndexKey);
            sessionStorage.removeItem(fromIndexKey);
        } catch {
            return;
        }

        // Giá trị server render sẵn (style="--auth-index:N") là đích cần trả về.
        const target = (indicator.style.getPropertyValue("--auth-index") || "").trim();
        if (from === null || from === "" || target === "" || from === target) return;

        indicator.style.setProperty("--auth-index", from);
        void indicator.offsetWidth;
        indicator.style.setProperty("--auth-index", target);
    }

    function initOtp() {
        const host = document.querySelector("[data-otp]");
        const source = host ? document.getElementById(host.dataset.source) : null;
        if (!host || !source || host.dataset.ready === "true") return;

        const length = Math.max(4, Math.min(8, Number(host.dataset.length) || 6));
        const labelTemplate = host.dataset.cellLabel || "Digit {0}";
        const cells = [];

        source.classList.add("otp-native-hidden");
        source.setAttribute("aria-hidden", "true");

        for (let index = 0; index < length; index++) {
            const cell = document.createElement("input");
            cell.type = "text";
            cell.inputMode = "numeric";
            cell.maxLength = 1;
            cell.autocomplete = index === 0 ? "one-time-code" : "off";
            cell.id = `${source.id}-digit-${index + 1}`;
            cell.setAttribute("aria-label", labelTemplate.replace("{0}", index + 1));
            cell.className = "otp-cell";
            host.append(cell);
            cells.push(cell);
        }

        const label = document.querySelector(`label[for="${source.id}"]`);
        if (label && cells[0]) label.htmlFor = cells[0].id;

        const sync = () => {
            source.value = cells.map(cell => cell.value).join("");
            source.dispatchEvent(new Event("input", { bubbles: true }));
        };

        const fillFromSource = () => {
            const digits = (source.value || "").replace(/\D/g, "").slice(0, length);
            cells.forEach((cell, index) => {
                cell.value = digits[index] ?? "";
            });
        };

        cells.forEach((cell, index) => {
            cell.addEventListener("input", () => {
                cell.value = cell.value.replace(/\D/g, "").slice(-1);
                if (cell.value && cells[index + 1]) cells[index + 1].focus();
                sync();
            });
            cell.addEventListener("keydown", event => {
                if (event.key === "Backspace" && !cell.value && cells[index - 1]) {
                    event.preventDefault();
                    cells[index - 1].focus();
                    cells[index - 1].value = "";
                    sync();
                }
                if (event.key === "ArrowLeft" && cells[index - 1]) cells[index - 1].focus();
                if (event.key === "ArrowRight" && cells[index + 1]) cells[index + 1].focus();
            });
            cell.addEventListener("paste", event => {
                event.preventDefault();
                const digits = (event.clipboardData?.getData("text") || "").replace(/\D/g, "").slice(0, length);
                if (!digits) return;
                cells.forEach((item, itemIndex) => {
                    item.value = digits[itemIndex] ?? "";
                });
                cells[Math.min(digits.length, length - 1)].focus();
                sync();
            });
        });

        fillFromSource();
        host.dataset.ready = "true";
        setTimeout(() => {
            if (document.activeElement?.tagName !== "INPUT" && !cells.some(cell => cell.value)) {
                cells[0]?.focus();
            }
        }, 350);
    }

    function initResend() {
        document.querySelectorAll("[data-resend-at]").forEach(button => {
            if (button.dataset.ready === "true") return;
            button.dataset.ready = "true";

            const at = Date.parse(button.dataset.resendAt || "");
            if (!Number.isFinite(at)) {
                button.disabled = false;
                return;
            }

            const label = button.dataset.resendLabel || button.textContent || "Resend";
            const template = button.dataset.resendTemplate || "Resend in {0}s";
            const tick = () => {
                const remaining = Math.max(0, Math.ceil((at - Date.now()) / 1000));
                if (remaining <= 0) {
                    button.disabled = false;
                    button.textContent = label;
                    clearInterval(timer);
                    return;
                }
                button.disabled = true;
                button.textContent = template.replace("{0}", String(remaining));
            };
            const timer = setInterval(tick, 1000);
            tick();
        });
    }

    function init() {
        initOtp();
        initResend();
        animateIndicator();
        animateCardHeight();
        animateAuthCard();
    }

    // Không preventDefault: để enhanced navigation chạy ngay, animation trượt ra
    // chạy song song trong lúc chờ server trả trang mới.
    document.addEventListener("click", event => {
        if (!(event.target instanceof Element)) return;
        const tab = event.target.closest("[data-auth-tab]");
        if (!tab) return;
        // Chuột trái + không phím phụ (mở tab mới thì đừng chạy animation).
        if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        if (tab.getAttribute("aria-current") === "page") return;

        const direction = rememberDirection(tab);
        if (direction) {
            rememberHeight();
            playLeave(direction);
        }
    });

    // Quay lại bằng Back/Forward (trang lấy từ bfcache) có thể còn class trượt ra.
    window.addEventListener("pageshow", () => {
        document.querySelectorAll(".auth-card.auth-leave-forward, .auth-card.auth-leave-back")
            .forEach(card => card.classList.remove("auth-leave-forward", "auth-leave-back"));
    });

    document.addEventListener("DOMContentLoaded", init, { once: true });
    document.addEventListener("DOMContentLoaded", () => {
        window.Blazor?.addEventListener("enhancedload", init);
    }, { once: true });
})();
