window.appDocumentUpload = {
    openFilePicker(inputId) {
        const input = document.getElementById(inputId);
        if (!input) return;
        input.click();
    },
    // Kéo-thả: InputFile chỉ đọc tệp từ <input> gốc nên phải ghi DataTransfer vào input
    // rồi bắn change để OnChange của Blazor chạy như khi người dùng chọn tệp.
    watchDropZone(zoneId, inputId) {
        const zone = document.getElementById(zoneId);
        if (!zone || zone.dataset.dropWatched === "true") return;
        zone.dataset.dropWatched = "true";

        const allow = (event) => {
            event.preventDefault();
            zone.classList.add("drop-zone-active");
        };
        const release = () => zone.classList.remove("drop-zone-active");
        const drop = (event) => {
            event.preventDefault();
            release();
            const files = event.dataTransfer?.files;
            if (!files || files.length === 0) return;
            const input = document.getElementById(inputId);
            if (!input) return;
            const transfer = new DataTransfer();
            for (const file of files) transfer.items.add(file);
            input.files = transfer.files;
            input.dispatchEvent(new Event("change", { bubbles: true }));
        };

        zone.addEventListener("dragenter", allow);
        zone.addEventListener("dragover", allow);
        zone.addEventListener("dragleave", release);
        zone.addEventListener("drop", drop);
    }
};

window.appTenantExport = {
    download(fileName, content) {
        // BOM UTF-8: Excel trên Windows diễn giải tệp không BOM theo codepage địa phương nên tiếng Việt bị lỗi font.
        // Một số nguồn xuất (TenantCsvExporter) đã gắn BOM sẵn nên chỉ thêm khi thiếu.
        const csv = content.charCodeAt(0) === 0xFEFF ? content : "\uFEFF" + content;
        const blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
        this.downloadBlob(fileName, blob);
    },
    async downloadStream(fileName, contentType, streamReference) {
        const stream = await streamReference.stream();
        const response = new Response(stream, {
            headers: { "Content-Type": contentType || "application/octet-stream" }
        });
        this.downloadBlob(fileName, await response.blob());
    },
    downloadBlob(fileName, blob) {
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        link.click();
        // Hủy URL sau khi trình duyệt kịp khởi tạo tải; revoke đồng bộ có thể làm mất tệp trên một số trình duyệt.
        setTimeout(() => URL.revokeObjectURL(url), 0);
    }
};
