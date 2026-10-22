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
