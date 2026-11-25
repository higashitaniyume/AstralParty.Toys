// v2/changelog.js — 统一更新日志弹窗（工具箱 + 加载器完整渲染、更新前/更新后自动触发与永久关闭）
(function () {
  'use strict';

  const U = () => window.V2UI;
  const HOST_AVAILABLE = Boolean(window.chrome?.webview);

  const S = {
    open: false,
    activeTab: 'app',
    data: null,
    queue: [],
    refreshing: false,
    observerInstalled: false,
  };

  function isOtherModalBusy() {
    if (S.open && !$('v2ChangelogBody')) S.open = false;
    const v2 = $('v2Modal');
    const global = $('globalModal');
    const v2Busy = Boolean(v2?.classList.contains('show') && !S.open);
    const globalBusy = Boolean(global?.classList.contains('open'));
    const dialogBusy = Boolean(document.querySelector('dialog[open]'));
    return v2Busy || globalBusy || dialogBusy;
  }

  function installModalObserver() {
    if (S.observerInstalled) return;
    S.observerInstalled = true;
    const drain = () => {
      if (!S.queue.length || S.open || isOtherModalBusy()) return;
      const next = S.queue.shift();
      if (next) showNow(next);
    };
    const obs = new MutationObserver(() => setTimeout(drain, 60));
    const v2 = $('v2Modal'), global = $('globalModal');
    if (v2) obs.observe(v2, { attributes: true, attributeFilter: ['class'] });
    if (global) obs.observe(global, { attributes: true, attributeFilter: ['class'] });
    document.addEventListener('close', () => setTimeout(drain, 60), true);
  }

  function isAutoPopupEnabled(payload) {
    if (AppState.librarySettings && typeof AppState.librarySettings.autoChangelogPopup === 'boolean') {
      return AppState.librarySettings.autoChangelogPopup;
    }
    return payload?.autoChangelogPopup !== false;
  }

  function renderMarkdown(md, emptyHint) {
    const text = String(md || '').trim();
    if (!text) return `<p class="v2-cl-empty">${esc(emptyHint || '暂无更新日志内容。')}</p>`;
    if (window.Markdown?.render) {
      return `<div class="markdown-body v2-cl-md">${window.Markdown.render(text)}</div>`;
    }
    return `<pre class="v2-cl-fallback">${esc(text)}</pre>`;
  }

  function buildTitle(d) {
    const t = d.trigger || 'manual';
    if (t === 'fresh-install') {
      return d.appCurrentVersion ? `欢迎使用 AstralParty.Toys v${d.appCurrentVersion}` : '欢迎使用 AstralParty.Toys';
    }
    if (t === 'app-updated') {
      return d.appCurrentVersion ? `工具箱已更新至 v${d.appCurrentVersion}` : '工具箱已更新';
    }
    if (t === 'app-update-available') {
      return d.appTargetVersion ? `发现工具箱新版本 v${d.appTargetVersion}` : '发现工具箱新版本';
    }
    if (t === 'loader-updated') {
      const v = d.loaderTargetVersion || d.loaderCurrentVersion;
      return v ? `加载器已更新至 v${v}` : '加载器已更新';
    }
    if (t === 'loader-update-available') {
      return d.loaderTargetVersion ? `发现加载器新版本 v${d.loaderTargetVersion}` : '发现加载器新版本';
    }
    return '更新日志';
  }

  function buildVersionSummary(d) {
    const appText = d.appTargetVersion && d.appPrevVersion && d.appTargetVersion !== d.appPrevVersion
      ? `v${d.appPrevVersion} → v${d.appTargetVersion}`
      : (d.appTargetVersion ? `目标 v${d.appTargetVersion}` : (d.appCurrentVersion ? `v${d.appCurrentVersion}` : '未知'));

    const loaderBase = d.loaderCurrentVersion || d.loaderEmbeddedVersion || '';
    let loaderText = loaderBase ? `v${loaderBase}` : '未安装';
    if (d.loaderTargetVersion && d.loaderPrevVersion && d.loaderTargetVersion !== d.loaderPrevVersion) {
      loaderText = `v${d.loaderPrevVersion} → v${d.loaderTargetVersion}`;
    } else if (d.loaderTargetVersion && loaderBase && d.loaderTargetVersion !== loaderBase) {
      loaderText = `v${loaderBase} → v${d.loaderTargetVersion}`;
    } else if (d.loaderTargetVersion) {
      loaderText = `v${d.loaderTargetVersion}`;
    } else if (d.loaderCurrentVersion && d.loaderEmbeddedVersion && d.loaderCurrentVersion !== d.loaderEmbeddedVersion) {
      loaderText = `已装 v${d.loaderCurrentVersion} · 内置 v${d.loaderEmbeddedVersion}`;
    }

    const appBadge = d.trigger === 'app-update-available' ? '<span class="v2-badge pink">可更新</span>'
      : (d.trigger === 'app-updated' || d.trigger === 'fresh-install' ? '<span class="v2-badge">工具箱</span>' : '');
    const loaderBadge = d.trigger === 'loader-update-available' ? '<span class="v2-badge pink">可更新</span>'
      : (d.trigger === 'loader-updated' ? '<span class="v2-badge">已更新</span>' : '');

    return `
      <div class="v2-cl-pills">
        <span class="v2-cl-pill"><span class="k">工具箱</span><b>${esc(appText)}</b>${appBadge}</span>
        <span class="v2-cl-pill"><span class="k">加载器 CesiumLoader</span><b>${esc(loaderText)}</b>${loaderBadge}</span>
      </div>`;
  }

  function buildActionButton(d) {
    const t = d.trigger || 'manual';
    if (t === 'app-update-available') {
      const st = String(AppState.appUpdateState?.status || '').toLowerCase();
      const mode = String(AppState.appUpdateState?.updateMode || '');
      if (st === 'readytoinstall') {
        return `<button class="v2-btn primary" data-v2cl="applyAppUpdate">立即安装并重启</button>`;
      }
      if (st === 'downloading') {
        return `<button class="v2-btn line" data-v2cl="gotoAppUpdate">查看下载进度</button>`;
      }
      if (mode && mode !== 'manual') {
        return `<button class="v2-btn primary" data-v2cl="downloadAppUpdate">立即下载更新</button>`;
      }
      return `<button class="v2-btn primary" data-v2cl="gotoAppUpdate">前往更新设置</button>`;
    }
    if (t === 'loader-update-available') {
      const onDevices = AppState.currentPage === 'devices';
      return `<button class="v2-btn primary" data-v2cl="${onDevices ? 'gotoDevices' : 'gotoMods'}">${onDevices ? '去设备页更新' : '前往模组页更新'}</button>`;
    }
    return '';
  }

  function sourceText(d) {
    const src = S.activeTab === 'loader' ? d.loaderSource : d.appSource;
    if (S.refreshing) return '正在拉取 GitHub 最新更新日志…';
    if (src === 'github') return '来源：GitHub 仓库最新 CHANGELOG.md';
    if (src === 'package') return '来源：本次下载的加载器发布包';
    if (src === 'bundled') return '来源：程序内置版本（可点右侧刷新获取最新）';
    return '离线模式下暂未读取到远端日志';
  }

  function renderBody() {
    const body = $('v2ChangelogBody');
    const srcEl = $('v2ChangelogSource');
    const refreshBtn = $('v2ChangelogRefresh');
    if (!body || !S.data) return;
    const md = S.activeTab === 'loader' ? S.data.loaderMarkdown : S.data.appMarkdown;
    const empty = S.activeTab === 'loader'
      ? '暂未读取到加载器更新日志，可点击「拉取最新」尝试从 GitHub 获取。'
      : '暂未读取到工具箱更新日志。';
    body.innerHTML = renderMarkdown(md, empty);
    body.scrollTop = 0;
    if (srcEl) srcEl.textContent = sourceText(S.data);
    if (refreshBtn) refreshBtn.disabled = S.refreshing;
    document.querySelectorAll('#v2Modal .v2-cl-tab').forEach(btn => {
      const on = btn.dataset.tab === S.activeTab;
      btn.classList.toggle('is-active', on);
      btn.setAttribute('aria-selected', String(on));
    });
  }

  function buildModalHtml(d) {
    const autoOn = isAutoPopupEnabled(d);
    const appTabTag = d.trigger === 'app-update-available' ? '<span class="v2-cl-dot"></span>'
      : (d.trigger === 'app-updated' ? '<span class="v2-cl-tag">新</span>' : '');
    const loaderTabTag = d.trigger === 'loader-update-available' ? '<span class="v2-cl-dot"></span>'
      : (d.trigger === 'loader-updated' ? '<span class="v2-cl-tag">新</span>' : '');
    const actionBtn = buildActionButton(d);
    const closeLabel = d.trigger === 'app-update-available' || d.trigger === 'loader-update-available'
      ? '暂不更新'
      : (d.isAutoPopup ? '知道了' : '关闭');
    const closeCls = actionBtn ? 'line' : 'primary';

    return `
      <div class="v2-modal-head v2-cl-head">
        <div class="v2-cl-title-wrap">
          <b>${esc(buildTitle(d))}</b>
          ${buildVersionSummary(d)}
        </div>
        <button class="v2-icon-btn" data-v2cl="close" aria-label="关闭">${U().ICON.x}</button>
      </div>
      <div class="v2-cl-toolbar">
        <div class="changelog-tabs v2-cl-tabs" role="tablist">
          <button type="button" class="changelog-tab v2-cl-tab${S.activeTab === 'app' ? ' is-active' : ''}" data-tab="app" role="tab" aria-selected="${S.activeTab === 'app'}">工具箱（AstralParty.Toys）${appTabTag}</button>
          <button type="button" class="changelog-tab v2-cl-tab${S.activeTab === 'loader' ? ' is-active' : ''}" data-tab="loader" role="tab" aria-selected="${S.activeTab === 'loader'}">加载器（CesiumLoader）${loaderTabTag}</button>
        </div>
        <div class="v2-cl-meta">
          <span class="v2-cl-source" id="v2ChangelogSource">${esc(sourceText(d))}</span>
          <button type="button" class="v2-btn ghost v2-cl-refresh" id="v2ChangelogRefresh" data-v2cl="refresh" ${S.refreshing ? 'disabled' : ''}>${U().ICON.refresh}拉取最新</button>
        </div>
      </div>
      <div class="v2-cl-scroll" id="v2ChangelogBody">
        ${renderMarkdown(S.activeTab === 'loader' ? d.loaderMarkdown : d.appMarkdown)}
      </div>
      <div class="v2-modal-foot v2-cl-foot">
        ${autoOn
          ? `<button type="button" class="v2-btn ghost v2-cl-never" data-v2cl="neverAuto">以后永久不弹窗</button>`
          : `<span class="v2-muted" style="font-size:12px">已关闭自动弹窗（可在设置中开启）</span>`}
        <span class="v2-top-spacer"></span>
        ${actionBtn}
        <button type="button" class="v2-btn ${closeCls}" data-v2cl="close">${esc(closeLabel)}</button>
      </div>`;
  }

  function acknowledgeAndClose() {
    const wasOpen = S.open;
    const d = S.data;
    S.open = false;
    S.refreshing = false;
    if (wasOpen && d && (d.isAutoPopup || d.trigger === 'fresh-install' || d.trigger === 'app-updated')) {
      post({ type: 'markChangelogSeen' });
    }
    U().closeModal();
  }

  function showNow(payload) {
    S.data = { ...payload };
    S.activeTab = payload.defaultTab === 'loader' ? 'loader' : 'app';
    S.open = true;
    S.refreshing = false;
    const m = U().openModal(buildModalHtml(S.data), 'changelog-wide');
    U().setModalCancel(acknowledgeAndClose);
    m.querySelectorAll('.v2-cl-tab').forEach(btn => {
      btn.addEventListener('click', () => {
        S.activeTab = btn.dataset.tab === 'loader' ? 'loader' : 'app';
        renderBody();
      });
    });
  }

  function receiveModal(payload) {
    if (!payload) return;
    installModalObserver();
    if (S.open && !$('v2ChangelogBody')) S.open = false;

    // 若弹窗已经开着（例如启动时先后发现工具箱与加载器更新），原地合并信息而不是顶掉阅读位置
    if (S.open && S.data) {
      S.data = {
        ...S.data,
        appPrevVersion: payload.appPrevVersion || S.data.appPrevVersion,
        appTargetVersion: payload.appTargetVersion || S.data.appTargetVersion,
        appMarkdown: payload.appMarkdown || S.data.appMarkdown,
        appSource: payload.appSource || S.data.appSource,
        loaderPrevVersion: payload.loaderPrevVersion || S.data.loaderPrevVersion,
        loaderCurrentVersion: payload.loaderCurrentVersion || S.data.loaderCurrentVersion,
        loaderTargetVersion: payload.loaderTargetVersion || S.data.loaderTargetVersion,
        loaderMarkdown: payload.loaderMarkdown || S.data.loaderMarkdown,
        loaderSource: payload.loaderSource || S.data.loaderSource,
      };
      renderBody();
      return;
    }

    if (isOtherModalBusy()) {
      S.queue.push(payload);
      return;
    }

    showNow(payload);
  }

  function receiveRefresh(payload) {
    if (!payload || !S.data) return;
    S.refreshing = false;
    if (payload.appMarkdown) {
      S.data.appMarkdown = payload.appMarkdown;
      if (payload.appSource) S.data.appSource = payload.appSource;
    }
    if (payload.loaderMarkdown) {
      S.data.loaderMarkdown = payload.loaderMarkdown;
      if (payload.loaderSource) S.data.loaderSource = payload.loaderSource;
    }
    if (S.open) renderBody();
  }

  function openManual(tab) {
    if (!HOST_AVAILABLE) {
      U()?.openModal(`<div class="v2-modal-head"><b>更新日志</b><button class="v2-icon-btn" data-v2modal="close">${U().ICON.x}</button></div>
        <div style="padding:18px"><p class="v2-muted">在 AstralParty.Toys 程序窗口里打开才能查看更新日志。</p></div>
        <div class="v2-modal-foot"><span class="v2-top-spacer"></span><button class="v2-btn primary" data-v2modal="close">关闭</button></div>`, 'narrow');
      return;
    }
    post({ type: 'openChangelogModal', tab: tab === 'loader' ? 'loader' : 'app' });
  }

  document.addEventListener('click', ev => {
    const el = ev.target.closest?.('[data-v2cl]');
    if (!el || el.disabled) return;
    const act = el.dataset.v2cl;
    if (act === 'close') {
      acknowledgeAndClose();
    } else if (act === 'neverAuto') {
      post({ type: 'setAutoChangelogPopup', enabled: false });
      acknowledgeAndClose();
    } else if (act === 'refresh') {
      S.refreshing = true;
      renderBody();
      post({ type: 'changelogGet' });
      post({ type: 'loaderChangelogGet', fresh: true });
    } else if (act === 'downloadAppUpdate') {
      acknowledgeAndClose();
      post({ type: 'startAppUpdateDownload' });
      if (window.V2SettingsModule?.state) window.V2SettingsModule.state.tab = 'update';
      showPage('settings');
      window.V2SettingsModule?.render?.();
    } else if (act === 'applyAppUpdate') {
      acknowledgeAndClose();
      post({ type: 'applyAppUpdate' });
    } else if (act === 'gotoAppUpdate') {
      acknowledgeAndClose();
      if (window.V2SettingsModule?.state) window.V2SettingsModule.state.tab = 'update';
      showPage('settings');
      window.V2SettingsModule?.render?.();
    } else if (act === 'gotoMods') {
      acknowledgeAndClose();
      showPage('mods');
    } else if (act === 'gotoDevices') {
      acknowledgeAndClose();
      showPage('devices');
    }
  });

  // 兼容：若用户点击了「拉取最新」触发的传统 changelog / loaderChangelog 消息，也能更新弹窗内容
  function syncFromLegacy(kind, payload) {
    if (!S.open || !S.data || !payload) return;
    S.refreshing = false;
    if (kind === 'app' && payload.markdown) {
      S.data.appMarkdown = payload.markdown;
      S.data.appSource = 'github';
    } else if (kind === 'loader' && payload.markdown) {
      S.data.loaderMarkdown = payload.markdown;
      S.data.loaderSource = payload.source || 'github';
    }
    renderBody();
  }

  window.V2Changelog = {
    open: openManual,
    receiveModal,
    receiveRefresh,
    syncFromLegacy,
  };
})();
