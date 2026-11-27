// Ảnh đại diện có bản gốc ở server (bảng UserAvatars, phục vụ qua /api/avatar/{userId}).
// JS chỉ lo hiển thị vòng avatar trong sidebar/menu và nén ảnh về 256px JPEG trước khi nộp,
// nên chuỗi gửi đi nhỏ và trình duyệt không phải giữ ảnh trong localStorage.
(function () {
  const BASE = '/api/avatar/';
  const cache = new Map();

  function token() {
    const input = document.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : null;
  }

  function apply(el, dataUrl) {
    if (dataUrl) {
      el.style.backgroundImage = `url("${dataUrl}")`;
      el.textContent = '';
      el.classList.add('has-avatar-image');
    } else {
      el.style.backgroundImage = '';
      el.textContent = el.getAttribute('data-initials') || '';
      el.classList.remove('has-avatar-image');
    }
  }

  function applyAll() {
    document.querySelectorAll('[data-avatar-user]').forEach(function (el) {
      const userId = el.getAttribute('data-avatar-user');
      if (!userId) return;
      apply(el, cache.get(userId) || null);
      if (cache.has(userId)) return;
      load(userId).then(function (value) {
        if (document.body.contains(el)) apply(el, value);
      });
    });
  }

  function toDataUrl(blob) {
    return new Promise(function (resolve) {
      const reader = new FileReader();
      reader.onload = function () { resolve(typeof reader.result === 'string' ? reader.result : null); };
      reader.onerror = function () { resolve(null); };
      reader.readAsDataURL(blob);
    });
  }

  // Một request cho mỗi userId trong trang; 404 (chưa có ảnh) cũng được ghi nhớ để không hỏi lại.
  async function load(userId) {
    let value = null;
    try {
      const response = await fetch(BASE + encodeURIComponent(userId), {
        credentials: 'same-origin',
        cache: 'no-store',
      });
      if (response.ok) value = await toDataUrl(await response.blob());
    } catch {
      value = null;
    }
    cache.set(userId, value);
    return value;
  }

  // Thu nhỏ về tối đa 256px: ảnh nộp lên server nhỏ và vòng avatar không cần lớn hơn.
  function compress(dataUrl) {
    return new Promise(function (resolve, reject) {
      const img = new Image();
      img.onload = function () {
        const scale = Math.min(1, 256 / Math.max(img.width, img.height));
        const w = Math.max(1, Math.round(img.width * scale));
        const h = Math.max(1, Math.round(img.height * scale));
        const canvas = document.createElement('canvas');
        canvas.width = w;
        canvas.height = h;
        canvas.getContext('2d').drawImage(img, 0, 0, w, h);
        resolve(canvas.toDataURL('image/jpeg', 0.85));
      };
      img.onerror = function () { reject(new Error('decode')); };
      img.src = dataUrl;
    });
  }

  async function post(userId, body) {
    const antiforgery = token();
    if (!antiforgery) return { ok: false, error: 'token' };

    let response;
    try {
      response = await fetch(BASE + encodeURIComponent(userId), {
        method: body ? 'POST' : 'DELETE',
        credentials: 'same-origin',
        headers: body
          ? { 'Content-Type': 'application/json', RequestVerificationToken: antiforgery }
          : { RequestVerificationToken: antiforgery },
        body: body ? JSON.stringify(body) : undefined,
      });
    } catch {
      return { ok: false, error: 'network' };
    }

    if (response.status === 413) return { ok: false, error: 'too-large' };
    if (response.status === 400) return { ok: false, error: 'not-image' };
    if (!response.ok) return { ok: false, error: 'server' };
    return { ok: true };
  }

  // Mở hộp chọn ảnh, nén rồi nộp lên server. Trả { ok } hoặc { ok:false, error }.
  function pick(userId) {
    return new Promise(function (resolve) {
      const input = document.createElement('input');
      input.type = 'file';
      input.accept = 'image/png,image/jpeg,image/webp';
      input.style.position = 'fixed';
      input.style.left = '-9999px';
      document.body.appendChild(input);

      function finish(result) {
        input.remove();
        resolve(result);
      }

      input.addEventListener('change', function () {
        const file = input.files && input.files[0];
        if (!file) { finish({ ok: false, error: 'cancel' }); return; }
        if (file.size > 2 * 1024 * 1024) { finish({ ok: false, error: 'too-large' }); return; }
        if (!/^image\//.test(file.type)) { finish({ ok: false, error: 'not-image' }); return; }

        const reader = new FileReader();
        reader.onload = function () {
          compress(reader.result).then(function (small) {
            post(userId, { image: small }).then(function (result) {
              if (!result.ok) { finish(result); return; }
              cache.set(userId, small);
              applyAll();
              finish({ ok: true });
            });
          }).catch(function () {
            finish({ ok: false, error: 'decode' });
          });
        };
        reader.onerror = function () { finish({ ok: false, error: 'decode' }); };
        reader.readAsDataURL(file);
      });

      input.addEventListener('cancel', function () { finish({ ok: false, error: 'cancel' }); });
      input.click();
    });
  }

  async function clear(userId) {
    const result = await post(userId, null);
    if (!result.ok) return result;
    cache.set(userId, null);
    applyAll();
    return { ok: true };
  }

  window.nhatroAvatar = { pick: pick, clear: clear, refresh: applyAll };

  document.addEventListener('DOMContentLoaded', applyAll);
  document.addEventListener('enhancedload', applyAll);
})();
