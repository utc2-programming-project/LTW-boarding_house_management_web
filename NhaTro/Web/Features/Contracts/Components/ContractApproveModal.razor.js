// VHT-124: dieu khien <dialog> cua hop thoai duyet/tu choi hop dong.
// showModal() lo focus trap, top-layer va tra focus ve nut mo khi close().
// Esc (su kien "cancel") duoc chan lai va chuyen ve .NET de ton trong IsBusy.
export function register(dialog, dotnet) {
    const onCancel = event => {
        event.preventDefault();
        dotnet.invokeMethodAsync('RequestCloseAsync');
    };

    dialog.addEventListener('cancel', onCancel);
    return {
        dispose() {
            dialog.removeEventListener('cancel', onCancel);
        }
    };
}

export function open(dialog) {
    if (!dialog.open) dialog.showModal();
}

export function close(dialog) {
    if (dialog.open) dialog.close();
}
