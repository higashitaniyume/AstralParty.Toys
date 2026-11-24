/* 移动设备页：设备列表 / 检测报告 / 渠道包 / 载荷 / 模组 / 操作日志。
   标记：所有 data-dev-* 属性与 window.V2Devices 都是宿主与路由约定的接口，不要改名。 */
(function () {
  const host = document.getElementById('v2Devices');
  if (!host) return;

  const state = {
    targets: [],
    active: null,
    report: null,
    notice: '',
    packages: null,
    logPath: '',
    payloadPath: '',
    payloadPresent: false,
    mods: null,
    skins: null,
    config: null,
    logs: [],
    selected: null,
    busy: false,
    progress: null,
    loaded: false,
  };

  /** 页面上最多渲染的日志行数（文件里是全量的，这里只是别把 DOM 撑爆）。 */
  const LOG_LIMIT = 800;

  const tr = key => (typeof window.t === 'function' ? window.t(key) : key);
  const esc = value => String(value ?? '').replace(/[&<>"']/g, ch => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;',
  })[ch]);
  const icon = (name, cls) => (window.Icons ? window.Icons.svg(name, cls) : '');
  const fmtBytes = value => {
    const n = Number(value || 0);
    if (!n) return '—';
    if (n < 1024) return n + ' B';
    if (n < 1024 * 1024) return (n / 1024).toFixed(1) + ' KB';
    return (n / 1024 / 1024).toFixed(1) + ' MB';
  };
  const yesNo = (value, label) => value
    ? `<span class="dv-yes">${esc(label ?? tr('devices.yes'))}</span>`
    : `<span class="dv-no">${esc(label ?? tr('devices.no'))}</span>`;

  /* 补丁状态 → 文案 + 色调 + 图标 */
  const PATCH = {
    clean: { key: 'devices.stateClean', tone: 'ok', icon: 'check' },
    patched: { key: 'devices.statePatched', tone: 'accent', icon: 'bolt' },
    'patched-external': { key: 'devices.stateExternal', tone: 'warn', icon: 'info' },
    stale: { key: 'devices.stateStale', tone: 'warn', icon: 'warning' },
    unknown: { key: 'devices.stateUnknown', tone: 'warn', icon: 'warning' },
  };
  const patch = value => PATCH[value] || PATCH.unknown;
  const patchPill = value => {
    const p = patch(value);
    return `<span class="dv-pill is-${p.tone}">${icon(p.icon)}${esc(tr(p.key))}</span>`;
  };

  const SEGMENTS = 6;
  const segments = percent => {
    const filled = Math.round((Math.max(0, Math.min(100, Number(percent ?? 0))) / 100) * SEGMENTS);
    let out = '';
    for (let i = 0; i < SEGMENTS; i++) {
      const cls = i < filled ? 'is-done' : (i === filled ? 'is-active' : '');
      out += `<span class="${cls}"></span>`;
    }
    return out;
  };

  /* ── 骨架 ─────────────────────────────────────────── */
  function skeleton() {
    host.innerHTML = `<div class="dv-page">
      <div class="dv-top"><div><h1>${esc(tr('devices.title'))}</h1></div></div>
      <div class="v2-skel v2-skel-grid">
        <div class="v2-skel v2-skel-card"></div>
        <div class="v2-skel v2-skel-card"></div>
        <div class="v2-skel v2-skel-card"></div>
      </div>
    </div>`;
  }

  function busyTitle() {
    const phase = state.progress?.phase;
    if (phase === 'write') return tr('devices.busyWrite');
    if (phase === 'scan') return tr('devices.packageScan');
    return tr('devices.busyDetect');
  }

  /* ── 进度面板（类名 dev-busy 是测试断言的一部分，不要改） ── */
  function busyPanel() {
    const percent = Math.max(0, Math.min(100, Number(state.progress?.percent ?? 0)));
    return `<div class="dv-busy dev-busy" id="devBusy" role="status" aria-live="polite" ${state.busy ? '' : 'hidden'}>
      <div class="dv-busy-head">
        ${icon('bolt')}
        <span class="dv-busy-title" id="devBusyTitle">${esc(busyTitle())}</span>
        <span class="dv-busy-msg" id="devBusyMsg">${esc(state.progress?.message || '')}</span>
      </div>
      <div class="dv-seg" id="devBusySeg" aria-hidden="true">${segments(percent)}</div>
    </div>`;
  }

  /* ── 设备卡 ───────────────────────────────────────── */
  function deviceCard(target, index) {
    const offline = !target.online || target.state === 'untrusted';
    const selected = state.selected === target.id;
    const kind = target.kind === 'ios' ? 'iOS' : 'Android';
    const classes = ['dv-dev', 'dv-in'];
    if (selected) classes.push('is-selected');
    if (offline) classes.push('is-offline');

    const actions = [];
    actions.push(`<button type="button" class="dv-btn is-primary" data-dev-detect="${esc(target.id)}" ${state.busy ? 'disabled' : ''}>` +
      `${icon('search')}${esc(tr('devices.detect'))}</button>`);
    actions.push(`<button type="button" class="dv-btn" data-dev-deploy="${esc(target.id)}" ` +
      `${state.busy || !state.payloadPresent ? 'disabled' : ''} title="${esc(state.payloadPath)}">` +
      `${icon('inventory_2')}${esc(tr('devices.deploy'))}</button>`);
    if (!selected) {
      // 选中必须是真按钮：整张卡片挂点击会让键盘用户用不了，也会和卡内按钮抢事件。
      actions.push(`<button type="button" class="dv-btn" data-dev-select="${esc(target.id)}" ${state.busy ? 'disabled' : ''}>` +
        `${icon('chevron_right')}${esc(tr('devices.select'))}</button>`);
    }
    if (!offline) {
      actions.push(`<button type="button" class="dv-btn" data-dev-patch="${esc(target.id)}" ${state.busy ? 'disabled' : ''}>` +
        `${icon('bolt')}${esc(tr('devices.patch'))}</button>`);
      actions.push(`<button type="button" class="dv-btn" data-dev-restore="${esc(target.id)}" ${state.busy ? 'disabled' : ''}>` +
        `${icon('upgrade')}${esc(tr('devices.restore'))}</button>`);
      actions.push(`<button type="button" class="dv-btn is-danger" data-dev-uninstall="${esc(target.id)}" ${state.busy ? 'disabled' : ''}>` +
        `${icon('delete')}${esc(tr('devices.uninstall'))}</button>`);
    }

    const tags = [
      `<span class="dv-tag ${target.kind === 'ios' ? 'is-ios' : 'is-android'}">${esc(kind)}</span>`,
      target.paired ? `<span class="dv-tag">${icon('check')}${esc(tr('devices.paired'))}</span>`
                    : `<span class="dv-tag is-off">${esc(tr('devices.offline'))}</span>`,
      target.installed ? `<span class="dv-tag">${icon('extension')}${esc(tr('devices.loaderInstalled'))}</span>` : '',
      selected ? `<span class="dv-tag">${icon('chevron_right')}${esc(tr('devices.select'))}</span>` : '',
    ].filter(Boolean).join('');

    return `<article class="${classes.join(' ')}" style="animation-delay:${Math.min(index, 6) * 30}ms">
      <div class="dv-dev-head">
        <span class="dv-dev-icon">${icon('smart_toy')}</span>
        <div style="min-width:0;flex:1">
          <div class="dv-dev-name">${esc(target.label || target.serial)}</div>
          <div class="dv-dev-meta">${esc([kind, target.model, target.osVersion].filter(Boolean).join(' · '))}</div>
        </div>
        <span class="dv-dot"><i></i>${esc(offline ? tr('devices.offline') : tr('devices.online'))}</span>
      </div>
      <div class="dv-tags">${tags}</div>
      <div class="dv-dev-actions">${actions.join('')}</div>
    </article>`;
  }

  function renderDevices() {
    if (!state.targets.length) {
      return `<div class="dv-empty dv-in">${icon('smart_toy')}<div>${esc(tr('devices.noDevices'))}</div></div>`;
    }
    const note = state.payloadPresent
      ? ''
      : `<div class="dv-note dv-in">${icon('warning')}<span>${esc(tr('devices.deployMissing'))}` +
        `<code class="dv-log-path" title="${esc(state.payloadPath)}">${esc(state.payloadPath)}</code></span></div>`;
    return `${note}<div class="dv-devices">${state.targets.map(deviceCard).join('')}</div>`;
  }

  /* ── 报告 ─────────────────────────────────────────── */
  const kv = (label, value, opts = {}) => {
    const cls = ['dv-v'];
    if (opts.mono) cls.push('dv-mono');
    if (opts.full) cls.push('dv-full');
    if (opts.dim) cls.push('dv-dim');
    return `<div class="dv-kv"><span class="dv-k">${esc(label)}</span>` +
      `<span class="${cls.join(' ')}">${value}</span></div>`;
  };

  const card = (iconName, title, note, body) => `<section class="dv-card dv-in">
      <div class="dv-card-h">${icon(iconName)}<b>${esc(title)}</b>${note ? `<small>${esc(note)}</small>` : ''}</div>
      <div class="dv-card-b">${body}</div>
    </section>`;

  function renderOverview(report) {
    const online = state.targets.filter(t => t.online && t.state !== 'untrusted').length;
    const p = patch(report.patchState);
    const tone = p.tone === 'ok' ? 'is-ok' : (p.tone === 'warn' ? 'is-warn' : 'is-accent');
    return `<div class="dv-overview dv-in">
      <div class="dv-ov-state ${tone}">${icon(p.icon)}
        <div><div class="dv-ov-label">${esc(tr('devices.patchState'))}</div>
        <div class="dv-ov-value">${esc(tr(p.key))}</div></div>
      </div>
      <div class="dv-stats">
        <div class="dv-stat"><b>${esc(fmtBytes(report.bundleBytes))}</b><small>${esc(tr('devices.size'))}</small></div>
        <div class="dv-stat"><b>${esc(report.assetCount ?? 0)}</b><small>${esc(tr('devices.assetCount'))}</small></div>
        <div class="dv-stat"><b>${esc(report.loaderPresent ? tr('devices.yes') : tr('devices.no'))}</b><small>${esc(tr('devices.loaderPresent'))}</small></div>
        <div class="dv-stat"><b>${esc(report.modCount ?? 0)}</b><small>${esc(tr('devices.modCount'))}</small></div>
        <div class="dv-stat"><b>${online}/${state.targets.length}</b><small>${esc(tr('devices.online'))}</small></div>
      </div>
    </div>`;
  }

  function renderReport() {
    const r = state.report;
    if (!r) {
      // 写操作刚结束时宿主只回了一条结果，还没有新的检测报告；这里先把结果留在原位。
      return state.notice
        ? `<div class="dv-note dv-in">${icon('info')}<span>${esc(state.notice)}</span></div>`
        : `<div class="dv-empty dv-in">${icon('info')}<div>${esc(tr('devices.detectHint'))}</div></div>`;
    }

    const device = card('smart_toy', tr('devices.deviceCard'), r.bundleId,
      kv(tr('devices.label'), esc(r.label || '—')) +
      kv(tr('devices.bundleId'), esc(r.bundleId || '—'), { mono: true }) +
      kv(tr('devices.appVersion'), esc(r.appVersion || '—'), { mono: true }) +
      kv(tr('devices.detectedAt'), esc(r.detectedAt || '—'), { dim: true }));

    const bundle = card('data_object', tr('devices.codeBundleCard'), r.codeBundleHash,
      kv(tr('devices.codeBundleHash'), esc(r.codeBundleHash || '—'), { mono: true }) +
      kv(tr('devices.bundleDir'), esc(r.bundleDir || '—'), { mono: true, full: true }) +
      kv(tr('devices.size'), esc(fmtBytes(r.bundleBytes))) +
      kv(tr('devices.assetCount'), esc(r.assetCount ?? 0)) +
      kv(tr('devices.sha256'), esc(r.bundleSha256 || '—'), { mono: true, full: true }));

    const catalog = card('description', tr('devices.catalogCard'), r.catalogBuildHash,
      kv(tr('devices.catalog'), esc(r.catalog || '—'), { mono: true }) +
      kv(tr('devices.catalogBuildHash'), esc(r.catalogBuildHash || '—'), { mono: true, full: true }) +
      kv(tr('devices.sha256'), esc(r.catalogSha256 || '—'), { mono: true, full: true }));

    const patches = card('bolt', tr('devices.patchCard'), null,
      kv(tr('devices.patchState'), patchPill(r.patchState)) +
      kv(tr('devices.injected'), yesNo(r.injected)) +
      kv(tr('devices.legacyMarker'), yesNo(r.legacyMarker)) +
      kv(tr('devices.currentMarker'), yesNo(r.currentMarker)));

    const loader = card('extension', tr('devices.loaderCard'),
      r.loaderVersion ? r.loaderVersion : null,
      kv(tr('devices.loaderPresent'), yesNo(r.loaderPresent)) +
      kv(tr('devices.loaderVersion'), esc(r.loaderVersion || '—'), { mono: true }) +
      kv(tr('devices.sdkVersion'), esc(r.sdkVersion || '—'), { mono: true }) +
      kv(tr('devices.modCount'), esc(r.modCount ?? 0)) +
      kv(tr('devices.modsOk'), esc(`${r.modsOk ?? 0} / ${r.modsFailed ?? 0}`)) +
      kv(tr('devices.bootstrapAssembly'), esc(r.bootstrapAssembly || '—'), { mono: true, full: true }) +
      kv(tr('devices.bootstrapLog'), esc(r.bootstrapLog || '—'), { mono: true, full: true }) +
      (r.bootstrapTail && r.bootstrapTail.length
        ? `<pre class="dv-log">${esc(r.bootstrapTail.join('\n'))}</pre>` : ''));

    const issues = r.issues && r.issues.length
      ? `<section class="dv-card dv-in">
          <div class="dv-card-h">${icon('warning')}<b>${esc(tr('devices.issues'))}</b><small>${r.issues.length}</small></div>
          <div class="dv-issues">${r.issues.map(text =>
            `<div class="dv-issue">${icon('warning')}<span>${esc(text)}</span></div>`).join('')}</div>
        </section>`
      : '';

    return `${renderOverview(r)}<div class="dv-grid">${device}${patches}${bundle}${catalog}${loader}${issues}</div>`;
  }

  /* ── 游戏包候选（一台设备上可能装了多个渠道服） ─────── */
  function packageRow(pkg, target) {
    const current = !!target && pkg.appId === target.appId;
    const meta = [
      pkg.name && pkg.name !== pkg.appId ? pkg.name : '',
      pkg.version,
      pkg.hasCache ? tr('devices.packageCache') : '',
      pkg.matchedBy ? '[' + pkg.matchedBy + ']' : '',
    ].filter(Boolean).join(' · ');
    return `<div class="dv-pkg${current ? ' is-current' : ''}">
      <div class="dv-pkg-main">
        <div class="dv-pkg-id">${esc(pkg.appId)}</div>
        <div class="dv-pkg-meta">${esc(meta || '—')}</div>
      </div>
      <div class="dv-pkg-actions">
        ${current
          ? `<span class="dv-pill is-accent">${icon('check')}${esc(tr('devices.packageCurrent'))}</span>`
          : `<button type="button" class="dv-btn" data-dev-setapp="${esc(pkg.appId)}"
               data-dev-target="${esc(target ? target.id : '')}" ${state.busy ? 'disabled' : ''}>
               ${esc(tr('devices.packageSet'))}</button>`}
      </div>
    </div>`;
  }

  function renderPackages() {
    const target = state.targets.find(t => t.id === state.selected);
    let body;
    if (!state.packages) body = `<div class="dv-hint">${esc(tr('devices.packageEmpty'))}</div>`;
    else if (!state.packages.length) body = `<div class="dv-hint">${esc(tr('devices.packageNone'))}</div>`;
    else body = `<div class="dv-pkgs">${state.packages.map(pkg => packageRow(pkg, target)).join('')}</div>`;
    return `<section class="dv-card dv-in">
      <div class="dv-card-h">${icon('inventory_2')}<b>${esc(tr('devices.packageCard'))}</b>
        <button type="button" class="dv-btn" data-dev-scan="${esc(target ? target.id : '')}"
          ${state.busy || !target ? 'disabled' : ''}>${icon('search')}${esc(tr('devices.packageScan'))}</button>
      </div>
      ${body}
    </section>`;
  }

  /* ── 设备上的模组（目录即状态） ─────────────────────── */
  function renderMods() {
    const target = state.targets.find(t => t.id === state.selected);
    let body;
    if (!state.mods) {
      body = `<div class="dv-hint">${esc(tr('devices.modsHint'))}</div>`;
    } else if (!state.mods.length) {
      body = `<div class="dv-hint">${esc(tr('devices.modsEmpty'))}</div>`;
    } else {
      body = `<div class="dv-pkgs">${state.mods.map(mod => {
        const meta = [mod.version ? 'v' + mod.version : '',
          mod.sdkVersion ? 'SDK ' + mod.sdkVersion : ''].filter(Boolean).join(' · ');
        return `<div class="dv-pkg${mod.enabled ? ' is-current' : ''}">
          <div class="dv-pkg-main">
            <div class="dv-pkg-id">${esc(mod.name || mod.id)}</div>
            <div class="dv-pkg-meta">${esc([mod.id, meta].filter(Boolean).join(' · ') || '—')}</div>
          </div>
          <div class="dv-pkg-actions">
            ${mod.hasConfig ? `<button type="button" class="dv-btn" data-dev-modconfig="${esc(mod.id)}"
              data-dev-target="${esc(target ? target.id : '')}" ${state.busy ? 'disabled' : ''}>
              ${icon('settings')}${esc(tr('devices.modConfig'))}</button>` : ''}
            <button type="button" class="dv-btn" data-dev-modtoggle="${esc(mod.id)}"
              data-dev-enabled="${mod.enabled ? '0' : '1'}"
              data-dev-target="${esc(target ? target.id : '')}" ${state.busy ? 'disabled' : ''}>
              ${icon(mod.enabled ? 'cancel' : 'check')}${esc(tr(mod.enabled ? 'devices.modDisable' : 'devices.modEnable'))}</button>
          </div>
        </div>`;
      }).join('')}</div>`;
    }
    return `<section class="dv-card dv-in">
      <div class="dv-card-h">${icon('extension')}<b>${esc(tr('devices.modsCard'))}</b>
        <small>${state.mods ? state.mods.length : 0}</small>
        <button type="button" class="dv-btn" data-dev-market="${esc(target ? target.id : '')}"
          ${state.busy || !target ? 'disabled' : ''}>${icon('shopping_cart')}${esc(tr('devices.modMarket'))}</button>
        <button type="button" class="dv-btn" data-dev-modsload="${esc(target ? target.id : '')}"
          ${state.busy || !target ? 'disabled' : ''}>${icon('refresh')}${esc(tr('devices.modsLoad'))}</button>
        <button type="button" class="dv-btn" data-dev-modimport="${esc(target ? target.id : '')}"
          ${state.busy || !target ? 'disabled' : ''}>${icon('folder_zip')}${esc(tr('devices.modImport'))}</button>
      </div>
      ${body}
    </section>`;
  }

  /* ── 设备上的卡面方案（M5）：目录 = <加载器根>/skins/<方案> ── */
  function renderSkins() {
    const target = state.targets.find(t => t.id === state.selected);
    const skins = state.skins;
    let body;
    if (!skins) {
      body = `<div class="dv-hint">${esc(tr('devices.skinsHint'))}</div>`;
    } else {
      const rows = (skins.profiles || []).filter(p => !p.isDefault).map(profile => {
        const meta = [
          profile.cardCount ? `${profile.cardCount} ${tr('devices.skinCards')}` : '',
          profile.author || '',
        ].filter(Boolean).join(' · ');
        return `<div class="dv-pkg${profile.active ? ' is-current' : ''}">
          <div class="dv-pkg-main">
            <div class="dv-pkg-id">${esc(profile.displayName || profile.name)}</div>
            <div class="dv-pkg-meta">${esc([profile.name, meta].filter(Boolean).join(' · ') || '—')}</div>
            ${profile.description ? `<div class="dv-pkg-meta">${esc(profile.description)}</div>` : ''}
          </div>
          <div class="dv-pkg-actions">
            ${profile.active
              ? `<span class="dv-pill is-accent">${icon('check')}${esc(tr('devices.skinActive'))}</span>`
              : `<button type="button" class="dv-btn" data-dev-skinset="${esc(profile.name)}"
                   data-dev-target="${esc(target ? target.id : '')}"
                   ${state.busy || !skins.cardSkinModPresent ? 'disabled' : ''}>
                   ${icon('bolt')}${esc(tr('devices.skinSet'))}</button>`}
            <button type="button" class="dv-btn" data-dev-skindelete="${esc(profile.name)}"
              data-dev-target="${esc(target ? target.id : '')}" ${state.busy ? 'disabled' : ''}>
              ${icon('delete')}${esc(tr('devices.skinDelete'))}</button>
          </div>
        </div>`;
      }).join('');

      const defaultRow = `<div class="dv-pkg${skins.activeSkin === '__game_default__' ? ' is-current' : ''}">
        <div class="dv-pkg-main">
          <div class="dv-pkg-id">${esc(tr('devices.skinGameDefault'))}</div>
          <div class="dv-pkg-meta">${esc(tr('devices.skinGameDefaultHint'))}</div>
        </div>
        <div class="dv-pkg-actions">
          ${skins.activeSkin === '__game_default__'
            ? `<span class="dv-pill is-accent">${icon('check')}${esc(tr('devices.skinActive'))}</span>`
            : `<button type="button" class="dv-btn" data-dev-skinset="__game_default__"
                 data-dev-target="${esc(target ? target.id : '')}"
                 ${state.busy || !skins.cardSkinModPresent ? 'disabled' : ''}>
                 ${icon('cancel')}${esc(tr('devices.skinSetDefault'))}</button>`}
        </div>
      </div>`;

      body = `<div class="dv-pkgs">${rows || `<div class="dv-hint">${esc(tr('devices.skinsEmpty'))}</div>`}${defaultRow}</div>`;

      const notes = [];
      if (!skins.cardSkinModPresent) notes.push(tr('devices.skinsModMissing'));
      if (skins.hasDefaultPool === false) notes.push(tr('devices.skinsNoDefaultPool'));
      (skins.issues || []).forEach(text => notes.push(text));
      if (notes.length) {
        body += notes.map(text =>
          `<div class="dv-note">${icon('info')}<span>${esc(text)}</span></div>`).join('');
      }
    }

    return `<section class="dv-card dv-in">
      <div class="dv-card-h">${icon('playing_cards')}<b>${esc(tr('devices.skinsCard'))}</b>
        <small>${skins ? (skins.profiles || []).filter(p => !p.isDefault).length : 0}</small>
        <button type="button" class="dv-btn" data-dev-skinsload="${esc(target ? target.id : '')}"
          ${state.busy || !target ? 'disabled' : ''}>${icon('refresh')}${esc(tr('devices.skinsLoad'))}</button>
        <button type="button" class="dv-btn" data-dev-skinimport="${esc(target ? target.id : '')}"
          ${state.busy || !target ? 'disabled' : ''}>${icon('folder_zip')}${esc(tr('devices.skinsImport'))}</button>
      </div>
      ${body}
    </section>`;
  }

  /* ── 操作日志：文件里一份，这里实时渲染一份 ─────────── */
  const logLine = entry => `<div class="dv-log-line lv-${esc(String(entry.level || 'info').toLowerCase())}">` +
    `<span class="dv-log-t">${esc(entry.time)}</span>` +
    `<span class="dv-log-l">${esc(entry.level)}</span>` +
    `<span class="dv-log-m">${esc(entry.message)}</span></div>`;

  function renderLogCard() {
    const lines = state.logs;
    const body = lines.length
      ? `<div class="dv-logs" id="devLogs" role="region" aria-label="${esc(tr('devices.logCard'))}">${lines.map(logLine).join('')}</div>`
      : `<div class="dv-logs-empty">${esc(tr('devices.logEmpty'))}</div>`;
    return `<section class="dv-card dv-logcard dv-in">
      <div class="dv-card-h">${icon('description')}<b>${esc(tr('devices.logCard'))}</b>
        <span class="dv-log-path" title="${esc(state.logPath)}">${esc(state.logPath || '—')}</span>
        <small>${lines.length}</small>
        <button type="button" class="dv-btn" data-dev-action="openlogs">${icon('folder_open')}${esc(tr('devices.logOpen'))}</button>
        <button type="button" class="dv-btn" data-dev-action="clearlogs" ${lines.length ? '' : 'disabled'}>${icon('delete')}${esc(tr('devices.logClear'))}</button>
      </div>
      ${body}
    </section>`;
  }

  /** 追加一条日志。已经在底部才自动跟随，否则不打断往上翻的人。 */
  function appendLog(entry) {
    state.logs.push(entry);
    if (state.logs.length > LOG_LIMIT) state.logs.splice(0, state.logs.length - LOG_LIMIT);

    const box = document.getElementById('devLogs');
    if (!box) {
      render();
      return;
    }
    const pinned = box.scrollTop + box.clientHeight >= box.scrollHeight - 24;
    box.insertAdjacentHTML('beforeend', logLine(entry));
    while (box.childElementCount > LOG_LIMIT) box.removeChild(box.firstElementChild);
    if (pinned) box.scrollTop = box.scrollHeight;
  }

  function pinLogs() {
    const box = document.getElementById('devLogs');
    if (box) box.scrollTop = box.scrollHeight;
  }

  /* ── 确认对话框：原生 <dialog> + showModal() ──────────
     焦点陷阱、Esc、背景 inert 都由浏览器负责；不用 window.confirm（样式脱节，也无法本地化按钮）。 */
  let confirmResolve = null;

  function settleConfirm(value) {
    const dialog = document.getElementById('devConfirm');
    if (dialog && dialog.open) dialog.close();
    const resolve = confirmResolve;
    confirmResolve = null;
    if (resolve) resolve(value);
  }

  function ensureConfirmDialog() {
    let dialog = document.getElementById('devConfirm');
    if (dialog) return dialog;
    dialog = document.createElement('dialog');
    dialog.id = 'devConfirm';
    dialog.className = 'dv-dialog';
    dialog.innerHTML = `<div class="dv-dialog-h">${esc(tr('devices.restore'))}</div>
      <div class="dv-dialog-b" data-confirm-msg></div>
      <div class="dv-dialog-f">
        <button type="button" class="dv-dialog-btn" data-confirm-no>${esc(tr('common.cancel'))}</button>
        <button type="button" class="dv-dialog-btn is-primary" data-confirm-ok>${esc(tr('common.confirm'))}</button>
      </div>`;
    dialog.addEventListener('cancel', event => {
      // 自己关：先拦下浏览器默认关闭，再走统一收尾，避免重复 resolve
      event.preventDefault();
      settleConfirm(false);
    });
    dialog.querySelector('[data-confirm-no]').addEventListener('click', () => settleConfirm(false));
    dialog.querySelector('[data-confirm-ok]').addEventListener('click', () => settleConfirm(true));
    document.body.appendChild(dialog);
    return dialog;
  }

  function confirmDialog(message) {
    const dialog = ensureConfirmDialog();
    if (typeof dialog.showModal !== 'function') {
      return Promise.resolve(typeof window.confirm === 'function' ? window.confirm(message) : true);
    }
    dialog.querySelector('[data-confirm-msg]').textContent = message;
    return new Promise(resolve => {
      confirmResolve = resolve;
      dialog.showModal();
    });
  }

  /* ── 模组配置弹窗（只做标量；对象/数组提示去编辑文件） ── */
  function renderConfigForm(fields) {
    const scalar = fields.filter(f => f.kind !== 'other');
    const complex = fields.filter(f => f.kind === 'other');
    if (!scalar.length) {
      return `<div class="dv-hint">${esc(tr('devices.configNoScalars'))}</div>`;
    }

    const rows = scalar.map(field => {
      const control = field.kind === 'bool'
        ? `<input type="checkbox" data-cfg-field="${esc(field.name)}" ${field.boolValue ? 'checked' : ''}>`
        : field.kind === 'number'
          ? `<input type="number" step="any" data-cfg-field="${esc(field.name)}" value="${esc(field.stringValue)}">`
          : `<input type="text" data-cfg-field="${esc(field.name)}" value="${esc(field.stringValue)}">`;
      return `<label class="dv-cfg-row">
        <span class="dv-cfg-name">${esc(field.name)}</span>${control}
        ${field.comment ? `<small class="dv-cfg-comment">${esc(field.comment)}</small>` : ''}
      </label>`;
    }).join('');

    return `<form class="dv-cfg-form" onsubmit="return false">${rows}</form>` +
      (complex.length ? `<div class="dv-hint">${esc(tr('devices.configComplexHint'))}</div>` : '');
  }

  function ensureConfigDialog() {
    let dialog = document.getElementById('devConfig');
    if (dialog) return dialog;
    dialog = document.createElement('dialog');
    dialog.id = 'devConfig';
    dialog.className = 'dv-dialog dv-dialog-wide';
    dialog.innerHTML = `<div class="dv-dialog-h" data-cfg-title></div>
      <div class="dv-dialog-b" data-cfg-body></div>
      <div class="dv-dialog-f">
        <button type="button" class="dv-dialog-btn" data-cfg-cancel>${esc(tr('common.cancel'))}</button>
        <button type="button" class="dv-dialog-btn is-primary" data-cfg-save>${esc(tr('common.confirm'))}</button>
      </div>`;
    dialog.querySelector('[data-cfg-cancel]').addEventListener('click', () => dialog.close());
    dialog.addEventListener('cancel', () => dialog.close());
    dialog.querySelector('[data-cfg-save]').addEventListener('click', saveConfigDialog);
    document.body.appendChild(dialog);
    return dialog;
  }

  function openConfigDialog(payload) {
    const dialog = ensureConfigDialog();
    state.config = payload;
    dialog.querySelector('[data-cfg-title]').textContent = `${payload.modId} · ${tr('devices.modConfig')}`;
    dialog.querySelector('[data-cfg-body]').innerHTML = renderConfigForm(payload.fields || []);
    if (typeof dialog.showModal === 'function') dialog.showModal();
  }

  /** 保存：只提交真正改过的字段，交给后端逐个做文本定点替换。 */
  function saveConfigDialog() {
    const payload = state.config;
    const dialog = document.getElementById('devConfig');
    if (!payload || !dialog) return;

    const changes = [];
    dialog.querySelectorAll('[data-cfg-field]').forEach(input => {
      const field = (payload.fields || []).find(f => f.name === input.dataset.cfgField);
      if (!field) return;
      if (field.kind === 'bool') {
        const next = input.checked ? 'true' : 'false';
        if (next !== String(!!field.boolValue)) changes.push({ key: field.name, kind: 'bool', value: next });
        return;
      }
      if (input.value !== field.stringValue) {
        changes.push({ key: field.name, kind: field.kind, value: input.value });
      }
    });

    dialog.close();
    state.config = null;
    if (!changes.length) {
      if (typeof toast === 'function') toast(tr('devices.configNoChange'));
      return;
    }

    post({ type: 'deviceSetModConfig', targetId: payload.targetId, modId: payload.modId, changes });
  }

  function render() {
    if (!state.loaded) {
      skeleton();
      return;
    }
    const online = state.targets.filter(t => t.online && t.state !== 'untrusted').length;
    const subtitle = state.targets.length
      ? `${online}/${state.targets.length} · ${tr('devices.online')}`
      : tr('devices.detectHint');
    host.innerHTML = `<div class="dv-page">
      <div class="dv-top">
        <div><h1>${esc(tr('devices.title'))}</h1><div class="dv-top-sub">${esc(subtitle)}</div></div>
        <div class="dv-top-actions">
          <button type="button" class="dv-btn" data-dev-action="refresh" ${state.busy ? 'disabled' : ''}>
            ${icon('refresh')}${esc(tr('devices.refresh'))}</button>
        </div>
      </div>
      ${busyPanel()}
      ${renderDevices()}
      ${renderReport()}
      <div class="dv-grid">${renderPackages()}${renderMods()}${renderSkins()}${renderLogCard()}</div>
    </div>`;
    pinLogs();
  }

  /* 进度只更新进度面板，不重建整页，避免闪烁 */
  function updateBusy(message, percent) {
    const panel = document.getElementById('devBusy');
    if (!panel) return;
    panel.hidden = !state.busy;
    if (!state.busy) return;
    const title = document.getElementById('devBusyTitle');
    const msg = document.getElementById('devBusyMsg');
    const seg = document.getElementById('devBusySeg');
    if (title) title.textContent = busyTitle();
    if (msg) msg.textContent = message;
    if (seg) seg.innerHTML = segments(percent);
    document.querySelectorAll('#v2Devices [data-dev-detect], #v2Devices [data-dev-patch], #v2Devices [data-dev-restore], #v2Devices [data-dev-uninstall]')
      .forEach(button => { button.disabled = true; });
  }

  function onHostMessage(msg) {
    if (!msg || !msg.type) return;
    switch (msg.type) {
      case 'devices':
        state.loaded = true;
        state.targets = msg.payload?.targets || [];
        state.active = msg.payload?.active || null;
        state.logPath = msg.payload?.logPath || state.logPath;
        state.payloadPath = msg.payload?.payloadPath || state.payloadPath;
        state.payloadPresent = !!msg.payload?.payloadPresent;
        if (Array.isArray(msg.payload?.logLines)) state.logs = msg.payload.logLines.slice(-LOG_LIMIT);
        // 刷新后也要让守卫知道当前目标是设备还是本机：
        // gameProfiles 带统一 id（local:<档案> / <设备 id>），devices 只带设备清单。
        const activeId = msg.payload?.activeTargetId || '';
        if (activeId) {
          if (activeId.startsWith('local:')) window.TargetGuard?.set({ kind: 'local' });
          else {
            const device = (msg.payload?.targets || []).find(t => t.id === activeId);
            if (device) {
              window.TargetGuard?.set({
                kind: 'mobile',
                deviceLabel: device.label,
                online: device.online,
                targetId: device.id,
              });
            }
          }
        }
        if (!state.selected) {
          const active = state.targets.find(t => t.id === state.active?.targetId) ||
            state.targets.find(t => t.online) || state.targets[0];
          state.selected = active?.id || null;
        }
        render();
        break;
      case 'devicePackages':
        state.packages = msg.payload?.packages || [];
        state.busy = false;
        render();
        break;
      case 'deviceMods':
        state.mods = msg.payload?.mods || [];
        state.busy = false;
        render();
        break;
      case 'deviceSkins':
        state.skins = msg.payload || null;
        state.busy = false;
        render();
        break;
      case 'deviceModConfig':
        openConfigDialog(msg.payload);
        break;
      case 'deviceLog':
        if (msg.payload) appendLog(msg.payload);
        break;
      case 'deviceDetectReport':
        state.report = msg.payload || null;
        state.notice = '';
        state.busy = false;
        render();
        break;
      case 'deviceDetectProgress': {
        const wasBusy = state.busy;
        state.progress = msg.payload || null;
        state.busy = !!state.progress?.active;
        // 收尾那一条（active=false）必须整页重绘：进度更新只禁用按钮，不重绘的话
        // 写操作结束后按钮会一直保持禁用，直到离开页面再回来。
        if (wasBusy && !state.busy) render();
        else updateBusy(state.progress?.message || '', state.progress?.percent);
        break;
      }
      case 'deviceActive':
        state.selected = msg.payload?.targetId || null;
        render();
        break;
      case 'targetChanged':
        // 宿主已在 targetSetActive 里写过 active-target.json；这里只同步给守卫与设备页
        window.TargetGuard?.set(msg.payload);
        if (msg.payload?.kind === 'mobile') state.selected = msg.payload.targetId || state.selected;
        break;
      case 'deviceOperationResult':
        state.notice = msg.payload?.message || '';
        if (typeof toast === 'function') toast(state.notice);
        state.report = null;
        state.mods = null;
        state.skins = null;
        render();
        post({ type: 'deviceMods', targetId: msg.payload?.targetId || state.selected });
        // 写操作也会动 skins/ 与 CardSkinMod 的配置，方案卡片跟着重取一次
        if (state.skins) post({ type: 'deviceSkins', targetId: msg.payload?.targetId || state.selected });
        break;
      default:
        break;
    }
  }

  document.addEventListener('click', event => {
    const button = event.target.closest(
      '[data-dev-action], [data-dev-select], [data-dev-detect], [data-dev-patch], [data-dev-restore],' +
      ' [data-dev-uninstall], [data-dev-scan], [data-dev-setapp], [data-dev-deploy],' +
      ' [data-dev-modsload], [data-dev-modtoggle], [data-dev-modconfig], [data-dev-modimport],' +
      ' [data-dev-market], [data-dev-skinsload], [data-dev-skinimport], [data-dev-skinset], [data-dev-skindelete]');
    if (!button || !host.contains(button)) return;

    if (button.dataset.devAction === 'openlogs') {
      post({ type: 'openLogsFolder' });
      return;
    }
    if (button.dataset.devAction === 'clearlogs') {
      state.logs = [];
      render();
      return;
    }
    if (button.dataset.devAction === 'refresh') {
      state.loaded = false;
      state.notice = '';
      render();
      post({ type: 'devicesRefresh' });
      return;
    }
    if (button.dataset.devScan && !state.busy) {
      state.selected = button.dataset.devScan;
      state.busy = true;
      state.progress = { phase: 'scan', percent: 0, message: '', active: true };
      state.packages = null;
      render();
      post({ type: 'deviceScanPackages', targetId: button.dataset.devScan });
      return;
    }
    if (button.dataset.devDeploy && !state.busy) {
      const id = button.dataset.devDeploy;
      state.selected = id;
      state.busy = true;
      state.progress = { phase: 'write', percent: 0, message: '', active: true };
      state.report = null;
      state.notice = '';
      render();
      post({ type: 'deviceDeployPayload', targetId: id });
      return;
    }
    if (button.dataset.devModimport && !state.busy) {
      state.selected = button.dataset.devModimport;
      state.busy = true;
      state.progress = { phase: 'write', percent: 0, message: '', active: true };
      render();
      post({ type: 'deviceImportMod', targetId: button.dataset.devModimport });
      return;
    }
    if (button.dataset.devMarket && !state.busy) {
      // 市场页按「当前目标」决定装到哪：先把目标设成这台设备，再切过去。
      const id = button.dataset.devMarket;
      state.selected = id;
      post({ type: 'targetSetActive', id });
      if (typeof showPage === 'function') showPage('workshop');
      return;
    }
    if (button.dataset.devSkinsload && !state.busy) {
      state.selected = button.dataset.devSkinsload;
      state.busy = true;
      state.progress = { phase: 'scan', percent: 0, message: '', active: true };
      state.skins = null;
      render();
      post({ type: 'deviceSkins', targetId: button.dataset.devSkinsload });
      return;
    }
    if (button.dataset.devSkinimport && !state.busy) {
      const id = button.dataset.devSkinimport;
      state.selected = id;
      state.busy = true;
      state.progress = { phase: 'write', percent: 0, message: '', active: true };
      render();
      post({ type: 'deviceImportSkin', targetId: id });
      return;
    }
    if (button.dataset.devSkinset && !state.busy) {
      const id = button.dataset.devTarget || state.selected;
      state.busy = true;
      state.progress = { phase: 'write', percent: 0, message: '', active: true };
      render();
      post({ type: 'deviceSetSkin', targetId: id, profile: button.dataset.devSkinset });
      return;
    }
    if (button.dataset.devSkindelete && !state.busy) {
      const id = button.dataset.devTarget || state.selected;
      const profile = button.dataset.devSkindelete;
      confirmDialog(tr('devices.skinDeleteConfirm').replace('{name}', profile)).then(ok => {
        if (!ok) return;
        state.busy = true;
        state.progress = { phase: 'write', percent: 0, message: '', active: true };
        render();
        post({ type: 'deviceDeleteSkin', targetId: id, profile });
      });
      return;
    }
    if (button.dataset.devModsload && !state.busy) {
      state.selected = button.dataset.devModsload;
      state.busy = true;
      state.progress = { phase: 'scan', percent: 0, message: '', active: true };
      state.mods = null;
      render();
      post({ type: 'deviceMods', targetId: button.dataset.devModsload });
      return;
    }
    if (button.dataset.devModconfig && !state.busy) {
      post({
        type: 'deviceModConfig',
        targetId: button.dataset.devTarget,
        modId: button.dataset.devModconfig,
        language: document.documentElement.lang || '',
      });
      return;
    }
    if (button.dataset.devModtoggle && !state.busy) {
      post({
        type: 'deviceSetMod',
        targetId: button.dataset.devTarget,
        modId: button.dataset.devModtoggle,
        enabled: button.dataset.devEnabled === '1',
      });
      return;
    }
    if (button.dataset.devSetapp && !state.busy) {
      post({
        type: 'deviceSetApp',
        targetId: button.dataset.devTarget,
        appId: button.dataset.devSetapp,
      });
      return;
    }
    if (button.dataset.devSelect) {
      if (state.busy) return;
      state.selected = button.dataset.devSelect;
      state.report = null;
      state.notice = '';
      post({ type: 'deviceSetActive', targetId: state.selected });
      render();
      return;
    }
    if (button.dataset.devDetect && !state.busy) {
      state.selected = button.dataset.devDetect;
      state.busy = true;
      state.progress = { phase: 'detect', percent: 0, message: '', active: true };
      state.report = null;
      state.notice = '';
      render();
      post({ type: 'deviceDetect', targetId: button.dataset.devDetect });
      return;
    }
    const patchId = button.dataset.devPatch;
    const restoreId = button.dataset.devRestore;
    const uninstallId = button.dataset.devUninstall;
    const writeId = patchId || restoreId || uninstallId;
    if (writeId && !state.busy) {
      const run = () => {
        state.selected = writeId;
        state.busy = true;
        state.progress = { phase: 'write', percent: 0, message: '', active: true };
        state.report = null;
        state.notice = '';
        render();
        post({
          type: patchId ? 'devicePatch' : 'deviceRestore',
          targetId: writeId,
          removeLoader: !!uninstallId,
        });
      };
      if (restoreId || uninstallId) {
        confirmDialog(tr('devices.restoreConfirm')).then(ok => { if (ok) run(); });
      } else {
        run();
      }
    }
  });

  window.chrome?.webview?.addEventListener('message', event => onHostMessage(event.data));

  /* ── §8.4 目标守卫：当前目标是设备时，把设备上做不到的入口禁用并给出原因 ──
     页面/子页各自渲染，所以不能用静态名单一次性绑事件；这里提供 apply()，
     由路由在每次切页后调用一次，再做一次「按钮被动态重建」的兜底观察。
     约定：任何视图都可以给元素加 data-target-unsupported="<i18n 键>" 来加入名单。 */
  const TARGET = { kind: 'local', label: '', online: false };

  /** 静态已知的入口（动态渲染的那几个由 data-target-unsupported 标注）。 */
  const UNSUPPORTED_SELECTORS = [
    ['#characterSkinTab', 'devices.unsupportedCharacter'],
    ['#audioSkinTab', 'devices.unsupportedAudio'],
    ['#resourceModTab', 'devices.unsupportedResources'],
    ['#btnMenuLaunchGame', 'devices.unsupportedLaunch'],
    ['#launchModeSteam', 'devices.unsupportedLaunch'],
  ];

  const targetIsMobile = () => TARGET.kind === 'mobile';

  function noticeStrip() {
    let strip = document.getElementById('targetNotice');
    if (!targetIsMobile()) {
      if (strip) strip.remove();
      return;
    }

    if (!strip) {
      strip = document.createElement('div');
      strip.id = 'targetNotice';
      strip.className = 'dv-target-notice';
      strip.setAttribute('role', 'status');
      const header = document.querySelector('.global-header');
      if (!header || !header.parentNode) return;
      header.parentNode.insertBefore(strip, header.nextSibling);
    }

    const why = tr('devices.unsupportedWhy');
    strip.innerHTML = `${icon('smart_toy')}<span>${esc(
      tr('devices.targetDevice').replace('{name}', TARGET.label || tr('devices.title'))
    )}${TARGET.online ? '' : ' · ' + esc(tr('devices.offline'))} — ${esc(why)}</span>`;
  }

  function applyTargetGuard() {
    const mobile = targetIsMobile();
    for (const [selector, key] of UNSUPPORTED_SELECTORS) {
      document.querySelectorAll(selector).forEach(el => {
        if (mobile) {
          el.disabled = true;
          el.setAttribute('aria-disabled', 'true');
          el.title = tr(key);
          el.classList.add('is-target-blocked');
        } else if (el.classList.contains('is-target-blocked')) {
          // 切回本机：只解掉我们加上的东西，恢复页面自己维护的 disabled 状态
          el.disabled = false;
          el.removeAttribute('aria-disabled');
          el.removeAttribute('title');
          el.classList.remove('is-target-blocked');
        }
      });
    }

    document.querySelectorAll('[data-target-unsupported]').forEach(el => {
      const key = el.dataset.targetUnsupported || 'devices.unsupportedWhy';
      if (mobile) {
        el.disabled = true;
        el.setAttribute('aria-disabled', 'true');
        el.title = tr(key);
      } else {
        el.disabled = false;
        el.removeAttribute('aria-disabled');
        el.removeAttribute('title');
      }
    });

    noticeStrip();
  }

  window.TargetGuard = {
    apply: applyTargetGuard,
    isMobile: targetIsMobile,
    get: () => ({ ...TARGET }),
    /** 由宿主消息驱动；路由与模组/卡面页只读这里的结果。 */
    set(next) {
      TARGET.kind = next?.kind === 'mobile' ? 'mobile' : 'local';
      TARGET.label = next?.deviceLabel || '';
      TARGET.online = !!next?.online;
      applyTargetGuard();
    },
  };

  // 动态重建的按钮（页面切回来、列表重绘）不会触发路由，用观察器兜一层；
  // 只在设备目标下工作，本机时几乎零开销。
  const observer = new MutationObserver(() => {
    if (targetIsMobile()) applyTargetGuard();
  });
  observer.observe(document.body, { childList: true, subtree: true });

  function onShow() {
    render();
    post({ type: 'devicesGet' });
    applyTargetGuard();
  }

  window.V2Devices = { onShow, render, applyTargetGuard };
})();
