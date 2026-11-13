// Thẻ giải thích khi hover/focus cho badge nằm trong bảng cuộn ngang (.table-card).
// Thẻ dùng position:fixed và canh lại theo khung nhìn để không bị vùng cuộn cắt.
// Uỷ quyền sự kiện ở document nên vẫn chạy sau khi Blazor điều hướng/tải lại bảng.
(function () {
    const triggerSelector = '[data-hover-tip]';
    const tipSelector = '.hover-tip';
    const gap = 8;
    const margin = 12;

    function tipOf(trigger) {
        return trigger.querySelector(':scope > ' + tipSelector);
    }

    function place(trigger) {
        const tip = tipOf(trigger);
        if (!tip) {
            return;
        }

        const anchor = trigger.getBoundingClientRect();

        // Đo kích thước thẻ và gốc khối chứa: top/left = 0 cho ra đúng gốc khối chứa
        // (khác viewport nếu tổ tiên nào đó dùng transform/filter), nhờ vậy toạ độ
        // tính theo viewport vẫn đặt đúng chỗ.
        tip.style.top = '0px';
        tip.style.left = '0px';
        const box = tip.getBoundingClientRect();
        const originLeft = box.left;
        const originTop = box.top;
        const viewportWidth = document.documentElement.clientWidth;
        const viewportHeight = window.innerHeight;

        // Ưu tiên dưới badge; hết chỗ thì lật lên trên, luôn giữ trong khung nhìn.
        let top = anchor.bottom + gap;
        if (top + box.height > viewportHeight - margin) {
            top = Math.max(margin, anchor.top - gap - box.height);
        }

        let left = anchor.left;
        if (left + box.width > viewportWidth - margin) {
            left = viewportWidth - margin - box.width;
        }
        left = Math.max(margin, left);

        tip.style.top = Math.round(top - originTop) + 'px';
        tip.style.left = Math.round(left - originLeft) + 'px';
    }

    function open(trigger) {
        const tip = tipOf(trigger);
        if (!tip) {
            return;
        }
        place(trigger);
        tip.classList.add('is-open');
    }

    function close(trigger) {
        const tip = tipOf(trigger);
        if (tip) {
            tip.classList.remove('is-open');
        }
    }

    function closestTrigger(node) {
        return node instanceof Element ? node.closest(triggerSelector) : null;
    }

    function contains(trigger, node) {
        return node instanceof Node && trigger.contains(node);
    }

    document.addEventListener('mouseover', function (event) {
        const trigger = closestTrigger(event.target);
        if (!trigger || contains(trigger, event.relatedTarget)) {
            return;
        }
        open(trigger);
    });

    document.addEventListener('mouseout', function (event) {
        const trigger = closestTrigger(event.target);
        if (!trigger || contains(trigger, event.relatedTarget)) {
            return;
        }
        close(trigger);
    });

    document.addEventListener('focusin', function (event) {
        const trigger = closestTrigger(event.target);
        if (trigger) {
            open(trigger);
        }
    });

    document.addEventListener('focusout', function (event) {
        const trigger = closestTrigger(event.target);
        if (trigger) {
            close(trigger);
        }
    });

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape') {
            closeAll();
        }
    });

    // Cuộn/đổi kích thước làm toạ độ đã tính bị lệch — đóng thay vì để thẻ trôi sai chỗ.
    function closeAll() {
        document.querySelectorAll(tipSelector + '.is-open').forEach(function (tip) {
            tip.classList.remove('is-open');
        });
    }

    window.addEventListener('scroll', closeAll, true);
    window.addEventListener('resize', closeAll);
})();
