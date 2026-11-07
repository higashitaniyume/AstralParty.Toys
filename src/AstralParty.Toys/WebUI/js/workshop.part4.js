        }
      });

      detailHost?.addEventListener('keydown', event => {
        if (!['ArrowRight', 'ArrowLeft', 'Home', 'End'].includes(event.key)) return;

        const tabs = [...document.querySelectorAll('#workshopDetailContent [data-tab]')];
        const current = tabs.indexOf(document.activeElement);
        if (current < 0) return;

        let next = current;
        if (event.key === 'ArrowRight') next = (current + 1) % tabs.length;
        else if (event.key === 'ArrowLeft') next = (current - 1 + tabs.length) % tabs.length;
        else if (event.key === 'Home') next = 0;
        else next = tabs.length - 1;

        event.preventDefault();
        selectDetailTab(tabs[next].getAttribute('data-tab'));
        $('workshopTab-' + tabs[next].getAttribute('data-tab'))?.focus();
      });

      window.addEventListener('appLanguageChanged', () => {
        // 名称/简介/更新说明都是服务端按语言返回的，必须重新取，不能只重画旧数据。
        state.details.clear();
        state.mdCache.clear();
        const detailId = state.detail?.id;
        state.detail = null;
        state.loaded = false;
        renderInstallToast();

        if (AppState.currentPage === 'workshopDetail') {
          if (detailId) requestDetail(detailId);
          return;
        }

        if (AppState.currentPage === 'workshop') {
          load(1);
        } else {
          syncTagOptions();
          renderGrid();
          renderPager();
        }
      });
    },

    clearDetailReturn() {
      state.pendingListScroll = null;
    },

    onShow() {
      this.init();
      syncSource();
      if (state.pendingListScroll !== null) {
        // 从详情页返回时回到原来的滚动位置。
        renderGrid();
        renderPager();
        const scroller = document.querySelector('#workshopView .workshop-scroll');
        if (scroller) scroller.scrollTop = state.pendingListScroll;
        state.pendingListScroll = null;
        return;
      }

      // 每次进入市场：清空老信息，重置筛选，拉取最新数据
      state.items = [];
      state.total = 0;
      state.page = 1;
      state.query = '';
      state.tag = '';
      state.sort = 'downloads';
      state.statusFilter = 'all';
      state.loaded = false;
      state.details.clear();
      state.mdCache.clear();

      const searchInput = $('workshopSearchInput');
      if (searchInput) searchInput.value = '';
      const tagSelect = $('workshopTagSelect');
      if (tagSelect) tagSelect.value = '';
      const sortSelect = $('workshopSortSelect');
      if (sortSelect) sortSelect.value = 'downloads';
      const statusSelect = $('workshopStatusSelect');
      if (statusSelect) statusSelect.value = 'all';

      state.loading = false;
      const pager = $('workshopPager');
      if (pager) pager.innerHTML = '';

      load(1);
    },

    onShowDetail() {
      this.init();
      // 无论是点「返回市场」还是面包屑，回到列表时都回到原来的位置。
      state.pendingListScroll = state.listScrollTop;
      renderDetailPage();
      const scroller = $('workshopDetailScroll');
      if (scroller) scroller.scrollTop = 0;
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
      window.StickerModule?.requestCounts(state.items.map(item => item.id));

      if (state.items.length) {
        notice(t('workshop.total', { total: fmt(state.total) }), 'muted');
      } else {
        hideNotice();
      }

      if (pendingPage) load(pendingPage);
    },

    renderDetail(payload) {
      if (!payload) return;

      const expectedId = payload.mod?.id || payload.id || '';
      // 已经打开了别的模组：这条是上一次请求的迟到回复，丢掉。
      // 注意不要用 AppState.currentPage 判断——页面切换动画期间它还是旧值。
      if (expectedId && state.detailId && expectedId !== state.detailId) return;

      if (payload.error) {
        state.detailError = payload.error;
        renderDetailPage();
        return;
      }

      const mod = payload.mod;
      if (!mod) return;

      state.detailError = '';
      state.detail = mod;
      state.detailId = mod.id;
      state.details.set(mod.id, mod);
      state.mdCache.delete(`${mod.id}:readme`);

      renderDetailPage();
    },

    renderStickers(payload) {
      window.StickerModule?.applyBulk(payload);
    },

    renderStickerCatalog(payload) {
      window.StickerModule?.applyCatalog(payload);
    },

    renderStickerCatalogProgress(payload) {
      window.StickerModule?.applyCatalogProgress(payload);
    },

    renderStickerToggle(payload) {
      window.StickerModule?.applyToggle(payload);
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
