// v2/devices.js — Mobile devices view, host message routing & global target guard
(function () {
  const D = window._V2Devices;
  const Files = window._V2DevicesFiles;
  const {
    tr, icon, post, root, isWideLayout, activeTab, state,
    currentTarget, isSelectedGlobalActive, syncSelection,
    patchStateInfo, renderBusyBar, renderCornerHudHtml, syncProgressInPlace,
    showCornerToast, dismissCornerToast, confirmAction, openModConfigModal,
  } = D;

  function clearBusy() {
    state.busy = false;
    state.pullingPayload = false;
    state.progress = { phase: '', percent: 0, message: '', active: false, indeterminate: false };
    render();
  }

  function startBusy(message = '', indeterminate = true) {
    state.busy = true;
    state.progress = { phase: 'work', percent: 0, message, active: true, indeterminate };
    state.opResult = null;
    render();
  }

  function onDeviceSwitch(nextId) {
    if (!nextId || nextId === state.selectedId) return;
    state.selectedId = nextId;
    state.opResult = null;
    if (state.reportTargetId !== nextId) state.report = null;
    if (state.packagesTargetId !== nextId) state.packages = null;
    if (state.modsTargetId !== nextId) state.mods = null;
    if (state.skinsTargetId !== nextId) state.skins = null;
    if (state.filesTargetId !== nextId) {
      state.filesEntries = null;
      state.filesError = '';
      state.filesPath = '';
      state.filesSelectedPath = '';
      state.filesRenamingPath = '';
      state.filesScrollTop = 0;
      state.filesHistory = [''];
      state.filesHistoryIndex = 0;
    }
    const nextDev = currentTarget();
    if (activeTab() === 'files' && nextDev?.online && !state.busy) {
      Files.navigateTo(nextDev, '', false);
      return;
    }
    render();
  }

  function renderHead() {
    return `
      <header class="dv-head">
        <div class="dv-head-text">
          <button type="button" class="dv-head-experimental" data-dev-show-notice>
            ${icon('error')}
            <span>${esc(tr('devices.experimentalBadge'))}</span>
          </button>
          <h1>${esc(tr('devices.title'))}</h1>
          <p class="dv-head-sub">${esc(tr('devices.subtitle'))}</p>
        </div>
        <div class="dv-head-actions">
          <button type="button" class="v2-btn line" data-dev-pull ${state.busy ? 'disabled' : ''}>
            ${icon('cloud_download')}
            <span>${esc(state.pullingPayload ? tr('devices.pulling') : tr('devices.pullPayload'))}</span>
          </button>
          <button type="button" class="v2-btn ghost" data-dev-refresh ${state.busy ? 'disabled' : ''}>
            ${icon('refresh')}
            <span>${esc(tr('devices.refresh'))}</span>
          </button>
        </div>
      </header>`;
  }

  function renderEnvironmentCard() {
    const env = state.env;
    const adb = env?.adb;
    const itunes = env?.itunes;
    const hasProblem = !env || !adb?.ok || !itunes?.ok;
    const expanded = state.envExpanded || hasProblem;

    const adbTone = !adb ? '' : adb.ok ? 'ok' : 'bad';
    const adbStatusText = !adb ? tr('devices.envChecking')
      : adb.ok ? tr('devices.envOk')
      : adb.configInvalid ? tr('devices.envAdbInvalid')
      : tr('devices.envMissing');
    const adbSourceLabel = adb?.source === 'custom' ? tr('devices.envSourceCustom')
      : adb?.source === 'sdk' ? tr('devices.envSourceSdk')
      : adb?.source === 'path' ? tr('devices.envSourcePath')
      : '';

    const itunesTone = !itunes ? '' : itunes.ok ? 'ok' : (itunes.status === 'stopped' ? 'warn' : 'bad');
    const itunesStatusText = !itunes ? tr('devices.envChecking')
      : itunes.ok ? tr('devices.envRunning')
      : itunes.status === 'stopped' ? tr('devices.envStopped')
      : tr('devices.envMissing');

    const renderDownloadLink = (url, label) => url ? `
      <div class="dv-env-link-row">
        <a class="dv-env-link" href="${esc(url)}" target="_blank" rel="noopener noreferrer">
          ${icon('cloud_download')}
          <span class="dv-env-link-label">${esc(label)}</span>
          <span class="dv-env-link-url">${esc(url)}</span>
          ${icon('open_in_new')}
        </a>
      </div>` : '';

    const adbDownloadUrl = adb?.downloadUrl || 'https://developer.android.com/tools/releases/platform-tools';
    const itunesDownloadUrl = itunes?.downloadUrl || 'https://www.apple.com/itunes/download/win64';

    const bodyHtml = !expanded ? '' : `
      <div class="dv-env-list">
        <div class="dv-env-item">
          <div class="dv-env-item-head">
            <div class="dv-env-item-title">
              ${icon('android')}
              <span>${esc(tr('devices.envAdbTitle'))}</span>
              <span class="dv-pill ${adbTone}">${esc(adbStatusText)}</span>
              ${adbSourceLabel ? `<span class="dv-pill">${esc(adbSourceLabel)}</span>` : ''}
            </div>
            <div class="dv-env-controls">
              <button type="button" class="v2-btn line" data-dev-scan-adb ${state.busy ? 'disabled' : ''}>
                ${icon('radar')}<span>${esc(tr('devices.envScanAdb'))}</span>
              </button>
              <button type="button" class="v2-btn line" data-dev-browse-adb ${state.busy ? 'disabled' : ''}>
                ${icon('folder_open')}<span>${esc(tr('devices.envBrowseAdb'))}</span>
              </button>
              ${adb?.configuredPath ? `
                <button type="button" class="v2-btn ghost" data-dev-clear-adb ${state.busy ? 'disabled' : ''}>
                  <span>${esc(tr('devices.envClearAdb'))}</span>
                </button>` : ''}
            </div>
          </div>
          <div class="dv-item-desc">${esc(adb?.message || tr('devices.envChecking'))}</div>
          ${adb && !adb.ok ? renderDownloadLink(adbDownloadUrl, tr('devices.envDownloadAdb')) : ''}
          ${adb?.resolvedPath ? `<div class="dv-item-sub">${esc(adb.resolvedPath)}</div>` : ''}
          <form class="dv-env-path-form" data-dev-adb-form>
            <input id="dvAdbPathInput" class="dv-input" type="text"
              autocomplete="off" spellcheck="false"
              placeholder="C:\\Android\\platform-tools\\adb.exe"
              value="${esc(adb?.configuredPath || adb?.resolvedPath || '')}">
            <button type="submit" class="v2-btn line" ${state.busy ? 'disabled' : ''}>${esc(tr('devices.envApplyAdb'))}</button>
          </form>
        </div>

        <div class="dv-env-item">
          <div class="dv-env-item-head">
            <div class="dv-env-item-title">
              ${icon('smartphone')}
              <span>${esc(tr('devices.envItunesTitle'))}</span>
              <span class="dv-pill ${itunesTone}">${esc(itunesStatusText)}</span>
            </div>
          </div>
          <div class="dv-item-desc">${esc(itunes?.message || tr('devices.envChecking'))}</div>
          ${itunes && !itunes.ok ? renderDownloadLink(itunesDownloadUrl, tr('devices.envDownloadItunes')) : ''}
          ${itunes?.executablePath ? `<div class="dv-item-sub">${esc(itunes.executablePath)}</div>` : ''}
        </div>
      </div>`;

    return `
      <section class="v2-card dv-env-card" aria-label="${esc(tr('devices.envTitle'))}">
        <div class="dv-env-head">
          <div class="dv-env-summary">
            <h2 class="dv-card-title">${icon('build')}${esc(tr('devices.envTitle'))}</h2>
            <span class="dv-pill ${adbTone}">ADB: ${esc(adbStatusText)}</span>
            <span class="dv-pill ${itunesTone}">iTunes: ${esc(itunesStatusText)}</span>
          </div>
          <div class="dv-env-controls">
            <button type="button" class="v2-btn ghost" data-dev-check-env ${state.busy ? 'disabled' : ''}>
              ${icon('refresh')}<span>${esc(tr('devices.envRecheck'))}</span>
            </button>
            ${!hasProblem ? `
              <button type="button" class="v2-btn ghost" data-dev-env-toggle>
                <span>${esc(expanded ? tr('devices.envCollapse') : tr('devices.envExpand'))}</span>
              </button>` : ''}
          </div>
        </div>
        ${bodyHtml}
      </section>`;
  }

  function renderDeviceStrip() {
    if (!state.targets.length) {
      return `
        <section class="dv-empty-hero">
          <div class="dv-empty-art">${icon('devices')}</div>
          <h2 class="dv-empty-title">${esc(tr('devices.title'))}</h2>
          <p class="dv-empty-desc">${esc(tr('devices.noDevices'))}</p>
          <div class="dv-empty-actions">
            <button type="button" class="v2-btn primary" data-dev-refresh ${state.busy ? 'disabled' : ''}>
              ${icon('refresh')}<span>${esc(tr('devices.refresh'))}</span>
            </button>
          </div>
        </section>`;
    }

    const cards = state.targets.map(dev => {
      const selected = state.selectedId === dev.id;
      const globalActive = isSelectedGlobalActive(dev);
      const isAndroid = (dev.kind || '').toLowerCase() !== 'ios';
      return `
        <button type="button"
          class="dv-device${selected ? ' is-selected' : ''}${dev.online ? '' : ' is-offline'}"
          data-dev-select="${esc(dev.id)}"
          aria-pressed="${selected ? 'true' : 'false'}">
          <div class="dv-device-top">
            <div class="dv-device-id">
              <div class="dv-device-icon${isAndroid ? ' is-android' : ''}">
                ${icon(isAndroid ? 'android' : 'smartphone')}
              </div>
              <div class="dv-device-titles">
                <span class="dv-device-name">${esc(dev.label || dev.model || dev.serial)}</span>
                <span class="dv-device-model">${esc(dev.kind?.toUpperCase() || '')} · ${esc(dev.serial)}</span>
              </div>
            </div>
          </div>
          <div class="dv-device-meta">
            <div class="dv-device-badges">
              <span class="dv-pill ${dev.online ? 'ok' : 'bad'}">
                ${icon(dev.online ? 'check_circle' : 'cloud_off')}
                ${esc(dev.online ? tr('devices.online') : tr('devices.offline'))}
              </span>
              ${dev.installed ? `<span class="dv-pill accent">${icon('extension')}${esc(tr('devices.loaderInstalled'))}</span>` : ''}
              ${globalActive ? `<span class="dv-pill ok">${icon('star')}${esc(tr('devices.activeTarget'))}</span>` : ''}
            </div>
            ${dev.appId ? `<span class="dv-pill">${esc(dev.appId)}</span>` : ''}
          </div>
        </button>`;
    }).join('');

    return `<section class="dv-device-strip" aria-label="${esc(tr('devices.deviceCard'))}">${cards}</section>`;
  }

  function renderHeroAndActions(dev) {
    if (!dev) return '';
    const report = state.reportTargetId === dev.id ? state.report : null;
    const isAndroid = (dev.kind || '').toLowerCase() !== 'ios';
    const globalActive = isSelectedGlobalActive(dev);
    const offline = !dev.online;
    const [patchTone, patchIcon, patchText] = report
      ? patchStateInfo(report.patchState)
      : [dev.installed ? 'accent' : '', dev.installed ? 'check_circle' : 'help', dev.installed ? tr('devices.loaderInstalled') : tr('devices.detectHint')];

    const loaderSummary = report
      ? `${report.loaderPresent ? tr('devices.yes') : tr('devices.no')}${report.loaderVersion ? ' · v' + report.loaderVersion : ''}`
      : (dev.installed ? tr('devices.yes') : '—');

    const payloadBadge = state.payloadPresent
      ? `<span class="dv-pill ok">${icon('inventory_2')}${esc(tr('devices.payloadReady'))}${state.payloadVersion ? ' · v' + esc(state.payloadVersion) : ''}${state.payloadSdkVersion ? ' · SDK ' + esc(state.payloadSdkVersion) : ''}</span>`
      : `<span class="dv-pill warn">${icon('warning')}${esc(tr('devices.deployMissing'))}</span>`;

    const staleWarning = report?.patchState === 'stale'
      ? `<div class="dv-alert warn" role="alert">${icon('warning')}<div class="dv-alert-content"><b>${esc(tr('devices.stateStale'))}</b><span>${esc(tr('devices.staleBanner'))}</span></div></div>`
      : '';

    const offlineWarning = offline
      ? `<div class="dv-alert warn" role="status">${icon('cloud_off')}<div class="dv-alert-content"><b>${esc(tr('devices.offline'))}</b><span>${esc(tr('devices.offlineBanner'))}</span></div></div>`
      : '';

    const opFeedback = state.opResult
      ? `<div class="dv-alert ${state.opResult.ok ? 'ok' : 'bad'}" role="status">
          ${icon(state.opResult.ok ? 'check_circle' : 'error')}
          <div class="dv-alert-content">
            <b>${esc(state.opResult.ok ? tr('devices.lastResultOk') : tr('devices.lastResultFail'))}：${esc(state.opResult.message || '')}</b>
            ${state.opResult.detail ? `<span class="dv-alert-detail">${esc(state.opResult.detail)}</span>` : ''}
          </div>
        </div>`
      : '';

    return `
      <section class="v2-card dv-hero-card">
        <div class="dv-hero-head">
          <div class="dv-hero-left">
            <div class="dv-device-icon${isAndroid ? ' is-android' : ''}">
              ${icon(isAndroid ? 'android' : 'smartphone')}
            </div>
            <div>
              <div class="dv-hero-title">
                <span>${esc(dev.label || dev.model || dev.serial)}</span>
                <span class="dv-pill ${patchTone}">${icon(patchIcon)}${esc(patchText)}</span>
                ${report?.appVersion ? `<span class="dv-pill">${esc(report.appVersion)}</span>` : ''}
              </div>
              <div class="dv-hero-sub">
                <button type="button" class="dv-serial-btn" data-dev-copy-serial="${esc(dev.serial)}" title="${esc(tr('devices.copySerial'))}">
                  <span>${esc(dev.serial)}</span>${icon('content_copy')}
                </button>
                <span>${esc(tr('devices.bundleId'))}: <code>${esc(dev.appId || '—')}</code></span>
                <span>${esc(tr('devices.loaderCard'))}: ${esc(loaderSummary)}</span>
              </div>
            </div>
          </div>

          <div class="dv-hero-right">
            <button type="button" class="v2-btn ${globalActive ? 'ghost' : 'line'}"
              data-dev-activate="${esc(dev.id)}" ${globalActive ? 'disabled' : ''}>
              ${icon(globalActive ? 'check' : 'star')}
              <span>${esc(globalActive ? tr('devices.activeTarget') : tr('devices.setActiveTarget'))}</span>
            </button>
          </div>
        </div>

        ${offlineWarning}
        ${staleWarning}
        ${opFeedback}
        ${renderBusyBar()}

        <div class="dv-actions-bar">
          <button type="button" class="v2-btn primary" data-dev-install="${esc(dev.id)}"
            ${state.busy || offline || !state.payloadPresent ? 'disabled' : ''}>
            ${icon('install_mobile')}<span>${esc(tr('devices.installAll'))}</span>
          </button>
          <button type="button" class="v2-btn line" data-dev-detect="${esc(dev.id)}"
            ${state.busy || offline ? 'disabled' : ''}>
            ${icon('search')}<span>${esc(tr('devices.detect'))}</span>
          </button>
          <button type="button" class="v2-btn line" data-dev-deploy="${esc(dev.id)}"
            ${state.busy || offline || !state.payloadPresent ? 'disabled' : ''}>
            ${icon('publish')}<span>${esc(tr('devices.deploy'))}</span>
          </button>
          <button type="button" class="v2-btn line" data-dev-patch="${esc(dev.id)}"
            ${state.busy || offline ? 'disabled' : ''}>
            ${icon('build')}<span>${esc(tr('devices.patch'))}</span>
          </button>
          <div class="dv-actions-danger">
            <button type="button" class="v2-btn line" data-dev-restore="${esc(dev.id)}"
              ${state.busy || offline ? 'disabled' : ''}>
              ${icon('restart_alt')}<span>${esc(tr('devices.restore'))}</span>
            </button>
            <button type="button" class="v2-btn danger dv-danger-btn" data-dev-uninstall="${esc(dev.id)}"
              ${state.busy || offline ? 'disabled' : ''}>
              ${icon('delete')}<span>${esc(tr('devices.uninstall'))}</span>
            </button>
          </div>
        </div>

        <div class="dv-payload-strip">
          <div class="dv-payload-info">
            ${payloadBadge}
            ${state.payloadPath ? `<span class="dv-payload-path" title="${esc(state.payloadPath)}">${esc(state.payloadPath)}</span>` : ''}
          </div>
        </div>
      </section>`;
  }

  function renderTabs(dev) {
    if (!dev) return '';
    const modsCount = state.modsTargetId === dev.id && Array.isArray(state.mods) ? state.mods.length : null;
    const skinsCount = state.skinsTargetId === dev.id && state.skins?.profiles ? state.skins.profiles.length : null;
    const pkgCount = state.packagesTargetId === dev.id && Array.isArray(state.packages) ? state.packages.length : null;
    const filesCount = state.filesTargetId === dev.id && Array.isArray(state.filesEntries) ? state.filesEntries.length : null;
    const curTab = activeTab();

    const tabs = [
      ['overview', 'monitoring', tr('devices.tabOverview'), null],
      ['packages', 'apps', tr('devices.tabPackages'), pkgCount],
      ['mods', 'extension', tr('devices.tabMods'), modsCount],
      ['skins', 'style', tr('devices.tabSkins'), skinsCount],
      ['files', 'folder', tr('devices.tabFiles'), filesCount],
      ['logs', 'terminal', tr('devices.tabLogs'), state.logs.length || null],
    ];

    return `
      <div class="dv-tabs" role="tablist" aria-label="${esc(tr('devices.title'))}">
        ${tabs.map(([id, ic, label, count]) => `
          <button type="button" class="dv-tab${id === 'logs' ? ' dv-log-tab' : ''}" role="tab"
            id="dvTab-${id}"
            data-dev-tab="${id}"
            aria-selected="${curTab === id ? 'true' : 'false'}"
            tabindex="${curTab === id ? '0' : '-1'}">
            ${icon(ic)}
            <span>${esc(label)}</span>
            ${count !== null ? `<span class="dv-tab-count"${id === 'logs' ? ' data-dev-log-count' : ''}>${count}</span>` : (id === 'logs' ? '<span class="dv-tab-count" data-dev-log-count hidden>0</span>' : '')}
          </button>`).join('')}
      </div>`;
  }

  const kvRows = pairs => `<dl class="dv-kv-list">${pairs.map(([k, v, mono]) =>
    `<dt class="dv-kv-k">${esc(k)}</dt><dd class="dv-kv-v${mono ? ' mono' : ''}">${esc(v ?? '—')}</dd>`
  ).join('')}</dl>`;

  function renderOverviewPanel(dev) {
    const r = state.reportTargetId === dev.id ? state.report : null;
    if (!r) return `<div class="v2-card dv-card"><p class="dv-muted">${esc(tr('devices.detectHint'))}</p></div>`;

    const yn = v => v ? tr('devices.yes') : tr('devices.no');
    const [tone, ic, patchText] = patchStateInfo(r.patchState);
    const detectedTime = r.detectedAt ? new Date(r.detectedAt).toLocaleString() : '—';

    return `
      <div class="dv-bento">
        <article class="v2-card dv-card">
          <div class="dv-card-head">
            <h2 class="dv-card-title">${icon('smartphone')}${esc(tr('devices.deviceCard'))}</h2>
            <span class="dv-pill">${esc(tr('devices.detectedAt'))}: ${esc(detectedTime)}</span>
          </div>
          ${kvRows([
            [tr('devices.label'), r.label || dev.label || '—', false],
            [tr('devices.bundleId'), r.bundleId || dev.appId || '—', true],
            [tr('devices.appVersion'), r.appVersion || '—', true],
            [tr('devices.paired'), yn(dev.paired), false],
          ])}
        </article>

        <article class="v2-card dv-card">
          <div class="dv-card-head">
            <h2 class="dv-card-title">${icon('deployed_code')}${esc(tr('devices.patchCard'))}</h2>
            <span class="dv-pill ${tone}">${icon(ic)}${esc(patchText)}</span>
          </div>
          ${kvRows([
            [tr('devices.injected'), yn(r.injected), false],
            [tr('devices.legacyMarker'), yn(r.legacyMarker), false],
            [tr('devices.currentMarker'), yn(r.currentMarker), false],
            [tr('devices.bootstrapAssembly'), r.bootstrapAssembly || '—', true],
          ])}
        </article>

        <article class="v2-card dv-card">
          <div class="dv-card-head">
            <h2 class="dv-card-title">${icon('data_object')}${esc(tr('devices.codeBundleCard'))} & ${esc(tr('devices.catalogCard'))}</h2>
          </div>
          ${kvRows([
            [tr('devices.codeBundleHash'), r.codeBundleHash || '—', true],
            [tr('devices.assetCount'), r.assetCount ?? '—', false],
            [tr('devices.size'), r.bundleBytes ? `${(r.bundleBytes / 1048576).toFixed(2)} MB` : '—', false],
            [tr('devices.sha256'), r.bundleSha256 ? r.bundleSha256.slice(0, 20) + '…' : '—', true],
            [tr('devices.catalog'), r.catalog || '—', true],
            [tr('devices.catalogBuildHash'), r.catalogBuildHash || '—', true],
            [tr('devices.bundleDir'), r.bundleDir || '—', true],
          ])}
        </article>

        <article class="v2-card dv-card">
          <div class="dv-card-head">
            <h2 class="dv-card-title">${icon('extension')}${esc(tr('devices.loaderCard'))}</h2>
            <span class="dv-pill ${r.loaderPresent ? 'ok' : ''}">${esc(r.loaderPresent ? tr('devices.loaderInstalled') : tr('devices.no'))}</span>
          </div>
          ${kvRows([
            [tr('devices.loaderPresent'), yn(r.loaderPresent), false],
            [tr('devices.loaderVersion'), r.loaderVersion || '—', true],
            [tr('devices.sdkVersion'), r.sdkVersion || '—', true],
            [tr('devices.modCount'), r.modCount ?? 0, false],
            [tr('devices.modsOk'), `${r.modsOk ?? 0} / ${r.modsFailed ?? 0}`, false],
            [tr('devices.bootstrapLog'), r.bootstrapLog ? tr('devices.yes') : tr('devices.no'), false],
          ])}
        </article>

        ${Array.isArray(r.issues) && r.issues.length ? `
          <article class="v2-card dv-card dv-card-wide">
            <div class="dv-card-head">
              <h2 class="dv-card-title">${icon('warning')}${esc(tr('devices.issues'))}</h2>
            </div>
            <ul class="dv-issues-list">${r.issues.map(issue => `<li>${esc(issue)}</li>`).join('')}</ul>
          </article>` : ''}
      </div>`;
  }

  function renderPackagesPanel(dev) {
    const offline = !dev.online;
    const list = state.packagesTargetId === dev.id ? state.packages : null;

    let body = `<p class="dv-muted">${esc(tr('devices.packageEmpty'))}</p>`;
    if (Array.isArray(list)) {
      if (!list.length) {
        body = `<p class="dv-muted">${esc(tr('devices.packageNone'))}</p>`;
      } else {
        body = `<div class="dv-pkg-grid">${list.map(pkg => {
          const isCurrent = dev.appId && dev.appId.toLowerCase() === (pkg.appId || '').toLowerCase();
          return `
            <div class="dv-pkg-item${isCurrent ? ' is-current' : ''}">
              <div class="dv-pkg-info">
                <div class="dv-pkg-name">${esc(pkg.name || pkg.appId)}</div>
                <div class="dv-pkg-id">${esc(pkg.appId)}</div>
                <div class="dv-device-badges">
                  ${pkg.version ? `<span class="dv-pill">${esc(pkg.version)}</span>` : ''}
                  ${pkg.hasCache ? `<span class="dv-pill ok">${esc(tr('devices.packageCache'))}</span>` : ''}
                  ${isCurrent ? `<span class="dv-pill accent">${esc(tr('devices.packageCurrent'))}</span>` : ''}
                </div>
              </div>
              <button type="button" class="v2-btn ${isCurrent ? 'ghost' : 'line'}"
                data-dev-use-pkg="${esc(pkg.appId)}" ${isCurrent || state.busy ? 'disabled' : ''}>
                ${esc(isCurrent ? tr('devices.packageCurrent') : tr('devices.packageSet'))}
              </button>
            </div>`;
        }).join('')}</div>`;
      }
    }

    return `
      <div class="v2-card dv-card">
        <div class="dv-panel-toolbar">
          <h2 class="dv-card-title">${icon('apps')}${esc(tr('devices.packageCard'))}</h2>
          <div class="dv-panel-toolbar-right">
            <button type="button" class="v2-btn line" data-dev-scan-pkg ${state.busy || offline ? 'disabled' : ''}>
              ${icon('radar')}<span>${esc(tr('devices.packageScan'))}</span>
            </button>
          </div>
        </div>
        <form class="dv-manual-form" data-dev-manual-pkg-form>
          <div class="dv-manual-field">
            <label class="dv-manual-label" for="dvManualAppInput">${esc(tr('devices.manualPackageLabel'))}</label>
            <input id="dvManualAppInput" class="dv-input" type="text"
              autocomplete="off" autocorrect="off" autocapitalize="none" spellcheck="false"
              placeholder="com.feimo.astralparty" value="${esc(dev.appId || '')}">
            <span class="dv-manual-hint">${esc(tr('devices.manualPackageHint'))}</span>
          </div>
          <button type="submit" class="v2-btn line" ${state.busy ? 'disabled' : ''}>
            ${icon('check')}<span>${esc(tr('devices.manualPackageApply'))}</span>
          </button>
        </form>
        ${body}
      </div>`;
  }

  function renderModsPanel(dev) {
    const offline = !dev.online;
    const mods = state.modsTargetId === dev.id ? state.mods : null;

    let body = `<p class="dv-muted">${esc(tr('devices.modsHint'))}</p>`;
    if (Array.isArray(mods)) {
      if (!mods.length) {
        body = `<p class="dv-muted">${esc(tr('devices.modsEmpty'))}</p>`;
      } else {
        body = `<div class="dv-list">${mods.map(mod => `
          <div class="dv-item-row${mod.enabled ? '' : ' is-disabled'}">
            <div class="dv-item-main">
              <div class="dv-item-title">
                <span>${esc(mod.name || mod.id)}</span>
                ${mod.version ? `<span class="dv-pill">v${esc(mod.version)}</span>` : ''}
                ${mod.sdkVersion ? `<span class="dv-pill">SDK ${esc(mod.sdkVersion)}</span>` : ''}
                <span class="dv-pill ${mod.enabled ? 'ok' : ''}">${esc(mod.enabled ? tr('devices.modEnable') : tr('devices.modDisable'))}</span>
              </div>
              ${mod.description ? `<div class="dv-item-desc">${esc(mod.description)}</div>` : ''}
              <div class="dv-item-sub">${esc(mod.id)}${mod.author ? ' · ' + esc(mod.author) : ''}</div>
            </div>
            <div class="dv-item-actions">
              <button type="button" class="v2-btn ghost" data-dev-mod-config="${esc(mod.id)}"
                ${state.busy || offline ? 'disabled' : ''}>
                ${icon('tune')}<span>${esc(tr('devices.modConfig'))}</span>
              </button>
              <button type="button" class="v2-btn ${mod.enabled ? 'line' : 'primary'}"
                data-dev-mod-toggle="${esc(mod.id)}" data-dev-mod-next="${mod.enabled ? 'false' : 'true'}"
                ${state.busy || offline ? 'disabled' : ''}>
                ${esc(mod.enabled ? tr('devices.modDisable') : tr('devices.modEnable'))}
              </button>
              <button type="button" class="v2-btn danger dv-danger-btn"
                data-dev-mod-del="${esc(mod.id)}" data-dev-mod-name="${esc(mod.name || mod.id)}"
                ${state.busy || offline ? 'disabled' : ''}>
                ${icon('delete')}<span>${esc(tr('devices.modDelete'))}</span>
              </button>
            </div>
          </div>`).join('')}</div>`;
      }
    }

    return `
      <div class="v2-card dv-card">
        <div class="dv-panel-toolbar">
          <h2 class="dv-card-title">${icon('extension')}${esc(tr('devices.modsCard'))}</h2>
          <div class="dv-panel-toolbar-right">
            <button type="button" class="v2-btn ghost" data-dev-market ${offline ? 'disabled' : ''}>
              ${icon('storefront')}<span>${esc(tr('devices.modMarket'))}</span>
            </button>
            <button type="button" class="v2-btn line" data-dev-mod-import ${state.busy || offline ? 'disabled' : ''}>
              ${icon('upload')}<span>${esc(tr('devices.modImport'))}</span>
            </button>
            <button type="button" class="v2-btn line" data-dev-mods-load ${state.busy || offline ? 'disabled' : ''}>
              ${icon('refresh')}<span>${esc(tr('devices.modsLoad'))}</span>
            </button>
          </div>
        </div>
        ${body}
      </div>`;
  }

  function renderSkinsPanel(dev) {
    const offline = !dev.online;
    const skins = state.skinsTargetId === dev.id ? state.skins : null;
    const warnings = [];
    if (skins && skins.cardSkinModPresent === false) {
      warnings.push(`<div class="dv-alert warn">${icon('warning')}<div class="dv-alert-content"><span>${esc(tr('devices.skinsModMissing'))}</span></div></div>`);
    }
    if (skins && skins.hasDefaultPool === false) {
      warnings.push(`<div class="dv-alert">${icon('info')}<div class="dv-alert-content"><span>${esc(tr('devices.skinsNoDefaultPool'))}</span></div></div>`);
    }

    let body = `<p class="dv-muted">${esc(tr('devices.skinsHint'))}</p>`;
    if (skins) {
      const profiles = skins.profiles || [];
      const activeName = (skins.activeSkin || '').toLowerCase();
      const usingDefault = !activeName || activeName === '__game_default__';
      const defaultCard = `
        <div class="dv-item-row">
          <div class="dv-item-main">
            <div class="dv-item-title">
              <span>${esc(tr('devices.skinGameDefault'))}</span>
              ${usingDefault ? `<span class="dv-pill ok">${esc(tr('devices.skinActive'))}</span>` : ''}
            </div>
            <div class="dv-item-desc">${esc(tr('devices.skinGameDefaultHint'))}</div>
          </div>
          <div class="dv-item-actions">
            <button type="button" class="v2-btn line" data-dev-skin-set="__game_default__"
              ${usingDefault || state.busy || offline ? 'disabled' : ''}>
              ${esc(usingDefault ? tr('devices.skinActive') : tr('devices.skinSetDefault'))}
            </button>
          </div>
        </div>`;

      const profileRows = profiles.map(p => {
        const isActive = !!p.active || (!!activeName && (p.name || '').toLowerCase() === activeName);
        return `
          <div class="dv-item-row">
            <div class="dv-item-main">
              <div class="dv-item-title">
                <span>${esc(p.displayName || p.name)}</span>
                <span class="dv-pill">${p.cardCount ?? 0} ${esc(tr('devices.skinCards'))}</span>
                ${isActive ? `<span class="dv-pill ok">${esc(tr('devices.skinActive'))}</span>` : ''}
              </div>
              ${p.name && p.displayName && p.name !== p.displayName ? `<div class="dv-item-sub">${esc(p.name)}</div>` : ''}
            </div>
            <div class="dv-item-actions">
              <button type="button" class="v2-btn ${isActive ? 'ghost' : 'line'}"
                data-dev-skin-set="${esc(p.name)}" ${isActive || state.busy || offline ? 'disabled' : ''}>
                ${esc(isActive ? tr('devices.skinActive') : tr('devices.skinSet'))}
              </button>
              <button type="button" class="v2-btn danger dv-danger-btn"
                data-dev-skin-del="${esc(p.name)}" ${state.busy || offline ? 'disabled' : ''}>
                ${icon('delete')}<span>${esc(tr('devices.skinDelete'))}</span>
              </button>
            </div>
          </div>`;
      }).join('');

      body = `<div class="dv-list">${defaultCard}${profiles.length ? profileRows : `<p class="dv-muted">${esc(tr('devices.skinsEmpty'))}</p>`}</div>`;
    }

    return `
      <div class="v2-card dv-card">
        <div class="dv-panel-toolbar">
          <h2 class="dv-card-title">${icon('style')}${esc(tr('devices.skinsCard'))}</h2>
          <div class="dv-panel-toolbar-right">
            <button type="button" class="v2-btn line" data-dev-skin-import ${state.busy || offline ? 'disabled' : ''}>
              ${icon('upload')}<span>${esc(tr('devices.skinsImport'))}</span>
            </button>
            <button type="button" class="v2-btn line" data-dev-skins-load ${state.busy || offline ? 'disabled' : ''}>
              ${icon('refresh')}<span>${esc(tr('devices.skinsLoad'))}</span>
            </button>
          </div>
        </div>
        ${warnings.join('')}
        ${body}
      </div>`;
  }

  function formatLogEntryHtml(entry) {
    const lvl = String(entry.level || 'INFO').toUpperCase();
    const cls = lvl === 'ERROR' ? 'is-error' : lvl === 'WARN' ? 'is-warn' : lvl === 'FILE' ? 'is-file' : '';
    const rawTime = String(entry.time || '');
    const time = /^\d{2}:\d{2}:\d{2}/.test(rawTime)
      ? rawTime
      : (rawTime ? new Date(rawTime).toLocaleTimeString() : '');
    return `<div class="dv-log-line ${cls}"><span class="dv-log-time">${esc(time)}</span><span class="dv-log-lvl">[${esc(lvl)}]</span><span class="dv-log-msg">${esc(entry.message || '')}</span></div>`;
  }

  function renderLogEntries() {
    const lines = state.logs.slice(-300);
    if (!lines.length) {
      return `<div class="dv-log" data-dev-log-list role="log" aria-live="off"><p class="dv-muted">${esc(tr('devices.logEmpty'))}</p></div>`;
    }
    return `<div class="dv-log" data-dev-log-list role="log" aria-live="off">${lines.map(formatLogEntryHtml).join('')}</div>`;
  }

  function renderLogsPanel() {
    return `
      <div class="v2-card dv-card">
        <div class="dv-panel-toolbar">
          <h2 class="dv-card-title">${icon('terminal')}${esc(tr('devices.logCard'))}</h2>
          <div class="dv-panel-toolbar-right">
            ${state.logPath ? `<span class="dv-payload-path" title="${esc(state.logPath)}">${esc(state.logPath)}</span>` : ''}
            <button type="button" class="v2-btn ghost" data-dev-log-open>${icon('folder_open')}<span>${esc(tr('devices.logOpen'))}</span></button>
            <button type="button" class="v2-btn ghost" data-dev-log-clear ${state.logs.length ? '' : 'disabled'}><span>${esc(tr('devices.logClear'))}</span></button>
          </div>
        </div>
        ${renderLogEntries()}
      </div>`;
  }

  function renderSideLogDock() {
    return `
      <aside class="dv-side-log" aria-label="${esc(tr('devices.logCard'))}">
        <div class="v2-card dv-card dv-side-log-card">
          <div class="dv-panel-toolbar">
            <h2 class="dv-card-title">${icon('terminal')}${esc(tr('devices.logCard'))}</h2>
            <div class="dv-panel-toolbar-right">
              <button type="button" class="v2-btn ghost" data-dev-log-open title="${esc(state.logPath || '')}">${icon('folder_open')}</button>
              <button type="button" class="v2-btn ghost" data-dev-log-clear ${state.logs.length ? '' : 'disabled'}>${esc(tr('devices.logClear'))}</button>
            </div>
          </div>
          ${renderLogEntries()}
        </div>
      </aside>`;
  }

  function renderTabPanel(dev) {
    if (!dev) return '';
    const curTab = activeTab();
    return `
      <div class="dv-tabpanel" role="tabpanel" aria-labelledby="dvTab-${curTab}">
        ${curTab === 'overview' ? renderOverviewPanel(dev)
          : curTab === 'packages' ? renderPackagesPanel(dev)
          : curTab === 'mods' ? renderModsPanel(dev)
          : curTab === 'skins' ? renderSkinsPanel(dev)
          : curTab === 'files' ? Files.renderPanel(dev)
          : renderLogsPanel()}
      </div>`;
  }

  function render() {
    const el = root();
    if (!el) return;
    const dev = currentTarget();
    el.innerHTML = `
      <div class="v2-page dv-page">
        ${renderHead()}
        ${renderEnvironmentCard()}
        <div class="dv-workbench">
          <div class="dv-main-col">
            ${renderDeviceStrip()}
            ${dev ? renderHeroAndActions(dev) : ''}
            ${dev ? renderTabs(dev) : ''}
            ${dev ? renderTabPanel(dev) : ''}
          </div>
          ${renderSideLogDock()}
        </div>
        ${renderCornerHudHtml()}
      </div>`;

    el.querySelectorAll('[data-dev-log-list]').forEach(box => {
      box.scrollTop = box.scrollHeight;
    });
    if (dev && activeTab() === 'files') {
      Files.afterRender(dev, render);
    }
  }

  Object.assign(D, { startBusy, clearBusy, render });

  function onHostMessage(msg) {
    if (!msg?.type) return;
    if (Files.onMessage(msg, render)) return;
    switch (msg.type) {
      case 'devices': {
        const p = msg.payload || {};
        state.targets = p.targets || [];
        const activeObj = p.active || {};
        state.activeId = p.activeTargetId
          || (activeObj.kind === 'mobile' ? activeObj.targetId : (activeObj.profileId ? 'local:' + activeObj.profileId : ''));
        state.logPath = p.logPath || '';
        if (Array.isArray(p.logLines)) state.logs = p.logLines;
        state.payloadPath = p.payloadPath || '';
        state.payloadPresent = !!p.payloadPresent;
        state.payloadVersion = p.payloadVersion || '';
        state.payloadSdkVersion = p.payloadSdkVersion || '';
        state.payloadFileCount = p.payloadFileCount || 0;
        syncSelection(state.targets);

        if (state.activeId.startsWith('local:') || !state.activeId) {
          window.TargetGuard?.set({ kind: 'local' });
        } else {
          const act = state.targets.find(t => t.id === state.activeId);
          if (act) window.TargetGuard?.set({ kind: 'mobile', deviceLabel: act.label, online: act.online });
        }
        render();
        break;
      }
      case 'deviceActive': {
        const targetId = msg.payload?.targetId || '';
        state.activeId = targetId;
        if (targetId) state.selectedId = targetId;
        render();
        break;
      }
      case 'targetChanged': {
        const p = msg.payload || {};
        state.activeId = p.kind === 'mobile' ? (p.targetId || '') : '';
        window.TargetGuard?.set(p);
        render();
        break;
      }
      case 'deviceDetectProgress': {
        const p = msg.payload || {};
        const wasBusy = !!state.busy;
        const nextBusy = !!p.active;
        state.progress = p;
        state.busy = nextBusy;
        if (!nextBusy) state.pullingPayload = false;
        if (wasBusy && nextBusy && syncProgressInPlace()) break;
        render();
        break;
      }
      case 'deviceDetectReport': {
        const p = msg.payload;
        if (p) {
          state.report = p;
          state.reportTargetId = p.targetId || state.selectedId;
          render();
        }
        break;
      }
      case 'deviceOperationResult': {
        const p = msg.payload;
        if (p) {
          state.opResult = p;
          const summary = String(p.message || '').split(/\r?\n/)[0];
          const shortMsg = summary.length > 120 ? summary.slice(0, 120) + '…' : summary;
          showCornerToast(p.ok, shortMsg || (p.ok ? tr('devices.lastResultOk') : tr('devices.lastResultFail')), p.detail || '');
          render();
        }
        break;
      }
      case 'devicePullResult': {
        const p = msg.payload || {};
        state.pullingPayload = false;
        if (p.ok) showCornerToast(true, `${tr('devices.pullPayload')}: ${p.tag || p.version}`);
        render();
        break;
      }
      case 'devicePackages': {
        state.packagesTargetId = msg.payload?.targetId || state.selectedId;
        state.packages = msg.payload?.packages || [];
        render();
        break;
      }
      case 'deviceMods': {
        state.modsTargetId = msg.payload?.targetId || state.selectedId;
        state.mods = msg.payload?.mods || [];
        render();
        break;
      }
      case 'deviceModConfig': {
        openModConfigModal(msg.payload, startBusy);
        break;
      }
      case 'deviceSkins': {
        state.skinsTargetId = msg.payload?.targetId || state.selectedId;
        state.skins = msg.payload || null;
        render();
        break;
      }
      case 'deviceEnvReport': {
        state.env = msg.payload || null;
        render();
        break;
      }
      case 'deviceAdbScanResult': {
        const p = msg.payload || {};
        showCornerToast(!!p.found, p.message || '');
        if (!p.found) {
          state.envExpanded = true;
          render();
        }
        break;
      }
      case 'deviceLog': {
        if (msg.payload) {
          state.logs.push(msg.payload);
          if (state.logs.length > 400) state.logs.splice(0, state.logs.length - 400);
          const el = root();
          if (el) {
            const countBadge = el.querySelector('[data-dev-log-count]');
            if (countBadge) {
              countBadge.textContent = String(state.logs.length);
              countBadge.hidden = false;
            }
            const html = formatLogEntryHtml(msg.payload);
            el.querySelectorAll('[data-dev-log-list]').forEach(box => {
              box.querySelector('.dv-muted')?.remove();
              box.insertAdjacentHTML('beforeend', html);
              while (box.children.length > 300) box.firstElementChild?.remove();
              box.scrollTop = box.scrollHeight;
            });
            el.querySelectorAll('[data-dev-log-clear]').forEach(btn => { btn.disabled = false; });
          }
        }
        break;
      }
      case 'error': {
        if (state.busy || state.pullingPayload || state.progress?.active) clearBusy();
        if (msg.message) showCornerToast(false, msg.message);
        break;
      }
    }
  }

  document.addEventListener('click', event => {
    const view = root();
    if (!view || !view.contains(event.target)) return;

    if (event.target.closest?.('[data-dev-toast-close]')) {
      dismissCornerToast();
      return;
    }

    const dev = currentTarget();
    if (activeTab() === 'files' && Files.handleClick(event.target, dev, render)) return;

    const el = event.target.closest('button');
    if (!el || el.disabled) return;

    if (el.dataset.devSelect) { onDeviceSwitch(el.dataset.devSelect); return; }
    if (el.dataset.devTab) {
      state.tab = el.dataset.devTab;
      if (state.tab === 'files' && dev?.online && !state.busy && state.filesTargetId !== dev.id) {
        Files.navigateTo(dev, state.filesPath || '', false);
        return;
      }
      render();
      return;
    }
    if (el.hasAttribute('data-dev-show-notice')) {
      openExperimentalNotice();
      return;
    }
    if (el.hasAttribute('data-dev-refresh')) { startBusy(tr('devices.busyDetect')); post({ type: 'devicesRefresh' }); return; }
    if (el.hasAttribute('data-dev-check-env') && !state.busy) {
      startBusy(tr('devices.envChecking'));
      post({ type: 'deviceCheckEnv' });
      return;
    }
    if (el.hasAttribute('data-dev-env-toggle')) {
      state.envExpanded = !state.envExpanded;
      render();
      return;
    }
    if (el.hasAttribute('data-dev-scan-adb') && !state.busy) {
      startBusy(tr('devices.envScanAdb'));
      post({ type: 'deviceScanAdb' });
      return;
    }
    if (el.hasAttribute('data-dev-browse-adb') && !state.busy) {
      startBusy(tr('devices.busyWrite'));
      post({ type: 'deviceBrowseAdb' });
      return;
    }
    if (el.hasAttribute('data-dev-clear-adb') && !state.busy) {
      startBusy(tr('devices.busyWrite'));
      post({ type: 'deviceClearAdbPath' });
      return;
    }
    if (el.hasAttribute('data-dev-pull') && !state.busy) {
      state.pullingPayload = true;
      startBusy(tr('devices.pulling'));
      post({ type: 'devicePullPayload' });
      return;
    }
    if (el.dataset.devCopySerial) {
      navigator.clipboard?.writeText(el.dataset.devCopySerial).then(() => showCornerToast(true, tr('devices.copySerial')));
      return;
    }
    if (el.dataset.devActivate) { post({ type: 'deviceSetActive', targetId: el.dataset.devActivate }); return; }
    if (el.dataset.devDetect && !state.busy) {
      state.selectedId = el.dataset.devDetect;
      startBusy(tr('devices.busyDetect'));
      post({ type: 'deviceDetect', targetId: el.dataset.devDetect });
      return;
    }
    if (el.dataset.devInstall && !state.busy) {
      startBusy(tr('devices.busyWrite'));
      post({ type: 'deviceInstall', targetId: el.dataset.devInstall, payloadPath: state.payloadPath });
      return;
    }
    if (el.dataset.devDeploy && !state.busy) {
      startBusy(tr('devices.busyWrite'));
      post({ type: 'deviceDeployPayload', targetId: el.dataset.devDeploy, payloadPath: state.payloadPath });
      return;
    }
    if (el.dataset.devPatch && !state.busy) {
      const targetId = el.dataset.devPatch;
      confirmAction({
        title: tr('devices.patch'),
        message: tr('devices.patchConfirm'),
        okLabel: tr('devices.patch'),
        danger: false,
        onConfirm: () => { startBusy(tr('devices.busyWrite')); post({ type: 'devicePatch', targetId }); },
      });
      return;
    }
    if ((el.dataset.devRestore || el.dataset.devUninstall) && !state.busy) {
      const isUninstall = !!el.dataset.devUninstall;
      const targetId = el.dataset.devUninstall || el.dataset.devRestore;
      confirmAction({
        title: isUninstall ? tr('devices.uninstall') : tr('devices.restore'),
        message: isUninstall ? tr('devices.uninstallConfirm') : tr('devices.restoreConfirm'),
        okLabel: isUninstall ? tr('devices.uninstall') : tr('devices.restore'),
        danger: isUninstall,
        onConfirm: () => { startBusy(tr('devices.busyWrite')); post({ type: 'deviceRestore', targetId, removeLoader: isUninstall }); },
      });
      return;
    }
    if (el.hasAttribute('data-dev-scan-pkg') && dev && !state.busy) {
      startBusy(tr('devices.busyDetect'));
      post({ type: 'deviceScanPackages', targetId: dev.id });
      return;
    }
    if (el.dataset.devUsePkg && dev) { post({ type: 'deviceSetApp', targetId: dev.id, appId: el.dataset.devUsePkg }); return; }
    if (el.hasAttribute('data-dev-mods-load') && dev && !state.busy) {
      startBusy(tr('devices.busyDetect'));
      post({ type: 'deviceMods', targetId: dev.id });
      return;
    }
    if (el.hasAttribute('data-dev-mod-import') && dev && !state.busy) {
      startBusy(tr('devices.busyWrite'));
      post({ type: 'deviceImportMod', targetId: dev.id });
      return;
    }
    if (el.hasAttribute('data-dev-market') && dev) {
      post({ type: 'deviceSetActive', targetId: dev.id });
      window.showPage?.('workshop');
      return;
    }
    if (el.dataset.devModToggle && dev && !state.busy) {
      startBusy(tr('devices.busyWrite'));
      post({ type: 'deviceSetMod', targetId: dev.id, modId: el.dataset.devModToggle, enabled: el.dataset.devModNext === 'true' });
      return;
    }
    if (el.dataset.devModConfig && dev && !state.busy) {
      startBusy(tr('devices.busyDetect'));
      post({ type: 'deviceModConfig', targetId: dev.id, modId: el.dataset.devModConfig, language: window.i18n?.lang || 'zh-Hans' });
      return;
    }
    if (el.dataset.devModDel && dev && !state.busy) {
      const modId = el.dataset.devModDel;
      confirmAction({
        title: tr('devices.modDelete'),
        message: tr('devices.modDeleteConfirm', { name: el.dataset.devModName || modId }),
        okLabel: tr('devices.modDelete'),
        danger: true,
        onConfirm: () => { startBusy(tr('devices.busyWrite')); post({ type: 'deviceDeleteMod', targetId: dev.id, modId }); },
      });
      return;
    }
    if (el.hasAttribute('data-dev-skins-load') && dev && !state.busy) {
      startBusy(tr('devices.busyDetect'));
      post({ type: 'deviceSkins', targetId: dev.id });
      return;
    }
    if (el.hasAttribute('data-dev-skin-import') && dev && !state.busy) {
      startBusy(tr('devices.busyWrite'));
      post({ type: 'deviceImportSkin', targetId: dev.id });
      return;
    }
    if (el.dataset.devSkinSet && dev && !state.busy) {
      startBusy(tr('devices.busyWrite'));
      post({ type: 'deviceSetSkin', targetId: dev.id, profile: el.dataset.devSkinSet });
      return;
    }
    if (el.dataset.devSkinDel && dev && !state.busy) {
      const profile = el.dataset.devSkinDel;
      confirmAction({
        title: tr('devices.skinDelete'),
        message: tr('devices.skinDeleteConfirm', { name: profile }),
        okLabel: tr('devices.skinDelete'),
        danger: true,
        onConfirm: () => { startBusy(tr('devices.busyWrite')); post({ type: 'deviceDeleteSkin', targetId: dev.id, profile }); },
      });
      return;
    }
    if (el.hasAttribute('data-dev-log-open')) { post({ type: 'openLogsFolder' }); return; }
    if (el.hasAttribute('data-dev-log-clear')) { state.logs = []; render(); }
  });

  document.addEventListener('dblclick', event => {
    const view = root();
    if (!view || !view.contains(event.target)) return;
    Files.handleDblClick(event.target, currentTarget());
  });

  document.addEventListener('change', event => {
    const view = root();
    if (!view || !view.contains(event.target)) return;
    Files.handleChange(event.target, currentTarget());
  });

  document.addEventListener('submit', event => {
    const adbForm = event.target.closest?.('[data-dev-adb-form]');
    if (adbForm) {
      event.preventDefault();
      if (state.busy) return;
      const path = document.getElementById('dvAdbPathInput')?.value?.trim() || '';
      startBusy(tr('devices.busyWrite'));
      post({ type: 'deviceSetAdbPath', path });
      return;
    }
    const form = event.target.closest?.('[data-dev-manual-pkg-form]');
    if (!form) return;
    event.preventDefault();
    const dev = currentTarget();
    const appId = document.getElementById('dvManualAppInput')?.value?.trim();
    if (!dev || !appId) return;
    post({ type: 'deviceSetApp', targetId: dev.id, appId });
    showCornerToast(true, `${tr('devices.bundleId')}: ${appId}`);
  });

  document.addEventListener('keydown', event => {
    const view = root();
    if (!view || !view.contains(event.target)) return;
    const tab = event.target.closest?.('[data-dev-tab]');
    if (tab) {
      const tabs = [...view.querySelectorAll('[data-dev-tab]')].filter(t => t.offsetParent !== null);
      const idx = tabs.indexOf(tab);
      if (idx >= 0) {
        let next = -1;
        if (event.key === 'ArrowRight') next = (idx + 1) % tabs.length;
        else if (event.key === 'ArrowLeft') next = (idx - 1 + tabs.length) % tabs.length;
        else if (event.key === 'Home') next = 0;
        else if (event.key === 'End') next = tabs.length - 1;
        if (next >= 0) {
          event.preventDefault();
          state.tab = tabs[next].dataset.devTab;
          const dev = currentTarget();
          if (state.tab === 'files' && dev?.online && !state.busy && state.filesTargetId !== dev.id) {
            Files.navigateTo(dev, state.filesPath || '', false);
          } else {
            render();
          }
          root()?.querySelector(`[data-dev-tab="${state.tab}"]`)?.focus();
          return;
        }
      }
    }
    Files.handleKeyDown(event, currentTarget(), render);
  });

  window.chrome?.webview?.addEventListener('message', event => onHostMessage(event.data));

  /* ── §8.4 Global Target Guard (v2 UI selectors & safe state restore) ── */
  const TARGET = { kind: 'local', label: '', online: false };
  const UNSUPPORTED_SELECTORS = [
    ['[data-page="profiles"]', 'devices.unsupportedProfiles'],
    ['[data-v2="launch"]', 'devices.unsupportedLaunch'],
    ['[data-v2="launchMenu"]', 'devices.unsupportedLaunch'],
    ['[data-v2-launch]', 'devices.unsupportedLaunch'],
    ['#v2HomeProfile', 'devices.unsupportedModProfiles'],
    ['#v2mpSelect', 'devices.unsupportedModProfiles'],
    ['#v2mProfiles [data-v2mp]', 'devices.unsupportedModProfiles'],
    ['[data-v2s-tab="audio"]', 'devices.unsupportedAudio'],
    ['[data-v2s-tab="resource"]', 'devices.unsupportedResources'],
    ['[data-v2s-tab="fonts"]', 'devices.unsupportedFonts'],
    ['[data-v2s-tab="homebg"]', 'devices.unsupportedHomeBg'],
    ['[data-v2s-tab="characters"]', 'devices.unsupportedCharacter'],
  ];

  const targetIsMobile = () => TARGET.kind === 'mobile';
  let applyingGuard = false;
  let guardRaf = 0;

  function switchBackToLocal() {
    const localTarget = (AppState.gameProfiles?.targets || []).find(t => t.kind === 'local');
    if (localTarget) post({ type: 'targetSetActive', id: localTarget.id });
    else window.showPage?.('settings');
  }

  function renderNoticeStrip() {
    let strip = document.getElementById('targetNotice');
    if (!targetIsMobile()) {
      strip?.remove();
      return;
    }
    if (!strip) {
      strip = document.createElement('div');
      strip.id = 'targetNotice';
      strip.className = 'dv-target-notice';
      strip.setAttribute('role', 'status');
      strip.addEventListener('click', e => { if (e.target.closest('[data-tg-reset]')) switchBackToLocal(); });
      const header = document.querySelector('.global-header');
      if (!header?.parentNode) return;
      header.parentNode.insertBefore(strip, header.nextSibling);
    }
    const label = TARGET.label || tr('devices.title');
    strip.innerHTML = `
      <div class="dv-target-notice-body">
        ${icon('smartphone')}
        <span><b>${esc(tr('devices.targetDevice', { name: label }))}</b>${TARGET.online ? '' : ' · ' + esc(tr('devices.offline'))} — ${esc(tr('devices.unsupportedWhy'))}</span>
      </div>
      <div class="dv-target-notice-actions">
        <button type="button" class="v2-btn line dv-target-notice-btn" data-tg-reset>${esc(tr('devices.switchToLocal'))}</button>
      </div>`;
  }

  function blockElement(el, reasonText, mobile) {
    if (mobile) {
      if (el.dataset.tgLocked !== '1') {
        el.dataset.tgLocked = '1';
        el.dataset.tgPrevDisabled = el.disabled ? '1' : '0';
        el.dataset.tgPrevTitle = el.getAttribute('title') || '';
      }
      if (!el.disabled) el.disabled = true;
      el.setAttribute('aria-disabled', 'true');
      if (el.getAttribute('title') !== reasonText) el.setAttribute('title', reasonText);
      el.classList.add('is-target-blocked');
    } else if (el.dataset.tgLocked === '1') {
      const wasDisabled = el.dataset.tgPrevDisabled === '1';
      const prevTitle = el.dataset.tgPrevTitle || '';
      el.disabled = wasDisabled;
      if (wasDisabled) el.setAttribute('aria-disabled', 'true');
      else el.removeAttribute('aria-disabled');
      if (prevTitle) el.setAttribute('title', prevTitle);
      else el.removeAttribute('title');
      el.classList.remove('is-target-blocked');
      delete el.dataset.tgLocked;
      delete el.dataset.tgPrevDisabled;
      delete el.dataset.tgPrevTitle;
    }
  }

  function applyTargetGuard() {
    if (applyingGuard) return;
    applyingGuard = true;
    try {
      const mobile = targetIsMobile();
      for (const [selector, key] of UNSUPPORTED_SELECTORS) {
        const text = tr(key);
        document.querySelectorAll(selector).forEach(el => blockElement(el, text, mobile));
      }
      document.querySelectorAll('[data-target-unsupported]').forEach(el => {
        blockElement(el, tr(el.dataset.targetUnsupported || 'devices.unsupportedWhy'), mobile);
      });
      renderNoticeStrip();
    } finally {
      applyingGuard = false;
    }
  }

  const scheduleGuardApply = () => {
    if (!targetIsMobile() || guardRaf) return;
    guardRaf = requestAnimationFrame(() => { guardRaf = 0; applyTargetGuard(); });
  };

  window.TargetGuard = {
    apply: applyTargetGuard,
    isMobile: targetIsMobile,
    get: () => ({ ...TARGET }),
    set(next) {
      TARGET.kind = next?.kind === 'mobile' ? 'mobile' : 'local';
      TARGET.label = next?.deviceLabel || '';
      TARGET.online = !!next?.online;
      applyTargetGuard();
    },
  };

  new MutationObserver(() => {
    if (!applyingGuard && targetIsMobile()) scheduleGuardApply();
  }).observe(document.body, { childList: true, subtree: true });

  window.addEventListener('appLanguageChanged', () => { render(); applyTargetGuard(); });
  window.matchMedia?.('(min-width: 1200px)').addEventListener?.('change', () => render());

  const EXPERIMENTAL_NOTICE_KEY = 'devices.experimentalNoticeSeen.v1';

  function hasSeenExperimentalNotice() {
    try { return window.localStorage?.getItem(EXPERIMENTAL_NOTICE_KEY) === '1'; }
    catch { return false; }
  }

  function markExperimentalNoticeSeen() {
    try { window.localStorage?.setItem(EXPERIMENTAL_NOTICE_KEY, '1'); }
    catch { /* ignore storage errors */ }
  }

  function ensureExperimentalDialog() {
    let dlg = document.getElementById('dvExperimentalDialog');
    if (!dlg) {
      dlg = document.createElement('dialog');
      dlg.id = 'dvExperimentalDialog';
      dlg.className = 'dv-experimental-dialog';
      dlg.setAttribute('aria-labelledby', 'dvExperimentalTitle');
      dlg.addEventListener('close', () => {
        dlg.returnValue = '';
        markExperimentalNoticeSeen();
      });
      document.body.appendChild(dlg);
    }
    return dlg;
  }

  function openExperimentalNotice() {
    const dlg = ensureExperimentalDialog();
    dlg.innerHTML = `
      <form method="dialog" class="dv-experimental-form">
        <div class="dv-experimental-head">
          <div class="dv-experimental-heading">
            ${icon('warning')}
            <h1 id="dvExperimentalTitle" class="dv-experimental-title">${esc(tr('devices.experimentalTitle'))}</h1>
          </div>
          <button type="submit" value="close" class="v2-icon-btn dv-experimental-close" aria-label="${esc(tr('common.close'))}">
            ${window.V2UI?.ICON?.x || '×'}
          </button>
        </div>
        <div class="dv-experimental-body">
          <p class="dv-experimental-warn">${esc(tr('devices.experimentalWarn'))}</p>
          <p class="dv-experimental-lead">${esc(tr('devices.experimentalLead'))}</p>
          <ul class="dv-experimental-points">
            <li>${esc(tr('devices.experimentalPurpose'))}</li>
            <li>${esc(tr('devices.experimentalDetails'))}</li>
            <li>${esc(tr('devices.experimentalDisclaimer'))}</li>
          </ul>
          <div class="dv-experimental-foot">
            <button type="submit" value="ack" class="v2-btn primary" autofocus>${esc(tr('devices.experimentalAck'))}</button>
          </div>
        </div>
      </form>`;
    if (!dlg.open) {
      if (typeof dlg.showModal === 'function') dlg.showModal();
      else dlg.setAttribute('open', '');
    }
  }

  window.V2Devices = {
    onShow() {
      render();
      post({ type: 'devicesGet' });
      applyTargetGuard();
      if (!hasSeenExperimentalNotice()) openExperimentalNotice();
    },
    render,
    clearBusy,
    applyTargetGuard,
  };
})();
