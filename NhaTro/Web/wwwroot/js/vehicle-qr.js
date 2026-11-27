// In thẻ QR xe trên một cửa sổ riêng để không lẫn giao diện trang.
window.printVehicleQr = function (dataUrl, plate, roomLabel, token, cardTitle) {
    const win = window.open('', '_blank', 'width=480,height=640');
    if (!win) {
        return;
    }

    const safePlate = String(plate ?? '').replace(/[<>&]/g, '');
    const safeRoom = String(roomLabel ?? '').replace(/[<>&]/g, '');
    const safeToken = String(token ?? '').replace(/[<>&]/g, '');
    const safeTitle = String(cardTitle ?? '').replace(/[<>&]/g, '');

    win.document.write(
        '<!doctype html><html><head><meta charset="utf-8"><title>' + safeTitle + ' ' + safePlate + '</title>' +
        '<style>' +
        'body{font-family:Segoe UI,Arial,sans-serif;text-align:center;padding:24px;color:#111}' +
        'h1{font-size:20px;margin:0 0 4px}p{margin:2px 0;font-size:14px}' +
        'img{width:280px;height:280px;margin:16px auto;display:block}' +
        'code{font-size:13px;letter-spacing:1px}' +
        '</style></head><body>' +
        '<h1>' + safeTitle + '</h1>' +
        '<p><strong>' + safePlate + '</strong></p>' +
        '<p>' + safeRoom + '</p>' +
        (dataUrl ? '<img src="' + dataUrl + '" alt="QR" />' : '') +
        '<code>' + safeToken + '</code>' +
        '</body></html>');
    win.document.close();
    win.focus();
    win.onload = function () {
        win.print();
        win.close();
    };
    setTimeout(function () {
        try { win.print(); win.close(); } catch (e) { /* noop */ }
    }, 300);
};