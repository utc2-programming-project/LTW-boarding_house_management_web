window.appTenantExport = {
    download(fileName, content) {
        const blob = new Blob([content], { type: "text/csv;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        link.click();
        URL.revokeObjectURL(url);
    },
    async downloadStream(fileName, contentType, streamReference) {
        const stream = await streamReference.stream();
        const response = new Response(stream, {
            headers: { "Content-Type": contentType || "application/octet-stream" }
        });
        const blob = await response.blob();
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        link.click();
        URL.revokeObjectURL(url);
    }
};
