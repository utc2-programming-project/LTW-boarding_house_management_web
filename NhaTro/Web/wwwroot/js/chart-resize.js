// Do bề rộng khung biểu đồ để Blazor vẽ lại SVG theo pixel thật thay vì phóng to/thu nhỏ.
(function () {
    const observers = new Map();

    function unobserve(element) {
        const observer = observers.get(element);
        if (observer) {
            observer.disconnect();
            observers.delete(element);
        }
    }

    window.nhatroCharts = {
        observe(element, dotNetRef) {
            if (!(element instanceof Element) || !dotNetRef) {
                return;
            }
            unobserve(element);
            const observer = new ResizeObserver(entries => {
                const width = Math.round(entries[0].contentRect.width);
                if (width > 0) {
                    dotNetRef.invokeMethodAsync("OnChartWidthChanged", width).catch(() => { });
                }
            });
            observer.observe(element);
            observers.set(element, observer);
        },
        unobserve
    };
})();
