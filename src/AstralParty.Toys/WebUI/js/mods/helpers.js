// mods/helpers.js - Version comparison and cell helpers
(function () {
  'use strict';
  // ---------- 版本号比较（数字点分；认不出的段按 0 处理） ----------

  function parseVersion(value) {
    return String(value ?? '')
      .split('.')
      .map(part => parseInt(part, 10) || 0);
  }

  /** a > b → 1；a < b → -1；相等 / 无法比较 → 0。 */
  function compareVersion(a, b) {
    const left = parseVersion(a);
    const right = parseVersion(b);
    const length = Math.max(left.length, right.length);
    for (let i = 0; i < length; i++) {
      const l = left[i] || 0;
      const r = right[i] || 0;
      if (l !== r) return l > r ? 1 : -1;
    }
    return 0;
  }

  /** 写一栏版本信息：值 + 说明 + 状态色（状态同时有文字说明，不靠颜色单独表意）。 */
  function setVersionCell(cellId, valueId, noteId, options) {
    const valueEl = $(valueId);
    if (valueEl) valueEl.textContent = options.value;
    const noteEl = noteId ? $(noteId) : null;
    if (noteEl && options.note !== undefined) noteEl.textContent = options.note;
    const cell = $(cellId);
    if (!cell) return;
    cell.classList.remove('is-current', 'is-update', 'is-conflict', 'is-unknown');
    if (options.state) cell.classList.add(options.state);
  }
  function fmtBytes(bytes) {
    const value = Number(bytes ?? 0);
    if (value >= 1024 * 1024) return (value / 1024 / 1024).toFixed(2) + ' MB';
    if (value >= 1024) return (value / 1024).toFixed(1) + ' KB';
    return value + ' B';
  }
  window.ModsHelpers = { parseVersion, compareVersion, setVersionCell, fmtBytes };
})();
