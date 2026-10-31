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
    sort: 'updated',
    detail: null,
    installingId: '',
    installName: '',
    installVersion: '',
    installStatus: '',
    installMessage: '',
    installPhase: '',
    progress: null,
    pendingPage: 0,
    pendingDetailId: '',
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
    const bar = card.querySelector('.workshop-card-progress');
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
        button.textContent = action.label;
        button.disabled = true;
        button.removeAttribute('data-install');
        button.removeAttribute('data-version');
      } else {
        button.className = 'primary-btn workshop-install-btn';
        button.textContent = action.label;
        button.disabled = false;
        button.setAttribute('data-install', item.id);
        button.setAttribute('data-version', action.version);
      }
    }

    if (bar) {
      bar.hidden = percent === null;
      const fill = bar.firstElementChild;
      if (fill) fill.style.width = `${percent ?? 0}%`;
    }
  }

  function renderGrid() {
    const grid = $('workshopGrid');
    if (!grid) return;

    if (!state.items.length) {
      grid.innerHTML = state.loading
        ? `<div class="workshop-empty"><span class="mi" data-icon="hourglass_empty"></span><p>${esc(
            t('workshop.loading')
          )}</p></div>`
        : `<div class="workshop-empty"><span class="mi" data-icon="extension"></span><p>${esc(
            t('workshop.empty')
          )}</p><button class="secondary-btn" data-retry="1">${esc(t('workshop.retry'))}</button></div>`;
      window.Icons?.hydrate(grid);
      return;
    }

    grid.innerHTML = state.items.map(cardMarkup).join('');
    syncCards();
    window.Icons?.hydrate(grid);
  }

  // 按当前安装状态刷新所有卡片的按钮/进度条（不重建节点）。
  function syncCards() {
    const grid = $('workshopGrid');
    if (!grid) return;
    grid.querySelectorAll('.workshop-card').forEach(card => {
      const id = card.getAttribute('data-mod-id');
      const item = state.items.find(entry => entry.id === id);
      if (item) applyActionState(card, item);
    });
  }

  function renderPager() {
    const pager = $('workshopPager');
    if (!pager) return;
    const show = state.pages > 1;
    pager.classList.toggle('hidden', !show);
    if (!show) return;

    const info = $('workshopPageInfo');
    if (info) info.textContent = t('workshop.page_info', { page: state.page, pages: state.pages });
    const prev = $('workshopPrevBtn');
    const next = $('workshopNextBtn');
    if (prev) prev.disabled = state.page <= 1;
    if (next) next.disabled = state.page >= state.pages;
  }

  function syncTagOptions() {
    const select = $('workshopTagSelect');
    if (!select) return;
    const current = state.tag;
    select.innerHTML =
      `<option value="">${esc(t('workshop.tags_all'))}</option>` +
      state.tags.map(tag => `<option value="${esc(tag)}">${esc(tag)}</option>`).join('');
    select.value = current;
  }

  function syncSource() {
    const node = $('workshopSourceUrl');
    if (!node) return;
    const base = state.baseUrl || AppState.librarySettings?.workshopUrl || 'https://forge.hiynet.com';
    node.textContent = base;
    node.title = base;
  }

  function load(page) {
    if (state.loading) {
      state.pendingPage = Math.max(1, page || 1);
      return;
    }
    state.loading = true;
    state.page = Math.max(1, page || 1);
    hideNotice();
    renderGrid();
    syncSource();

    post({
      type: 'workshopList',
      query: state.query,
      tag: state.tag,
      sort: state.sort,
      page: state.page
    });
  }

  function openDetail(id) {
    if (state.consent !== 'granted') {
      openConsentModal();
      return;
    }

    const cached = state.details.get(id);
    if (cached) {
      state.detail = cached;
      showDetailModal(cached);
      return;
    }

    post({ type: 'workshopDetail', id });
    state.pendingDetailId = id;
    openModal(t('workshop.detail_loading_title'), `<p class="workshop-modal-loading">${esc(
      t('workshop.loading')
    )}</p>`, 'auto_awesome', 'modal-wide');
    // 用户等不及关掉弹窗：结果回来时不要再自己弹开。
    const modal = $('globalModal');
    if (modal) modal._onClose = () => { state.pendingDetailId = ''; };
  }

  // ============================== 使用同意 ==============================

  function consentBody() {
    return `
      <div class="workshop-consent">
        <p class="workshop-consent-lead">${esc(t('workshop.consent_lead'))}</p>
        <ul class="workshop-consent-points">
          <li>${esc(t('workshop.consent_point_third_party'))}</li>
          <li>${esc(t('workshop.consent_point_risk'))}</li>
          <li>${esc(t('workshop.consent_point_network'))}</li>
        </ul>
        <div class="workshop-consent-actions">
          <button class="secondary-btn" id="workshopConsentSkip">${esc(t('workshop.consent_skip'))}</button>
          <button class="primary-btn" id="workshopConsentAgree">${esc(t('workshop.consent_agree'))}</button>
        </div>
      </div>
    `;
  }

  function openConsentModal() {
    const modal = $('globalModal');
    if (modal?.classList.contains('open') && $('modalBody')?.querySelector('.workshop-consent')) return;

    openModal(t('workshop.consent_title'), consentBody(), 'warning', 'modal-wide');

    const current = $('globalModal');
    if (current) {
      // 右上角关闭 = 本次不用（不写任何记录，下次进入再问）。
      current._onClose = () => {
        state.consent = 'required';
        showConsentBlocked();
      };
    }

    $('workshopConsentSkip')?.addEventListener('click', () => closeModal());
    $('workshopConsentAgree')?.addEventListener('click', () => {
      const agree = $('workshopConsentAgree');
      if (agree) {
        agree.disabled = true;
        agree.textContent = t('workshop.consent_agreeing');
      }
      post({ type: 'workshopConsent', accept: true });
    });
  }

  function showConsentBlocked() {
    state.loading = false;
    const grid = $('workshopGrid');
    if (!grid) return;

    grid.innerHTML = `
      <div class="workshop-blocked">
        <span class="mi" data-icon="warning"></span>
        <h3>${esc(t('workshop.consent_blocked_title'))}</h3>
        <p>${esc(t('workshop.consent_blocked_body'))}</p>
        <button class="secondary-btn" data-consent-open="1">${esc(t('workshop.consent_open'))}</button>
      </div>
    `;
    window.Icons?.hydrate(grid);
    $('workshopPager')?.classList.add('hidden');
    hideNotice();
  }

  function showDetailModal(mod) {
    openModal(mod.name, detailBody(mod), 'auto_awesome', 'modal-wide');
    if (window.Icons) window.Icons.hydrate($('modalBody'));
    updateDetailProgress();
  }

  function detailBody(mod) {
    state.detail = mod;
    const versions = Array.isArray(mod.versions) ? mod.versions : [];
    const latest = mod.latestVersion || '';
    const canInstall = !mod.builtIn;
    const installing = state.installingId === mod.id;

    const versionRows = versions.length
      ? versions
          .map(version => {
            const permissions = describePermissions(version.permissions);
            const dependencies = Array.isArray(version.dependencies) ? version.dependencies : [];
            const isLatest = version.version === latest;

            return `
              <div class="workshop-version-row">
                <div class="workshop-version-head">
                  <strong>v${esc(version.version)}</strong>
                  ${isLatest ? `<span class="workshop-mini-badge">${esc(t('workshop.latest'))}</span>` : ''}
                  <span class="workshop-version-date">${formatDate(version.publishedAt)}</span>
                  <span class="workshop-version-size">${formatBytes(version.sizeBytes)}</span>
                  ${
                    canInstall
                      ? `<button class="secondary-btn workshop-version-install" data-install="${esc(
                          mod.id
                        )}" data-version="${esc(version.version)}">${esc(
                          t('workshop.install')
                        )}</button>`
                      : ''
                  }
                </div>
                <p class="workshop-version-notes">${esc(
                  version.changelog || t('workshop.no_changelog')
                )}</p>
                <div class="workshop-version-meta">
                  <span>${esc(t('workshop.sdk'))} ${esc(version.sdkVersion || t('workshop.undeclared'))}</span>
                  <span>${esc(t('workshop.version_downloads', { count: fmt(version.downloads) }))}</span>
                  ${
                    permissions.length
                      ? `<span>${esc(t('workshop.permissions'))} ${esc(permissions.join(' / '))}</span>`
                      : ''
                  }
                  <span>${esc(t('workshop.dependencies'))} ${
                    dependencies.length
                      ? esc(
                          dependencies
                            .map(dependency =>
                              dependency.minVersion
                                ? `${dependency.id} ≥ ${dependency.minVersion}`
                                : dependency.id
                            )
                            .join(' / ')
                        )
                      : esc(t('workshop.no_dependencies'))
                  }</span>
                </div>
                <code class="workshop-sha" title="${esc(version.sha256)}">SHA-256 ${esc(
                  version.sha256
                )}</code>
              </div>
            `;
          })
          .join('')
      : `<p class="workshop-modal-loading">${esc(t('workshop.no_versions'))}</p>`;

    const installButton =
      canInstall && latest
        ? installing
          ? `<button class="secondary-btn" id="workshopModalCancelBtn">${esc(t('workshop.cancel'))}</button>`
          : `<button class="primary-btn" id="workshopModalInstallBtn" data-install="${esc(
              mod.id
            )}" data-version="${esc(latest)}">${esc(t('workshop.install_version', { version: latest }))}</button>`
        : mod.builtIn
          ? `<span class="workshop-chip is-builtin">${esc(t('workshop.builtin'))}</span>`
          : '';

    const html = `
      <div class="workshop-detail">
        <div class="workshop-detail-hero">
          <div class="workshop-detail-cover">${coverMarkup(mod)}</div>
          <div class="workshop-detail-main">
            <div class="workshop-detail-title">
              <h3>${esc(mod.name)}</h3>
              ${mod.featured ? `<span class="workshop-mini-badge">${esc(t('workshop.featured'))}</span>` : ''}
            </div>
            <p class="workshop-detail-summary">${esc(mod.summary || t('workshop.no_summary'))}</p>
            <div class="workshop-detail-meta">
              <span>${esc(t('workshop.author'))} ${esc(mod.author || t('workshop.anonymous'))}</span>
              <span>${esc(t('workshop.latest'))} v${esc(latest || '—')}</span>
              <span>${t('workshop.downloads_short', { count: fmt(mod.downloads) })}</span>
              <span>${esc(t('workshop.updated'))} ${formatDate(mod.updatedAt)}</span>
            </div>
            <div class="workshop-detail-actions">
              ${chipFor({
                installedVersion: mod.installedVersion,
                enabled: mod.enabled,
                builtIn: mod.builtIn
              })}
              ${installButton}
            </div>
            <div class="workshop-progress hidden" id="workshopDetailProgress">
              <div class="workshop-progress-bar"><span id="workshopDetailProgressBar"></span></div>
              <span class="workshop-progress-text" id="workshopDetailProgressText"></span>
            </div>
          </div>
        </div>

        <div class="workshop-detail-description">
          <h4>${esc(t('workshop.description'))}</h4>
          <p>${esc(mod.description || t('workshop.no_description'))}</p>
        </div>

        <div class="workshop-detail-versions">
          <h4>${esc(t('workshop.versions'))} <span>${versions.length}</span></h4>
          ${versionRows}
        </div>
      </div>
    `;

    return html;
  }

  function updateDetailProgress() {
    const box = $('workshopDetailProgress');
    if (!box) return;

    const active = state.installingId && state.detail && state.installingId === state.detail.id;
    box.classList.toggle('hidden', !active);
    if (!active) return;

    const bar = $('workshopDetailProgressBar');
    const text = $('workshopDetailProgressText');
    const percent = progressPercent(state.progress);
    if (bar) bar.style.width = percent === null ? '35%' : `${percent}%`;
    if (bar) bar.parentElement?.classList.toggle('is-indeterminate', percent === null);
    if (text) text.textContent = progressLabel(state.progress);
  }

  // 详情弹窗开着时按最新状态原地重画（不重开弹窗、不重置关闭回调）。
  function refreshDetailBody() {
    const modal = $('globalModal');
    const body = $('modalBody');
    if (!state.detail || !modal?.classList.contains('open') || !body?.querySelector('.workshop-detail')) return;
    body.innerHTML = detailBody(state.detail);
    if (window.Icons) window.Icons.hydrate(body);
    updateDetailProgress();
    if (state.installingId) $('workshopModalCancelBtn')?.focus();
  }

  function install(id, version) {
    if (state.consent !== 'granted') {
      openConsentModal();
      return;
    }

    if (state.installingId) {
      toast(t('workshop.busy'));
      return;
    }

    state.installingId = id;
    state.installName = installDisplayName(id);
    state.installVersion = version || '';
    state.installStatus = 'running';
    state.installMessage = '';
    state.installPhase = 'prepare';
    clearTimeout(scheduleInstallToastHide._timer);
    state.progress = { id, phase: 'prepare', percent: null };
    renderInstallToast();
    syncCards();
    if (state.detail?.id === id) refreshDetailBody();
    updateDetailProgress();

    post({ type: 'workshopInstall', id, version: version || '' });
  }

  function cancel() {
    if (!state.installingId) return;
    post({ type: 'workshopCancelDownload' });
    toast(t('workshop.cancelling'));
  }

  const WorkshopModule = {
    init() {
      if (this.initialized) return;
      this.initialized = true;

      $('workshopBackHomeBtn')?.addEventListener('click', () => showPage('home'));
      $('workshopRefreshBtn')?.addEventListener('click', () => load(1));

      let searchTimer = 0;
      $('workshopSearchInput')?.addEventListener('input', event => {
        const value = event.target.value || '';
        clearTimeout(searchTimer);
        searchTimer = window.setTimeout(() => {
          state.query = value.trim();
          load(1);
        }, 350);
      });

      $('workshopTagSelect')?.addEventListener('change', event => {
        state.tag = event.target.value || '';
        load(1);
      });

      $('workshopSortSelect')?.addEventListener('change', event => {
        state.sort = event.target.value || 'updated';
        load(1);
      });

      $('workshopPrevBtn')?.addEventListener('click', () => {
        if (state.page > 1) load(state.page - 1);
      });
      $('workshopNextBtn')?.addEventListener('click', () => {
        if (state.page < state.pages) load(state.page + 1);
      });

      $('workshopInstallClose')?.addEventListener('click', () => {
        state.installStatus = '';
        renderInstallToast();
      });
      $('workshopInstallCancel')?.addEventListener('click', () => cancel());

      $('workshopGrid')?.addEventListener('click', event => {
        const consentOpen = event.target.closest('[data-consent-open]');
        if (consentOpen) {
          openConsentModal();
          return;
        }

        const retry = event.target.closest('[data-retry]');
        if (retry) {
          load(1);
          return;
        }

        const installBtn = event.target.closest('[data-install]');
        if (installBtn) {
          event.preventDefault();
          event.stopPropagation();
          install(installBtn.getAttribute('data-install'), installBtn.getAttribute('data-version'));
          return;
        }

        const open = event.target.closest('[data-open], .workshop-card');
        if (open) {
          const id = open.getAttribute('data-open') || open.closest('.workshop-card')?.getAttribute('data-mod-id');
          if (id) openDetail(id);
        }
      });

      $('modalBody')?.addEventListener('click', event => {
        if (event.target.closest('#workshopModalCancelBtn')) {
          cancel();
          return;
        }

        const installBtn = event.target.closest('[data-install]');
        if (installBtn) {
          event.preventDefault();
          install(installBtn.getAttribute('data-install'), installBtn.getAttribute('data-version'));
        }
      });

      window.addEventListener('appLanguageChanged', () => {
        // 名称/简介/更新说明都是服务端按语言返回的，必须重新取，不能只重画旧数据。
        state.details.clear();
        const detailId = state.detail?.id;
        state.detail = null;
        state.loaded = false;
        renderInstallToast();
        if (AppState.currentPage === 'workshop') {
          load(1);
        } else {
          syncTagOptions();
          renderGrid();
          renderPager();
        }
        // 详情弹窗还开着就刷新它的内容。
        if (detailId) post({ type: 'workshopDetail', id: detailId });
      });
    },

    onShow() {
      this.init();
      syncSource();
      if (!state.loaded) {
        load(1);
      } else {
        renderGrid();
        renderPager();
      }
    },

    requireConsent() {
      state.loading = false;
      state.pendingPage = 0;
      state.consent = 'required';
      // 本次不用不写任何记录：离开再进入时要重新询问。
      state.loaded = false;
      showConsentBlocked();
      openConsentModal();
    },

    renderConsent(payload) {
      if (!payload?.granted) return;

      state.consent = 'granted';
      const modal = $('globalModal');
      if (modal?.classList.contains('open') && $('modalBody')?.querySelector('.workshop-consent')) {
        modal._onClose = null;
        closeModal();
      }

      toast(t('workshop.consent_granted'));
      load(1);
    },

    renderList(payload) {
      state.loading = false;
      state.loaded = true;
      const pendingPage = state.pendingPage;
      state.pendingPage = 0;

      if (!payload) return;
      if (payload.baseUrl) state.baseUrl = payload.baseUrl;
      syncSource();

      if (payload.error) {
        state.items = [];
        state.total = 0;
        state.pages = 1;
        renderGrid();
        renderPager();
        notice(
          `<p>${esc(t('workshop.error'))}</p><p class="workshop-notice-detail">${esc(
            payload.error
          )}</p><button class="secondary-btn" data-retry="1">${esc(t('workshop.retry'))}</button>`,
          'error'
        );
        // 通知栏里的重试按钮也要能点（grid 的委托覆盖不到这里）。
        const retry = $('workshopNotice')?.querySelector('[data-retry]');
        if (retry) retry.addEventListener('click', () => load(1), { once: true });
        return;
      }

      state.items = Array.isArray(payload.items) ? payload.items : [];
      state.total = payload.total || 0;
      state.page = payload.page || 1;
      state.pageSize = payload.pageSize || 24;
      state.tags = Array.isArray(payload.tags) ? payload.tags : [];
      state.pages = Math.max(1, Math.ceil(state.total / Math.max(1, state.pageSize)));
      state.consent = 'granted';

      syncTagOptions();
      renderGrid();
      renderPager();

      if (state.items.length) {
        notice(t('workshop.total', { total: fmt(state.total) }), 'muted');
      } else {
        hideNotice();
      }

      if (pendingPage) load(pendingPage);
    },

    renderDetail(payload) {
      if (!payload) return;

      const modal = $('globalModal');
      const body = $('modalBody');
      const modalOpen = !!modal?.classList.contains('open');
      const showingDetail = !!body?.querySelector('.workshop-detail');
      const expectedId = payload.mod?.id || payload.id || '';

      if (payload.error) {
        // 等的是别的模组：这条是上一次请求的迟到回复，丢掉。
        if (state.pendingDetailId && expectedId && state.pendingDetailId !== expectedId) return;
        state.pendingDetailId = '';
        openModal(t('workshop.detail_failed_title'), `<p class="workshop-modal-loading">${esc(
          payload.error
        )}</p>`, 'warning');
        return;
      }

      const mod = payload.mod;
      if (!mod) return;
      state.details.set(mod.id, mod);

      const requested = state.pendingDetailId === mod.id;
      state.pendingDetailId = '';

      if (modalOpen && showingDetail) {
        // 弹窗已经开着（例如安装完回来刷新）：原地换内容，不重新抢焦点。
        state.detail = mod;
        refreshDetailBody();
        return;
      }

      if (!requested) return;
      showDetailModal(mod);
    },

    renderProgress(payload) {
      if (!payload) return;
      if (!state.installingId) state.installingId = payload.id;
      if (payload.id && state.installingId !== payload.id) return;

      state.progress = {
        id: payload.id,
        phase: payload.phase || 'download',
        percent: typeof payload.percent === 'number' ? payload.percent : null,
        receivedBytes: payload.receivedBytes,
        totalBytes: payload.totalBytes
      };
      state.installPhase = state.progress.phase;

      syncCards();
      updateDetailProgress();
      if (state.installStatus === 'running') renderInstallToast();
    },

    renderResult(payload) {
      if (!payload) return;
      const id = payload.id || '';

      state.installStatus = payload.success ? 'success' : payload.cancelled ? 'cancelled' : 'failed';
      state.installMessage = payload.message || '';
      renderInstallToast();
      scheduleInstallToastHide(payload.success || payload.cancelled ? 4500 : 10000);

      state.installingId = '';
      state.progress = null;

      if (payload.success) {
        state.details.delete(id);
        let text = t('workshop.install_success', { version: payload.version || '' });
        // 宿主把「保留了什么」结构化回推，这里按界面语言拼，避免整句固定中文。
        const preserved = Array.isArray(payload.preservedConfigs) ? payload.preservedConfigs : [];
        const disabled = Array.isArray(payload.preservedDisabled) ? payload.preservedDisabled : [];
        if (preserved.length) text += ' ' + t('workshop.preserved_config', { names: preserved.join(', ') });
        if (disabled.length) text += ' ' + t('workshop.preserved_disabled', { names: disabled.join(', ') });
        toast(text);
      } else if (payload.cancelled) {
        toast(t('workshop.cancelled'));
      } else {
        toast(payload.message || t('workshop.install_failed'));
      }

      load(state.page);
      if (state.detail && state.detail.id === id) {
        post({ type: 'workshopDetail', id });
      }
    },

    openInstallConfirm(payload) {
      if (!payload) return;
      const packages = Array.isArray(payload.packages) ? payload.packages : [];

      const html = `
        <p class="workshop-confirm-lead">${esc(t('workshop.downgrade_message'))}</p>
        <ul class="workshop-confirm-list">
          ${packages
            .map(
              item =>
                `<li><strong>${esc(item.name)}</strong><span>v${esc(item.from)} → v${esc(item.to)}</span></li>`
            )
            .join('')}
        </ul>
        <div class="workshop-confirm-actions">
          <button class="secondary-btn" id="workshopConfirmNo">${esc(t('workshop.downgrade_reject'))}</button>
          <button class="primary-btn" id="workshopConfirmYes">${esc(t('workshop.downgrade_accept'))}</button>
        </div>
      `;

      openModal(t('workshop.downgrade_title'), html, 'warning');

      const modal = $('globalModal');
      if (modal) {
        // 点 X 关掉弹窗 = 拒绝：否则宿主会一直等一个永远不会来的回答。
        modal._onClose = () => post({ type: 'workshopInstallConfirmAnswer', accept: false });
      }

      $('workshopConfirmYes')?.addEventListener('click', () => {
        if (modal) modal._onClose = null;
        closeModal();
        post({ type: 'workshopInstallConfirmAnswer', accept: true });
      });

      $('workshopConfirmNo')?.addEventListener('click', () => closeModal());
    }
  };

  window.WorkshopModule = WorkshopModule;
})();
