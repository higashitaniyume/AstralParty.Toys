// utilities/status.js - Utilities lifecycle, event binding and Speedhack status rendering
(function () {
  'use strict';
  const UtilitiesModule = window.UtilitiesModule = {
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
      // 兼容性警告里的"去模组页面"——用户想要新功能(含热键变速)时引导到模组页
      $('utilsGoModsBtn')?.addEventListener('click', () => showPage('mods'));
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
      // configBroken 是 payload 的兄弟字段，不在 status 里；之前 loadConfigIntoEditor
      // 读 this.status?.configBroken 永远是 undefined，"解析失败"徽章成了死代码
      this.configBroken = !!payload.configBroken;
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
      const running = !!status.gameRunning;
      if (!status.bundleDllPresent || !status.templateConfigPresent) {
        badge.textContent = '缺少内置文件';
        badge.className = 'badge badge-notice';
        title.textContent = '程序缺少变速器文件';
      } else if (installedOk) {
        badge.textContent = running ? '已安装 · 游戏运行中' : '已安装';
        badge.className = 'badge badge-update';
        title.textContent = running
          ? '变速器已就位；游戏正在运行，新写入的文件要重启游戏才加载'
          : '变速器已就位';
      } else if (status.installed) {
        badge.textContent = '文件不一致';
        badge.className = 'badge badge-notice';
        title.textContent = '目录里是其它 version.dll';
      } else {
        badge.textContent = running ? '未安装 · 游戏运行中' : '未安装';
        badge.className = 'badge badge-event';
        title.textContent = running
          ? '尚未安装；游戏正在运行，本次安装重启游戏后才加载'
          : '尚未安装到游戏';
      }
      icon.innerHTML = Icons.svg(installedOk ? 'bolt' : (status.installed ? 'warning' : (running ? 'hourglass_empty' : 'search')));
      text.textContent = status.message || '';
      if ($('utilsConfigPathText')) $('utilsConfigPathText').textContent =
        status.profileConfigPath
          ? `主配置：${status.profileConfigPath}${status.profileLocationDocuments ? '（文档目录）' : '（自定义数据文件夹）'}`
          : '';

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
          lines.push(dot('warn', '检测到游戏正在运行——安装 / 卸载不会被拦，但先看清下面的后果：'));
          lines.push(dot('warn', `安装：${status.runningInstallHint || '新写入的 version.dll 要重启游戏才会加载，且运行中的游戏可能占用该文件导致覆盖失败。'}`));
          lines.push(dot('warn', `卸载：${status.runningUninstallHint || 'version.dll 已被游戏占用，删除可能失败；重启游戏后才恢复正常速度。'}`));
          if (status.installed) {
            lines.push(dot('warn', `同步配置：${status.runningPushConfigHint || '配置写入后需按重载热键或重启游戏生效。'}`));
          }
        } else {
          lines.push(dot('ok', '游戏进程未运行，安装 / 卸载会立刻生效'));
        }
        if (status.profileLocationNote) {
          lines.push(dot(status.profileLocationDocuments ? 'ok' : 'warn', status.profileLocationNote));
        }
        if (status.gameExeFound) lines.push(dot('ok', '目录内检测到游戏主程序（AstralParty*.exe）'));
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

      // 按钮可用性：游戏在运行只警告，不阻止操作
      const dirReady = !!status.gameDirectory;
      $('utilsInstallBtn').disabled = !status.bundleDllPresent || !dirReady;
      $('utilsSyncConfigBtn').disabled = !status.installed;
      $('utilsUninstallBtn').disabled = !dirReady;
      $('utilsEditJsonBtn').disabled = !status.templateConfigPresent;
      if (!status.gameDirectory) $('utilsSyncConfigBtn').disabled = true;
    },

    // ---------- 配置编辑器 ----------
  };
})();
