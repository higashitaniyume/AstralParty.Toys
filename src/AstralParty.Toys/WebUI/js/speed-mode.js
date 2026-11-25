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
    const nativeUnavailable = saved?.canSelectNative === false;
    $('speedhackNativeMarket').hidden = !nativeUnavailable;
    const nativeHotkey = $('speedhackNativeHotkey');
    nativeHotkey.hidden = !saved || nativeUnavailable;
    nativeHotkey.textContent = t('speedhack.native_hotkey', { key: saved?.toggleKey || 'Delete' });
    const unityUnavailable = saved?.canSelectUnity === false;
    $('speedhackUnityUnavailable').hidden = !unityUnavailable;
    for (const button of modeButtons()) {
      const selected = button.dataset.speedMode === mode;
      const unavailable = button.dataset.speedMode === 'unity' ? unityUnavailable : nativeUnavailable;
      button.hidden = unavailable;
      button.disabled = disabled || unavailable;
      button.setAttribute('aria-pressed', String(selected));
      button.textContent = t(selected ? 'speedhack.selected' : 'speedhack.select_this_mode');
      button.closest('[data-mode]').classList.toggle('speedhack-mode-card-selected', selected);
    }
    const [min, max] = rangeFor(mode);
    speedInput().disabled = disabled || !mode || min === max;
    const enabled = $('speedhackEnabled');
    enabled.checked = Boolean(mode) && Number(speedInput().value) > 1.0001;
    enabled.disabled = disabled || !mode || max === 1;
  }

  function rangeFor(selectedMode) {
    return selectedMode === 'unity' ? [saved.unityMinSpeed, saved.unityMaxSpeed]
      : [saved?.nativeMinSpeed ?? 1, saved?.nativeMaxSpeed ?? 100];
  }

  function showDraft(nextMode, speed) {
    mode = nextMode;
    const [min, max] = rangeFor(mode);
    speedInput().value = String(speed);
    speedInput().removeAttribute('aria-invalid');
    $('speedhackSpeedRange').textContent = t('speedhack.range', { min, max });
  }

  function readInput() {
    const value = speedInput().value.trim();
    if (value === '' || /^\d+\.$/.test(value)) return {};
    if (!/^\d+(?:\.\d+)?$/.test(value)) return { error: t('speedhack.invalid_speed') };
    const speed = Number(value);
    const [min, max] = rangeFor(mode);
    if (!Number.isFinite(speed) || speed < min || speed > max) {
      return { error: t('speedhack.out_of_range', { min, max }) };
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
    else $('speedhackCurrentMode').textContent = t('speedhack.read_failed');
    feedback(message, 'error');
    renderControls();
  }

  function request(kind, details = {}, recovering = false) {
    const id = ++requestId;
    clearTimeout(pending?.timer);
    const timer = setTimeout(() => {
      if (pending?.id !== id) return;
      if (kind === 'save') {
        feedback(t('speedhack.save_timeout'));
        request('read', {}, true);
      } else {
        showFailure(t(recovering ? 'speedhack.save_unconfirmed' : 'speedhack.read_timeout'));
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
    feedback(t('speedhack.saving'));
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
      $('speedhackCurrentMode').textContent = t('speedhack.reading');
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
      $('speedhackCurrentMode').textContent = saved.mode ? `${name} · ${saved.speed}x` : t('speedhack.no_mode');
      showDraft(saved.mode, saved.speed);
      feedback(kind === 'save' ? t('speedhack.saved')
        : recovering ? t('speedhack.reloaded') : '', kind === 'save' ? 'success' : '');
      renderControls();
    }
  };

  document.addEventListener('DOMContentLoaded', () => {
    $('speedhackNativeMarket').addEventListener('click', () => {
      window.WorkshopModule?.openExternalMod({ id: 'NativeSpeedMod' });
    });
    for (const button of modeButtons()) {
      button.closest('[data-mode]').addEventListener('click', () => {
        const nextMode = button.dataset.speedMode;
        if (button.disabled || AppState.isPageTransitioning || nextMode === mode) return;
        const speed = readInput().speed ?? saved.speed;
        const [min, max] = rangeFor(nextMode);
        save(nextMode, Math.max(min, Math.min(max, speed)));
      });
    }

    $('speedhackEnabled').addEventListener('change', event => {
      if (pending || !saved) return;
      const [min, max] = rangeFor(mode);
      if (!event.target.checked && min > 1) {
        feedback(t('speedhack.out_of_range', { min, max }), 'error');
        renderControls();
        return;
      }
      save(mode, event.target.checked ? Math.max(min, Math.min(max, 2)) : 1);
    });

    const input = speedInput();
    input?.addEventListener('input', () => {
      cancelInputSave();
      if (!saved || pending || AppState.currentPage !== 'speedhack' || AppState.isPageTransitioning) return;
      const { speed, error } = readInput();
      if (error) input.setAttribute('aria-invalid', 'true');
      else input.removeAttribute('aria-invalid');
      feedback(error || (speed === undefined ? t('speedhack.typing') : ''), error ? 'error' : '');
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
