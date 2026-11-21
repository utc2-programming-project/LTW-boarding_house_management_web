// Màn hình chờ khi submit form nằm trong khối có data-wait="<id panel>" (vd. chọn vai trò
// ở /select-role). Form POST/redirect vẫn giữ DOM hiện tại cho tới khi trang đích tải xong,
// nên overlay che đúng khoảng chờ; trang mới render thì overlay mặc định ẩn.
// Không vô hiệu hoá nút submit trong sự kiện submit: làm vậy có thể rơi mất Input.Role.
// Overlay tự tắt sau 20 giây để không bao giờ chặn người dùng khi server chết/đang rebuild.
(function () {
  const MAX_WAIT_MS = 20000;
  let hideTimer = null;

  function panelFor(form) {
    const host = form.closest('[data-wait]');
    const id = host ? host.getAttribute('data-wait') : null;
    return id ? document.getElementById(id) : null;
  }

  function reset() {
    if (hideTimer !== null) {
      clearTimeout(hideTimer);
      hideTimer = null;
    }
    document.querySelectorAll('form').forEach(function (form) {
      if (!form.closest('[data-wait]')) return;
      form.removeAttribute('aria-busy');
      const panel = panelFor(form);
      if (panel) panel.hidden = true;
    });
  }

  document.addEventListener('submit', function (event) {
    const form = event.target instanceof HTMLFormElement ? event.target : null;
    if (!form || form.checkValidity() === false) return;

    const panel = panelFor(form);
    if (!panel) return;
    form.setAttribute('aria-busy', 'true');
    panel.hidden = false;

    if (hideTimer !== null) clearTimeout(hideTimer);
    hideTimer = setTimeout(reset, MAX_WAIT_MS);
  });

  // Điều hướng lỗi, back/forward (kể cả bfcache) đều phải nhả overlay.
  window.addEventListener('pageshow', reset);
  window.addEventListener('pageswap', reset);
})();
