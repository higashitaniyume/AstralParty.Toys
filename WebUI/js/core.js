// core.js - Host communication & Global Application State
const host = window.chrome?.webview;

const AppState = {
  currentPage: 'home',
  homeData: null,
  replayReport: null,
  replayLibrary: null,
  selectedFrame: -1,
  activeReplayTab: 'summary',
  activeWikiTab: 'heroes',
  currentPortrait: null,
  currentGreetingIndex: 0
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

// Router: Switch between home, tools, wiki, settings
function showPage(pageName) {
  AppState.currentPage = pageName;

  // Update Views visibility
  document.querySelectorAll('.page-view').forEach(view => {
    const isTarget = view.id === `${pageName}View`;
    view.classList.toggle('hidden', !isTarget);
  });

  // Update Breadcrumb buttons
  document.querySelectorAll('.nav-crumb-btn').forEach(btn => {
    btn.classList.toggle('active', btn.dataset.page === pageName);
  });

  // Page specific hooks
  if (pageName === 'home') {
    // If returning to home, refresh/display current hero
    if (window.HomeModule && AppState.homeData) {
      window.HomeModule.onShowHome();
    }
  } else if (pageName === 'tools') {
    // In tools, ensure replay state is updated
    if (window.ReplayModule) {
      window.ReplayModule.onShowTools();
    }
  } else if (pageName === 'wiki') {
    if (window.WikiModule && window.WikiModule.onShowWiki) {
      window.WikiModule.onShowWiki();
    }
  } else if (pageName === 'settings') {
    if (window.SettingsModule) {
      window.SettingsModule.render();
    }
  }
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
    case 'hostReady':
      if ($('runtimeBadgeText')) {
        $('runtimeBadgeText').textContent = msg.payload?.runtime || '在线 · 本地离线环境';
      }
      break;

    case 'homeData':
      AppState.homeData = msg.payload;
      if (window.HomeModule) window.HomeModule.init(msg.payload);
      if (window.WikiModule) window.WikiModule.init(msg.payload);
      if (window.SettingsModule) window.SettingsModule.init(msg.payload);
      break;

    case 'replayLibrary':
      AppState.replayLibrary = msg.payload;
      if (window.ReplayModule) window.ReplayModule.renderLibrary(msg.payload);
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
