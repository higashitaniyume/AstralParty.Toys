// resource-mods.js - 资源模组（RML 格式 ZIP）安装器
(function () {
  const t = (key, params) => window.t ? window.t(key, params) : key;

  const formatSize = bytes => {
    const value = Number(bytes) || 0;
    if (value >= 1 << 30) return (value / (1 << 30)).toFixed(1) + ' GiB';
    if (value >= 1 << 20) return (value / (1 << 20)).toFixed(1) + ' MiB';
    if (value >= 1 << 10) return (value / (1 << 10)).toFixed(1) + ' KiB';
    return value + ' B';
  };

  const module = {
    status: null,
    progressPaused: false,
    progressPhase: "",
    progressPercent: 0,
    logCollapsed: false,

    init() {
      $('resourceModImportBtn')?.addEventListener('click', () => post({ type: 'resourceModsImport' }));
      $('resourceModApplyBtn')?.addEventListener('click', () => post({ type: 'resourceModsApply' }));
      $('resourceModRestoreBtn')?.addEventListener('click', () => post({ type: 'resourceModsRestore' }));
      $('resourceModFolderBtn')?.addEventListener('click', () => post({ type: 'resourceModsOpenFolder' }));
      $('resourceModRefreshBtn')?.addEventListener('click', () => this.refresh());
      $('rmpPause')?.addEventListener('click', () => post({ type: 'resourceModsPause', paused: !this.progressPaused }));
      $('rmpCancel')?.addEventListener('click', () => this.confirmCancel());
      $('rmpLogToggle')?.addEventListener('click', () => this.toggleLog());
      window.addEventListener('appLanguageChanged', () => {
        this.applyLogCollapsed();
        const pause = $('rmpPause');
        if (pause) pause.textContent = t(this.progressPaused ? 'resource_mods.resume' : 'resource_mods.pause');
      });
      this.applyLogCollapsed();

      $('resourceModList')?.addEventListener('click', event => {
        const card = event.target.closest('.resource-mod-card');
        if (!card) return;
        const id = card.dataset.id;
        if (event.target.closest('[data-action="toggle"]')) {
          const mod = this.status?.mods?.find(item => item.id === id);
          if (mod) post({ type: 'resourceModsToggle', id, enabled: !mod.enabled, name: mod.name });
        } else if (event.target.closest('[data-action="delete"]')) {
          this.confirmDelete(id);
        }
      });
    },

    onShow() {
      this.refresh();
    },

    refresh() {
      post({ type: 'resourceModsStatus' });
    },

    render(status) {
      this.status = status || {};
      const banner = $('resourceModBanner');
      if (banner) {
        const parts = [];
        let cls = 'is-ok';
        if (status?.catalogError) {
          cls = 'is-error';
          parts.push(status.catalogError);
        } else {
          parts.push(t('resource_mods.status_version', { v: status?.catalogVersion || '—' }));
          parts.push(status?.appliedVersion
            ? t('resource_mods.status_applied', { n: status.appliedRedirects ?? 0 })
            : t('resource_mods.status_not_applied'));
          if (status?.appliedErrors > 0) { parts.push(`${status.appliedErrors} 条未生效`); cls = 'is-error'; }
          else if (status?.appliedWarnings > 0) { parts.push(`${status.appliedWarnings} 条警告`); cls = 'is-warn'; }
          if (status?.needsApply) parts.push(t('resource_mods.status_pending'));
          if (status?.gameRunning) parts.push(t('resource_mods.status_running'));
          if (status?.needsApply || status?.gameRunning) cls = 'is-warn';
        }
        banner.className = 'resource-mod-banner ' + cls;
        banner.textContent = parts.join(' · ');

        const warnings = $('resourceModWarnings');
        if (warnings) {
          const messages = status?.appliedMessages || [];
          warnings.classList.toggle('hidden', messages.length === 0);
          if (messages.length > 0) {
            $('resourceModWarningsSummary').textContent = `${status.appliedErrors || 0} 条未生效 / ${status.appliedWarnings || 0} 条警告（点击查看）`;
            $('resourceModWarningsBody').textContent = messages.join('\n');
          }
        }
      }

      const mods = status?.mods || [];
      const list = $('resourceModList');
      if (list) list.innerHTML = mods.map(mod => this.cardHtml(mod)).join('');
      $('resourceModEmpty')?.classList.toggle('hidden', mods.length > 0);

      const apply = $('resourceModApplyBtn');
      if (apply) apply.disabled = !status?.catalogAvailable || !!status?.gameRunning
        || !mods.some(mod => mod.enabled && mod.matchedBundles > 0);
      const restore = $('resourceModRestoreBtn');
      if (restore) restore.disabled = !status?.appliedVersion || !!status?.gameRunning;
    },

    renderOperation(p) {
      const toast = $('opToast');
      if (!toast || !p) return;
      if (p.active === false) {
        toast.classList.remove('show');
        return;
      }
      // 新一轮操作开始时清掉上一次的日志；导入过程中的自动应用不会走到这里。
      if (p.percent == null) this.clearLog();
      toast.classList.add('show');
      $('opToastTitle').textContent = p.title || '正在处理…';
      $('opToastDetail').textContent = p.detail || '';
      const percent = Number(p.percent);
      $('opToastPercent').textContent = Number.isFinite(percent) && percent > 0 ? `${percent}%` : '';
      $('opToastCancel').classList.toggle('hidden', !p.canCancel);
    },
    renderProgress(p) {
      const panel = $('resourceModProgress');
      if (!panel || !p) return;
      if (p.phase === 'import' && (p.percent || 0) <= 3) this.clearLog();
      panel.classList.remove('hidden');
      if (p.phase === 'import' && (p.percent || 0) <= 3) this.progressPaused = false;
      if (p.phase === 'apply' && (p.percent || 0) <= 5) this.progressPaused = false;
      panel.classList.toggle('is-paused', this.progressPaused);

      const phaseText = { import: '正在导入…', apply: '正在应用…', done: '完成', cancelled: '已取消并还原' }[p.phase] || '处理中…';
      $('rmpPhase').textContent = phaseText;
      $('rmpPercent').textContent = `${p.percent || 0}%`;
      $('rmpBar').value = p.percent || 0;
      $('rmpTask').textContent = p.task || '';

      if (p.detail) this.appendLog(p.detail);

      const finished = p.phase === 'done' || p.phase === 'cancelled';
      $('rmpPause').disabled = finished || p.canPause === false;
      $('rmpPause').classList.toggle('hidden', finished);
      $('rmpPause').textContent = t(p.paused ? 'resource_mods.resume' : 'resource_mods.pause');
      $('rmpCancel').disabled = finished || p.canCancel === false;
      $('rmpCancel').classList.toggle('hidden', finished);
      this.setBusy(!finished);
      if (finished) panel.classList.remove('is-paused');
    },

    renderPaused(paused) {
      this.progressPaused = !!paused;
      $('resourceModProgress')?.classList.toggle('is-paused', !!paused);
      const button = $('rmpPause');
      if (button) button.textContent = t(paused ? 'resource_mods.resume' : 'resource_mods.pause');
    },

    setBusy(busy) {
      for (const id of ['resourceModImportBtn', 'resourceModApplyBtn', 'resourceModRestoreBtn']) {
        const button = $(id);
        if (button) button.disabled = busy;
      }
      if (!busy && this.status) this.render(this.status);
    },

    appendLog(detail) {
      const log = $('rmpLog');
      if (!log) return;
      const lines = (log.textContent ? log.textContent.split('\n') : []);
      lines.push(detail);
      while (lines.length > 200) lines.shift();
      log.textContent = lines.join('\n');
      log.scrollTop = log.scrollHeight;
    },

    clearLog() {
      const log = $('rmpLog');
      if (log) log.textContent = '';
    },

    toggleLog() {
      this.logCollapsed = !this.logCollapsed;
      this.applyLogCollapsed();
      if (!this.logCollapsed) {
        const log = $('rmpLog');
        if (log) log.scrollTop = log.scrollHeight;
      }
    },

    applyLogCollapsed() {
      $('rmpLog')?.classList.toggle('hidden', this.logCollapsed);
      const button = $('rmpLogToggle');
      if (button) button.textContent = t(this.logCollapsed ? 'resource_mods.log_expand' : 'resource_mods.log_collapse');
    },

    confirmCancel() {
      openModal('取消并还原', `
        <p>取消当前操作，并把游戏 catalog 恢复到操作前的状态。已经解压的临时文件会被删除。</p>
        <div style="display:flex;justify-content:flex-end;gap:10px;margin-top:18px">
          <button class="secondary-btn" id="rmpCancelNo">继续执行</button>
          <button class="primary-btn" id="rmpCancelYes">取消并还原</button>
        </div>`, 'cancel');
      $('rmpCancelNo')?.addEventListener('click', () => closeModal());
      $('rmpCancelYes')?.addEventListener('click', () => {
        closeModal();
        post({ type: 'resourceModsCancel' });
      });
    },
    cardHtml(mod) {
      const unmatched = (mod.unmatchedNames || []).map(name => `<li>${esc(name)}</li>`).join('');
      const details = mod.unmatchedBundles > 0
        ? `<details class="resource-mod-unmatched"><summary>${t('resource_mods.unmatched_summary', { n: mod.unmatchedBundles })}</summary><ul>${unmatched}</ul></details>`
        : '';
      const badge = mod.enabled
        ? `<span class="resource-mod-badge is-on">${t('resource_mods.enabled')}</span>`
        : `<span class="resource-mod-badge is-off">${t('resource_mods.disabled')}</span>`;
      return `
        <article class="resource-mod-card${mod.enabled ? '' : ' is-disabled'}" data-id="${esc(mod.id)}">
          <div class="resource-mod-head">
            <div class="resource-mod-title">
              <strong>${esc(mod.name)}</strong>
              <span class="resource-mod-source">${esc(mod.sourceFile)}</span>
            </div>
            <div class="resource-mod-actions">
              <button class="secondary-btn" data-action="toggle">${mod.enabled ? t('resource_mods.toggle_off') : t('resource_mods.toggle_on')}</button>
              <button class="secondary-btn" data-action="delete">${t('resource_mods.delete')}</button>
            </div>
          </div>
          <div class="resource-mod-meta">
            ${badge}
            <span>${t('resource_mods.bundles', { n: mod.bundleCount })}</span>
            <span>${t('resource_mods.matched', { n: mod.matchedBundles })}</span>
            <span>${formatSize(mod.size)}</span>
          </div>
          ${details}
        </article>`;
    },

    confirmDelete(id) {
      const mod = this.status?.mods?.find(item => item.id === id);
      if (!mod) return;
      openModal(t('resource_mods.delete_title'), `
        <p>${t('resource_mods.delete_body', { name: esc(mod.name) })}</p>
        <div style="display:flex;justify-content:flex-end;gap:10px;margin-top:18px">
          <button class="secondary-btn" id="resourceModDeleteCancel">${t('common.cancel')}</button>
          <button class="primary-btn" id="resourceModDeleteConfirm">${t('common.delete')}</button>
        </div>`, 'delete');
      $('resourceModDeleteCancel')?.addEventListener('click', () => closeModal());
      $('resourceModDeleteConfirm')?.addEventListener('click', () => {
        closeModal();
        post({ type: 'resourceModsDelete', id, name: mod.name });
      });
    }
  };

  document.addEventListener('DOMContentLoaded', () => module.init());
  window.ResourceModsModule = module;
})();
