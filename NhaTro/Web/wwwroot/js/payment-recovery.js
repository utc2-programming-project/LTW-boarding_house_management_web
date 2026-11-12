// VHT-143: ngữ cảnh tra cứu P6 tối thiểu theo tab + cầu nối sự kiện reconnect của VHT-62.
const storagePrefix = "nhatro.payment-recovery.";
const operations = new Set(["submit", "confirm", "reject"]);

let receiver = null;
let listenersReady = false;
let connectionLost = false;

function storageKey(target) {
    return storagePrefix + target;
}

export function mark(target, operation, userId) {
    if (!target || !operations.has(operation) || !userId) return false;
    try {
        sessionStorage.setItem(storageKey(target), JSON.stringify({
            target,
            operation,
            userId,
            storedAt: Date.now()
        }));
        return true;
    } catch {
        return false;
    }
}

export function clear(target) {
    if (!target) return;
    try {
        sessionStorage.removeItem(storageKey(target));
    } catch {
    }
}

export function has(target, userId) {
    if (!target || !userId) return false;
    try {
        const raw = sessionStorage.getItem(storageKey(target));
        if (!raw) return false;
        const value = JSON.parse(raw);
        return Boolean(value)
            && value.target === target
            && value.userId === userId
            && operations.has(value.operation)
            && Number.isFinite(value.storedAt);
    } catch {
        // Dữ liệu hỏng hoặc storage bị chặn: coi như không có marker, không suy đoán kết quả.
        clear(target);
        return false;
    }
}

export function connect(reference) {
    receiver = reference;
    if (listenersReady) return;
    listenersReady = true;
    window.addEventListener("app:connection-lost", onConnectionLost);
    window.addEventListener("app:reconnected", onReconnected);
}

export function disconnect(reference) {
    if (receiver === reference) receiver = null;
}

function onConnectionLost() {
    connectionLost = true;
    // CSS dùng thuộc tính này để vô hiệu hóa nút ghi P6 trong lúc mất kết nối.
    document.documentElement.dataset.connectionLost = "true";
}

async function onReconnected() {
    const wasLost = connectionLost;
    connectionLost = false;
    try {
        // Chỉ trang P6 đang có ngữ cảnh chưa rõ mới cần tra; trang tự kiểm tra marker/cờ.
        if (wasLost && receiver) {
            await receiver.invokeMethodAsync("OnReconnectedAsync");
        }
    } catch {
    } finally {
        delete document.documentElement.dataset.connectionLost;
    }
}
