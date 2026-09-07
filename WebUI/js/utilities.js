// utilities.js - 游戏工具页（变速器安装/卸载 + speedhack_config.json 配置编辑器）
(function () {
  const VK_GROUPS = [
    ['修饰键', [['VK_CONTROL', 'Ctrl'], ['VK_SHIFT', 'Shift'], ['VK_MENU', 'Alt'], ['VK_LWIN', 'Win'],
      ['VK_LCONTROL', '左Ctrl'], ['VK_RCONTROL', '右Ctrl'], ['VK_LSHIFT', '左Shift'], ['VK_RSHIFT', '右Shift'],
      ['VK_LMENU', '左Alt'], ['VK_RMENU', '右Alt']]],
    ['字母', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'.split('').map(c => ['VK_' + c, c])],
    ['数字', Array.from({ length: 10 }, (_, i) => ['VK_' + i, String(i)])],
    ['功能键', Array.from({ length: 12 }, (_, i) => ['VK_F' + (i + 1), 'F' + (i + 1)])],
    ['常用', [['VK_SPACE', '空格'], ['VK_TAB', 'Tab'], ['VK_RETURN', '回车'], ['VK_ESCAPE', 'Esc'],
      ['VK_BACK', '退格'], ['VK_DELETE', 'Del'], ['VK_INSERT', 'Ins'], ['VK_HOME', 'Home'], ['VK_END', 'End'],
      ['VK_PRIOR', 'PgUp'], ['VK_NEXT', 'PgDn'], ['VK_UP', '↑'], ['VK_DOWN', '↓'], ['VK_LEFT', '←'], ['VK_RIGHT', '→']]]
  ];
  const VK_LABELS = Object.fromEntries(VK_GROUPS.flatMap(([, list]) => list));

  const UtilitiesModule = {
    status: null,
    cfg: null,
    dirty: false,
    picker: null, // { target: 'reload' | 'state:<idx>', keys: [] }

    // ---------- 生命周期 ----------

    init() {
      this.bindEvents();
    },

    onShowUtilities() {
      this.refreshStatus();
    },

    bindEvents() {
      $('utilsBackHomeBtn')?.addEventListener('click', () => showPage('home'));
      $('utilsInstallBtn')?.addEventListener('click', () => {
        this.saveConfig('install');
      });
      $('utilsUninstallBtn')?.addEventListener('click', () => {
        post({ type: 'speedhackUninstall', force: $('utilsForceUninstallCheck')?.checked === true });
      });
      $('utilsSyncConfigBtn')?.addEventListener('click', () => post({ type: 'speedhackPushConfig' }));
      $('utilsDetectDirBtn')?.addEventListener('click', () => post({ type: 'speedhackDetect' }));
      $('utilsBrowseDirBtn')?.addEventListener('click', () => post({ type: 'speedhackBrowse' }));
      $('utilsEditJsonBtn')?.addEventListener('click', () => post({ type: 'speedhackEditConfig' }));
      $('utilsRestoreConfigBtn')?.addEventListener('click', () => {
        this.dirty = false;
        post({ type: 'speedhackResetConfig' });
      });
      $('utilsSaveConfigBtn')?.addEventListener('click', () => this.saveConfig());

      // 打开游戏目录
      $('utilsGameDirText')?.addEventListener('dblclick', () => post({ type: 'speedhackOpenGameDir' }));

      // 自动倍速 / 基础字段
      $('utilsAutoSpeedToggle')?.addEventListener('change', e => {
        if (!this.cfg) return;
        this.cfg.autoSpeedEnabled = e.target.checked;
        if (e.target.checked && this.cfg.baseSpeed <= 1) this.cfg.baseSpeed = 2;
        $('utilsBaseSpeed').disabled = !e.target.checked;
        $('utilsBaseSpeed').value = String(this.cfg.baseSpeed);
        document.querySelectorAll('.auto-speed-preset').forEach(button => button.disabled = !e.target.checked);
        this.markDirty();
      });
      document.querySelectorAll('.auto-speed-preset').forEach(button => {
        button.addEventListener('click', () => {
          if (!this.cfg || !this.cfg.autoSpeedEnabled) return;
          this.cfg.baseSpeed = parseFloat(button.dataset.speed);
          $('utilsBaseSpeed').value = String(this.cfg.baseSpeed);
          this.markDirty();
        });
      });
      $('utilsBaseSpeed')?.addEventListener('input', e => {
        if (!this.cfg) return;
        const value = parseFloat(e.target.value);
        if (!Number.isNaN(value) && value > 0) { this.cfg.baseSpeed = value; this.markDirty(); }
      });
      $('utilsConsoleToggle')?.addEventListener('change', e => {
        if (!this.cfg) return;
        this.cfg.console = e.target.checked;
        this.markDirty();
      });
      $('utilsWaitMs')?.addEventListener('input', e => {
        if (!this.cfg) return;
        const value = parseFloat(e.target.value);
        if (!Number.isNaN(value) && value >= 0) { this.cfg.waitMs = value; this.markDirty(); }
      });

      // 启动加速
      const startupToggle = $('utilsStartupToggle');
      startupToggle?.addEventListener('change', e => {
        if (!this.cfg) return;
        this.cfg.startup.enabled = e.target.checked;
        $('utilsStartupSpeed').disabled = !e.target.checked;
        $('utilsStartupDuration').disabled = !e.target.checked;
        this.markDirty();
      });
      $('utilsStartupSpeed')?.addEventListener('input', e => {
        if (!this.cfg) return;
        const value = parseFloat(e.target.value);
        if (!Number.isNaN(value) && value > 0) { this.cfg.startup.speed = value; this.markDirty(); }
      });
      $('utilsStartupDuration')?.addEventListener('input', e => {
        if (!this.cfg) return;
        const value = parseInt(e.target.value, 10);
        if (!Number.isNaN(value) && value > 0) { this.cfg.startup.durationSecs = value; this.markDirty(); }
      });

      // 档位列表事件委托（行重建后无需重新绑定）
      const rows = $('utilsStateRows');
      rows?.addEventListener('click', e => {
        if (!this.cfg) return;
        const del = e.target.closest('.state-del-btn');
        if (del) {
          this.cfg.states.splice(Number(del.dataset.idx), 1);
          this.markDirty();
          this.renderRows();
          return;
        }
        const preset = e.target.closest('.speed-preset-btn');
        if (preset) {
          const row = this.cfg.states[Number(preset.dataset.idx)];
          if (!row) return;
          row.speed = parseFloat(preset.dataset.speed);
          const input = rows.querySelector(`.state-speed-input[data-idx="${preset.dataset.idx}"]`);
          if (input) input.value = String(row.speed);
          this.markDirty();
          return;
        }
        const addKey = e.target.closest('.util-add-chip-btn[data-target]');
        if (addKey) { this.openKeyPicker(addKey.dataset.target); return; }
        const removeChip = e.target.closest('.key-remove');
        if (removeChip && removeChip.dataset.target?.startsWith('state:')) {
          const idx = Number(removeChip.dataset.target.split(':')[1]);
          const state = this.cfg.states[idx];
          if (!state) return;
          state.keys = state.keys.filter(k => k !== removeChip.dataset.vk);
          this.renderRows();
          this.markDirty();
        }
      });
      rows?.addEventListener('input', e => {
        if (!this.cfg) return;
        const speedInput = e.target.closest('.state-speed-input');
        if (speedInput) {
          const state = this.cfg.states[Number(speedInput.dataset.idx)];
          if (!state) return;
          const value = parseFloat(speedInput.value);
          if (!Number.isNaN(value) && value > 0) { state.speed = value; this.markDirty(); }
        }
      });
      rows?.addEventListener('change', e => {
        if (!this.cfg) return;
        const toggle = e.target.closest('.state-toggle-input');
        if (toggle) {
          const state = this.cfg.states[Number(toggle.dataset.idx)];
          if (!state) return;
          state.isToggle = toggle.checked;
          this.renderRows();
          this.markDirty();
        }
      });

      // 重载热键区
      $('utilsReloadKeysArea')?.addEventListener('click', e => {
        const remove = e.target.closest('.key-remove[data-vk]');
        if (remove && remove.dataset.target === 'reload') {
          if (!this.cfg) return;
          this.cfg.reloadKeys = this.cfg.reloadKeys.filter(k => k !== remove.dataset.vk);
          this.renderReloadKeys();
          this.markDirty();
        }
      });
      $('utilsReloadKeysAddBtn')?.addEventListener('click', () => this.openKeyPicker('reload'));

      $('utilsAddStateBtn')?.addEventListener('click', () => {
        if (!this.cfg) return;
        this.cfg.states.push({ keys: ['VK_CONTROL'], speed: 2, isToggle: true });
        this.renderRows();
        this.markDirty();
      });

      // 按键选择器（复用全局弹窗）
      $('globalModal')?.addEventListener('click', e => {
        if (!this.picker) return;
        const pick = e.target.closest('.key-pick-btn');
        if (pick) {
          const vk = pick.dataset.vk;
          const index = this.picker.keys.indexOf(vk);
          if (index >= 0) this.picker.keys.splice(index, 1);
          else this.picker.keys.push(vk);
          pick.classList.toggle('on', index < 0);
          return;
        }
        if (e.target.closest('#keyPickerCancel')) { closeModal(); this.picker = null; return; }
        if (e.target.closest('#keyPickerConfirm')) {
          this.applyPickedKeys(this.picker.target, this.picker.keys);
          this.picker = null;
          closeModal();
        }
      });
    },

    // ---------- 状态刷新 ----------

    refreshStatus() {
      post({ type: 'speedhackStatus' });
    },

    applyStatus(payload) {
      this.status = payload.status || payload;
      this.renderStatus();
      // 配置加载：仅在无未保存修改时覆盖编辑器
      if (!this.dirty) this.loadConfigIntoEditor(payload.config);
      else if (payload.configBroken) {
        this.dirty = false;
        this.loadConfigIntoEditor(payload.config);
      }
    },

    renderStatus() {
      const status = this.status;
      if (!status) return;
      const badge = $('utilsStatusBadge');
      const title = $('utilsStatusTitle');
      const icon = $('utilsStatusIcon');
      const text = $('utilsStatusText');
      const badgeText = $('utilsConfigStateBadge');

      const installedOk = status.installed && status.dllMatchesBundle;
      if (!status.bundleDllPresent || !status.templateConfigPresent) {
        badge.textContent = '缺少内置文件';
        badge.className = 'badge badge-notice';
        title.textContent = '程序缺少变速器文件';
      } else if (status.gameRunning) {
        badge.textContent = '游戏运行中';
        badge.className = 'badge badge-notice';
        title.textContent = '游戏正在运行';
      } else if (installedOk) {
        badge.textContent = '已安装';
        badge.className = 'badge badge-update';
        title.textContent = '变速器已就位';
      } else if (status.installed) {
        badge.textContent = '文件不一致';
        badge.className = 'badge badge-notice';
        title.textContent = '目录里是其它 version.dll';
      } else {
        badge.textContent = '未安装';
        badge.className = 'badge badge-event';
        title.textContent = '尚未安装到游戏';
      }
      icon.textContent = status.gameRunning ? '⏳' : (installedOk ? '⚡' : status.installed ? '⚠️' : '🔍');
      text.textContent = status.message || '';
      if ($('utilsConfigPathText')) $('utilsConfigPathText').textContent =
        status.profileConfigPath ? `主配置：${status.profileConfigPath}` : '';

      // 游戏目录
      const dirText = $('utilsGameDirText');
      if (dirText) {
        if (status.gameDirectory) {
          dirText.textContent = status.gameDirectory;
          dirText.classList.remove('placeholder');
          dirText.title = '双击在资源管理器中打开';
        } else {
          dirText.textContent = '尚未选择游戏目录（可点「自动检测」）';
          dirText.classList.add('placeholder');
        }
      }

      // 元信息列表
      const meta = $('utilsMetaList');
      if (meta) {
        const lines = [];
        const dot = (cls, t) => `<span class="util-meta-dot ${cls}"></span><span>${t}</span>`;
        if (status.gameRunning) {
          lines.push(dot('warn', '检测到游戏正在运行——安装 / 卸载前请先完全退出游戏'));
        } else {
          lines.push(dot('ok', '游戏进程未运行，可以安全安装 / 卸载'));
        }
        if (status.gameExeFound) lines.push(dot('ok', '目录内检测到游戏程序 AstralParty.exe / AstralParty_CN.exe'));
        else if (status.gameDirectory) lines.push(dot('warn', '目录内未找到 AstralParty 游戏程序（仍可安装，但请确认目录正确）'));
        else lines.push(dot('bad', '尚未定位游戏目录'));
        if (status.bundleDllPresent) {
          const hash = status.bundleHash ? status.bundleHash.slice(0, 12).toLowerCase() : '';
          lines.push(dot('info', `内置文件就绪：version.dll ${status.templateConfigPresent ? '· speedhack_config.json' : ''}`));
          if (hash) lines.push(dot('info', `内置 SHA-256 ${hash}…（卸载时按此校验，防止误删他人文件）`));
        }
        if (status.gameDirectory) {
          if (status.dllPresent) {
            lines.push(status.dllMatchesBundle
              ? dot('ok', '游戏目录 version.dll 与内置一致')
              : dot('warn', '游戏目录 version.dll 与内置不一致——可能来自其它工具'));
          } else {
            lines.push(dot('info', '游戏目录里暂无 version.dll，可直接安装'));
          }
          lines.push(status.configPresent
            ? dot('ok', '游戏目录 speedhack_config.json 存在')
            : dot('info', '游戏目录暂无 speedhack_config.json'));
        }
        meta.innerHTML = lines.map(l => `<li>${l}</li>`).join('');
      }

      // 按钮可用性
      const dirReady = !!status.gameDirectory && !status.gameRunning;
      $('utilsInstallBtn').disabled = !status.bundleDllPresent || !dirReady;
      $('utilsSyncConfigBtn').disabled = !status.installed || status.gameRunning;
      $('utilsUninstallBtn').disabled = !dirReady;
      $('utilsEditJsonBtn').disabled = !status.templateConfigPresent;
      if (!status.gameDirectory) $('utilsSyncConfigBtn').disabled = true;
    },

    // ---------- 配置编辑器 ----------

    loadConfigIntoEditor(config) {
      if (!config) {
        this.cfg = this.defaultEditorConfig();
      } else {
        this.cfg = {
          console: !!config.console,
          autoSpeedEnabled: numberOr(config.baseSpeed, 1) !== 1,
          baseSpeed: numberOr(config.baseSpeed, 1),
          waitMs: numberOr(config.waitMs, 250),
          startup: {
            enabled: !!config.startup?.enabled,
            speed: numberOr(config.startup?.speed, 10),
            durationSecs: numberOr(config.startup?.durationSecs, 5)
          },
          reloadKeys: Array.isArray(config.reloadKeys) ? [...config.reloadKeys] : [],
          states: Array.isArray(config.speedStates) && config.speedStates.length
            ? config.speedStates.map(s => ({
                keys: Array.isArray(s.keys) ? [...s.keys] : [],
                speed: numberOr(s.speed, 2),
                isToggle: !!s.isToggle
              }))
            : [{ keys: ['VK_CONTROL'], speed: 2, isToggle: true }]
        };
      }
      const badge = $('utilsConfigStateBadge');
      if (this.status?.configBroken) {
        badge.textContent = '解析失败';
        badge.className = 'badge badge-notice';
      } else {
        badge.textContent = '已同步';
        badge.className = 'badge badge-update';
      }
      this.renderAll();
    },

    defaultEditorConfig() {
      return {
        console: false,
        autoSpeedEnabled: true,
        baseSpeed: 2,
        waitMs: 250,
        startup: { enabled: false, speed: 10, durationSecs: 5 },
        reloadKeys: ['VK_CONTROL', 'VK_SHIFT', 'VK_R'],
        states: [{ keys: ['VK_CONTROL'], speed: 2, isToggle: true }]
      };
    },

    renderAll() {
      const cfg = this.cfg;
      if (!cfg) return;
      $('utilsAutoSpeedToggle').checked = cfg.autoSpeedEnabled;
      $('utilsBaseSpeed').value = String(cfg.baseSpeed);
      $('utilsBaseSpeed').disabled = !cfg.autoSpeedEnabled;
      document.querySelectorAll('.auto-speed-preset').forEach(button => button.disabled = !cfg.autoSpeedEnabled);
      $('utilsConsoleToggle').checked = cfg.console;
      $('utilsWaitMs').value = String(Math.round(cfg.waitMs));
      const startupSpeed = $('utilsStartupSpeed');
      startupSpeed.value = String(cfg.startup.speed);
      startupSpeed.disabled = !cfg.startup.enabled;
      $('utilsStartupDuration').value = String(cfg.startup.durationSecs);
      $('utilsStartupDuration').disabled = !cfg.startup.enabled;
      $('utilsStartupToggle').checked = cfg.startup.enabled;
      this.renderRows();
      this.renderReloadKeys();
    },

    renderRows() {
      const container = $('utilsStateRows');
      if (!container || !this.cfg) return;
      const html = this.cfg.states.map((state, idx) => `
        <div class="util-state-row" data-idx="${idx}">
          <div class="util-state-row-head">
            <span class="util-state-title">档位 ${idx + 1}${state.isToggle ? ' · 点按切换' : ' · 按住生效'}</span>
            <button class="state-del-btn" data-idx="${idx}" title="删除该档位">✕</button>
          </div>
          <div class="util-keys-area">
            ${this.chipsHtml(state.keys, `state:${idx}`)}
            <button class="util-add-chip-btn" data-target="state:${idx}">＋ 按键</button>
          </div>
          <div class="util-state-speed-row">
            <div class="speed-presets">
              <button class="speed-preset-btn" data-idx="${idx}" data-speed="1">×1</button>
              <button class="speed-preset-btn" data-idx="${idx}" data-speed="2">×2</button>
              <button class="speed-preset-btn" data-idx="${idx}" data-speed="5">×5</button>
              <button class="speed-preset-btn" data-idx="${idx}" data-speed="10">×10</button>
            </div>
            <label style="display:flex;align-items:center;gap:6px;font-size:12.5px;color:var(--gp-text-sub)">
              倍速
              <input type="number" class="util-input state-speed-input" data-idx="${idx}"
                     value="${state.speed}" min="0.1" step="0.5" style="width:84px">
            </label>
            <label class="util-state-toggle">
              <input type="checkbox" class="state-toggle-input" data-idx="${idx}" ${state.isToggle ? 'checked' : ''}>
              点按切换（不勾选 = 按住才生效）
            </label>
          </div>
        </div>`).join('');
      container.innerHTML = html || '<div class="util-empty-hint">还没有倍速档位——点下面「＋ 添加倍速档位」创建一个，例如 Ctrl = 2 倍速。</div>';
    },

    renderReloadKeys() {
      const area = $('utilsReloadKeysArea');
      if (!area || !this.cfg) return;
      const chips = this.chipsHtml(this.cfg.reloadKeys, 'reload');
      area.innerHTML = chips +
        `<button class="util-add-chip-btn" id="utilsReloadKeysAddBtn">＋ 按键</button>`;
      $('utilsReloadKeysAddBtn')?.addEventListener('click', () => this.openKeyPicker('reload'));
    },

    chipsHtml(keys, target) {
      if (!keys.length) return '<span class="key-chip empty-chip">未设置按键</span>';
      return keys.map(vk => `
        <span class="key-chip">${esc(VK_LABELS[vk] || vk)}
          <span class="key-remove" data-vk="${esc(vk)}" data-target="${target}" title="移除">×</span>
        </span>`).join('');
    },

    // ---------- 按键选择 ----------

    openKeyPicker(target) {
      if (!this.cfg) return;
      const existing = target === 'reload'
        ? [...this.cfg.reloadKeys]
        : [...(this.cfg.states[Number(target.split(':')[1])]?.keys || [])];
      this.picker = { target, keys: existing };
      const grid = VK_GROUPS.map(([group, list]) =>
        `<div class="key-picker-group-name">${esc(group)}</div>` +
        list.map(([vk]) =>
          `<button class="key-pick-btn ${existing.includes(vk) ? 'on' : ''}" data-vk="${vk}">${esc(VK_LABELS[vk] || vk)}</button>`
        ).join('')).join('');
      openModal(`选择按键组合 · ${target === 'reload' ? '重载配置热键' : `档位 ${Number(target.split(':')[1]) + 1}`}`, `
        <p style="margin:0 0 10px;font-size:12.5px;color:var(--gp-text-sub)">点击选择 / 取消选择，确认后应用到该档位。档位按键可多选（如 Ctrl + Shift）。</p>
        <div class="key-picker-grid">${grid}</div>
        <div style="display:flex;justify-content:flex-end;gap:10px;margin-top:16px">
          <button class="secondary-btn" id="keyPickerCancel" type="button">取消</button>
          <button class="primary-btn" id="keyPickerConfirm" type="button">确定（已选 ${existing.length} 个）</button>
        </div>`);
      // 同步已选数量文案
      const counter = $('keyPickerConfirm');
      $('globalModal')?.querySelectorAll('.key-pick-btn').forEach(btn => {
        btn.addEventListener('click', () => {
          if (!this.picker) return;
          counter.textContent = `确定（已选 ${this.picker.keys.length} 个）`;
        });
      });
    },

    applyPickedKeys(target, keys) {
      if (target === 'reload') {
        this.cfg.reloadKeys = [...keys];
        this.renderReloadKeys();
      } else {
        const idx = Number(target.split(':')[1]);
        if (this.cfg.states[idx]) {
          this.cfg.states[idx].keys = [...keys];
          this.renderRows();
        }
      }
      this.markDirty();
    },

    markDirty() {
      this.dirty = true;
      const badge = $('utilsConfigStateBadge');
      if (badge) {
        badge.textContent = '未保存修改';
        badge.className = 'badge badge-event';
      }
    },

    saveConfig(afterSave) {
      if (!this.cfg) return;
      const model = {
        console: this.cfg.console,
        baseSpeed: this.cfg.autoSpeedEnabled ? this.cfg.baseSpeed : 1,
        waitMs: Math.round(this.cfg.waitMs),
        startup: this.cfg.startup.enabled ? {
          enabled: true,
          speed: this.cfg.startup.speed,
          durationSecs: Math.max(1, Math.round(this.cfg.startup.durationSecs))
        } : { enabled: false, speed: this.cfg.startup.speed, durationSecs: this.cfg.startup.durationSecs },
        reloadKeys: this.cfg.reloadKeys,
        speedStates: this.cfg.states.map(s => ({
          keys: s.keys,
          speed: s.speed,
          isToggle: s.isToggle
        }))
      };
      this.dirty = false;
      post({ type: 'speedhackSaveConfig', config: model, afterSave: afterSave || null,
        overwriteDll: $('utilsOverwriteCheck')?.checked === true });
    }
  };

  function numberOr(value, fallback) {
    const number = Number(value);
    return Number.isFinite(number) && number > 0 ? number : fallback;
  }

  // 页面标记存在即完成事件绑定（index.html 脚本位于 DOM 末尾）
  if (document.getElementById('utilitiesView')) {
    UtilitiesModule.init();
  }

  window.UtilitiesModule = UtilitiesModule;
})();
