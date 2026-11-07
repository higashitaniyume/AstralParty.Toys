// workshop.js - Mod market (community catalog) page logic
(function () {
  'use strict';

  const state = {
    initialized: false,
    loaded: false,
    loading: false,
    baseUrl: '',
    items: [],
    total: 0,
    page: 1,
    pages: 1,
    tags: [],
    query: '',
    tag: '',
    sort: 'downloads',
    viewMode: localStorage.getItem('workshop_view_mode') || 'list',
    statusFilter: 'all',
    detail: null,
    detailId: '',
    detailTab: 'details',
    detailError: '',
    listScrollTop: 0,
    pendingListScroll: null,
    mdCache: new Map(),
    installingId: '',
    installName: '',
    installVersion: '',
    installStatus: '',
    installMessage: '',
    installPhase: '',
    progress: null,
    pendingPage: 0,
    consent: 'unknown',
    details: new Map()
  };

  const t = (key, params) => (window.i18n ? window.i18n.t(key, params) : key);
  const locale = () => window.i18n?.currentLocale?.() || 'zh-CN';

  function formatBytes(bytes) {
    const value = Number(bytes || 0);
    if (value <= 0) return '—';
    if (value >= 1024 * 1024) return `${(value / 1024 / 1024).toFixed(1)} MB`;
    if (value >= 1024) return `${(value / 1024).toFixed(1)} KB`;
    return `${value} B`;
  }

  function formatDate(value) {
    if (!value) return '—';
    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) return '—';
    return parsed.toLocaleDateString(locale());
  }

  function compareVersions(left, right) {
    const parts = value => {
      const result = [0, 0, 0];
      String(value || '').split('.').slice(0, 3).forEach((segment, index) => {
        const parsed = parseInt(segment, 10);
        result[index] = Number.isFinite(parsed) ? parsed : 0;
      });
      return result;
    };
    const a = parts(left);
    const b = parts(right);
    for (let index = 0; index < 3; index += 1) {
      if (a[index] !== b[index]) return a[index] > b[index] ? 1 : -1;
    }
    return 0;
  }

  const permissionLabels = [
    [1, 'workshop.perm_state'],
    [2, 'workshop.perm_action'],
    [4, 'workshop.perm_speed'],
    [8, 'workshop.perm_files'],
    [16, 'workshop.perm_modify'],
    [32, 'workshop.perm_camera'],
    [64, 'workshop.perm_input'],
    [128, 'workshop.perm_ui'],
    [256, 'workshop.perm_file_system'],
    [512, 'workshop.perm_network'],
    [1024, 'workshop.perm_debug']
  ];

  function describePermissions(mask) {
    if (!mask) return [];
    return permissionLabels.filter(([bit]) => (mask & bit) !== 0).map(([, key]) => t(key));
  }

  function notice(text, kind) {
    const node = $('workshopNotice');
    if (!node) return;
    node.className = `workshop-notice${kind ? ` is-${kind}` : ''}`;
    node.innerHTML = text;
    node.classList.remove('hidden');
  }

  function hideNotice() {
    $('workshopNotice')?.classList.add('hidden');
  }

  function actionFor(item) {
    if (item.builtIn) {
      return { label: t('workshop.builtin'), disabled: true, chip: 'builtin' };
    }

    const installed = item.installedVersion || '';
    const latest = item.latestVersion || '';

    if (!installed) {
      return {
        label: t('workshop.install_version', { version: latest }),
        install: true,
        version: latest
      };
    }

    const comparison = compareVersions(installed, latest);
    if (comparison < 0) {
      return {
        label: t('workshop.update_version', { version: latest }),
        install: true,
        version: latest,
        upgrade: true
      };
    }

    return { label: t('workshop.installed_version', { version: installed }), disabled: true };
  }

  function chipFor(item) {
    if (item.builtIn) {
      return `<span class="workshop-chip is-builtin">${esc(t('workshop.builtin'))}</span>`;
    }
    if (!item.installedVersion) return '';
    if (item.enabled === false) {
      return `<span class="workshop-chip is-off">${esc(t('workshop.chip_disabled'))}</span>`;
    }
    return `<span class="workshop-chip is-installed">${esc(
      t('workshop.chip_installed', { version: item.installedVersion })
    )}</span>`;
  }

  function progressLabel(progress) {
    if (!progress) return '';
    if (progress.phase === 'prepare') return t('workshop.preparing');
    if (progress.phase === 'verify') return t('workshop.verifying');
    if (progress.phase === 'install') return t('workshop.installing');
    if (progress.percent !== null && progress.percent !== undefined) {
      return t('workshop.downloading_percent', { percent: progress.percent });
    }
    return t('workshop.downloading');
  }

  function progressPercent(progress) {
    if (!progress) return null;
    if (progress.phase !== 'download') return null;
    return typeof progress.percent === 'number' ? progress.percent : null;
  }

  // ============================== 安装进度浮窗 ==============================

  function installDisplayName(id) {
    const fromList = state.items.find(item => item.id === id);
    if (fromList?.name) return fromList.name;
    if (state.detail?.id === id && state.detail.name) return state.detail.name;
    return id;
  }

  function installToastText(progress) {
    if (!progress) return t('workshop.preparing');
    if (progress.phase === 'prepare') return t('workshop.preparing');
    if (progress.phase === 'verify') return t('workshop.verifying');
    if (progress.phase === 'install') return t('workshop.installing');
    if (typeof progress.percent === 'number') return t('workshop.downloading_percent', { percent: progress.percent });
    return t('workshop.downloading');
  }

  function renderInstallToast() {
    const toast = $('workshopInstallToast');
    if (!toast) return;

    const status = state.installStatus;
    if (!status) {
      toast.classList.remove('show');
      toast.setAttribute('aria-hidden', 'true');
      return;
    }

    const running = status === 'running';
    const failed = status === 'failed';
    const done = status === 'success';
    const cancelled = status === 'cancelled';
    const phase = running ? state.progress?.phase || state.installPhase || 'prepare' : state.installPhase;
    const activeIndex = phase === 'install' ? 2 : phase === 'verify' ? 1 : 0;
    const percent = running ? progressPercent(state.progress) : done ? 100 : null;

    toast.classList.add('show');
    toast.removeAttribute('aria-hidden');
    toast.classList.toggle('is-done', done);
    toast.classList.toggle('is-error', failed);
    toast.classList.toggle('is-cancelled', cancelled);

    const title = $('workshopInstallTitle');
    if (title) title.textContent = t('workshop.install_toast_title', { name: state.installName || state.installingId });

    const subtitle = $('workshopInstallSubtitle');
    if (subtitle) {
      subtitle.textContent = state.installVersion ? `v${state.installVersion}` : '';
      subtitle.hidden = !state.installVersion;
    }

    const percentNode = $('workshopInstallPercent');
    if (percentNode) percentNode.textContent = done ? '100%' : percent !== null ? `${percent}%` : '';

    const steps = document.querySelectorAll('#workshopInstallSteps li');
    steps.forEach((li, index) => {
      li.classList.toggle('is-done', done || (running && index < activeIndex));
      li.classList.toggle('is-active', running && index === activeIndex);
      li.classList.toggle('is-error', failed && index === activeIndex);
      const label = li.querySelector('.wit-label');
      if (label) {
        label.textContent = t(
          index === 0
            ? 'workshop.install_step_download'
            : index === 1
              ? 'workshop.install_step_verify'
              : 'workshop.install_step_install'
        );
      }
    });

    const bar = $('workshopInstallBar');
    if (bar) {
      bar.style.width = done ? '100%' : percent !== null ? `${percent}%` : failed ? '100%' : '35%';
      bar.parentElement?.classList.toggle('is-indeterminate', running && percent === null);
    }

    const text = $('workshopInstallText');
    if (text) {
      text.textContent = running
        ? installToastText(state.progress)
        : done
          ? t('workshop.install_done')
          : cancelled
            ? t('workshop.cancelled')
            : state.installMessage || t('workshop.install_failed');
    }

    const cancelBtn = $('workshopInstallCancel');
    if (cancelBtn) {
      const canCancel = running && (phase === 'prepare' || phase === 'download');
      cancelBtn.classList.toggle('hidden', !canCancel);
    }

    window.Icons?.hydrate(toast);
  }

  function scheduleInstallToastHide(delay) {
    clearTimeout(scheduleInstallToastHide._timer);
    scheduleInstallToastHide._timer = window.setTimeout(() => {
      state.installStatus = '';
      renderInstallToast();
    }, delay);
  }

  function coverMarkup(item) {
    if (item.coverUrl) {
      return `<img src="${esc(item.coverUrl)}" alt="" loading="lazy" />`;
    }
    const initials = String(item.name || item.id || '?').slice(0, 2);
    return `<span class="workshop-cover-fallback">${esc(initials)}</span>`;
  }

  function rowMarkup(item) {
    return `
      <article class="workshop-row" data-mod-id="${esc(item.id)}">
        <div class="workshop-row-thumb">
          ${coverMarkup(item)}
        </div>
        <div class="workshop-row-main">
          <div class="workshop-row-header">
            <h3 class="workshop-row-title" title="${esc(item.name)}">${esc(item.name)}</h3>
            <span class="workshop-version">v${esc(item.latestVersion)}</span>
            ${chipFor(item)}
            ${item.featured ? `<span class="workshop-featured" style="position:static;padding:2px 8px;font-size:10.5px;">${esc(t('workshop.featured'))}</span>` : ''}
          </div>
          <p class="workshop-row-desc">${esc(item.summary || t('workshop.no_summary'))}</p>
          <div class="workshop-row-meta">
            <span>${esc(item.author || t('workshop.anonymous'))}</span>
            <span aria-hidden="true">·</span>
            <span>${formatBytes(item.sizeBytes)}</span>
            <span aria-hidden="true">·</span>
            <span>${t('workshop.downloads_short', { count: fmt(item.downloads) })}</span>
            ${
              item.tags && item.tags.length
                ? `<div class="workshop-row-tags">${item.tags.slice(0, 3).map(tag => `<span>${esc(tag)}</span>`).join('')}</div>`
                : ''
            }
          </div>
          ${window.StickerModule?.placeholder ? window.StickerModule.placeholder(item.id) : ''}
        </div>
        <div class="workshop-row-actions">
          <div class="workshop-row-progress" hidden><span></span></div>
          <button class="secondary-btn workshop-install-btn" data-role="action" type="button"></button>
        </div>
      </article>
    `;
  }

  function filterItems(items) {
    if (!Array.isArray(items)) return [];
    const status = state.statusFilter || 'all';
    if (status === 'all') return items;

    return items.filter(item => {
      const isBuiltIn = !!item.builtIn;
      const installed = item.installedVersion || '';
      const latest = item.latestVersion || '';
      const isInstalled = !!installed && !isBuiltIn;
      const isUpdatable = isInstalled && compareVersions(installed, latest) < 0;

      if (status === 'updatable') return isUpdatable;
      if (status === 'installed') return isInstalled;
      if (status === 'not_installed') return !installed && !isBuiltIn;
      if (status === 'builtin') return isBuiltIn;
      return true;
    });
  }

  function cardMarkup(item) {
    return `
      <article class="workshop-card" data-mod-id="${esc(item.id)}">
        <button class="workshop-cover" type="button" data-open="${esc(item.id)}" aria-label="${esc(item.name)}">
          ${coverMarkup(item)}
          ${item.featured ? `<span class="workshop-featured">${esc(t('workshop.featured'))}</span>` : ''}
        </button>
        <div class="workshop-card-body">
          <div class="workshop-card-head">
            <h3 title="${esc(item.name)}">${esc(item.name)}</h3>
            <span class="workshop-version">v${esc(item.latestVersion)}</span>
          </div>
          <p class="workshop-card-summary">${esc(item.summary || t('workshop.no_summary'))}</p>
          ${
            item.tags && item.tags.length
              ? `<div class="workshop-tags">${item.tags
                  .slice(0, 3)
                  .map(tag => `<span>${esc(tag)}</span>`)
                  .join('')}</div>`
              : ''
          }
          <div class="workshop-card-meta">
            <span>${esc(item.author || t('workshop.anonymous'))}</span>
            <span aria-hidden="true">·</span>
            <span>${formatBytes(item.sizeBytes)}</span>
            <span aria-hidden="true">·</span>
            <span>${t('workshop.downloads_short', { count: fmt(item.downloads) })}</span>
            ${
              item.downloads7Days > 0
                ? `<span aria-hidden="true">·</span><span class="workshop-recent-downloads">${esc(
                    t('workshop.downloads_7d', { count: fmt(item.downloads7Days) })
                  )}</span>`
                : ''
            }
          </div>
          ${window.StickerModule?.placeholder ? window.StickerModule.placeholder(item.id) : ''}
          <div class="workshop-card-foot">
            <span class="workshop-chip-row">${chipFor(item)}</span>
            <button class="secondary-btn workshop-install-btn" data-role="action" type="button"></button>
          </div>
          <div class="workshop-card-progress" hidden><span></span></div>
        </div>
      </article>
    `;
  }

  // 只改按钮与进度条，不重画整张卡：封面图不会跟着进度闪，
  // 键盘焦点也留在原来的按钮上。
  function applyActionState(card, item) {
    const installing = state.installingId === item.id;
    const action = actionFor(item);
    const button = card.querySelector('[data-role="action"]');
    const bar = card.querySelector('.workshop-card-progress, .workshop-row-progress');
    const percent = installing ? progressPercent(state.progress) : null;

    card.classList.toggle('is-installing', installing);

    if (button) {
      button.classList.remove('is-busy');
      if (installing) {
        button.className = 'secondary-btn workshop-install-btn is-busy';
        button.textContent = progressLabel(state.progress);
        button.disabled = true;
        button.removeAttribute('data-install');
        button.removeAttribute('data-version');
      } else if (action.disabled) {
        button.className = 'secondary-btn workshop-install-btn';
