// profiles.js - 游戏多开（账号 profile）管理页
//
// 一页管一件事：列出 / 新建 / 改名 / 删除账号档案，并把选中的档案写进加载器配置。
// 真正的目录重定向由加载器(version.dll)在游戏进程内完成，这里只碰目录与配置。
(function () {
  // 与首页「启动方式」单选框共用同一个偏好（从 Steam 启动 / 绕过 Steam 启动）
  const LAUNCH_BYPASS_KEY = 'launchBypassSteam';

  function t(key, params) {
    return window.i18n ? window.i18n.t(key, params) : key;
  }

  function fmtBytes(bytes) {
    const value = Number(bytes) || 0;
    if (value >= 1024 * 1024 * 1024) return (value / 1024 / 1024 / 1024).toFixed(2) + ' GB';
    if (value >= 1024 * 1024) return (value / 1024 / 1024).toFixed(1) + ' MB';
    if (value >= 1024) return (value / 1024).toFixed(1) + ' KB';
    return value + ' B';
  }

  const AccountProfilesModule = {
    data: { profiles: [], activeName: '', canManage: false, error: null, loaderInstalled: false, windowTitleEnabled: true },
    bound: false,

    onShow() {
      this.bindOnce();
      post({ type: 'accountProfilesGet' });
    },

    bindOnce() {
      if (this.bound) return;
      this.bound = true;

      $('accountProfilesRefreshBtn')?.addEventListener('click', () => post({ type: 'accountProfilesGet' }));
      $('accountProfileCreateBtn')?.addEventListener('click', () => this.create());
      $('accountProfileNameInput')?.addEventListener('input', () => this.refreshNameValidation());
      $('accountProfileNameInput')?.addEventListener('keydown', event => {
        if (event.key === 'Enter') {
          event.preventDefault();
          this.create();
        }
      });
      $('accountProfilesList')?.addEventListener('click', event => this.onListClick(event));
      $('accountProfilesList')?.addEventListener('change', event => this.onListChange(event));
      $('accountProfileWindowTitleToggle')?.addEventListener('change', event => this.onWindowTitleChange(event));
    },

    // ---------- 名称规则（宿主下发，页面实时校验；不要在这里另写一套） ----------
    rule() {
      const fallback = /^[A-Za-z0-9_]{1,32}$/;
      const maxLength = Number(this.data.nameMaxLength) || 32;
      const text = this.data.namePattern;
      if (!text) return { re: fallback, maxLength };
      try { return { re: new RegExp(text), maxLength }; }
      catch { return { re: fallback, maxLength }; }
    },

    /** 返回 { ok, message }；ignoreName = 改名时允许保持原名不变。 */
    validateName(raw, ignoreName) {
      const value = (raw || '').trim();
      const { re, maxLength } = this.rule();
      if (!value) return { ok: false, message: t('profile.name_required') };
      if (value.length > maxLength) return { ok: false, message: t('profile.name_too_long', { max: maxLength }) };
      if (!re.test(value)) return { ok: false, message: t('profile.name_invalid') };

      const lower = value.toLowerCase();
      const taken = (this.data.profiles || []).some(p =>
        !p.isDefault && String(p.name || '').toLowerCase() === lower);
      if (taken && lower !== String(ignoreName || '').toLowerCase())
        return { ok: false, message: t('profile.name_exists', { name: value }) };

      return { ok: true, message: '' };
    },

    applyNameRule() {
      const input = $('accountProfileNameInput');
      if (!input) return;
      const { maxLength } = this.rule();
      input.maxLength = maxLength;
    },

    refreshNameValidation() {
      const input = $('accountProfileNameInput');
      const hint = $('accountProfileNameHint');
      const button = $('accountProfileCreateBtn');
      if (!input) return;

      const raw = input.value;
      const value = raw.trim();
      const result = value ? this.validateName(value) : { ok: false, message: '' };
      if (button) button.disabled = !result.ok;

      if (!hint) return;
      if (!value) {
        hint.textContent = t('profile.name_hint');
        hint.classList.remove('is-error');
      } else if (result.ok) {
        hint.textContent = t('profile.name_ok', { name: value });
        hint.classList.remove('is-error');
      } else {
        hint.textContent = result.message;
        hint.classList.add('is-error');
      }
    },

    create() {
      const input = $('accountProfileNameInput');
      const name = (input?.value || '').trim();
      const result = this.validateName(name);
      if (!result.ok) {
        toast(result.message);
        input?.focus();
        return;
      }
      post({ type: 'accountProfileCreate', name });
      if (input) {
        input.value = '';
        this.refreshNameValidation();
      }
    },

    render(payload) {
      this.data = payload || { profiles: [], activeName: '', canManage: false, error: null, loaderInstalled: false, windowTitleEnabled: true };
      this.applyNameRule();
      this.renderStatus();
      this.renderWindowTitle();
      this.renderList();
      this.refreshNameValidation();
    },

    // ---------- 窗口标题开关（写加载器的 profileWindowTitle；默认开） ----------
    renderWindowTitle() {
      const toggle = $('accountProfileWindowTitleToggle');
      if (!toggle) return;
      const ready = !!this.data.loaderInstalled;
      toggle.checked = !ready || this.data.windowTitleEnabled !== false;
      toggle.disabled = !ready;
      $('accountProfileWindowTitleOption')?.classList.toggle('is-disabled', !ready);
    },

    onWindowTitleChange(event) {
      const enabled = !!event.target.checked;
      if (!this.data.loaderInstalled) return;
      this.data.windowTitleEnabled = enabled;
      post({ type: 'accountProfileSetWindowTitle', enabled });
    },

    renderStatus() {
      const el = $('accountProfilesStatus');
      if (!el) return;
      const data = this.data;

      if (data.error) {
        el.innerHTML = `<span class="profiles-warn">${Icons.svg('warning')} ${esc(data.error)}</span>`;
        return;
      }

      const profiles = data.profiles || [];
      const current = profiles.find(p => p.active);
      const currentLabel = current ? (current.isDefault ? t('profile.default_name') : current.name) : t('profile.default_name');
      const named = profiles.filter(p => !p.isDefault).length;

      let html = `<span class="profiles-current">${esc(t('profile.current_label'))}: <strong>${esc(currentLabel)}</strong></span>`;
      html += `<span class="profiles-count">${esc(t('profile.count', { count: named }))}</span>`;
      if (!data.loaderInstalled) {
        html += `<span class="profiles-warn">${Icons.svg('warning')} ${esc(t('profile.loader_missing'))}</span>`;
      }
      el.innerHTML = html;
    },

    renderList() {
      const container = $('accountProfilesList');
      if (!container) return;
      const profiles = this.data.profiles || [];
      if (!profiles.length) {
        container.innerHTML = `<div class="profiles-empty">${esc(this.data.error || t('profile.empty'))}</div>`;
        return;
      }
      container.innerHTML = profiles.map(p => this.card(p)).join('');
    },

    card(p) {
      const isDefault = !!p.isDefault;
      const label = isDefault ? t('profile.default_name') : p.name;
      const loaderReady = !!this.data.loaderInstalled;
      const bypass = window.localStorage.getItem(LAUNCH_BYPASS_KEY) === 'true';

      const tags = [];
      if (p.active) tags.push(`<span class="profiles-tag active">${esc(t('profile.active_tag'))}</span>`);
      if (!p.exists) tags.push(`<span class="profiles-tag warn">${Icons.svg('warning')} ${esc(t('profile.missing_tag'))}</span>`);

      const meta = p.exists
        ? t('profile.meta', {
            replays: Number(p.replays) || 0,
            size: fmtBytes(p.bytes),
            last: p.lastWrite ? new Date(p.lastWrite).toLocaleString() : t('profile.never_used')
          })
        : t('profile.missing_tag');

      const cache = isDefault || !p.exists
        ? ''
        : `<div class="profiles-row-sub">${esc(t(p.cacheSeeded ? 'profile.cache_shared' : 'profile.cache_pending'))}</div>`;

      // 备注：点这一行编辑（只对独立档案提供；默认档案就是游戏自己的目录）
      const note = String(p.note || '').trim();
      const noteLine = isDefault ? '' : `
            <button class="profiles-note-line${note ? '' : ' is-empty'}" data-role="note"
                    title="${esc(t('profile.note_title'))}">
              ${Icons.svg('edit')}<span>${esc(note || t('profile.note_add'))}</span>
            </button>`;

      const actions = [];
      if (loaderReady && !p.active) {
        actions.push(`<button class="secondary-btn profiles-btn" data-role="activate">${esc(t('profile.set_active'))}</button>`);
      }
      if (loaderReady) {
        // 默认档案可以选启动方式；其它档案固定绕过 Steam（宿主侧也会强制）
        if (isDefault) {
          actions.push(`
            <select class="profiles-mode-select" data-role="launch-mode"
                    title="${esc(t('profile.launch_mode'))}" aria-label="${esc(t('profile.launch_mode'))}">
              <option value="steam"${bypass ? '' : ' selected'}>${esc(t('home.launch_steam'))}</option>
              <option value="bypass"${bypass ? ' selected' : ''}>${esc(t('home.launch_bypass'))}</option>
            </select>`);
        }
        const launchTitle = isDefault
          ? (bypass ? t('home.launch_bypass') : t('home.launch_steam'))
          : t('home.launch_bypass');
        actions.push(`<button class="primary-btn profiles-btn" data-role="launch" title="${esc(launchTitle)}">${Icons.svg('play_arrow')} ${esc(t('profile.launch'))}</button>`);
      }
      actions.push(`<button class="secondary-btn profiles-btn" data-role="open" title="${esc(p.directory)}">${Icons.svg('folder_open')} ${esc(t('profile.open'))}</button>`);
      if (!isDefault) {
        actions.push(`<button class="secondary-btn profiles-btn" data-role="rename">${Icons.svg('edit')} ${esc(t('profile.rename'))}</button>`);
        actions.push(`<button class="danger-btn profiles-btn" data-role="delete">${Icons.svg('delete')} ${esc(t('profile.delete'))}</button>`);
      }

      return `
        <div class="profiles-card${p.active ? ' is-active' : ''}${p.exists ? '' : ' is-missing'}" data-name="${esc(p.name)}">
          <div class="profiles-card-main">
            <div class="profiles-row-title">
              <span class="profiles-name">${esc(label)}</span>
              ${tags.join('')}
            </div>
            <div class="profiles-path" title="${esc(p.directory)}">${esc(p.directory)}</div>
            <div class="profiles-row-sub">${esc(meta)}</div>
            ${cache}
            ${noteLine}
          </div>
          <div class="profiles-card-actions">${actions.join('')}</div>
        </div>`;
    },

    onListChange(event) {
      const select = event.target.closest('select[data-role="launch-mode"]');
      if (!select) return;
      const bypass = select.value === 'bypass';
      window.localStorage.setItem(LAUNCH_BYPASS_KEY, String(bypass));
      // 与首页「启动方式」单选框是同一个偏好/同一组加载器配置，保持两边一致
      window.HomeModule?.applySteamBypassSync?.({ enabled: bypass });
      post({ type: 'setSteamBypass', enabled: bypass });
      this.renderList();
    },

    onListClick(event) {
      const button = event.target.closest('button[data-role]');
      if (!button) return;
      const card = button.closest('.profiles-card');
      if (!card) return;
      const name = card.dataset.name || '';
      const profile = (this.data.profiles || []).find(p => (p.name || '') === name);
      if (!profile) return;

      switch (button.dataset.role) {
        case 'activate':
          post({ type: 'accountProfileActivate', name });
          break;
        case 'launch':
          post({
            type: 'accountProfileLaunch',
            name,
            bypassSteam: window.localStorage.getItem(LAUNCH_BYPASS_KEY) === 'true'
          });
          break;
        case 'open':
          post({ type: 'accountProfileOpen', name });
          break;
        case 'rename':
          this.openRename(profile);
          break;
        case 'note':
          this.openNote(profile);
          break;
        case 'delete':
          this.confirmDelete(profile);
          break;
      }
    },

    openNote(profile) {
      openModal(t('profile.note_title'), `
        <div class="cfg-form">
          <textarea class="library-input wide profiles-note-input" id="accountProfileNoteInput"
                    maxlength="256" rows="3" spellcheck="false"
                    placeholder="${esc(t('profile.note_placeholder'))}">${esc(profile.note || '')}</textarea>
          <p class="cfg-hint">${esc(t('profile.note_hint'))}</p>
          <div class="cfg-actions">
            <button class="secondary-btn" id="accountProfileNoteCancel">${esc(t('common.cancel'))}</button>
            <button class="primary-btn" id="accountProfileNoteSave">${esc(t('common.save'))}</button>
          </div>
        </div>`, 'edit');

      $('accountProfileNoteCancel')?.addEventListener('click', closeModal);
      $('accountProfileNoteSave')?.addEventListener('click', () => {
        const value = ($('accountProfileNoteInput')?.value || '').trim();
        closeModal();
        post({ type: 'accountProfileSetNote', name: profile.name, note: value });
      });
      $('accountProfileNoteInput')?.focus();
    },

    openRename(profile) {
      const { maxLength } = this.rule();
      openModal(t('profile.rename_title'), `
        <div class="cfg-form">
          <p class="cfg-hint">${esc(t('profile.rename_hint'))}</p>
          <input class="library-input wide" id="accountProfileRenameInput" maxlength="${maxLength}"
                 pattern="[A-Za-z0-9_]+" spellcheck="false" autocomplete="off" value="${esc(profile.name)}">
          <div class="profiles-name-hint" id="accountProfileRenameHint"></div>
          <div class="cfg-actions">
            <button class="secondary-btn" id="accountProfileRenameCancel">${esc(t('common.cancel'))}</button>
            <button class="primary-btn" id="accountProfileRenameConfirm" disabled>${esc(t('common.confirm'))}</button>
          </div>
        </div>`, 'edit');

      const input = $('accountProfileRenameInput');
      const hint = $('accountProfileRenameHint');
      const confirm = $('accountProfileRenameConfirm');

      const refresh = () => {
        const value = (input?.value || '').trim();
        const result = this.validateName(value, profile.name);
        if (confirm) confirm.disabled = !result.ok;
        if (!hint) return;
        if (result.ok) {
          hint.textContent = '';
          hint.classList.remove('is-error');
        } else {
          hint.textContent = value ? result.message : t('profile.name_required');
          hint.classList.add('is-error');
        }
      };

      input?.addEventListener('input', refresh);
      refresh();

      $('accountProfileRenameCancel')?.addEventListener('click', closeModal);
      confirm?.addEventListener('click', () => {
        const value = (input?.value || '').trim();
        const result = this.validateName(value, profile.name);
        if (!result.ok) {
          toast(result.message);
          return;
        }
        closeModal();
        post({ type: 'accountProfileRename', name: profile.name, newName: value });
      });
      input?.focus();
      input?.select();
    },

    confirmDelete(profile) {
      openModal(t('profile.delete'), `
        <div class="cfg-form">
          <p class="cfg-hint">${esc(t('profile.confirm_delete', { name: profile.name }))}</p>
          <div class="cfg-actions">
            <button class="secondary-btn" id="accountProfileDeleteCancel">${esc(t('common.cancel'))}</button>
            <button class="danger-btn" id="accountProfileDeleteConfirm">${esc(t('profile.delete'))}</button>
          </div>
        </div>`, 'delete');

      $('accountProfileDeleteCancel')?.addEventListener('click', closeModal);
      $('accountProfileDeleteConfirm')?.addEventListener('click', () => {
        closeModal();
        post({ type: 'accountProfileDelete', name: profile.name });
      });
    }
  };

  window.AccountProfilesModule = AccountProfilesModule;
})();
