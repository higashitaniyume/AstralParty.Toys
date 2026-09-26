// core.js - Host communication & Global Application State
const host = window.chrome?.webview;

const AppState = {
  currentPage: 'home',
  homeData: null,
  replayReport: null,
  replayLibrary: null,
  librarySettings: null,
  selectedFrame: -1,
  activeReplayTab: 'summary',
  activeWikiTab: 'heroes',
  currentPortrait: null,
  currentGreetingIndex: 0,
  isPageTransitioning: false,
  pendingPage: null
};

// Utilities
const $ = id => document.getElementById(id);
const esc = value => String(value ?? '').replace(/[&<>'"]/g, c => ({
  '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;'
}[c]));
const fmt = value => Number(value ?? 0).toLocaleString('zh-CN');
const post = message => host?.postMessage(message);

function toast(message) {
  const node = $('toast');
  if (!node) return;
  node.textContent = message;
  node.classList.add('show');
  clearTimeout(toast._timer);
  toast._timer = setTimeout(() => node.classList.remove('show'), 3800);
}

function openModal(title, contentHtml) {
  const modal = $('globalModal');
  const titleEl = $('modalTitle');
  const bodyEl = $('modalBody');
  if (!modal || !titleEl || !bodyEl) return;
  titleEl.textContent = title;
  bodyEl.innerHTML = contentHtml;
  modal.classList.add('open');
  // 记下"打开弹窗之前焦点在哪"，关闭时还回去（键盘用户才不会丢掉位置）
  modal._restoreFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
  // 焦点进弹窗：否则键盘用户还在背后的页面上 Tab，弹窗里的按钮要绕一整圈才够得着
  $('modalCloseBtn')?.focus();
}

function closeModal() {
  const modal = $('globalModal');
  if (!modal) return;
  const wasOpen = modal.classList.contains('open');
  modal.classList.remove('open');
  if (!wasOpen) return;
  const restore = modal._restoreFocus;
  modal._restoreFocus = null;
  if (restore?.isConnected) restore.focus();
}

/** 弹窗里的可聚焦元素（Tab 循环用）。 */
function modalFocusables() {
  const modal = $('globalModal');
  if (!modal) return [];
  return [...modal.querySelectorAll('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])')]
    .filter(el => !el.disabled && el.offsetParent !== null);
}

// Router: animate between home, tools, utilities, mods, wiki, settings
function runPageHook(pageName) {
  if (pageName === 'home') {
    if (window.HomeModule && AppState.homeData) window.HomeModule.onShowHome();
  } else if (pageName === 'tools') {
    window.ReplayModule?.onShowTools();
  } else if (pageName === 'wiki') {
    window.WikiModule?.onShowWiki?.();
  } else if (pageName === 'settings') {
    window.SettingsModule?.init();
  } else if (pageName === 'utilities') {
    window.UtilitiesModule?.onShowUtilities?.();
  } else if (pageName === 'mods') {
    window.ModsModule?.onShowMods?.();
  }
}

function updateNavigation(pageName) {
  document.body.classList.toggle('page-home-active', pageName === 'home');
  document.querySelectorAll('.nav-crumb-btn').forEach(btn => {
    btn.classList.toggle('active', btn.dataset.page === pageName);
  });
}

function finishPageTransition(target, pageName) {
  document.querySelectorAll('.page-view').forEach(view => {
    if (view !== target) {
      view.classList.add('hidden');
      view.classList.remove('page-entering', 'page-leaving');
      view.removeAttribute('aria-hidden');
    }
  });
  target.classList.remove('page-entering', 'page-leaving');
  document.body.classList.remove('page-transitioning');
  AppState.currentPage = pageName;
  AppState.isPageTransitioning = false;
  runPageHook(pageName);

  const pending = AppState.pendingPage;
  AppState.pendingPage = null;
  if (pending && pending !== pageName) showPage(pending);
}

function showPage(pageName, options = {}) {
  const target = $(`${pageName}View`);
  if (!target) return;

  const current = document.querySelector('.page-view:not(.hidden)');
  const immediate = options.immediate || document.body.classList.contains('reduce-motion');
  if (AppState.isPageTransitioning) {
    AppState.pendingPage = pageName;
    return;
  }
  if (current === target) {
    updateNavigation(pageName);
    runPageHook(pageName);
    return;
  }

  updateNavigation(pageName);
  if (!current || immediate) {
    target.classList.remove('hidden', 'page-entering', 'page-leaving');
    finishPageTransition(target, pageName);
    return;
  }

  AppState.isPageTransitioning = true;
  document.body.classList.add('page-transitioning');
  current.classList.add('page-leaving');
  current.setAttribute('aria-hidden', 'true');

  window.setTimeout(() => {
    current.classList.add('hidden');
    current.classList.remove('page-leaving');
    target.classList.remove('hidden');
    target.classList.add('page-entering');
    target.removeAttribute('aria-hidden');
    void target.offsetWidth;
    requestAnimationFrame(() => target.classList.remove('page-entering'));
    window.setTimeout(() => finishPageTransition(target, pageName), 390);
  }, 180);
}

