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
}

function closeModal() {
  const modal = $('globalModal');
  if (modal) modal.classList.remove('open');
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

  // Brand logo click returns to home
  $('brandLogoBtn')?.addEventListener('click', () => showPage('home'));

  // Modal close handlers
  $('modalCloseBtn')?.addEventListener('click', closeModal);
  $('globalModal')?.addEventListener('click', e => {
    if (e.target === $('globalModal')) closeModal();
  });
  window.addEventListener('keydown', e => {
    if (e.key === 'Escape') closeModal();
  });
});

// Host Message Handler
host?.addEventListener('message', event => {
  const msg = event.data;
  if (!msg?.type) return;

  switch (msg.type) {
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
      if (window.ModsModule) window.ModsModule.renderModStatus(msg.payload);
      break;

    case 'modLoaderConfig':
      if (window.ModsModule) window.ModsModule.renderLoaderSettings(msg.payload?.config);
      break;

    case 'modPermissions':
      if (window.ModsModule) window.ModsModule.renderPermissions(msg.payload);
      break;

    case 'modConfigFields':
      if (window.ModsModule) window.ModsModule.renderConfigFields(msg.payload);
      break;

    case 'error':
      const loadEl = $('loadingOverlay');
      if (loadEl) loadEl.classList.add('hidden');
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
