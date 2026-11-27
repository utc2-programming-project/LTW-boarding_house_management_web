// Quét QR bằng camera cho kiosk. Dùng BarcodeDetector của trình duyệt
// (Chrome Android hỗ trợ sẵn); trình duyệt không có API thì trả "unsupported"
// để C# hướng dẫn nhập mã bằng tay.
window.vhtQrScan = (function () {
    let stream = null;
    let frame = null;
    let callback = null;
    let video = null;

    function cleanup() {
        if (frame) {
            cancelAnimationFrame(frame);
            frame = null;
        }
        if (video) {
            video.pause();
            video.srcObject = null;
            video = null;
        }
        if (stream) {
            stream.getTracks().forEach(track => track.stop());
            stream = null;
        }
        callback = null;
    }

    function log(step, extra) {
        console.info("[qr-scan]", step, extra ?? "");
    }

    async function start(videoId, dotnetRef) {
        log("start", videoId);
        if (!("BarcodeDetector" in window) || !navigator.mediaDevices?.getUserMedia) {
            log("unsupported-api", { barcodeDetector: "BarcodeDetector" in window, mediaDevices: !!navigator.mediaDevices });
            return "unsupported";
        }

        stop();
        video = document.getElementById(videoId);
        if (!video) {
            log("no-video-element", videoId);
            return "novideo";
        }
        const v = video;

        let detector;
        try {
            detector = new window.BarcodeDetector({ formats: ["qr_code"] });
            log("detector-ok");
        } catch (e) {
            log("detector-error", e);
            return "unsupported";
        }

        try {
            log("getUserMedia-request");
            stream = await Promise.race([
                navigator.mediaDevices.getUserMedia({
                    video: { facingMode: { ideal: "environment" } },
                    audio: false
                }),
                new Promise(resolve => setTimeout(() => resolve("timeout"), 15000))
            ]);
            if (stream === "timeout") {
                log("getUserMedia-timeout");
                stream = null;
                return "camera:timeout";
            }
            log("getUserMedia-ok");
        } catch (e) {
            // Trả tên lỗi (NotAllowedError/NotFoundError/NotReadableError...) để C# hiển thị.
            log("getUserMedia-error", e && e.name);
            return "camera:" + (e && e.name ? e.name : "Error");
        }

        v.autoplay = true;
        v.muted = true;
        v.setAttribute("playsinline", "");
        v.srcObject = stream;
        log("srcObject-attached");

        try {
            await v.play();
            log("play-ok");
        } catch (e) {
            // Một số máy chặn autoplay; vẫn chờ metadata bên dưới.
            log("play-error", e && e.name);
        }

        if (!v.videoWidth) {
            await new Promise(resolve => {
                v.addEventListener("loadedmetadata", resolve, { once: true });
                setTimeout(resolve, 3000);
            });
        }

        log("metadata", { videoWidth: v.videoWidth, readyState: v.readyState });
        if (!v.videoWidth) {
            cleanup();
            return "nostream";
        }

        callback = dotnetRef;

        const tick = async () => {
            if (!stream) return;
            try {
                const codes = await detector.detect(v);
                const text = codes.length > 0 ? codes[0].rawValue : null;
                if (text) {
                    const ref = callback;
                    cleanup();
                    // JS đã tắt camera; C# nhận mã và tự kiểm tra.
                    ref?.invokeMethodAsync("OnQrDetected", text);
                    return;
                }
            } catch (e) {
                // Frame lỗi (video chưa sẵn sàng): thử frame kế tiếp.
            }
            frame = requestAnimationFrame(tick);
        };
        frame = requestAnimationFrame(tick);
        log("started");
        return "started";
    }

    function stop() {
        cleanup();
    }

    return { start, stop };
})();
