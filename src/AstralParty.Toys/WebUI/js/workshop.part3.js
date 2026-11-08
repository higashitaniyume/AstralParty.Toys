        return `<button class="workshop-tab${active ? ' is-active' : ''}" role="tab"
            id="workshopTab-${id}" data-tab="${id}" aria-selected="${active}"
            aria-controls="workshopPanel-${id}" tabindex="${active ? '0' : '-1'}">${esc(label)}</button>`;
      })
      .join('');

    const panels = tabs
      .map(([id]) => {
        const active = state.detailTab === id;
        let body = '';
        if (id === 'details') body = detailFacts(mod) + detailDownloadBlock(mod) + detailDescriptionBlock(mod);
        else if (id === 'versions') body = detailVersionsPanel(mod);
        else if (id === 'readme') body = detailReadmePanel(mod);
        else body = detailLicensePanel(mod);

        return `<section class="workshop-tab-panel" role="tabpanel" id="workshopPanel-${id}"
            aria-labelledby="workshopTab-${id}"${active ? '' : ' hidden'}>${body}</section>`;
      })
      .join('');

    return `
      <div class="workshop-tabs" role="tablist" aria-label="${esc(
        t('workshop.tablist_label')
      )}">${buttons}</div>
      <div class="workshop-tab-panels">${panels}</div>
    `;
  }

  function detailHeroMarkup(mod) {
    return `
      <div class="workshop-detail-hero">
        <div class="workshop-detail-cover">${coverMarkup(mod)}</div>
        <div class="workshop-detail-main">
          <div class="workshop-detail-title">
            <h3>${esc(mod.name || mod.id)}</h3>
            ${mod.featured ? `<span class="workshop-mini-badge">${esc(t('workshop.featured'))}</span>` : ''}
          </div>
          <p class="workshop-detail-summary">${esc(mod.summary || t('workshop.no_summary'))}</p>
          ${
            mod.tags && mod.tags.length
              ? `<div class="workshop-tags">${mod.tags
                  .map(tag => `<span>${esc(tag)}</span>`)
                  .join('')}</div>`
              : ''
          }
          <div class="workshop-detail-meta">
            <span>${esc(t('workshop.author'))} ${esc(mod.author || t('workshop.anonymous'))}</span>
            <span>${esc(t('workshop.latest'))} v${esc(mod.latestVersion || '—')}</span>
            <span>${t('workshop.downloads_short', { count: fmt(mod.downloads) })}</span>
            <span>${esc(t('workshop.updated'))} ${formatDate(mod.updatedAt)}</span>
          </div>
          <div class="workshop-detail-actions">
            ${chipFor({
              installedVersion: mod.installedVersion,
              enabled: mod.enabled,
              builtIn: mod.builtIn
            })}
            <span id="workshopDetailAction">${detailActionMarkup(mod)}</span>
          </div>
          ${window.StickerModule?.placeholder ? window.StickerModule.placeholder(mod.id) : ''}
          <div class="workshop-progress hidden" id="workshopDetailProgress">
            <div class="workshop-progress-bar"><span id="workshopDetailProgressBar"></span></div>
            <span class="workshop-progress-text" id="workshopDetailProgressText"></span>
          </div>
        </div>
      </div>
    `;
  }

  function renderDetailPage() {
    const host = $('workshopDetailContent');
    if (!host) return;

    const source = $('workshopDetailSource');
    if (source) {
      source.textContent = state.baseUrl || AppState.librarySettings?.workshopUrl || '';
    }

    if (!state.detail) {
      const title = $('workshopDetailTitle');
      if (title) title.textContent = t('workshop.detail_title');
    }

    if (state.detailError) {
      host.innerHTML = `
        <div class="workshop-empty">
          <span class="mi" data-icon="warning"></span>
          <p>${esc(state.detailError)}</p>
          <button class="secondary-btn" data-detail-retry="1" type="button">${esc(
            t('workshop.retry')
          )}</button>
        </div>
      `;
      window.Icons?.hydrate(host);
      return;
    }

    if (!state.detail) {
      host.innerHTML = `<div aria-hidden="true" style="display:grid;gap:12px"><div class="v2-skel" style="height:96px;border-radius:10px"></div><div class="v2-skel v2-skel-block" style="width:45%"></div><div class="v2-skel v2-skel-block" style="width:85%"></div><div class="v2-skel v2-skel-block" style="width:70%"></div></div>`;
      return;
    }

    host.innerHTML = `${detailHeroMarkup(state.detail)}${detailTabsMarkup(state.detail)}`;
    window.Icons?.hydrate(host);
    window.StickerModule?.syncAfterRender();
    window.StickerModule?.requestCounts([state.detail.id]);

    const title = $('workshopDetailTitle');
    if (title) title.textContent = state.detail.name || t('workshop.detail_title');

    updateDetailProgress();
  }

  /** 只切换 tab 的显示状态，不重画面板（滚动位置和图片都保持不变）。 */
  function selectDetailTab(tab) {
    if (!tab || tab === state.detailTab) return;
    state.detailTab = tab;

    document.querySelectorAll('#workshopDetailContent [data-tab]').forEach(button => {
      const active = button.getAttribute('data-tab') === tab;
      button.classList.toggle('is-active', active);
      button.setAttribute('aria-selected', active ? 'true' : 'false');
      button.setAttribute('tabindex', active ? '0' : '-1');
    });

    document.querySelectorAll('#workshopDetailContent .workshop-tab-panel').forEach(panel => {
      panel.hidden = panel.id !== `workshopPanel-${tab}`;
    });
  }

  /** 只刷新安装按钮和进度，避免整页重画抢走焦点。 */
  function syncDetailActions() {
    const mod = state.detail;
    if (!mod) return;

    const holder = $('workshopDetailAction');
    if (holder) holder.innerHTML = detailActionMarkup(mod);

    updateDetailProgress();

    const title = $('workshopDetailTitle');
    if (title) title.textContent = mod.name || t('workshop.detail_title');
  }

  // ============================== 使用同意 ==============================

  function consentBody() {
    const base = state.baseUrl || AppState.librarySettings?.workshopUrl || 'https://forge.hiynet.com';
    return `
      <div class="workshop-consent">
        <p class="workshop-consent-warn">${esc(t('workshop.consent_warn'))}</p>
        <p class="workshop-consent-lead">${esc(t('workshop.consent_lead'))}</p>
        <ul class="workshop-consent-points">
          <li>${esc(t('workshop.consent_point_third_party'))}</li>
          <li>${esc(t('workshop.consent_point_risk'))}</li>
          <li>${esc(t('workshop.consent_point_network'))}</li>
        </ul>
        <p class="workshop-consent-url">${esc(t('workshop.consent_current_url', { url: base }))}</p>
        <p class="workshop-consent-url-note">${esc(t('workshop.consent_default_url_note'))}</p>
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
    if (state.detail?.id === id) syncDetailActions();
    updateDetailProgress();

    post({ type: 'workshopInstall', id, version: version || '' });
  }

  function cancel() {
    if (!state.installingId) return;
    post({ type: 'workshopCancelDownload' });
    toast(t('workshop.cancelling'));
  }

  const WorkshopModule = {
    openExternalMod(payload) {
      openExternalModRequest(payload);
    },

    init() {
      if (this.initialized) return;
      this.initialized = true;

      $('workshopBackHomeBtn')?.addEventListener('click', () => showPage('home'));
      $('workshopRefreshBtn')?.addEventListener('click', () => {
        state.items = [];
        state.loading = false;
        const pager = $('workshopPager');
        if (pager) pager.innerHTML = '';
        load(1);
      });

      $('workshopDetailBackBtn')?.addEventListener('click', () => backToList());
      $('workshopDetailRefreshBtn')?.addEventListener('click', () => {
        if (state.detailId) requestDetail(state.detailId);
      });

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
        state.sort = event.target.value || 'downloads';
        load(1);
      });

      $('workshopStatusSelect')?.addEventListener('change', event => {
        state.statusFilter = event.target.value || 'all';
        renderGrid();
      });

      const listBtn = $('workshopViewListBtn');
      const gridBtn = $('workshopViewGridBtn');

      function syncViewSwitchButtons() {
        const isList = state.viewMode === 'list';
        listBtn?.classList.toggle('active', isList);
        gridBtn?.classList.toggle('active', !isList);
      }

      listBtn?.addEventListener('click', () => {
        state.viewMode = 'list';
        localStorage.setItem('workshop_view_mode', 'list');
        syncViewSwitchButtons();
        renderGrid();
      });

      gridBtn?.addEventListener('click', () => {
        state.viewMode = 'grid';
        localStorage.setItem('workshop_view_mode', 'grid');
        syncViewSwitchButtons();
        renderGrid();
      });

      syncViewSwitchButtons();

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
        if (window.StickerModule?.handleClick(event)) return;

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

        const open = event.target.closest('[data-open], .workshop-card, .workshop-row');
        if (open) {
          const id = open.getAttribute('data-open') || open.closest('.workshop-card, .workshop-row')?.getAttribute('data-mod-id');
          if (id) openDetail(id);
        }
      });

      // 详情页：切 tab、安装、取消、重试都在这一层做委托。
      const detailHost = $('workshopDetailContent');

      detailHost?.addEventListener('click', event => {
        if (window.StickerModule?.handleClick(event)) return;

        const retry = event.target.closest('[data-detail-retry]');
        if (retry) {
          if (state.detailId) requestDetail(state.detailId);
          return;
        }

        if (event.target.closest('#workshopDetailCancelBtn')) {
          cancel();
          return;
        }

        const tab = event.target.closest('[data-tab]');
        if (tab) {
          selectDetailTab(tab.getAttribute('data-tab'));
          return;
        }

        const installBtn = event.target.closest('[data-install]');
        if (installBtn) {
          event.preventDefault();
          install(installBtn.getAttribute('data-install'), installBtn.getAttribute('data-version'));
