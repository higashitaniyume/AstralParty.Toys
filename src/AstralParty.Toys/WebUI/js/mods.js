// mods.js - 模组管理页（加载器安装/更新 + 模组列表 + 变速 + 高级排障）
//
// 三条设计前提：
//   1. 用户不会手改 JSON —— 所有可配置项都在界面里可视化编辑；
//   2. **版本信息是第一等公民**：这是一个 mod 管理器，用户来这里多半就是为了确认
//      「装的是哪一版 / 要不要更新」。所以「已安装 / 随程序内置 / GitHub 最新」三栏常驻页面顶部，
//      下面再给一句人话结论；版本号只认安装清单，读不到就说"未知"，不猜、不用别的字段顶替。
//   3. 去繁就简：常用操作（安装、检查更新、导入模组、开关模组）在前，
//      破坏性与排障项（覆盖 / 降级 / 强制卸载 / SDK 依赖 / 原理说明）收进「高级与排障」，默认折叠。
(function () {
  // ---------- 版本号比较（数字点分；认不出的段按 0 处理） ----------

  function parseVersion(value) {
    return String(value ?? '')
      .split('.')
      .map(part => parseInt(part, 10) || 0);
  }

  /** a > b → 1；a < b → -1；相等 / 无法比较 → 0。 */
  function compareVersion(a, b) {
    const left = parseVersion(a);
    const right = parseVersion(b);
    const length = Math.max(left.length, right.length);
    for (let i = 0; i < length; i++) {
      const l = left[i] || 0;
      const r = right[i] || 0;
      if (l !== r) return l > r ? 1 : -1;
    }
    return 0;
  }

  /** 写一栏版本信息：值 + 说明 + 状态色（状态同时有文字说明，不靠颜色单独表意）。 */
  function setVersionCell(cellId, valueId, noteId, options) {
    const valueEl = $(valueId);
    if (valueEl) valueEl.textContent = options.value;
    const noteEl = noteId ? $(noteId) : null;
    if (noteEl && options.note !== undefined) noteEl.textContent = options.note;
    const cell = $(cellId);
    if (!cell) return;
    cell.classList.remove('is-current', 'is-update', 'is-conflict', 'is-unknown');
    if (options.state) cell.classList.add(options.state);
  }

  const ModsModule = {
    modStatus: null,
    // 联网检查更新的结果（跨页面切换保留；state: idle | checking | done）
    updateInfo: { state: 'idle', latest: '', error: '' },
    builtInMods: [],
    // 正在进行的耗时操作（安装/卸载/检查/更新）：按钮转圈 + 锁住，避免重复点击
    busy: null,
    busyTimer: null,

    // ---------- 生命周期 ----------

    init() {
      this.bindEvents();
      window.GameLibModule?.init?.();
    },

    onShowMods() {
      window.GameLibModule?.init?.();
      this.refreshModStatus();
      // 只同步变速栏状态(不弹设置窗口; 弹窗仅在点「⚙ 加载器设置」时出现)
      post({ type: 'modSyncSpeedhack' });
    },

    bindEvents() {
      $('modsBackHomeBtn')?.addEventListener('click', () => showPage('home'));

      // ---------- Mod 加载器 ----------
      $('modInstallBtn')?.addEventListener('click', () => {
        this.setBusy('modInstallBtn', '安装中…');
        post({
          type: 'modInstall',
          overwriteDll: $('modOverwriteCheck')?.checked === true,
          allowDowngrade: $('modDowngradeCheck')?.checked === true,
          includeSample: $('modIncludeSampleCheck')?.checked !== false
        });
      });
      $('modUninstallBtn')?.addEventListener('click', () => {
        this.setBusy('modUninstallBtn', '卸载中…');
        post({ type: 'modUninstall', force: $('modForceUninstallCheck')?.checked === true });
      });
      $('modDetectDirBtn')?.addEventListener('click', () => post({ type: 'modDetect' }));
      $('modBrowseDirBtn')?.addEventListener('click', () => post({ type: 'modBrowse' }));
      $('modOpenModsBtn')?.addEventListener('click', () => post({ type: 'modOpenModsFolder' }));
      $('modOpenFolderTopBtn')?.addEventListener('click', () => post({ type: 'modOpenModsFolder' }));
      $('modOpenSdkBtn')?.addEventListener('click', () => post({ type: 'modOpenSdkFolder' }));
      $('modOpenLogsBtn')?.addEventListener('click', () => post({ type: 'modOpenLogsFolder' }));
      $('modImportBtn')?.addEventListener('click', () => post({ type: 'modPickImport' }));
      $('modImportTopBtn')?.addEventListener('click', () => post({ type: 'modPickImport' }));
      $('modCheckUpdateBtn')?.addEventListener('click', () => {
        this.updateInfo = { state: 'checking', latest: this.updateInfo.latest, error: '' };
        this.setBusy('modCheckUpdateBtn', '检查中…');
        this.renderUpdateInfo();
        post({ type: 'modCheckUpdate' });
      });
      $('modDownloadUpdateBtn')?.addEventListener('click', () => {
        // 下载 + 安装可能十几秒：按钮上转圈并锁住，避免重复点击（宿主完成/失败时才解除）
        this.setBusy('modDownloadUpdateBtn', '更新中…');
        post({
          type: 'modDownloadUpdate',
          overwriteDll: $('modUpdateOverwriteCheck')?.checked === true,
          allowDowngrade: $('modUpdateDowngradeCheck')?.checked === true
        });
      });
      $('modLoaderSettingsBtn')?.addEventListener('click', () => this.openLoaderSettings());
      $('modLoaderChangelogBtn')?.addEventListener('click', () => window.VersionModule?.requestChangelog('loader'));
      // 游戏变速栏（加载器内置功能）
      $('modSpeedhackEnable')?.addEventListener('change', () => {
        const enabled = $('modSpeedhackEnable').checked;
        const label = $('modSpeedhackEnableLabel');
        if (label) label.textContent = enabled ? '变速已启用' : '变速已禁用';
        $('modSpeedhackSpeed').disabled = !enabled;
      });
      $('modSpeedhackSaveBtn')?.addEventListener('click', () => this.saveSpeedhack());
      // 高级区里的游戏目录路径：双击在资源管理器里打开
      $('modGameDirText')?.addEventListener('dblclick', () => {
        const dir = this.modStatus?.gameDirectory;
        if (dir) post({ type: 'speedhackOpenGameDir' });
      });
      // mod 列表删除/配置按钮（事件委托）
      $('modListContainer')?.addEventListener('click', e => {
        const del = e.target.closest('.mod-del-btn');
        if (del) {
          const fileName = del.dataset.file;
          if (fileName && confirm(`确定删除模组「${fileName}」？`)) {
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

    /** 已知的「GitHub 最新版本」是否比已装版本更新；没有最新信息时返回 ''。 */
    newerVersion(installedVersion) {
      const latest = this.updateInfo.latest || '';
      if (!latest || !installedVersion) return '';
      return compareVersion(latest, installedVersion) > 0 ? latest : '';
    },

    renderModStatus(payload) {
      if (!payload) return;
      // 兼容两种载荷：{ status, builtInMods }（当前）与直接给 status（老格式）
      const status = payload.status ?? payload;
      this.modStatus = status;
      if (Array.isArray(payload.builtInMods)) this.builtInMods = payload.builtInMods;

      // 加载器的安装/卸载/更新都以此消息收尾（失败路径会走 error 消息），到这里就解除按钮忙碌
      this.clearBusy();

      this.renderStatusHeader(status);
      this.renderUpdateInfo();
      this.renderGameDirectory(status);
      this.renderAdvancedWarnings(status);

      const mods = status.mods || [];
      this.renderModList($('modListContainer'), mods, 'mod');
      this.renderModList($('modSdkContainer'), status.sdk || [], 'sdk');
      const count = $('modModsCount');
      if (count) count.textContent = String(mods.length);
    },

    // ---------- 加载器状态（徽标 + 一句话 + 按钮） ----------

    renderStatusHeader(status) {
      const running = status.gameRunning === true;
      const installed = status.installed === true;
      const managed = status.loaderManaged === true;
      const newer = this.newerVersion(status.installedVersion);

      const badge = $('modStatusBadge');
      const icon = $('modStatusIcon');
      const title = $('modStatusTitle');
      const text = $('modStatusText');

      let badgeText = installed ? '已安装' : '未安装';
      let badgeClass = installed ? 'badge badge-update' : 'badge badge-muted';
      let iconText = installed ? '🧩' : '🔍';
      let titleText = installed ? '加载器已就位' : '尚未安装加载器';

      if (status.bundleLoaderPresent === false) {
        badgeText = '缺少内置文件';
        badgeClass = 'badge badge-danger';
        iconText = '❌';
        titleText = '程序缺少内置加载器文件';
      } else if (installed && !managed) {
        badgeText = '文件不属于本工具';
        badgeClass = 'badge badge-danger';
        iconText = '⚠️';
        titleText = '游戏目录里的 version.dll 不是本工具装的';
      } else if (installed && newer) {
        badgeText = '可更新';
        badgeClass = 'badge badge-event';
        iconText = '⬆️';
        titleText = `可以更新到 v${newer}`;
      }
      if (running) badgeText += ' · 游戏运行中';

      if (badge) {
        badge.textContent = badgeText;
        badge.className = badgeClass;
      }
      if (icon) icon.textContent = iconText;
      if (title) title.textContent = titleText;
      if (text) text.textContent = status.message || '';

      // 主按钮随状态换角色：没装 → 「安装加载器」是主操作；装了 → 主操作变成「更新」
      const installBtn = $('modInstallBtn');
      if (installBtn) {
        installBtn.textContent = installed ? '⟳ 重新安装内置版本' : '⬇ 安装加载器';
        installBtn.className = installed ? 'secondary-btn' : 'primary-btn';
      }
      const updateBtn = $('modDownloadUpdateBtn');
      if (updateBtn) {
        updateBtn.textContent = newer ? `⬆ 更新到 v${newer}` : '⬆ 从 GitHub 更新';
        updateBtn.className = newer ? 'primary-btn' : 'secondary-btn';
        updateBtn.classList.toggle('hidden', !installed);
      }
      const settingsBtn = $('modLoaderSettingsBtn');
      if (settingsBtn) settingsBtn.disabled = !installed;

      // 没装加载器时 doorstop_config.json 还不存在，变速无处可写 → 整块禁用（而不是让保存报错）
      this.setSpeedhackAvailable(installed);
    },

    // ---------- 版本三栏 + 结论 ----------

    renderUpdateInfo() {
      const status = this.modStatus || {};
      const installed = status.installed === true;
      const managed = status.loaderManaged === true;
      const running = status.gameRunning === true;
      const installedVersion = status.installedVersion || '';
      const embeddedVersion = status.embeddedVersion || '';
      const info = this.updateInfo;
      const latest = info.latest || '';
      const newer = this.newerVersion(installedVersion);

      // ① 已安装
      let installedState = 'is-current';
      let installedNote;
      if (!installed) {
        installedState = 'is-unknown';
        installedNote = '点「安装加载器」把加载器写进游戏目录';
      } else if (!managed) {
        installedState = 'is-conflict';
        installedNote = '不是本工具装的文件 —— 见下方「高级与排障」';
      } else if (!installedVersion) {
        installedState = 'is-unknown';
        installedNote = '这份安装没有版本清单，重新安装一次即可记录';
      } else if (newer) {
        installedState = 'is-update';
        installedNote = `有新版本 v${latest} 可更新`;
      } else if (embeddedVersion && compareVersion(installedVersion, embeddedVersion) === 0) {
        installedNote = '与随程序内置的版本一致';
      } else if (embeddedVersion) {
        // 方向要说清楚：更新过的加载器比内置的新是正常的，比内置的旧则可以直接装回内置版
        installedNote = compareVersion(installedVersion, embeddedVersion) > 0
          ? `比内置的 v${embeddedVersion} 更新（从 GitHub 更新过）`
          : `比内置的 v${embeddedVersion} 旧 —— 点「重新安装内置版本」可装回内置版`;
      } else {
        installedNote = '已安装（内置版本号读取失败）';
      }
      setVersionCell('modInstalledCell', 'modInstalledVersion', 'modInstalledNote', {
        value: installed ? (installedVersion ? `v${installedVersion}` : '未知') : '未安装',
        note: installedNote,
        state: installedState
      });

      // ② 随程序内置
      setVersionCell('modEmbeddedCell', 'modEmbeddedVersion', null, {
        value: embeddedVersion ? `v${embeddedVersion}` : '缺失',
        state: embeddedVersion ? '' : 'is-conflict'
      });

      // ③ GitHub 最新
      const checking = info.state === 'checking';
      let latestState = '';
      if (latest && installedVersion) {
        const cmp = compareVersion(latest, installedVersion);
        latestState = cmp > 0 ? 'is-update' : (cmp === 0 ? 'is-current' : '');
      }
      setVersionCell('modLatestCell', 'modLatestVersion', 'modLatestNote', {
        value: checking ? '检查中…' : (latest ? `v${latest}` : (info.error ? '检查失败' : '未检查')),
        note: info.error ? info.error : (latest ? 'GitHub 最新 Release' : '点「检查更新」向 GitHub 查询'),
        state: latestState
      });

      this.renderVerdict(status, { installed, managed, running, installedVersion, embeddedVersion, latest, newer });
    },

    /** 三栏下面那句「所以现在该怎么办」——把状态收成一个动作，避免用户自己拼三个版本号。 */
    renderVerdict(status, ctx) {
      const verdict = $('modVersionVerdict');
      if (!verdict) return;
      const textEl = $('modVersionVerdictText');
      const { installed, managed, installedVersion, embeddedVersion, latest, newer } = ctx;

      let stateClass = '';
      let text;
      if (status.bundleLoaderPresent === false) {
        stateClass = 'is-warn';
        text = '本程序缺少内置加载器文件，无法安装 —— 请使用完整发布包（推荐 portable.zip）。';
      } else if (!status.gameDirectory) {
        stateClass = 'is-idle';
        text = '还没找到游戏目录 —— 展开「高级与排障」点「自动检测」，或手动选择游戏 exe 所在的文件夹。';
      } else if (!installed) {
        stateClass = 'is-idle';
        text = '尚未安装加载器 —— 点「安装加载器」，加载器与内置模组会一起装进游戏目录。';
      } else if (!managed) {
        stateClass = 'is-warn';
        text = '游戏目录里的 version.dll 不是本工具安装的（可能是旧版独立变速器或第三方文件）——安装或更新前请先确认来源。';
      } else if (!installedVersion) {
        stateClass = 'is-update';
        text = '已安装，但这一份没有版本清单，读不出准确版本 —— 重新安装一次即可纳入版本管理。';
      } else if (newer) {
        stateClass = 'is-update';
        text = `发现新版本：已装 v${installedVersion} → 最新 v${latest}，点「更新到 v${latest}」。`;
      } else if (latest) {
        stateClass = 'is-ok';
        text = compareVersion(latest, installedVersion) === 0
          ? `已是最新版本 v${latest}。`
          : `已装的 v${installedVersion} 比 GitHub 上的 v${latest} 更新，无需更新。`;
      } else if (embeddedVersion && compareVersion(installedVersion, embeddedVersion) === 0) {
        stateClass = 'is-ok';
        text = `已安装 v${installedVersion}，与随程序内置的版本一致 —— 点「检查更新」可确认 GitHub 上有没有新版。`;
      } else if (embeddedVersion && compareVersion(installedVersion, embeddedVersion) > 0) {
        text = `已安装 v${installedVersion}，比随程序内置的 v${embeddedVersion} 更新（这份是从 GitHub 更新来的）—— 点「检查更新」看看还有没有更新的版本。`;
      } else if (embeddedVersion && compareVersion(installedVersion, embeddedVersion) < 0) {
        text = `已安装 v${installedVersion}，比随程序内置的 v${embeddedVersion} 旧 —— 想回到内置版本就点「重新安装内置版本」，想知道有没有新版就点「检查更新」。`;
      } else {
        text = `已安装 v${installedVersion} —— 点「检查更新」看看 GitHub 上有没有新版。`;
      }
      if (ctx.running) text += ' 游戏正在运行：安装 / 更新 / 卸载都要重启游戏才生效。';

      verdict.classList.remove('is-ok', 'is-update', 'is-warn', 'is-idle');
      if (stateClass) verdict.classList.add(stateClass);
      if (textEl) textEl.textContent = text;
    },

    /** 「检查更新」的结果（Host 在最后一步回推）。 */
    renderUpdateCheck(payload) {
      this.clearBusy();
      if (!payload) return;
      if (payload.error) {
        this.updateInfo = { state: 'done', latest: '', error: `检查失败：${payload.error}` };
        toast(`检查更新失败：${payload.error}`);
      } else {
        this.updateInfo = { state: 'done', latest: payload.latest || '', error: '' };
      }
      this.renderUpdateInfo();
    },

    // ---------- 按钮忙碌态（安装/卸载/检查/更新） ----------

    /**
     * 把按钮切成"进行中"：禁用 + 转圈 + 换文案。
     * 解除由宿主消息触发（modStatus / modUpdateCheck），失败路径走 error 消息；
     * 另外挂一个兜底定时器，万一宿主异常没回音，两分钟后自己恢复，不会把按钮永久锁死。
     */
    setBusy(buttonId, label) {
      this.clearBusy();
      const button = $(buttonId);
      if (!button) return;
      this.busy = { id: buttonId, html: button.innerHTML, wasDisabled: button.disabled };
      button.disabled = true;
      button.classList.add('is-busy');
      button.innerHTML = `<span class="btn-spinner" aria-hidden="true"></span><span>${esc(label)}</span>`;
      this.busyTimer = setTimeout(() => this.clearBusy(), 120000);
    },

    clearBusy() {
      clearTimeout(this.busyTimer);
      this.busyTimer = null;
      const busy = this.busy;
      this.busy = null;
      if (!busy) return;
      const button = $(busy.id);
      if (!button) return;
      button.innerHTML = busy.html;
      button.disabled = busy.wasDisabled;
      button.classList.remove('is-busy');
    },

    // ---------- 游戏目录 / 高级区提示 ----------

    renderGameDirectory(status) {
      const dirText = $('modGameDirText');
      if (!dirText) return;
      if (status.gameDirectory) {
        dirText.textContent = status.gameDirectory;
        dirText.classList.remove('placeholder');
        dirText.title = '双击在资源管理器中打开';
      } else {
        dirText.textContent = '尚未选择游戏目录';
        dirText.classList.add('placeholder');
        dirText.title = '';
      }
    },

    /** 只有真出问题（文件不属于本工具 / 缺内置资源）时才在高级区给出处理办法。 */
    renderAdvancedWarnings(status) {
      const box = $('modAdvancedWarn');
      if (!box) return;
      const messages = [];
      if (status.installed && status.loaderManaged !== true) {
        messages.push('游戏目录里的 version.dll 既不是本程序内置的那一份，也没有记录在安装清单里：'
          + '多半是别的工具（例如「游戏工具」页的旧版独立变速器）或第三方放进来的文件。'
          + '要换成 CesiumLoader，勾选下面的「允许覆盖游戏目录里不属于本工具的 version.dll」，再点「安装加载器」；'
          + '只想清掉就勾选「强制卸载」。');
      }
      if (status.bundleLoaderPresent === false) {
        messages.push('本程序缺少内置加载器资源，安装按钮不会生效 —— 请重新下载完整发布包（推荐 portable.zip，single-file 版偶发解包不完整）。');
      }
      box.innerHTML = messages.map(m => `<p>${esc(m)}</p>`).join('');
      box.classList.toggle('hidden', messages.length === 0);
    },

    // ---------- 模组列表 ----------

    renderModList(container, entries, kind) {
      if (!container) return;
      const list = entries || [];
      if (list.length === 0) {
        container.innerHTML = kind === 'mod'
          ? '<p class="mods-empty">还没有模组 —— 点「导入模组 DLL」添加，或重新安装加载器时勾选内置模组</p>'
          : '<p class="mods-empty">sdk 目录为空（重新安装加载器会写入 CesiumLoader.SDK.dll）</p>';
        return;
      }
      container.innerHTML = list
        .map(entry => (kind === 'mod' ? this.modCard(entry) : this.sdkRow(entry)))
        .join('');
    },

    isBuiltInMod(entry) {
      const name = String(entry.name || '');
      const file = String(entry.fileName || '').replace(/\.dll$/i, '');
      return this.builtInMods.some(id => id === name || id === file);
    },

    /** 一个模组 = 一张卡片：名字 / 版本 / 能力标签 → 描述 → 事实行 → 开关 + 配置 + 删除。 */
    modCard(entry) {
      const enabled = entry.enabled !== false;
      const title = entry.displayName || entry.name || entry.fileName;

      const chips = [];
      if (this.isBuiltInMod(entry)) {
        chips.push('<span class="mod-chip mod-chip-builtin" title="随加载器一起安装的内置模组">内置</span>');
      }
      if ((Number(entry.permissions) & 2) === 2) {
        chips.push('<span class="mod-chip mod-chip-warn" title="此模组声明可操作游戏（模拟出牌 / 掷骰 / 移动等）。权限机制已取消，这里只提示：请确认来源可信">⚠ 可操作游戏</span>');
      }
      if (!enabled) chips.push('<span class="mod-chip mod-chip-off">已禁用</span>');

      const facts = [];
      if (entry.version) facts.push(`v${entry.version}`);
      if (entry.author) facts.push(entry.author);
      if (entry.sdkVersion) facts.push(`SDK ${entry.sdkVersion}`);
      facts.push(fmtBytes(entry.sizeBytes));
      if (entry.directoryName) facts.push(`mods\\${entry.directoryName}\\`);
      if (entry.modifiedUtc) facts.push(new Date(entry.modifiedUtc).toLocaleString('zh-CN'));

      const deps = (entry.dependencies || []).length
        ? '依赖：' + entry.dependencies
            .map(d => `${d.id}${d.minVersion ? ' ≥ ' + d.minVersion : ''}`)
            .join('、')
        : '';
      const description = [entry.description, deps].filter(Boolean).join('　');

      return `<article class="mod-card${enabled ? '' : ' mod-disabled'}" title="${esc(entry.fileName)}">
        <div class="mod-card-main">
          <div class="mod-card-name-row">
            <span class="mod-card-name">${esc(title)}</span>
            ${chips.join('')}
          </div>
          ${description ? `<p class="mod-card-desc">${esc(description)}</p>` : ''}
          <span class="mod-card-meta">${esc(facts.join(' · '))}</span>
        </div>
        <div class="mod-card-side">
          <label class="mod-toggle" title="启用 / 禁用（重启游戏后生效）">
            <input type="checkbox" data-toggle-file="${esc(entry.fileName)}" ${enabled ? 'checked' : ''}>
            <span class="switch-track"></span>
            <span class="mod-toggle-text">${enabled ? '已启用' : '已禁用'}</span>
          </label>
          <button class="mod-config-btn" data-config-file="${esc(entry.fileName)}" title="修改这个模组的配置" aria-label="修改配置">⚙</button>
          <button class="mod-del-btn" data-file="${esc(entry.fileName)}" title="删除这个模组" aria-label="删除模组">🗑</button>
        </div>
      </article>`;
    },

    sdkRow(entry) {
      const facts = [fmtBytes(entry.sizeBytes)];
      if (entry.modifiedUtc) facts.push(new Date(entry.modifiedUtc).toLocaleString('zh-CN'));
      return `<article class="mod-card">
        <div class="mod-card-main">
          <div class="mod-card-name-row">
            <span class="mod-card-name">${esc(entry.name || entry.fileName)}</span>
            ${entry.version ? `<span class="mod-card-version">v${esc(entry.version)}</span>` : ''}
          </div>
          <span class="mod-card-meta">${esc(facts.join(' · '))}</span>
        </div>
      </article>`;
    },

    // ---------- 游戏变速栏（加载器内置功能，非 mod） ----------

    /** 加载器没装时 doorstop_config.json 无处可写：整栏禁用，别让用户点了才报错。 */
    setSpeedhackAvailable(available) {
      const enableEl = $('modSpeedhackEnable');
      const speedEl = $('modSpeedhackSpeed');
      const saveBtn = $('modSpeedhackSaveBtn');
      const hintEl = $('modSpeedhackHint');
      const labelEl = $('modSpeedhackEnableLabel');
      if (saveBtn) saveBtn.disabled = !available;
      if (available) return;
      if (enableEl) { enableEl.checked = false; enableEl.disabled = true; }
      if (speedEl) speedEl.disabled = true;
      if (labelEl) labelEl.textContent = '变速已禁用';
      if (hintEl) hintEl.textContent = '先安装加载器';
    },

    // 用加载器配置填充变速栏状态（开关 + 倍速）
    syncSpeedhackBar(config) {
      if (!config) return;
      const available = this.modStatus ? this.modStatus.installed === true : true;
      // 加载器硬性下限 1.0：配置里残留的 <1 值(旧版写的)按 1.0 显示，避免界面显示"0.5x"却实际跑 1x
      const raw = Number(config.speedhackBaseSpeed ?? 1.0);
      const speed = Number.isFinite(raw) && raw >= 1.0 ? raw : 1.0;
      const enabled = Math.abs(speed - 1.0) > 0.001;
      const enableEl = $('modSpeedhackEnable');
      const speedEl = $('modSpeedhackSpeed');
      const labelEl = $('modSpeedhackEnableLabel');
      const hintEl = $('modSpeedhackHint');
      const saveBtn = $('modSpeedhackSaveBtn');
      if (enableEl) {
        enableEl.checked = enabled && available;
        enableEl.disabled = !available;
      }
      if (speedEl) {
        speedEl.value = enabled ? speed : '2.0';
        speedEl.disabled = !available || !enabled;
      }
      if (saveBtn) saveBtn.disabled = !available;
      if (labelEl) labelEl.textContent = enabled ? '变速已启用' : '变速已禁用';
      if (hintEl) {
        hintEl.textContent = !available
          ? '先安装加载器'
          : (enabled ? `当前 ${speed}x（重启游戏生效）` : '1.0 = 正常速度；最低 1.0，不支持减速');
      }
    },

    saveSpeedhack() {
      const enabled = $('modSpeedhackEnable')?.checked === true;
      const speed = Number($('modSpeedhackSpeed')?.value ?? 2.0);
      if (!(speed >= 1 && speed <= 100)) {
        toast('倍速必须在 1.0 ~ 100 之间（1.0 = 正常速度；加载器不支持减速）');
        return;
      }
      post({
        type: 'modSaveSpeedhack',
        speedhackBaseSpeed: enabled ? speed : 1.0
      });
    },

    // ---------- 加载器设置弹窗（doorstop_config.json 可视化编辑） ----------
    // 分两层：日常项直接摊开，Steam 绕过细节 / 引导 / 超时收进弹窗内的「高级选项」。

    openLoaderSettings() {
      post({ type: 'modReadLoaderConfig' });
    },

    renderLoaderSettings(config) {
      if (!config) return;
      // 同步变速栏（加载器设置里的基础倍速与页面变速开关一致）
      this.syncSpeedhackBar(config);

      const basicRows = [
        ['enabled', '启用加载器', 'bool', config.enabled],
        ['speedhackBaseSpeed', '启动时基础倍速（1.0 = 正常；2.0 = 全程 2 倍速；也可留 1.0 由游戏内热键变速。不支持减速，最小 1.0）', 'number', config.speedhackBaseSpeed, { min: 1, max: 100, step: 0.1 }],
        ['speedControlEnabled', '允许游戏内热键变速（Delete 在 1.0x 与刚才的倍率之间切换，Alt+= / Alt+- 调倍率）', 'bool', config.speedControlEnabled],
        ['consoleEnabled', '显示控制台窗口（模组日志）', 'bool', config.consoleEnabled],
        ['consoleTopmost', '控制台窗口置顶', 'bool', config.consoleTopmost],
        ['forwardActivityLog', '把模组日志转发到控制台', 'bool', config.forwardActivityLog],
        ['steamBypassEnabled', '不装 Steam 也能启动游戏（首页「启动方式」把这一套三个开关一起开 / 关）', 'bool', config.steamBypassEnabled]
      ];
      const advancedRows = [
        ['__steam', 'Steam 绕过细节 —— 要从 Steam 启动并使用真大厅，这三个开关必须全部关闭', 'header', ''],
        ['steamBypassRestartCheck', '附加保险：忽略 Steam 的“请从 Steam 启动”重启请求', 'bool', config.steamBypassRestartCheck],
        ['steamBypassMatchmaking', '修复点“创建 / 加入房间”没反应（大厅匹配改为直接返回空结果）', 'bool', config.steamBypassMatchmaking],
        ['steamBypassLobbyQuery', '修复退房 / 被踢时报错（房间列表查询返回空列表）', 'bool', config.steamBypassLobbyQuery],
        ['steamBypassLobbyHasValue', '阶段3 兜底（实测已作废、hook 从未命中；保持开启即可）', 'bool', config.steamBypassLobbyHasValue],
        ['steamBypassTaskCtorMode', 'Task 构造方式（auto / direct / invoke，一般保持 auto）', 'text', config.steamBypassTaskCtorMode],
        ['steamBypassLobbyMethods', '⚠ 方案B 备用安全网 —— 实测开启会导致游戏启动崩溃（0xC0000005），请保持关闭', 'bool', config.steamBypassLobbyMethods],
        ['__boot', '引导与超时（一般不用改）', 'header', ''],
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
          ${basicRows.map(r => this.cfgRow(...r)).join('')}
          <details class="mods-help cfg-advanced">
            <summary>高级选项（Steam 绕过细节 · 引导 · 超时）</summary>
            ${advancedRows.map(r => this.cfgRow(...r)).join('')}
          </details>
          <div class="cfg-actions">
            <button class="secondary-btn" data-cfg-cancel>取消</button>
            <button class="primary-btn" data-cfg-save>保存设置</button>
          </div>
          <div class="cfg-hint">保存后重启游戏生效；日常只需要改「启用加载器」或「启动时基础倍速」。</div>
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
        case 'header':
          // 分组标题：只是视觉分隔，不参与 collectLoaderConfig 取值
          return `<div class="cfg-hint cfg-section" data-cfg-header="${id}">${esc(label)}</div>`;
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
      // 加载器硬性下限 1.0（加载器不接受减速）；这里必须拦住，否则会出现
      // 「界面写 0.5、实际按 1.0 跑」这种最难排查的不一致。
      if (!(speed >= 1 && speed <= 100)) {
        toast('启动时基础倍速必须在 1.0 ~ 100 之间（1.0 = 正常速度，不支持减速）');
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
        speedControlEnabled: bool('speedControlEnabled'),
        steamBypassEnabled: bool('steamBypassEnabled'),
        steamBypassRestartCheck: bool('steamBypassRestartCheck'),
        steamBypassMatchmaking: bool('steamBypassMatchmaking'),
        steamBypassLobbyQuery: bool('steamBypassLobbyQuery'),
        steamBypassLobbyHasValue: bool('steamBypassLobbyHasValue'),
        steamBypassLobbyMethods: bool('steamBypassLobbyMethods'),
        steamBypassTaskCtorMode: text('steamBypassTaskCtorMode'),
        sdkVersion: text('sdkVersion')
      };
    },

    // ---------- mod 配置弹窗（mods\{mod}\config.json 键值表单编辑） ----------

    openModConfig(fileName) {
      post({ type: 'modReadConfigFields', fileName });
    },

    renderConfigFields(data) {
      if (!data || !data.fields) return;
      const fileName = data.fileName;
      const fields = data.fields;
      const rows = fields.length === 0
        ? '<div class="cfg-hint">还没有配置文件（mods\\' + esc(fileName.replace(/\.dll$/i, '')) + '\\config.json）。保存后将创建。</div>'
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
            if (f.kind === 'key') {
              const current = f.stringValue || '';
              return `<div class="cfg-row cfg-key">
                <span class="cfg-label">${esc(f.name)}</span>
                <button type="button" class="key-btn" data-field="${id}" data-kind="key" data-key="${esc(current)}" title="点一下，再按下要绑的键（支持鼠标左/右/中键与侧键；Esc 取消）">${esc(current || '未设置（用 mod 默认）')}</button>
                <button type="button" class="key-clear" data-clear="${id}" title="清空（让 mod 用它自己的默认键）">×</button>
              </div>`;
            }
            return `<label class="cfg-row"><span class="cfg-label">${esc(f.name)}</span><input type="text" data-field="${id}" data-kind="string" value="${esc(f.stringValue)}"></label>`;
          }).join('');
      const html = `
        <div class="cfg-form">
          <div class="cfg-hint">配置保存在 <b>mods\\${esc(fileName.replace(/\.dll$/i, ''))}\\config.json</b>（与 mod 的 dll 同目录，由 mod 的 SdkConfig 读写；重启游戏后生效）。勾选框 = 开关，数字框 = 数值，文本框 = 文字；<b>按键按钮</b> = 点一下再按要绑的键（键盘任意键或鼠标左/右/中键、侧键，Esc 取消）。</div>
          ${rows}
          <div class="cfg-actions">
            <button class="secondary-btn" data-cfg-field-cancel>取消</button>
            <button class="secondary-btn" data-cfg-field-raw title="高级：用系统默认编辑器直接改 JSON">高级编辑…</button>
            <button class="primary-btn" data-cfg-field-save>保存配置</button>
          </div>
        </div>`;
      openModal(`⚙ 配置 · ${esc(fileName)}`, html);
      const modal = $('globalModal');
      bindKeyCapture(modal);
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
          // 键位在后台就是普通字符串, 只是 UI 用"捕获"来采集
          if (kind === 'key') return { name: f.name, kind: 'string', stringValue: el?.dataset.key ?? f.stringValue ?? '' };
          return { name: f.name, kind: 'string', stringValue: el?.value ?? f.stringValue };
        });
        post({ type: 'modSaveConfigFields', fileName, fields: out });
      });
    }
  };

  // =====================================================================
  // 按键捕获（mod 配置里的 toggleKey / speedUpKey / resetKey …）
  //
  // mod 侧(Unity)读的是 KeyCode 名字，所以这里要把浏览器的事件翻译成那套名字：
  //   键盘：event.code（"KeyA" / "Digit1" / "NumpadAdd" / "AltLeft" …）
  //   鼠标：event.button（0 左 / 1 中 / 2 右 / 3 侧键"后退" / 4 侧键"前进"）
  // 认不出来的键（媒体键等）不写进配置，只提示 —— 绝不猜一个可能不对的名字存下去。
  // =====================================================================

  // 与 Unity KeyCode 名字相同的部分（不用换算）
  const UNITY_KEY_BY_CODE = {
    Backquote: 'BackQuote', Minus: 'Minus', Equal: 'Equals', Backspace: 'Backspace', Tab: 'Tab',
    CapsLock: 'CapsLock', Space: 'Space', Enter: 'Return', NumpadEnter: 'KeypadEnter',
    Escape: 'Escape', Delete: 'Delete', Insert: 'Insert', Home: 'Home', End: 'End',
    PageUp: 'PageUp', PageDown: 'PageDown', PrintScreen: 'Print', ScrollLock: 'ScrollLock', Pause: 'Pause',
    ArrowUp: 'UpArrow', ArrowDown: 'DownArrow', ArrowLeft: 'LeftArrow', ArrowRight: 'RightArrow',
    ShiftLeft: 'LeftShift', ShiftRight: 'RightShift',
    ControlLeft: 'LeftControl', ControlRight: 'RightControl',
    AltLeft: 'LeftAlt', AltRight: 'RightAlt',
    MetaLeft: 'LeftWindows', MetaRight: 'RightWindows',
    BracketLeft: 'LeftBracket', BracketRight: 'RightBracket', Backslash: 'Backslash',
    Semicolon: 'Semicolon', Quote: 'Quote', Comma: 'Comma', Period: 'Period', Slash: 'Slash',
    NumpadAdd: 'KeypadPlus', NumpadSubtract: 'KeypadMinus', NumpadMultiply: 'KeypadMultiply',
    NumpadDivide: 'KeypadDivide', NumpadDecimal: 'KeypadPeriod', NumLock: 'Numlock', ContextMenu: 'Menu'
  };

  // 浏览器鼠标键号 → Unity Mouse0-4（注意中间/右键的编号不是直觉顺序：Unity 里 1 = 右键、2 = 中键）
  const UNITY_KEY_BY_MOUSE_BUTTON = { 0: 'Mouse0', 1: 'Mouse2', 2: 'Mouse1', 3: 'Mouse3', 4: 'Mouse4' };

  function unityKeyNameFromCode(code) {
    if (!code) return '';
    if (UNITY_KEY_BY_CODE[code]) return UNITY_KEY_BY_CODE[code];
    let m = /^Key([A-Z])$/.exec(code);        // KeyA…KeyZ → A…Z
    if (m) return m[1];
    m = /^Digit([0-9])$/.exec(code);          // Digit0…Digit9 → Alpha0…Alpha9
    if (m) return 'Alpha' + m[1];
    m = /^F([1-9]|1[0-2])$/.exec(code);       // F1…F12
    if (m) return code;
    m = /^Numpad([0-9])$/.exec(code);         // Numpad0…Numpad9 → Keypad0…Keypad9
    if (m) return 'Keypad' + m[1];
    return '';
  }

  let stopKeyCapture = null;   // 当前正在捕获的那一个（同一时刻只允许一个）

  function bindKeyCapture(modal) {
    if (!modal) return;

    modal.querySelectorAll('.key-btn').forEach(btn => {
      btn.addEventListener('click', () => startKeyCapture(btn));
    });

    // 清空 = 让 mod 用它的默认键（mod 读到空值会退回内置默认，例如变速的 Delete）
    modal.querySelectorAll('.key-clear').forEach(btn => {
      btn.addEventListener('click', () => {
        const target = modal.querySelector(`[data-field="${btn.dataset.clear}"]`);
        if (target) setKeyButtonValue(target, '');
      });
    });
  }

  function setKeyButtonValue(btn, name) {
    btn.dataset.key = name;
    btn.textContent = name || '未设置（用 mod 默认）';
  }

  function startKeyCapture(btn) {
    if (stopKeyCapture) stopKeyCapture();   // 前一个还开着：直接取消

    const original = btn.textContent;
    btn.classList.add('capturing');
    btn.textContent = '按下按键…（Esc 取消）';

    const onKeyDown = e => {
      e.preventDefault();
      e.stopImmediatePropagation();
      if (e.repeat) return;
      if (e.key === 'Escape' || e.code === 'Escape') { done('', '取消'); return; }
      const name = unityKeyNameFromCode(e.code);
      if (!name) { toast('这个键不支持，请换一个'); return; }
      done(name, '已设为 ' + name);
    };

    const onMouseDown = e => {
      e.preventDefault();
      e.stopImmediatePropagation();
      const name = UNITY_KEY_BY_MOUSE_BUTTON[e.button];
      if (!name) { toast('这个鼠标键不支持，请换一个'); return; }
      done(name, '已设为 ' + name);
    };

    // 鼠标侧键按下去还会让浏览器"后退/前进"：捕获期间一并压掉（在 WebView 里按侧键不该跳页）
    const swallow = e => { e.preventDefault(); e.stopImmediatePropagation(); };

    function done(name, message) {
      stop();
      if (name) setKeyButtonValue(btn, name);
      else btn.textContent = original;
      btn.blur();
      if (message) toast(message);
    }

    function stop() {
      document.removeEventListener('keydown', onKeyDown, true);
      document.removeEventListener('mousedown', onMouseDown, true);
      document.removeEventListener('mouseup', swallow, true);
      document.removeEventListener('auxclick', swallow, true);
      document.removeEventListener('click', swallow, true);
      document.removeEventListener('contextmenu', swallow, true);
      btn.classList.remove('capturing');
      stopKeyCapture = null;

      // 结束这一下自身的 click 还可能落到按钮上(用鼠标键绑定时必然如此), 吞掉它,
      // 否则按钮会立刻又开始捕获。没有后续 click 也没关系, 到点自动撤掉。
      const onceClick = e => { e.preventDefault(); e.stopImmediatePropagation(); removeOnce(); };
      const timer = setTimeout(removeOnce, 400);
      function removeOnce() {
        clearTimeout(timer);
        document.removeEventListener('click', onceClick, true);
      }
      document.addEventListener('click', onceClick, true);
    }

    stopKeyCapture = stop;
    document.addEventListener('keydown', onKeyDown, true);
    document.addEventListener('mousedown', onMouseDown, true);
    document.addEventListener('mouseup', swallow, true);
    document.addEventListener('auxclick', swallow, true);
    document.addEventListener('click', swallow, true);
    document.addEventListener('contextmenu', swallow, true);
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
