// gamelib.js - 游戏目录多位置管理（设置页档案列表 + 系统搜索 + 模组页当前游戏选择器）
(function () {
  const EDITIONS = [
    { value: 'cn', label: '国服' },
    { value: 'global', label: '国际服' },
    { value: 'taptap', label: 'TapTap' },
    { value: 'custom', label: '自定义' }
  ];

  const GameLibModule = {
    data: { profiles: [], activeId: null, drives: [] },
    bound: false,
    scanRunning: false,

    init() {
      this.bindOnce();
      // 设置页 / 模组页首次进入时都会调用；档案数据由宿主在 ready 时推送，这里兜底再要一次
      if (!this.data.profiles.length) post({ type: 'gameProfilesGet' });
      else this.renderProfiles(this.data);
    },

    bindOnce() {
      if (this.bound) return;
      this.bound = true;

      $('gameProfileBrowseBtn')?.addEventListener('click', () => post({ type: 'gameProfileBrowse' }));
      $('gameScanBtn')?.addEventListener('click', () => this.startQuickScan());
      $('gameScanDeepBtn')?.addEventListener('click', () => this.startDeepScan());
      $('gameScanCancelBtn')?.addEventListener('click', () => post({ type: 'gameScanCancel' }));
      $('modGamePickerManageBtn')?.addEventListener('click', () => showPage('settings'));

      // 模组页顶部「当前游戏」下拉
      $('modGameSelect')?.addEventListener('change', event => {
        const id = event.target.value;
        if (id) post({ type: 'gameProfileSetActive', id });
      });

      // 档案列表用事件委托（行是动态生成的）
      $('gameProfileList')?.addEventListener('click', event => this.onListClick(event));
      $('gameProfileList')?.addEventListener('change', event => this.onListChange(event));
      $('gameScanResults')?.addEventListener('click', event => this.onScanResultsClick(event));
    },

    // ---------- 渲染档案 ----------
    renderProfiles(payload) {
      this.data = payload || { profiles: [], activeId: null, drives: [] };
      this.renderList();
      this.renderModSelect();
      this.renderLobbySelect();
      this.renderDriveSelect();
    },

    renderList() {
      const container = $('gameProfileList');
      const profiles = this.data.profiles || [];
      const countEl = $('gameProfileCount');
      if (countEl) countEl.textContent = profiles.length ? `${profiles.length} 个位置` : '';
      if (!container) return;
      if (!profiles.length) {
        container.innerHTML = '<div class="game-profile-empty">还没有添加游戏目录。<br>用右侧「浏览添加」手动选，或「扫描系统」自动找。</div>';
        return;
      }
      container.innerHTML = profiles.map(p => this.profileRow(p)).join('');
    },

    profileRow(p) {
      const editionOptions = EDITIONS.map(e =>
        `<option value="${e.value}"${e.value === p.edition ? ' selected' : ''}>${e.label}</option>`).join('');
      const warn = !p.directoryExists
        ? '<span class="game-profile-warn" title="这个目录已经不在了">⚠ 目录已失效</span>'
        : (!p.valid ? '<span class="game-profile-warn" title="没检测到标准 Unity 游戏结构（缺 *_Data 或 UnityPlayer.dll）">⚠ 结构可疑</span>' : '');
      return `
        <div class="game-profile-row${p.active ? ' is-active' : ''}${!p.directoryExists ? ' is-broken' : ''}" data-id="${esc(p.id)}">
          <div class="game-profile-main">
            <div class="game-profile-line1">
              <select class="game-profile-edition" data-role="edition" aria-label="区服">${editionOptions}</select>
              <input class="game-profile-label" data-role="label" value="${esc(p.label)}" spellcheck="false" aria-label="游戏名称">
              ${p.active ? '<span class="game-profile-active-tag">当前</span>' : ''}
              ${warn}
            </div>
            <div class="game-profile-path" title="${esc(p.directory)}">${esc(p.directory)}${p.exeName ? ` · ${esc(p.exeName)}` : ''}</div>
          </div>
          <div class="game-profile-actions">
            ${p.active ? '' : '<button class="secondary-btn game-profile-btn" data-role="activate">设为当前</button>'}
            <button class="secondary-btn game-profile-btn" data-role="open" title="在资源管理器中打开">打开</button>
            <button class="danger-btn game-profile-btn" data-role="remove" title="从列表移除（不会删除游戏文件）">移除</button>
          </div>
        </div>`;
    },

    onListClick(event) {
      const row = event.target.closest('.game-profile-row');
      const button = event.target.closest('button[data-role]');
      if (!row || !button) return;
      const id = row.dataset.id;
      switch (button.dataset.role) {
        case 'activate': post({ type: 'gameProfileSetActive', id }); break;
        case 'open': post({ type: 'gameProfileOpen', id }); break;
        case 'remove': post({ type: 'gameProfileRemove', id }); break;
      }
    },

    onListChange(event) {
      const row = event.target.closest('.game-profile-row');
      if (!row) return;
      const role = event.target.dataset.role;
      if (role !== 'label' && role !== 'edition') return;
      const id = row.dataset.id;
      const label = row.querySelector('[data-role="label"]')?.value?.trim() || '';
      const edition = row.querySelector('[data-role="edition"]')?.value || 'custom';
      post({ type: 'gameProfileUpdate', id, label, edition });
    },

    // ---------- 模组页当前游戏下拉 ----------
    renderModSelect() {
      const select = $('modGameSelect');
      const profiles = this.data.profiles || [];
      if (select) {
        if (!profiles.length) {
          select.innerHTML = '<option value="">未添加游戏 —— 去设置页添加</option>';
          select.disabled = true;
        } else {
          select.disabled = false;
          select.innerHTML = profiles.map(p =>
            `<option value="${esc(p.id)}"${p.active ? ' selected' : ''}>[${esc(p.editionLabel)}] ${esc(p.label)}</option>`).join('');
        }
      }
      const pathEl = $('modGamePickerPath');
      if (pathEl) {
        const active = profiles.find(p => p.active);
        pathEl.textContent = active ? active.directory : '';
        pathEl.title = active ? active.directory : '';
      }
    },

    // ---------- 大厅「启动版本」下拉（首页启动区，选中即设为当前游戏） ----------
    renderLobbySelect() {
      const select = $('lobbyGameSelect');
      if (!select) return;
      const profiles = this.data.profiles || [];
      if (!profiles.length) {
        select.innerHTML = '<option value="">未添加游戏 —— 去设置页添加</option>';
        select.disabled = true;
        return;
      }
      select.disabled = false;
      select.innerHTML = profiles.map(p =>
        `<option value="${esc(p.id)}"${p.active ? ' selected' : ''}>[${esc(p.editionLabel)}] ${esc(p.label)}</option>`).join('');
    },

    renderDriveSelect() {
      const select = $('gameScanDriveSelect');
      if (!select) return;
      const drives = this.data.drives || [];
      const prev = select.value;
      select.innerHTML = drives.map(d => `<option value="${esc(d)}">${esc(d)}</option>`).join('');
      if (prev && drives.includes(prev)) select.value = prev;
    },

    // ---------- 系统搜索 ----------
    startQuickScan() {
      $('gameScanPanel')?.classList.remove('hidden');
      $('gameScanResults') && ($('gameScanResults').innerHTML = '');
      post({ type: 'gameScanQuick' });
    },

    startDeepScan() {
      const drive = $('gameScanDriveSelect')?.value;
      if (!drive) { toast('请选择一个要深度扫描的盘符。'); return; }
      post({ type: 'gameScanDeep', drive });
    },

    renderScanProgress(payload) {
      if (!payload) return;
      const statusEl = $('gameScanStatus');
      if (statusEl) {
        let text = payload.message || '';
        if (payload.current) text += `\n${payload.current}`;
        statusEl.textContent = text;
      }
      // 深扫进行中：显示取消、禁用开始
      const running = payload.mode === 'deep' && payload.running;
      this.scanRunning = running;
      $('gameScanCancelBtn')?.classList.toggle('hidden', !running);
      const deepBtn = $('gameScanDeepBtn');
      if (deepBtn) deepBtn.disabled = running;
    },

    renderScanResult(payload) {
      this.scanRunning = false;
      $('gameScanCancelBtn')?.classList.add('hidden');
      const deepBtn = $('gameScanDeepBtn');
      if (deepBtn) deepBtn.disabled = false;

      const container = $('gameScanResults');
      if (!container) return;
      const candidates = payload?.candidates || [];
      const statusEl = $('gameScanStatus');
      if (statusEl) {
        statusEl.textContent = candidates.length
          ? `找到 ${candidates.length} 个候选：`
          : (payload?.mode === 'deep' ? '深度扫描没找到 AstralParty*.exe。' : '快速扫描没找到，试试下面的深度扫描。');
      }
      container.innerHTML = candidates.map(c => this.candidateRow(c)).join('');
    },

    candidateRow(c) {
      const srcLabel = { steam: 'Steam', registry: '注册表', common: '常见位置', deep: '深度扫描' }[c.source] || '';
      const validTag = c.valid
        ? '<span class="game-cand-tag ok">Unity 结构 ✓</span>'
        : '<span class="game-cand-tag warn" title="没检测到标准 Unity 结构">结构可疑</span>';
      return `
        <div class="game-cand-row" data-dir="${esc(c.directory)}">
          <div class="game-cand-main">
            <div class="game-cand-line1">
              <span class="game-cand-edition">[${esc(c.editionLabel)}]</span>
              <span class="game-cand-exe">${esc(c.exeName || '')}</span>
              ${validTag}
              ${srcLabel ? `<span class="game-cand-src">${srcLabel}</span>` : ''}
            </div>
            <div class="game-cand-path" title="${esc(c.directory)}">${esc(c.directory)}</div>
          </div>
          ${c.alreadyAdded
            ? '<span class="game-cand-added">已添加</span>'
            : '<button class="secondary-btn game-cand-add" data-role="add" data-activate="1">添加</button>'}
        </div>`;
    },

    onScanResultsClick(event) {
      const button = event.target.closest('button[data-role="add"]');
      if (!button) return;
      const row = event.target.closest('.game-cand-row');
      const directory = row?.dataset.dir;
      if (directory) post({ type: 'gameProfileAddPath', directory, activate: button.dataset.activate === '1' });
    }
  };

  window.GameLibModule = GameLibModule;
})();