// Setup Global Listeners
document.addEventListener('DOMContentLoaded', () => {
  // Navigation breadcrumbs
  document.querySelectorAll('.nav-crumb-btn').forEach(btn => {
    btn.addEventListener('click', () => showPage(btn.dataset.page));
  });

  // Brand logo click returns to home（role=button，补上键盘 Enter/Space）
  $('brandLogoBtn')?.addEventListener('click', () => showPage('home'));
  $('brandLogoBtn')?.addEventListener('keydown', e => {
    if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); showPage('home'); }
  });

  // Modal close handlers
  $('modalCloseBtn')?.addEventListener('click', closeModal);
  $('globalModal')?.addEventListener('click', e => {
    if (e.target === $('globalModal')) closeModal();
  });
  window.addEventListener('keydown', e => {
    if (e.key === 'Escape') { closeModal(); return; }
    // 弹窗打开时把 Tab 圈在弹窗内（焦点不跑到弹窗背后的页面上）
    const modal = $('globalModal');
    if (e.key !== 'Tab' || !modal?.classList.contains('open')) return;
    const focusables = modalFocusables();
    if (focusables.length === 0) return;
    const first = focusables[0];
    const last = focusables[focusables.length - 1];
    const active = document.activeElement;
    if (e.shiftKey && (active === first || !modal.contains(active))) {
      e.preventDefault();
      last.focus();
    } else if (!e.shiftKey && (active === last || !modal.contains(active))) {
      e.preventDefault();
      first.focus();
    }
  });
});

// Host Message Handler
host?.addEventListener('message', event => {
  const msg = event.data;
  if (!msg?.type) return;

  switch (msg.type) {
    case 'appVersion':
      window.VersionModule?.render(msg.payload);
      break;

    case 'changelog':
      window.VersionModule?.renderChangelog(msg.payload);
      break;

    case 'loaderChangelog':
      window.VersionModule?.renderLoaderChangelog(msg.payload);
      break;

    case 'homeData':
      AppState.homeData = msg.payload;
      if (window.HomeModule) window.HomeModule.init(msg.payload);
      if (window.WikiModule) window.WikiModule.init(msg.payload);
      if (window.SettingsModule) window.SettingsModule.init();
      break;

    case 'replayLibrary':
      AppState.replayLibrary = msg.payload;
      if (window.ReplayModule) window.ReplayModule.renderLibrary(msg.payload);
      break;

    case 'libraryResult':
      if (window.ReplayModule) window.ReplayModule.renderLibraryResult(msg.payload);
      break;

    case 'librarySettings':
      AppState.librarySettings = msg.payload;
      if (window.SettingsModule) {
        window.SettingsModule.init();
        window.SettingsModule.applyLibrarySettings(msg.payload);
      }
      break;

    case 'loading':
      const loading = $('loadingOverlay');
      if (loading) {
        loading.classList.toggle('hidden', !msg.active);
        if (msg.message && $('loadingText')) $('loadingText').textContent = msg.message;
      }
      break;

    case 'report':
      try {
        AppState.replayReport = msg.payload;
        showPage('tools');
        if (window.ReplayModule) window.ReplayModule.renderReport(msg.payload);
        toast('回放文件解析成功！');
      } catch (err) {
        console.error('Failed to display report:', err);
        toast(`解析展示失败: ${err.message}`);
      }
      break;

    case 'frameDetail':
      if (window.ReplayModule) window.ReplayModule.renderFrameDetail(msg.payload);
      break;

    case 'toast':
      toast(msg.message);
      break;

    case 'speedhackStatus':
      if (window.UtilitiesModule) window.UtilitiesModule.applyStatus(msg.payload);
      break;

    case 'modStatus':
      // payload = { status, builtInMods }：整份交给渲染，页面自己区分「哪个模组是内置的」
      if (window.ModsModule) window.ModsModule.renderModStatus(msg.payload);
      break;

    case 'modUpdateCheck':
      // 「检查更新」的联网结果（最新版本号 / 错误）
      if (window.ModsModule) window.ModsModule.renderUpdateCheck(msg.payload);
      break;

    case 'modLoaderConfig':
      if (window.ModsModule) window.ModsModule.renderLoaderSettings(msg.payload?.config);
      break;

    case 'modSpeedhackSync':
      if (window.ModsModule) window.ModsModule.syncSpeedhackBar(msg.payload?.config);
      break;

    case 'steamBypassSync':
      // 加载器设置里改了「Steam 绕过」→ 首页的「绕过 Steam 启动」单选框跟着走（同一个配置）
      if (window.HomeModule) window.HomeModule.applySteamBypassSync(msg.payload);
      break;

    case 'gameProfiles':
      AppState.gameProfiles = msg.payload;
      if (window.GameLibModule) window.GameLibModule.renderProfiles(msg.payload);
      break;

    case 'gameScanResult':
      if (window.GameLibModule) window.GameLibModule.renderScanResult(msg.payload);
      break;

    case 'gameScanProgress':
      if (window.GameLibModule) window.GameLibModule.renderScanProgress(msg.payload);
      break;

    case 'modConfigFields':
      if (window.ModsModule) window.ModsModule.renderConfigFields(msg.payload);
      break;

    case 'error':
      const loadEl = $('loadingOverlay');
      if (loadEl) loadEl.classList.add('hidden');
      // 宿主抛异常时不会有别的收尾消息，这里兜住按钮的忙碌态，免得一直转圈
      window.ModsModule?.clearBusy?.();
      toast(`错误：${msg.message}`);
      break;
  }
});

// Expose to window
window.AppState = AppState;
window.post = post;
window.toast = toast;
window.openModal = openModal;
window.closeModal = closeModal;
window.showPage = showPage;
window.$ = $;
window.esc = esc;
window.fmt = fmt;
