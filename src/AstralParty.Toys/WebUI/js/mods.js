// mods.js - 模组管理页（Mod 加载器安装/卸载 + mods\ sdk\ 目录管理 + 配置/权限编辑）
// 设计前提：用户不会手改 JSON —— 所有可配置项都在 UI 内可视化编辑。
(function () {
  const ModsModule = {
    modStatus: null,

    // ---------- 生命周期 ----------

    init() {
      this.bindEvents();
    },

    onShowMods() {
      this.refreshModStatus();
      // 只同步变速栏状态(不弹设置窗口; 弹窗仅在点「⚙ 加载器设置」时出现)
      post({ type: 'modSyncSpeedhack' });
    },

    bindEvents() {
      $('modsBackHomeBtn')?.addEventListener('click', () => showPage('home'));

      // ---------- Mod 加载器 ----------
      $('modInstallBtn')?.addEventListener('click', () => {
        post({
          type: 'modInstall',
          overwriteDll: $('modOverwriteCheck')?.checked === true,
          includeSample: $('modIncludeSampleCheck')?.checked !== false
        });
      });
      $('modUninstallBtn')?.addEventListener('click', () => {
        post({ type: 'modUninstall', force: $('modForceUninstallCheck')?.checked === true });
      });
      $('modDetectDirBtn')?.addEventListener('click', () => post({ type: 'modDetect' }));
      $('modBrowseDirBtn')?.addEventListener('click', () => post({ type: 'modBrowse' }));
      $('modOpenModsBtn')?.addEventListener('click', () => post({ type: 'modOpenModsFolder' }));
      $('modOpenSdkBtn')?.addEventListener('click', () => post({ type: 'modOpenSdkFolder' }));
      $('modOpenLogsBtn')?.addEventListener('click', () => post({ type: 'modOpenLogsFolder' }));
      $('modImportBtn')?.addEventListener('click', () => post({ type: 'modPickImport' }));
      $('modCheckUpdateBtn')?.addEventListener('click', () => post({ type: 'modCheckUpdate' }));
      $('modDownloadUpdateBtn')?.addEventListener('click', () => {
        post({
          type: 'modDownloadUpdate',
          overwriteDll: $('modUpdateOverwriteCheck')?.checked === true
        });
      });
      $('modLoaderSettingsBtn')?.addEventListener('click', () => this.openLoaderSettings());
      // 游戏变速栏（加载器内置功能）
      $('modSpeedhackEnable')?.addEventListener('change', () => {
        const enabled = $('modSpeedhackEnable').checked;
        const label = $('modSpeedhackEnableLabel');
        if (label) label.textContent = enabled ? '变速已启用' : '变速已禁用';
        $('modSpeedhackSpeed').disabled = !enabled;
      });
      $('modSpeedhackSaveBtn')?.addEventListener('click', () => this.saveSpeedhack());
      // 从加载器设置页也可进入变速栏
      $('modGameDirText')?.addEventListener('dblclick', () => {
        const dir = this.modStatus?.gameDirectory;
        if (dir) post({ type: 'speedhackOpenGameDir' });
      });
      // mod 列表删除/配置/权限按钮（事件委托）
      $('modListContainer')?.addEventListener('click', e => {
        const del = e.target.closest('.mod-del-btn');
        if (del) {
          const fileName = del.dataset.file;
          if (fileName && confirm(`确定删除 mod「${fileName}」？`)) {
            post({ type: 'modDelete', fileName });
          }
          return;
        }
        const cfg = e.target.closest('.mod-config-btn');
        if (cfg) {
          const fileName = cfg.dataset.configFile;
          if (fileName) this.openModConfig(fileName);
          return;
        }
      });
      // mod 启用/禁用开关（change 事件）
      $('modListContainer')?.addEventListener('change', e => {
        const toggle = e.target.closest('.mod-toggle input[data-toggle-file]');
        if (toggle) {
          const fileName = toggle.dataset.toggleFile;
          if (fileName) post({ type: 'modToggle', fileName, enabled: toggle.checked });
        }
      });
    },

    // ---------- 状态 ----------

    refreshModStatus() {
      post({ type: 'modStatus' });
    },

    renderModStatus(status) {
      if (!status) return;
      this.modStatus = status;
      const badge = $('modStatusBadge');
      const title = $('modStatusTitle');
      const text = $('modStatusText');
      const icon = $('modStatusIcon');
      const running = status.gameRunning === true;
      const installedOk = status.installed && status.loaderMatchesBundle;
      if (badge) {
        if (!status.bundleLoaderPresent) {
          badge.textContent = '缺少内置文件';
          badge.className = 'badge badge-notice';
          title.textContent = '程序缺少加载器文件（version.dll）';
        } else if (installedOk) {
          badge.textContent = running ? '已安装 · 游戏运行中' : '已安装';
          badge.className = 'badge badge-update';
          title.textContent = running
            ? '加载器已就位；游戏正在运行，本次写入要重启游戏才加载'
            : '加载器已就位';
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
        icon.textContent = installedOk ? '🧩' : (status.installed ? '⚠️' : (running ? '⏳' : '🔍'));
        text.textContent = status.message || '';
      }

      // 游戏目录
      const dirText = $('modGameDirText');
      if (dirText) {
        if (status.gameDirectory) {
          dirText.textContent = status.gameDirectory;
          dirText.classList.remove('placeholder');
          dirText.title = '双击在资源管理器中打开';
        } else {
          dirText.textContent = '尚未选择游戏目录（可点「自动检测」）';
          dirText.classList.add('placeholder');
          dirText.title = '';
        }
      }

      // 版本信息
      const embedded = $('modEmbeddedVersion');
      if (embedded) embedded.textContent = status.embeddedVersion || '—';
      const installed = $('modInstalledVersion');
      if (installed) installed.textContent = status.installedVersion || '—';
      const latest = $('modLatestVersion');
      if (latest) latest.textContent = status.latestVersion || '—';

      this.renderModList($('modListContainer'), status.mods, 'mod');
      this.renderModList($('modSdkContainer'), status.sdk, 'sdk');
    },

    renderModList(container, entries, kind) {
      if (!container) return;
      if (!entries || entries.length === 0) {
        container.innerHTML = '<span class="path-value placeholder">目录为空</span>';
        return;
      }
      const nodes = entries.map(entry => {
        const size = fmtBytes(entry.sizeBytes);
        const time = entry.modifiedUtc ? new Date(entry.modifiedUtc).toLocaleString('zh-CN') : '';
        const delBtn = kind === 'mod'
          ? `<button class="mod-del-btn" data-file="${esc(entry.fileName)}" title="删除该 mod">🗑</button>` : '';
        const title = entry.displayName || entry.name;
        const ver = entry.version ? ` v${esc(entry.version)}` : '';
        const author = entry.author ? ` · ${esc(entry.author)}` : '';
        const sdk = entry.sdkVersion ? ` · SDK ${esc(entry.sdkVersion)}` : '';
        // 警告: 声明了"操作游戏"(GameActions=2)能力的 mod —— 仅提示, 不阻止(权限机制已取消)
        const warning = kind === 'mod' && (entry.permissions & 2)
          ? `<span class="mod-warning" title="此 mod 声明可操作游戏（模拟出牌/掷骰/移动等）。权限机制已取消，仅提示：请确认 mod 来源可信">⚠️ 可操作游戏</span>` : '';
        const deps = kind === 'mod' && entry.dependencies && entry.dependencies.length
          ? ` · 依赖 ${entry.dependencies.map(d => esc(d.id) + (d.minVersion ? '≥' + esc(d.minVersion) : '')).join(', ')}` : '';
        const desc = entry.description ? `<span class="mod-desc">${esc(entry.description)}</span>` : '';
        // 启用/禁用开关 + 配置按钮(仅 mod)
        const controls = kind === 'mod' ? `
          <span class="mod-controls">
            <label class="mod-toggle" title="启用/禁用（重启游戏后生效）">
              <input type="checkbox" data-toggle-file="${esc(entry.fileName)}" ${entry.enabled !== false ? 'checked' : ''}>
              <span>${entry.enabled !== false ? '启用' : '禁用'}</span>
            </label>
            <button class="mod-config-btn" data-config-file="${esc(entry.fileName)}" title="修改 mod 配置">⚙</button>
          </span>` : '';
        return `<div class="mod-list-item${entry.enabled === false ? ' mod-disabled' : ''}">
          <span class="mod-name" title="${esc(entry.fileName)}">${esc(title)}${ver}${author}${sdk}${warning}${deps}</span>
          <span class="mod-meta">${size}${time ? ' · ' + esc(time) : ''}</span>
          ${desc}
          ${controls}
          ${delBtn}
        </div>`;
      });
      container.innerHTML = nodes.join('');
    },

    // ---------- 游戏变速栏（加载器内置功能，非 mod） ----------

    // 用加载器配置填充变速栏状态（开关 + 倍速）
    syncSpeedhackBar(config) {
      if (!config) return;
      const speed = Number(config.speedhackBaseSpeed ?? 1.0);
      const enabled = speed > 0 && Math.abs(speed - 1.0) > 0.001;
      const enableEl = $('modSpeedhackEnable');
      const speedEl = $('modSpeedhackSpeed');
      const labelEl = $('modSpeedhackEnableLabel');
      const hintEl = $('modSpeedhackHint');
      if (enableEl) {
        enableEl.checked = enabled;
        enableEl.disabled = false;
      }
      if (speedEl) {
        speedEl.value = enabled ? speed : (speed === 1.0 ? '2.0' : String(speed));
        speedEl.disabled = !enabled;
      }
      if (labelEl) labelEl.textContent = enabled ? '变速已启用' : '变速已禁用';
      if (hintEl) hintEl.textContent = enabled ? `当前 ${speed}x（重启游戏生效）` : '1.0 = 正常速度';
    },

    saveSpeedhack() {
      const enabled = $('modSpeedhackEnable')?.checked === true;
      const speed = Number($('modSpeedhackSpeed')?.value ?? 2.0);
      if (!(speed > 0 && speed <= 100)) {
        toast('倍速必须在 0.1 ~ 100 之间');
        return;
      }
      const baseSpeed = enabled ? speed : 1.0;
      post({
        type: 'modSaveSpeedhack',
        speedhackBaseSpeed: baseSpeed
      });
    },

    // ---------- 加载器设置弹窗（doorstop_config.json 可视化编辑） ----------

    openLoaderSettings() {
      post({ type: 'modReadLoaderConfig' });
    },

    renderLoaderSettings(config) {
      if (!config) return;
      // 同步变速栏（加载器设置里的基础倍速与页面变速开关一致）
      this.syncSpeedhackBar(config);
      const rows = [
        ['enabled', '启用加载器', 'bool', config.enabled],
        ['speedhackBaseSpeed', '启动时基础倍速（1.0 = 正常；2.0 = 全程 2 倍速；可留 1.0 后由 mod 热键变速）', 'number', config.speedhackBaseSpeed, { min: 0.1, max: 100, step: 0.1 }],
        ['consoleEnabled', '显示控制台窗口（mod 日志）', 'bool', config.consoleEnabled],
        ['consoleTopmost', '控制台窗口置顶', 'bool', config.consoleTopmost],
        ['forwardActivityLog', '把 mod 日志转发到控制台', 'bool', config.forwardActivityLog],
        ['useManagedBootstrap', '使用托管引导（实验性，一般保持关闭）', 'bool', config.useManagedBootstrap],
        ['gameAssemblyTimeoutSec', '等待 GameAssembly.dll 秒数', 'number', config.gameAssemblyTimeoutSec, { min: 1, max: 600 }],
        ['domainTimeoutSec', '等待托管域初始化秒数', 'number', config.domainTimeoutSec, { min: 1, max: 600 }],
        ['hybridclrTimeoutSec', '等待 HybridCLR 热更秒数', 'number', config.hybridclrTimeoutSec, { min: 1, max: 600 }],
        ['bootstrapAssembly', '托管引导程序集', 'text', config.bootstrapAssembly],
        ['bootstrapType', '引导类型', 'text', config.bootstrapType],
        ['bootstrapMethod', '引导方法', 'text', config.bootstrapMethod],
        ['sdkVersion', 'SDK 版本（只读，随包更新）', 'readonly', config.sdkVersion]
      ];
      const html = `
        <div class="cfg-form">
          ${rows.map(r => this.cfgRow(...r)).join('')}
          <div class="cfg-actions">
            <button class="secondary-btn" data-cfg-cancel>取消</button>
            <button class="primary-btn" data-cfg-save>保存设置</button>
          </div>
          <div class="cfg-hint">保存后重启游戏生效。一般只需改「启动时基础倍速」。</div>
        </div>`;
      openModal('⚙ 加载器设置', html);
      $('globalModal')?.querySelector('[data-cfg-cancel]')?.addEventListener('click', closeModal);
      $('globalModal')?.querySelector('[data-cfg-save]')?.addEventListener('click', () => {
        const cfg = this.collectLoaderConfig();
        if (cfg) post({ type: 'modSaveLoaderConfig', config: cfg });
      });
    },

    cfgRow(id, label, kind, value, attrs) {
      const a = attrs || {};
      const attrStr = Object.entries(a).map(([k, v]) => `${k}="${esc(String(v))}"`).join(' ');
      switch (kind) {
        case 'bool':
          return `<label class="cfg-row cfg-check"><input type="checkbox" data-cfg="${id}" ${value ? 'checked' : ''}> <span>${esc(label)}</span></label>`;
        case 'number':
          return `<label class="cfg-row"><span class="cfg-label">${esc(label)}</span><input type="number" data-cfg="${id}" value="${esc(String(value))}" ${attrStr}></label>`;
        case 'readonly':
          return `<label class="cfg-row"><span class="cfg-label">${esc(label)}</span><input type="text" data-cfg="${id}" value="${esc(String(value))}" readonly></label>`;
        default:
          return `<label class="cfg-row"><span class="cfg-label">${esc(label)}</span><input type="text" data-cfg="${id}" value="${esc(String(value))}"></label>`;
      }
    },

    collectLoaderConfig() {
      const modal = $('globalModal');
      if (!modal) return null;
      const get = id => modal.querySelector(`[data-cfg="${id}"]`);
      const bool = id => get(id)?.checked === true;
      const num = id => {
        const el = get(id);
        return el ? Number(el.value) : 0;
      };
      const text = id => get(id)?.value ?? '';
      const speed = num('speedhackBaseSpeed');
      if (!(speed > 0 && speed <= 100)) {
        toast('启动时基础倍速必须在 0.1 ~ 100 之间');
        return null;
      }
      return {
        enabled: bool('enabled'),
        useManagedBootstrap: bool('useManagedBootstrap'),
        bootstrapAssembly: text('bootstrapAssembly'),
        bootstrapType: text('bootstrapType'),
        bootstrapMethod: text('bootstrapMethod'),
        gameAssemblyTimeoutSec: num('gameAssemblyTimeoutSec'),
        domainTimeoutSec: num('domainTimeoutSec'),
        hybridclrTimeoutSec: num('hybridclrTimeoutSec'),
        consoleEnabled: bool('consoleEnabled'),
        consoleTopmost: bool('consoleTopmost'),
        forwardActivityLog: bool('forwardActivityLog'),
        speedhackBaseSpeed: speed,
        sdkVersion: text('sdkVersion')
      };
    },

    // ---------- mod 配置弹窗（configs\{mod}.json 键值表单编辑） ----------

    openModConfig(fileName) {
      post({ type: 'modReadConfigFields', fileName });
    },

    renderConfigFields(data) {
      if (!data || !data.fields) return;
      const fileName = data.fileName;
      const fields = data.fields;
      const rows = fields.length === 0
        ? '<div class="cfg-hint">还没有配置文件（configs\\' + esc(fileName.replace(/\.dll$/i, '')) + '.json）。保存后将创建。</div>'
        : fields.map((f, idx) => {
            const id = `cfgfield-${idx}`;
            if (f.kind === 'bool') {
              return `<label class="cfg-row cfg-check"><input type="checkbox" data-field="${id}" data-kind="bool" ${f.boolValue ? 'checked' : ''}> <span class="cfg-label">${esc(f.name)}</span></label>`;
            }
            if (f.kind === 'number') {
              return `<label class="cfg-row"><span class="cfg-label">${esc(f.name)}</span><input type="number" step="any" data-field="${id}" data-kind="number" value="${esc(String(f.numberValue))}"></label>`;
            }
            if (f.kind === 'other') {
              return `<label class="cfg-row"><span class="cfg-label">${esc(f.name)}（JSON）</span><input type="text" data-field="${id}" data-kind="other" value="${esc(f.stringValue)}"></label>`;
            }
            return `<label class="cfg-row"><span class="cfg-label">${esc(f.name)}</span><input type="text" data-field="${id}" data-kind="string" value="${esc(f.stringValue)}"></label>`;
          }).join('');
      const html = `
        <div class="cfg-form">
          <div class="cfg-hint">配置保存在 <b>configs\\${esc(fileName.replace(/\.dll$/i, ''))}.json</b>，由 mod 的 SdkConfig 读取。勾选框 = 开关，数字框 = 数值，文本框 = 文字。</div>
          ${rows}
          <div class="cfg-actions">
            <button class="secondary-btn" data-cfg-field-cancel>取消</button>
            <button class="secondary-btn" data-cfg-field-raw title="高级：用系统默认编辑器直接改 JSON">高级编辑…</button>
            <button class="primary-btn" data-cfg-field-save>保存配置</button>
          </div>
        </div>`;
      openModal(`⚙ 配置 · ${esc(fileName)}`, html);
      const modal = $('globalModal');
      modal?.querySelector('[data-cfg-field-cancel]')?.addEventListener('click', closeModal);
      modal?.querySelector('[data-cfg-field-raw]')?.addEventListener('click', () => {
        closeModal();
        post({ type: 'modOpenConfigRaw', fileName });
      });
      modal?.querySelector('[data-cfg-field-save]')?.addEventListener('click', () => {
        const out = fields.map((f, idx) => {
          const el = modal.querySelector(`[data-field="cfgfield-${idx}"]`);
          const kind = el?.dataset.kind || f.kind;
          if (kind === 'bool') return { name: f.name, kind: 'bool', boolValue: el?.checked === true };
          if (kind === 'number') return { name: f.name, kind: 'number', numberValue: Number(el?.value ?? f.numberValue) };
          return { name: f.name, kind: 'string', stringValue: el?.value ?? f.stringValue };
        });
        post({ type: 'modSaveConfigFields', fileName, fields: out });
      });
    }
  };

  function fmtBytes(bytes) {
    const value = Number(bytes ?? 0);
    if (value >= 1024 * 1024) return (value / 1024 / 1024).toFixed(2) + ' MB';
    if (value >= 1024) return (value / 1024).toFixed(1) + ' KB';
    return value + ' B';
  }

  // 页面标记存在即完成事件绑定（index.html 脚本位于 DOM 末尾）
  if (document.getElementById('modsView')) {
    ModsModule.init();
  }

  window.ModsModule = ModsModule;
})();
