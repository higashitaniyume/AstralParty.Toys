// mods.js - 模组管理页（Mod 加载器安装/卸载 + mods\ sdk\ 目录管理）
// 原为 utilities 页的一部分, 独立成页后逻辑随页面迁移到这里。
(function () {
  const ModsModule = {
    modStatus: null,

    // ---------- 生命周期 ----------

    init() {
      this.bindEvents();
    },

    onShowMods() {
      this.refreshModStatus();
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
      $('modGameDirText')?.addEventListener('dblclick', () => {
        const dir = this.modStatus?.gameDirectory;
        if (dir) post({ type: 'speedhackOpenGameDir' });
      });
      // mod 列表删除按钮（事件委托）
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
          if (fileName) post({ type: 'modOpenConfig', fileName });
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

    // ---------- 状态刷新 ----------

    refreshModStatus() {
      post({ type: 'modStatus' });
    },

    applyModStatus(payload) {
      this.modStatus = payload.status || payload;
      this.renderModStatus();
    },

    renderModStatus() {
      const status = this.modStatus;
      if (!status) return;
      const badge = $('modStatusBadge');
      const title = $('modStatusTitle');
      const icon = $('modStatusIcon');
      const text = $('modStatusText');

      const installedOk = status.installed && status.loaderMatchesBundle;
      const running = !!status.gameRunning;
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
        const perms = kind === 'mod' && entry.permissions ? ` · 权限 ${permLabel(entry.permissions)}` : '';
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
            <button class="mod-config-btn" data-config-file="${esc(entry.fileName)}" title="修改 mod 配置（configs\\${esc(entry.name)}.json）">⚙</button>
          </span>` : '';
        return `<div class="mod-list-item${entry.enabled === false ? ' mod-disabled' : ''}">
          <span class="mod-name" title="${esc(entry.fileName)}">${esc(title)}${ver}${author}${sdk}${perms}${deps}</span>
          <span class="mod-meta">${size}${time ? ' · ' + esc(time) : ''}</span>
          ${desc}
          ${controls}
          ${delBtn}
        </div>`;
      });
      container.innerHTML = nodes.join('');
    }
  };

  // 权限位掩码 → 可读名（与 C# ModPermission 枚举一致: 1=ReadGameState 2=GameActions 4=SpeedHack 8=FileWrite）
  function permLabel(mask) {
    const names = [];
    const map = [[1, '读对局'], [2, '操作'], [4, '变速'], [8, '写文件']];
    for (const [bit, label] of map) {
      if ((mask & bit) === bit) names.push(label);
    }
    return names.length ? names.join('|') : String(mask);
  }

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
