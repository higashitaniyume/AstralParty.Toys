// v2/devices-helpers.js — Shared state, icons, corner progress/toast HUD and modals for Devices page
(function () {
  const tr = (key, vars) => {
    const engine = window.i18n;
    const translated = engine?.t ? engine.t(key, vars) : window.t?.(key, vars);
    if (translated && translated !== key) return translated;
    let text = String(translated ?? key);
    if (vars) {
      for (const [k, v] of Object.entries(vars)) {
        text = text.replace(`{${k}}`, String(v ?? ''));
      }
    }
    return text;
  };

  const LOCAL_ICONS = {
    smartphone: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect width="14" height="20" x="5" y="2" rx="2"/><path d="M12 18h.01"/></svg>',
    android: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect width="14" height="20" x="5" y="2" rx="2"/><path d="M9 18h6"/></svg>',
    devices: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect width="12" height="18" x="6" y="3" rx="2"/><path d="M10 17h4"/></svg>',
    cloud_download: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" x2="12" y1="15" y2="3"/></svg>',
    upload: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" x2="12" y1="3" y2="15"/></svg>',
    publish: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 16V4"/><polyline points="7 9 12 4 17 9"/><rect x="4" y="18" width="16" height="2" rx="1"/></svg>',
    install_mobile: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 3v12"/><polyline points="8 11 12 15 16 11"/><path d="M5 17v2a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2v-2"/></svg>',
    check_circle: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="9"/><path d="m9 12 2 2 4-4"/></svg>',
    radio_button_unchecked: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"><circle cx="12" cy="12" r="9"/></svg>',
    help: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="9"/><path d="M9.5 9a2.5 2.5 0 0 1 5 .8c0 1.5-2.5 2-2.5 3.2"/><path d="M12 17h.01"/></svg>',
    cloud_off: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="9"/><path d="m9 9 6 6"/><path d="m15 9-6 6"/></svg>',
    star: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><polygon points="12 3 14.9 8.9 21.4 9.8 16.7 14.4 17.8 20.9 12 17.8 6.2 20.9 7.3 14.4 2.6 9.8 9.1 8.9 12 3"/></svg>',
    content_copy: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect width="13" height="13" x="9" y="9" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>',
    restart_alt: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 12a9 9 0 1 0 3-6.7L3 8"/><path d="M3 3v5h5"/></svg>',
    monitoring: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 3v18h18"/><path d="m7 14 4-4 3 3 5-6"/></svg>',
    apps: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect width="7" height="7" x="3" y="3" rx="1.5"/><rect width="7" height="7" x="14" y="3" rx="1.5"/><rect width="7" height="7" x="3" y="14" rx="1.5"/><rect width="7" height="7" x="14" y="14" rx="1.5"/></svg>',
    style: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect width="12" height="16" x="6" y="4" rx="2"/><path d="M10 10h4"/><path d="M10 14h2"/></svg>',
    terminal: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><polyline points="4 17 10 11 4 5"/><line x1="12" x2="20" y1="19" y2="19"/></svg>',
    deployed_code: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 2 3 7v10l9 5 9-5V7l-9-5Z"/><path d="m3 7 9 5 9-5"/><path d="M12 22V12"/></svg>',
    radar: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="5"/><path d="M12 12 18 6"/></svg>',
    tune: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" aria-hidden="true"><line x1="4" x2="4" y1="21" y2="14"/><line x1="4" x2="4" y1="10" y2="3"/><line x1="12" x2="12" y1="21" y2="12"/><line x1="12" x2="12" y1="8" y2="3"/><line x1="20" x2="20" y1="21" y2="16"/><line x1="20" x2="20" y1="12" y2="3"/><line x1="2" x2="6" y1="14" y2="14"/><line x1="10" x2="14" y1="8" y2="8"/><line x1="18" x2="22" y1="16" y2="16"/></svg>',
    storefront: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 9 5 4h14l2 5"/><path d="M4 9v11h16V9"/><path d="M9 20v-6h6v6"/></svg>',
    error: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="9"/><line x1="12" x2="12" y1="8" y2="12"/><line x1="12" x2="12.01" y1="16" y2="16"/></svg>',
    warning: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"/><line x1="12" x2="12" y1="9" y2="13"/><line x1="12" x2="12.01" y1="17" y2="17"/></svg>',
    folder: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z"/></svg>',
    folder_plus: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z"/><line x1="12" x2="12" y1="10" y2="16"/><line x1="9" x2="15" y1="13" y2="13"/></svg>',
    arrow_back: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m12 19-7-7 7-7"/><path d="M19 12H5"/></svg>',
    arrow_forward: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14"/><path d="m12 5 7 7-7 7"/></svg>',
    arrow_upward: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m5 12 7-7 7 7"/><path d="M12 19V5"/></svg>',
    description: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5L14.5 2Z"/><polyline points="14 2 14 8 20 8"/><line x1="8" x2="16" y1="13" y2="13"/><line x1="8" x2="13" y1="17" y2="17"/></svg>',
    open_in_new: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/><polyline points="15 3 21 3 21 9"/><line x1="10" x2="21" y1="14" y2="3"/></svg>',
    edit: '<svg class="dv-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M17 3a2.85 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z"/></svg>',
  };

  const icon = (name, cls) => LOCAL_ICONS[name] || window.Icons?.svg(name, cls) || '';
  const post = msg => window.chrome?.webview?.postMessage(msg);
  const root = () => document.getElementById('v2Devices');
  const U = () => window.V2UI;
  const isWideLayout = () => !!window.matchMedia?.('(min-width: 1200px)').matches;

  const state = {
    targets: [],
    activeId: '',
    selectedId: '',
    tab: 'overview',
    report: null,
    reportTargetId: '',
    busy: false,
    progress: { phase: '', percent: 0, message: '', active: false, indeterminate: false },
    opResult: null,
    cornerToast: null,
    env: null,
    envExpanded: false,
    payloadPath: '',
    payloadPresent: false,
    payloadVersion: '',
    payloadSdkVersion: '',
    payloadFileCount: 0,
    pullingPayload: false,
    packages: null,
    packagesTargetId: '',
    mods: null,
    modsTargetId: '',
    skins: null,
    skinsTargetId: '',
    filesTargetId: '',
    filesPath: '',
    filesRootLabel: '',
    filesEntries: null,
    filesError: '',
    filesSelectedPath: '',
    filesRenamingPath: '',
    filesShowHidden: false,
    filesSortKey: 'name',
    filesSortDir: 1,
    filesHistory: [''],
    filesHistoryIndex: 0,
    filesScrollTop: 0,
    logs: [],
    logPath: '',
  };

  const activeTab = () => (isWideLayout() && state.tab === 'logs' ? 'overview' : state.tab);

  function currentTarget() {
    if (!state.targets.length) return null;
    return state.targets.find(t => t.id === state.selectedId)
      || state.targets.find(t => t.id === state.activeId)
      || state.targets[0]
      || null;
  }

  function isSelectedGlobalActive(dev) {
    return !!dev && state.activeId && state.activeId.toLowerCase() === dev.id.toLowerCase();
  }

  function syncSelection(targets) {
    if (!targets.length) {
      state.selectedId = '';
      return;
    }
    if (!state.selectedId || !targets.some(t => t.id === state.selectedId)) {
      const mobileActive = state.activeId && !state.activeId.startsWith('local:') ? state.activeId : '';
      state.selectedId = (mobileActive && targets.some(t => t.id === mobileActive))
        ? mobileActive
        : targets[0].id;
    }
  }

  function patchStateInfo(patchState) {
    switch (patchState) {
      case 'patched':
        return ['ok', 'check_circle', tr('devices.statePatched')];
      case 'patched-external':
      case 'patched_external':
        return ['warn', 'tune', tr('devices.stateExternal')];
      case 'stale':
        return ['bad', 'warning', tr('devices.stateStale')];
      case 'clean':
        return ['', 'radio_button_unchecked', tr('devices.stateClean')];
      default:
        return ['', 'help', tr('devices.stateUnknown')];
    }
  }

  function progressDisplay() {
    const pct = Math.max(0, Math.min(100, Number(state.progress?.percent) || 0));
    const indeterminate = !!state.progress?.indeterminate || pct <= 0;
    const msg = state.progress?.message
      || (state.pullingPayload ? tr('devices.pulling')
        : state.progress?.phase === 'detect' ? tr('devices.busyDetect')
        : tr('devices.busyWrite'));
    return { pct, indeterminate, msg };
  }

  function renderBusyBar() {
    if (!state.busy && !state.progress?.active) return '';
    const { pct, indeterminate, msg } = progressDisplay();
    return `
      <div class="dev-busy" role="progressbar" aria-valuemin="0" aria-valuemax="100" aria-valuenow="${pct}" aria-live="polite">
        <div class="dv-busy-row">
          <span>${esc(msg)}</span>
          <span class="dv-busy-pct">${indeterminate ? '…' : pct + '%'}</span>
        </div>
        <div class="dv-busy-track">
          <div class="dv-busy-fill${indeterminate ? ' is-indeterminate' : ''}" style="width:${indeterminate ? 40 : pct}%"></div>
        </div>
      </div>`;
  }

  function renderCornerHudHtml() {
    const busy = state.busy || state.progress?.active;
    const toastItem = state.cornerToast;
    if (!busy && !toastItem) return '<div class="dv-corner-stack" id="dvCornerStack"></div>';

    let hudHtml = '';
    if (busy) {
      const { pct, indeterminate, msg } = progressDisplay();
      hudHtml = `
        <div class="dv-progress-hud" role="status" aria-live="polite">
          <div class="dv-progress-hud-head">
            <span class="dv-progress-hud-title">${esc(msg)}</span>
            <span class="dv-progress-hud-pct">${indeterminate ? '…' : pct + '%'}</span>
          </div>
          <div class="dv-busy-track">
            <div class="dv-busy-fill${indeterminate ? ' is-indeterminate' : ''}" style="width:${indeterminate ? 40 : pct}%"></div>
          </div>
        </div>`;
    }

    let toastHtml = '';
    if (toastItem) {
      toastHtml = `
        <div class="dv-corner-toast ${toastItem.ok ? 'is-ok' : 'is-bad'}" role="status" aria-live="polite">
          ${icon(toastItem.ok ? 'check_circle' : 'error')}
          <div class="dv-corner-toast-body">
            <div class="dv-corner-toast-msg">${esc(toastItem.message || (toastItem.ok ? tr('devices.lastResultOk') : tr('devices.lastResultFail')))}</div>
            ${toastItem.detail ? `<div class="dv-corner-toast-detail">${esc(toastItem.detail)}</div>` : ''}
          </div>
          <button type="button" class="dv-corner-toast-close" data-dev-toast-close aria-label="${esc(tr('common.close'))}">×</button>
        </div>`;
    }

    return `<div class="dv-corner-stack" id="dvCornerStack">${hudHtml}${toastHtml}</div>`;
  }

  function syncCornerHud() {
    const host = root();
    if (!host) return;
    const slot = document.getElementById('dvCornerStack');
    const html = renderCornerHudHtml();
    if (slot) {
      slot.outerHTML = html;
    } else {
      const page = host.querySelector('.dv-page');
      if (page) page.insertAdjacentHTML('beforeend', html);
    }
  }

  function syncProgressInPlace() {
    const host = root();
    if (!host) return false;
    const heroBusy = host.querySelector('.dev-busy');
    if (heroBusy && (state.busy || state.progress?.active)) {
      const { pct, indeterminate, msg } = progressDisplay();
      const label = heroBusy.querySelector('.dv-busy-row > span:first-child');
      const pctEl = heroBusy.querySelector('.dv-busy-pct');
      const fill = heroBusy.querySelector('.dv-busy-fill');
      if (label) label.textContent = msg;
      if (pctEl) pctEl.textContent = indeterminate ? '…' : `${pct}%`;
      if (fill) {
        fill.classList.toggle('is-indeterminate', indeterminate);
        fill.style.width = `${indeterminate ? 40 : pct}%`;
      }
      heroBusy.setAttribute('aria-valuenow', String(pct));
      syncCornerHud();
      return true;
    }
    syncCornerHud();
    return false;
  }

  let cornerToastTimer = 0;
  function showCornerToast(ok, message, detail = '') {
    state.cornerToast = { ok: !!ok, message: String(message || ''), detail: String(detail || '') };
    syncCornerHud();
    clearTimeout(cornerToastTimer);
    cornerToastTimer = setTimeout(() => {
      state.cornerToast = null;
      syncCornerHud();
    }, ok ? 4500 : 7500);
  }

  function dismissCornerToast() {
    clearTimeout(cornerToastTimer);
    state.cornerToast = null;
    syncCornerHud();
  }

  function confirmAction({ title, message, okLabel, danger, onConfirm }) {
    const ui = U();
    if (danger && ui?.confirmDelete) {
      ui.confirmDelete({
        title: esc(title),
        lines: [esc(message)],
        ok: esc(okLabel || tr('common.confirm')),
        onOk: onConfirm,
      });
      return;
    }
    if (ui?.openModal) {
      ui.modalActions.devConfirmOk = () => {
        ui.closeModal();
        onConfirm?.();
      };
      const m = ui.openModal(`
        <div class="v2-modal-head">
          <h2>${esc(title)}</h2>
          <button type="button" class="v2-icon-btn" data-v2modal="close" aria-label="${esc(tr('common.cancel'))}">${ui.ICON.x}</button>
        </div>
        <p style="margin:0;color:var(--gp-text-sub);font-size:13px;line-height:1.55">${esc(message)}</p>
        <div class="v2-modal-foot">
          <span class="v2-top-spacer"></span>
          <button type="button" class="v2-btn ghost" data-v2modal="close" id="dvModalCancel">${esc(tr('common.cancel'))}</button>
          <button type="button" class="v2-btn ${danger ? 'danger-fill' : 'primary'}" data-v2modal="devConfirmOk">${esc(okLabel || tr('common.confirm'))}</button>
        </div>`, 'narrow');
      if (danger) ui.armDanger(m);
      document.getElementById('dvModalCancel')?.focus();
      return;
    }
    if (window.confirm(message)) onConfirm?.();
  }

  function openPromptModal({ title, label, initialValue = '', placeholder = '', onSubmit }) {
    const ui = U();
    if (!ui?.openModal) return;
    ui.modalActions.devPromptSubmit = () => {
      const input = document.getElementById('dvPromptInput');
      const value = input?.value?.trim() || '';
      if (!value) return;
      ui.closeModal();
      onSubmit?.(value);
    };
    ui.openModal(`
      <div class="v2-modal-head">
        <h2>${esc(title)}</h2>
        <button type="button" class="v2-icon-btn" data-v2modal="close" aria-label="${esc(tr('common.cancel'))}">${ui.ICON.x}</button>
      </div>
      <form id="dvPromptForm" class="dv-prompt-form">
        <label class="dv-manual-label" for="dvPromptInput">${esc(label)}</label>
        <input id="dvPromptInput" class="dv-input" type="text" value="${esc(initialValue)}" placeholder="${esc(placeholder)}" required>
        <div class="v2-modal-foot">
          <span class="v2-top-spacer"></span>
          <button type="button" class="v2-btn ghost" data-v2modal="close">${esc(tr('common.cancel'))}</button>
          <button type="submit" class="v2-btn primary">${esc(tr('common.confirm'))}</button>
        </div>
      </form>`, 'narrow');
    const form = document.getElementById('dvPromptForm');
    form?.addEventListener('submit', e => {
      e.preventDefault();
      ui.modalActions.devPromptSubmit?.();
    });
    const input = document.getElementById('dvPromptInput');
    if (input) {
      input.focus();
      input.setSelectionRange(0, input.value.length);
    }
  }

  function openModConfigModal(payload, startBusy) {
    const ui = U();
    const fields = payload?.fields || [];
    const editable = fields.filter(f => f.kind === 'bool' || f.kind === 'number' || f.kind === 'string');
    const complex = fields.filter(f => f.kind === 'other');
    if (!ui?.openModal) return;

    const fieldHtml = editable.map((f, idx) => {
      const inputId = `dvCfgField_${idx}`;
      let control = '';
      if (f.kind === 'bool') {
        control = `<input id="${inputId}" class="dv-checkbox" type="checkbox" data-cfg-idx="${idx}" ${f.boolValue ? 'checked' : ''}>`;
      } else if (f.kind === 'number') {
        control = `<input id="${inputId}" class="dv-input" type="number" step="any" data-cfg-idx="${idx}" value="${esc(String(f.numberValue ?? 0))}">`;
      } else {
        control = `<input id="${inputId}" class="dv-input" type="text" data-cfg-idx="${idx}" value="${esc(f.stringValue ?? '')}">`;
      }
      return `
        <div class="dv-config-field">
          <div class="dv-config-row">
            <label class="dv-config-label" for="${inputId}">${esc(f.name)}</label>
            ${f.kind === 'bool' ? control : ''}
          </div>
          ${f.kind !== 'bool' ? control : ''}
          ${f.comment ? `<div class="dv-config-comment">${esc(f.comment)}</div>` : ''}
        </div>`;
    }).join('');

    const hintHtml = !editable.length ? `<p class="dv-muted">${esc(tr('devices.configNoScalars'))}</p>` : '';
    const complexHtml = complex.length
      ? `<div class="dv-alert">${icon('info')}<div class="dv-alert-content"><span>${esc(tr('devices.configComplexHint'))}</span><span class="dv-alert-detail">${esc(complex.map(c => c.name).join(', '))}</span></div></div>`
      : '';

    ui.modalActions.devSaveCfg = () => {
      const modal = document.getElementById('v2Modal');
      if (!modal) return;
      const changes = [];
      editable.forEach((f, idx) => {
        const input = modal.querySelector(`[data-cfg-idx="${idx}"]`);
        if (!input) return;
        if (f.kind === 'bool') {
          const val = !!input.checked;
          if (val !== !!f.boolValue) changes.push({ key: f.name, kind: 'bool', value: val ? 'true' : 'false' });
        } else if (f.kind === 'number') {
          const raw = input.value.trim();
          const num = Number(raw);
          if (raw && !Number.isNaN(num) && num !== f.numberValue) changes.push({ key: f.name, kind: 'number', value: raw });
        } else {
          const val = input.value;
          if (val !== (f.stringValue ?? '')) changes.push({ key: f.name, kind: 'string', value: val });
        }
      });

      if (!changes.length) {
        window.toast?.(tr('devices.configNoChange'));
        ui.closeModal();
        return;
      }

      ui.closeModal();
      startBusy(tr('devices.busyWrite'));
      post({ type: 'deviceSetModConfig', targetId: payload.targetId, modId: payload.modId, changes });
    };

    ui.openModal(`
      <div class="v2-modal-head">
        <h2>${esc(payload.modId)} · ${esc(tr('devices.modConfig'))}</h2>
        <button type="button" class="v2-icon-btn" data-v2modal="close" aria-label="${esc(tr('common.cancel'))}">${ui.ICON.x}</button>
      </div>
      <div class="dv-config-form">
        ${fieldHtml}
        ${hintHtml}
        ${complexHtml}
      </div>
      <div class="v2-modal-foot">
        <span class="dv-payload-path" title="${esc(payload.path || '')}">${esc(payload.path || '')}</span>
        <span class="v2-top-spacer"></span>
        <button type="button" class="v2-btn ghost" data-v2modal="close">${esc(tr('common.cancel'))}</button>
        <button type="button" class="v2-btn primary" data-v2modal="devSaveCfg" ${editable.length ? '' : 'disabled'}>${esc(tr('common.confirm'))}</button>
      </div>`);
  }

  window._V2Devices = {
    tr, icon, post, root, U, isWideLayout, activeTab, state,
    currentTarget, isSelectedGlobalActive, syncSelection,
    patchStateInfo, renderBusyBar, renderCornerHudHtml, syncCornerHud, syncProgressInPlace,
    showCornerToast, dismissCornerToast, confirmAction, openPromptModal, openModConfigModal,
  };
})();
