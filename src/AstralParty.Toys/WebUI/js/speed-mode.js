// Settings for the two acceleration modes of the CesiumLoader SpeedHackMod.
(function () {
  'use strict';

  const saveDelay = 500;
  const responseTimeout = 5000;
  let requestId = 0;
  let pending = null;
  let inputTimer = null;
  let saved = null;
  let gameDirectory = '';
  let mode = null;
  const speedInput = () => $('speedhackSpeedInput');
  const modeButtons = () => document.querySelectorAll('#speedhackView [data-speed-mode]');

  function feedback(message, kind = '') {
    const status = $('speedhackFeedback');
    status.textContent = message;
    status.dataset.kind = kind;
  }

  function renderControls() {
    const disabled = Boolean(pending) || !saved;
    for (const button of modeButtons()) {
      const selected = button.dataset.speedMode === mode;
      button.disabled = disabled;
      button.setAttribute('aria-pressed', String(selected));
      button.textContent = selected ? '已选中' : '选择此模式';
      button.closest('[data-mode]').classList.toggle('speedhack-mode-card-selected', selected);
    }
    speedInput().disabled = disabled || (mode === 'unity' && saved.unityMinSpeed === saved.unityMaxSpeed);
  }

  function rangeFor(selectedMode) {
    return selectedMode === 'unity' ? [saved.unityMinSpeed, saved.unityMaxSpeed] : [1, 100];
  }

  function showDraft(nextMode, speed) {
    mode = nextMode;
    const [min, max] = rangeFor(mode);
    speedInput().value = String(speed);
    speedInput().removeAttribute('aria-invalid');
    $('speedhackSpeedRange').textContent = `范围：${min}x–${max}x`;
  }

  function readInput() {
    const value = speedInput().value.trim();
    if (value === '' || /^\d+\.$/.test(value)) return {};
    if (!/^\d+(?:\.\d+)?$/.test(value)) return { error: '请输入有效的倍速。' };
    const speed = Number(value);
    const [min, max] = rangeFor(mode);
    if (!Number.isFinite(speed) || speed < min || speed > max) {
      return { error: `倍速超出范围（${min}x–${max}x）。` };
    }
    return { speed };
  }

  function cancelInputSave() {
    clearTimeout(inputTimer);
    inputTimer = null;
  }

  function showFailure(message) {
    pending = null;
    if (saved) showDraft(saved.mode, saved.speed);
    else $('speedhackCurrentMode').textContent = '无法读取设置';
    feedback(message, 'error');
    renderControls();
  }

  function request(kind, details = {}, recovering = false) {
    const id = ++requestId;
    clearTimeout(pending?.timer);
    const timer = setTimeout(() => {
      if (pending?.id !== id) return;
      if (kind === 'save') {
        feedback('保存响应超时，正在重新读取…');
        request('read', {}, true);
      } else {
        showFailure(recovering
          ? '保存结果未确认，请重新打开页面核对。'
          : '读取设置超时，请重新打开页面。');
      }
    }, responseTimeout);
    pending = { id, kind, timer, recovering };
    renderControls();
    post({ type: kind === 'save' ? 'speedModeSave' : 'speedModeRead', requestId: id, ...details });
  }

  function save(nextMode, speed) {
    cancelInputSave();
    if (nextMode === saved.mode && speed === saved.speed) {
      feedback('');
      return;
    }
    showDraft(nextMode, speed);
    feedback('正在保存…');
    request('save', { gameDirectory, mode, speed });
  }

  function saveInput() {
    cancelInputSave();
    if (pending || !saved) return;
    const { speed } = readInput();
    if (speed !== undefined) save(mode, speed);
  }

  window.SpeedModeModule = {
    onShow() {
      if (inputTimer !== null) saveInput();
      if (pending?.kind === 'save' || pending?.recovering) return;
      saved = null;
      mode = null;
      $('speedhackCurrentMode').textContent = '正在读取设置…';
      $('speedhackSpeedRange').textContent = '';
      speedInput().value = '';
      speedInput().removeAttribute('aria-invalid');
      feedback('');
      request('read');
    },

    receive(result) {
      if (!pending || result?.requestId !== pending.id) return;
      const { kind, timer, recovering } = pending;
      clearTimeout(timer);
      pending = null;
      if (result.error) {
        showFailure(result.error);
        return;
      }

      saved = result.settings;
      gameDirectory = result.gameDirectory;
      const name = t(saved.mode === 'unity' ? 'speedhack.unity_title' : 'speedhack.native_title');
      $('speedhackCurrentMode').textContent = `${name} · ${saved.speed}x`;
      showDraft(saved.mode, saved.speed);
      feedback(kind === 'save' ? '已保存。下次启动游戏时生效。'
        : recovering ? '已重新读取宿主设置。' : '', kind === 'save' ? 'success' : '');
      renderControls();
    }
  };

  document.addEventListener('DOMContentLoaded', () => {
    for (const button of modeButtons()) {
      button.closest('[data-mode]').addEventListener('click', () => {
        const nextMode = button.dataset.speedMode;
        if (pending || !saved || AppState.isPageTransitioning || nextMode === mode) return;
        const speed = readInput().speed ?? saved.speed;
        const [min, max] = rangeFor(nextMode);
        save(nextMode, Math.max(min, Math.min(max, speed)));
      });
    }

    const input = speedInput();
    input?.addEventListener('input', () => {
      cancelInputSave();
      if (!saved || pending || AppState.currentPage !== 'speedhack' || AppState.isPageTransitioning) return;
      const { speed, error } = readInput();
      if (error) input.setAttribute('aria-invalid', 'true');
      else input.removeAttribute('aria-invalid');
      feedback(error || (speed === undefined ? '正在输入…' : ''), error ? 'error' : '');
      if (speed !== undefined) inputTimer = setTimeout(saveInput, saveDelay);
    });
    input?.addEventListener('blur', () => {
      if (!saved || pending) return;
      const { speed, error } = readInput();
      if (speed === undefined && !error) {
        showDraft(saved.mode, saved.speed);
        feedback('');
      }
    });
    input?.addEventListener('keydown', event => {
      if (event.key !== 'Enter' || event.isComposing) return;
      event.preventDefault();
      if (AppState.currentPage === 'speedhack' && !AppState.isPageTransitioning) saveInput();
    });
  });
})();
