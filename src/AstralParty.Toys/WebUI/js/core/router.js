// core/router.js - Page routing and navigation
// Router: animate between home, tools, utilities, mods, wiki, settings
function runPageHook(pageName) {
  if (pageName === 'home') {
    if (window.HomeModule && AppState.homeData) window.HomeModule.onShowHome();
  } else if (pageName === 'profiles') {
    window.AccountProfilesModule?.onShow();
  } else if (pageName === 'account') {
    window.MyAccountModule?.onShow();
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
  } else if (pageName === 'workshop') {
    window.WorkshopModule?.onShow?.();
  } else if (pageName === 'workshopDetail') {
    window.WorkshopModule?.onShowDetail?.();
  } else if (pageName === 'skins') {
    window.SkinsModule?.onShowSkins?.();
  }
}

function updateNavigation(pageName) {
  document.body.classList.toggle('page-home-active', pageName === 'home');
  // 模组详情是「市场」下面的子页面，面包屑仍然高亮市场。
  const activePage = pageName === 'workshopDetail' ? 'workshop' : pageName;
  document.querySelectorAll('.nav-crumb-btn').forEach(btn => {
    btn.classList.toggle('active', btn.dataset.page === activePage);
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
  const immediate = options.immediate || window.Motion?.reduced();
  if (AppState.isPageTransitioning) {
    AppState.pendingPage = pageName;
    return;
  }
  if (current === target) {
    updateNavigation(pageName);
    runPageHook(pageName);
    return;
  }

  if (current?.id === 'accountView') window.MyAccountModule?.onHide();
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
    window.setTimeout(() => finishPageTransition(target, pageName), window.Motion?.duration('--motion-page-in', 180) ?? 180);
  }, window.Motion?.duration('--motion-page-out', 100) ?? 100);
}

// Setup Global Listeners
document.addEventListener('DOMContentLoaded', () => {
  // Navigation breadcrumbs
  document.querySelectorAll('.nav-crumb-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      if (btn.dataset.page === 'workshop') {
        window.WorkshopModule?.clearDetailReturn?.();
      }
      showPage(btn.dataset.page);
    });
  });

  // Brand logo click returns to home（role=button，补上键盘 Enter/Space）
  $('brandLogoBtn')?.addEventListener('click', () => showPage('home'));
  $('brandLogoBtn')?.addEventListener('keydown', e => {
    if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); showPage('home'); }
  });

  // Modal close handlers
  $('modalCloseBtn')?.addEventListener('click', closeModal);
  // 配置弹窗只能通过弹窗内的明确按钮关闭；点击遮罩和 Esc 都不关闭，避免误触丢失编辑内容。
  window.addEventListener('keydown', e => {
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
