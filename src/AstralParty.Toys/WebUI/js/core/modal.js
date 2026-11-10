// core/modal.js - Toast and modal dialogs
function toast(message) {
  const node = $('toast');
  if (!node) return;
  const text = window.i18n?.translateSource(message) ?? message;
  node.textContent = text;
  node.classList.add('show');
  clearTimeout(toast._timer);
  toast._timer = setTimeout(() => node.classList.remove('show'), 3800);
}

function openModal(title, contentHtml, iconName, modalClass) {
  const modal = $('globalModal');
  const titleEl = $('modalTitle');
  const bodyEl = $('modalBody');
  if (!modal || !titleEl || !bodyEl) return;
  // 新弹窗覆盖旧的：上一个弹窗登记的「关闭时回调」不能留到这次
  modal._onClose = null;
  // 标题可带一个 Material Symbols 图标（iconName）；标题文本本身仍做 HTML 转义。
  titleEl.innerHTML = (iconName && window.Icons ? Icons.svg(iconName) + ' ' : '') + esc(title);
  bodyEl.innerHTML = contentHtml;
  // 弹窗尺寸按内容切换（例如配置表单需要更宽）；同一个弹窗节点会复用，先清掉上一次的尺寸类。
  const contentEl = modal.querySelector('.modal-content');
  if (contentEl) {
    contentEl.classList.remove('modal-wide');
    if (modalClass) contentEl.classList.add(modalClass);
  }
  const wasOpen = modal.classList.contains('open');
  modal.inert = false;
  modal.classList.add('open');
  // 记下"打开弹窗之前焦点在哪"，关闭时还回去（键盘用户才不会丢掉位置）
  if (!wasOpen) modal._restoreFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
  // 焦点进弹窗：否则键盘用户还在背后的页面上 Tab，弹窗里的按钮要绕一整圈才够得着
  $('modalCloseBtn')?.focus();
}

function closeModal() {
  window.SkinCropModule?.cancel();
  window.CharacterCropModule?.cancel();
  const modal = $('globalModal');
  if (!modal) return;
  // 关闭回调（例如「导入降级确认」要在窗口被 X 关掉时告诉宿主：别等了）
  const onClose = modal._onClose;
  modal._onClose = null;
  const restore = modal._restoreFocus;
  modal._restoreFocus = null;
  const wasOpen = modal.classList.contains('open');
  modal.classList.remove('open');
  modal.inert = true;
  if (!wasOpen) return;
  onClose?.();
  if (!modal.classList.contains('open') && restore?.isConnected) restore.focus();
}

/** 弹窗里的可聚焦元素（Tab 循环用）。 */
function modalFocusables() {
  const modal = $('globalModal');
  if (!modal) return [];
  return [...modal.querySelectorAll('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])')]
    .filter(el => !el.disabled && el.offsetParent !== null);
}
