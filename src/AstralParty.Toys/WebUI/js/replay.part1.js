// replay.js - Full Replay Tool Logic Integrated within Tools Page (Light Theme)
(function () {
  // 下载进度用（宿主回推的是字节数）
  const fmtBytes = bytes => {
    const value = Number(bytes ?? 0);
    if (value >= 1024 * 1024) return (value / 1024 / 1024).toFixed(2) + ' MB';
    if (value >= 1024) return (value / 1024).toFixed(1) + ' KB';
    return value + ' B';
  };

  // 品质显示文本会跟随界面语言变化（普通/Common/通常/傳說…）。样式类名必须用
  // 稳定的颜色键，不能拿本地化后的文本拼 class，否则切语言后筹码底色就丢了。
  const QUALITY_TONE = {
    blue: 'blue', 普通: 'blue', common: 'blue', 通常: 'blue', 一般: 'blue',
    purple: 'purple', 稀有: 'purple', rare: 'purple', レア: 'purple',
    epic: 'epic', 史诗: 'epic', 史詩: 'epic',
    gold: 'gold', orange: 'gold', 传说: 'gold', 傳說: 'gold', legendary: 'gold', レジェンド: 'gold', 传奇: 'gold', 傳奇: 'gold'
  };
  const qualityTone = quality => {
    const key = String(quality ?? '').trim().toLowerCase();
    return QUALITY_TONE[key] || QUALITY_TONE[String(quality ?? '').trim()] || 'unknown';
  };

  const ReplayModule = {
    libraryData: null,
    // 当前是否在回放详情页：决定左上角按钮是「返回回放列表」还是「返回大厅」
    detailOpen: false,
    // 归档 / 放回 / 删除正在进行中（文件 IO + 之后整表重扫）：挡住重复点击
    busyButton: null,
    // 「按 ID 下载」弹窗正在下载的回放 ID（进度消息靠它认领自己的进度）
    downloadId: null,

    init() {
      this.bindEvents();
    },

    bindEvents() {
      // Sub-nav tab switcher（只绑外层页签：内层「数据统计/原始协议帧」也用 .subnav-tab，它们各有处理器）
      document.querySelectorAll('#replaySubnav .subnav-tab').forEach(tab => {
        tab.addEventListener('click', () => {
          if (!tab.dataset.tab) return;
          this.switchReplayTab(tab.dataset.tab);
        });
      });

      // Actions in Tools Header
      $('toolOpenBtn')?.addEventListener('click', () => post({ type: 'openReplay' }));
      $('toolExportBtn')?.addEventListener('click', () => post({ type: 'export' }));
      // 列表页返回大厅；详情页返回回放列表
      $('toolsBackHomeBtn')?.addEventListener('click', () => {
        if (this.detailOpen) this.showLibrary();
        else showPage('home');
      });

      // Empty state buttons
      $('refreshLibraryBtn')?.addEventListener('click', () => post({ type: 'refreshReplays' }));

      // 回放库：下载（按 ID）/ 导入 / 打开目录 / 筛选 / 横幅
      $('libraryDownloadBtn')?.addEventListener('click', () => this.openDownloadDialog());
      $('libraryImportBtn')?.addEventListener('click', () => post({ type: 'libraryImport' }));
      $('libraryOpenFolderBtn')?.addEventListener('click', () => post({ type: 'libraryOpenFolder' }));
      $('librarySearchInput')?.addEventListener('input', () => this.renderLibraryRows());
      $('libraryFilterSelect')?.addEventListener('change', () => this.renderLibraryRows());
      $('librarySortSelect')?.addEventListener('change', () => this.renderLibraryRows());
      $('recentReplayList')?.addEventListener('click', event => this.onLibraryClick(event));
      $('libraryBannerArchiveBtn')?.addEventListener('click', () => {
        const data = this.libraryData || {};
        const overflow = Math.max(0, (data.gameCount || 0) - (data.capacity || 10));
        post({ type: 'libraryArchiveOldest', count: overflow > 0 ? overflow : 3 });
      });
      $('libraryBannerAutoBtn')?.addEventListener('click', () => {
        const data = this.libraryData || {};
        // 保留局数用用户在设置页配的值；拿 capacity（游戏内上限，恒为 10）去覆盖会静默改掉用户的设置
        const keepInGame = data.keepInGame || data.capacity || 10;
        post({
          type: 'librarySaveSettings',
          libraryRoot: data.libraryRoot || null,
          autoMaintain: true,
          keepInGame
        });
        toast(`已开启自动托管：以后每次打开本工具都会自动把超出席位的旧回放归档进库（游戏内保留最近 ${keepInGame} 局）。`);
      });
      $('libraryBannerDismissBtn')?.addEventListener('click', () => {
        const data = this.libraryData || {};
        window.localStorage.setItem('libraryBannerSignature', `${data.gameCount || 0}/${data.capacity || 10}`);
        $('libraryBanner')?.classList.add('hidden');
      });

      // Technical sub tabs
      $('techTabStatistics')?.addEventListener('click', () => this.switchTechPane('stats'));
      $('techTabProtocol')?.addEventListener('click', () => this.switchTechPane('proto'));
    },

    onShowTools() {
      // 详情页（有已解析回放）或回放列表（按 ID 下载 / 从缓存打开）
      if (AppState.replayReport) this.setDetailMode(true);
      else this.showLibrary();
    },

    /** 切换列表 / 详情布局，并同步左上角返回按钮的文案与行为。 */
    setDetailMode(inDetail) {
      this.detailOpen = !!inDetail;
      $('replayEmptyBox')?.classList.toggle('hidden', this.detailOpen);
      $('replayWorkspaceBox')?.classList.toggle('hidden', !this.detailOpen);
      $('replaySubnav')?.classList.toggle('hidden', !this.detailOpen);
      if ($('toolExportBtn')) $('toolExportBtn').disabled = !this.detailOpen;

      const back = $('toolsBackHomeBtn');
      if (back) {
        const key = this.detailOpen ? 'replay.back_to_list' : 'replay.back_to_lobby';
        back.dataset.i18n = key;
        if (typeof window.t === 'function') back.textContent = window.t(key);
      }

      const meta = $('toolFileMeta');
      if (meta && this.detailOpen) {
        meta.removeAttribute('data-i18n');
      } else if (meta) {
        meta.dataset.i18n = 'replay.list_hint';
        if (typeof window.t === 'function') meta.textContent = window.t('replay.list_hint');
      }
    },

    showLibrary() {
      this.setDetailMode(false);
      // 回到列表即丢弃上一局详情，避免从大厅再次进入时又跳回旧报告
      AppState.replayReport = null;
      AppState.selectedFrame = -1;
      post({ type: 'refreshReplays' });
    },

    switchReplayTab(tabName) {
      AppState.activeReplayTab = tabName;
      document.querySelectorAll('#replaySubnav .subnav-tab').forEach(tab => {
        tab.classList.toggle('active', tab.dataset.tab === tabName);
      });

      const current = document.querySelector('.replay-view:not(.hidden)');
      const next = $(`${tabName}View`);
      if (!next || current === next) return;
      current?.classList.add('replay-view-leaving');
      window.setTimeout(() => {
        document.querySelectorAll('.replay-view').forEach(view => {
          view.classList.add('hidden');
          view.classList.remove('replay-view-leaving');
        });
        next.classList.remove('hidden');
        next.classList.add('replay-view-entering');
        requestAnimationFrame(() => next.classList.remove('replay-view-entering'));
      }, document.body.classList.contains('reduce-motion') ? 0 : 120);
    },

    switchTechPane(paneName) {
      const isStats = paneName === 'stats';
      $('techStatsPane')?.classList.toggle('hidden', !isStats);
      $('techProtocolPane')?.classList.toggle('hidden', isStats);
      $('techTabStatistics')?.classList.toggle('active', isStats);
      $('techTabProtocol')?.classList.toggle('active', !isStats);
    },

    renderLibrary(library) {
      this.libraryData = library || null;
      AppState.replayLibrary = library;
      // 列表回来了（归档/放回/删除/刷新都会走到这里）：解除行内操作的忙碌锁
      this.clearRowBusy();

      const dirEl = $('libraryDirectoryText');
      if (dirEl) dirEl.textContent = library?.directory || '未检测到游戏回放目录';

      this.renderLibraryStatus();
      this.renderLibraryRows();
    },

    renderLibraryStatus() {
      const data = this.libraryData;
      if (!data) return;

      const capacity = data.capacity || 10;
      const gameCount = data.gameCount || 0;
      const slotsEl = $('libraryGameSlots');
      if (slotsEl) {
        slotsEl.textContent = `${gameCount} / ${capacity}`;
        slotsEl.parentElement?.classList.toggle('full', gameCount >= capacity);
      }
      const barEl = $('librarySlotBar');
      if (barEl) barEl.style.width = `${Math.min(100, Math.round((gameCount / capacity) * 100))}%`;

      const countEl = $('libraryCountText');
      if (countEl) {
        countEl.textContent = data.libraryAvailable
          ? `${fmt(data.libraryCount)} 局 · ${data.librarySizeText}`
          : '库目录不可用';
      }

      const runningHint = $('libraryGameRunningHint');
      if (runningHint) {
        runningHint.textContent = data.gameRunning
          ? '游戏正在运行：归档/放回随时可用，但别在游戏里正播着回放时操作'
          : '';
      }

      const warnBox = $('libraryWarningBox');
      if (warnBox) {
        const warnings = data.warnings || [];
        warnBox.classList.toggle('hidden', warnings.length === 0);
        warnBox.innerHTML = warnings.map(w => esc(w)).join('<br>');
      }

      const banner = $('libraryBanner');
      if (!banner) return;
      const signature = `${gameCount}/${capacity}`;
      const isFull = gameCount >= capacity;
      const isNear = gameCount >= capacity - 1 && !isFull;
      // 席位腾出来之后清掉「稍后再说」的签名，否则下次再存满时这条提醒永远不会回来
      if (!isFull && !isNear) window.localStorage.removeItem('libraryBannerSignature');
      const dismissedFor = window.localStorage.getItem('libraryBannerSignature');
      const show = data.available && !data.sameFolder && (isFull || isNear) && dismissedFor !== signature;

      banner.classList.toggle('hidden', !show);
      banner.classList.toggle('soft', !isFull);
      if (show) {
        const titleEl = $('libraryBannerTitle');
        const textEl = $('libraryBannerText');
        if (titleEl) {
          titleEl.textContent = isFull
            ? `游戏内回放席位已满（${gameCount}/${capacity}）`
            : `游戏内回放席位快满了（${gameCount}/${capacity}）`;
        }
        if (textEl) {
          textEl.textContent = isFull
            ? '再在游戏里点「保存」会提示已达上限。归档最旧的几局就能立刻腾出席位——回放不会丢，它们会进上面的回放库。'
            : '离上限只差一点。开启自动托管后，每次打开本工具都会自动把超出的旧回放归档进库。';
        }
        const archiveBtn = $('libraryBannerArchiveBtn');
        if (archiveBtn) {
          const overflow = Math.max(0, gameCount - capacity);
          const take = overflow > 0 ? overflow : 3;
          archiveBtn.textContent = `归档最旧的 ${take} 局`;
        }
        // 自动托管已经开着就别再劝一次，并按真实配置显示保留局数
        const autoBtn = $('libraryBannerAutoBtn');
        if (autoBtn) {
          const keep = data.keepInGame || capacity;
          const alreadyOn = data.autoMaintain === true;
          autoBtn.disabled = alreadyOn;
          autoBtn.textContent = alreadyOn ? `自动托管已开启（保留 ${keep} 局）` : `开启自动托管（保留 ${keep} 局）`;
        }
      }
    },

    renderLibraryRows() {
      const container = $('recentReplayList');
      const data = this.libraryData;
      if (!container || !data) return;

      const query = ($('librarySearchInput')?.value || '').trim().toLowerCase();
      const filter = $('libraryFilterSelect')?.value || 'all';
      const sort = $('librarySortSelect')?.value || 'time';

      let items = (data.entries || []).slice();
      if (filter === 'game') items = items.filter(x => x.inGame);
      else if (filter === 'library') items = items.filter(x => x.inLibrary);
      if (query) {
        items = items.filter(x => [x.replayId, x.mapName, x.playersText, x.heroesText, x.finishText, x.gameVersion]
          .some(value => String(value ?? '').toLowerCase().includes(query)));
      }
      if (sort === 'size') items.sort((a, b) => (b.sizeBytes || 0) - (a.sizeBytes || 0));
      else if (sort === 'id') items.sort((a, b) => String(b.replayId).localeCompare(String(a.replayId)));
      else items.sort((a, b) => (b.sortTime || 0) - (a.sortTime || 0));

      const countEl = $('libraryListCount');
      if (countEl) countEl.textContent = `${items.length} / ${(data.entries || []).length} 条`;

      if (!items.length) {
        container.innerHTML = `<div class="library-empty">${
          data.available
            ? '还没有回放。在游戏里打完一局后，到「战绩」里对那条记录点「保存回放」，再回来刷新即可。'
            : '未检测到游戏回放目录。你仍然可以用左侧按钮直接打开任意回放文件。'
        }</div>`;
        return;
      }

      container.innerHTML = items.map(item => this.libraryRow(item)).join('');
    },

    libraryRow(item) {
      const badges = [];
      if (item.inGame) badges.push(`<span class="badge badge-game">${esc(window.t?.('replay.badge_game') || '游戏内')}</span>`);
      if (item.inLibrary) badges.push(`<span class="badge badge-library">${esc(window.t?.('replay.badge_library') || '回放库')}</span>`);
      if (!item.healthy) badges.push(`<span class="badge badge-warn" title="${esc(item.error || '解析不出结算帧，文件可能已损坏或来自不兼容的版本。')}">${esc(window.t?.('replay.badge_unparseable') || '无法解析')}</span>`);

      const actions = [`<button data-action="open">${esc(window.t?.('replay.action_view') || '查看')}</button>`];
      const supportedVersion = this.libraryData?.upgradeTargetVersion || '';
      let upgradeTitle = '';
      if (!item.healthy) upgradeTitle = item.error || '无法解析，不能升级';
      else if (!supportedVersion) upgradeTitle = '本工具没有可用的版本配置';
      else if (item.isCurrentVersion) upgradeTitle = window.t?.('replay.upgrade_current') || '已是当前支持版本';
      else if (item.isNewerVersion) upgradeTitle = (window.t?.('replay.upgrade_newer', { version: supportedVersion }) || `版本高于本工具支持的 ${supportedVersion}`);
      else if (!item.gameVersion || item.gameVersion === '—') upgradeTitle = window.t?.('replay.upgrade_unknown') || '文件里没有版本标记';
      else upgradeTitle = window.t?.('replay.upgrade_tooltip', { version: supportedVersion }) || `升级到本工具支持的 ${supportedVersion}`;
      actions.push(`<button data-action="upgrade" ${item.canUpgrade ? '' : 'disabled'} title="${esc(upgradeTitle)}">${esc(window.t?.('replay.action_upgrade') || '升级')}</button>`);
      if (item.inGame) actions.push(`<button data-action="archive">${esc(window.t?.('replay.action_archive') || '归档')}</button>` );
      if (item.inLibrary) actions.push(`<button data-action="restore">${esc(window.t?.('replay.action_restore') || '放回游戏')}</button>`);
      actions.push(`<button class="danger" data-action="delete">${esc(window.t?.('common.delete') || '删除')}</button>`);

      const title = [item.mapName, item.finishText, item.durationText].filter(v => v && v !== '—').join(' · ');
      const frameSuffix = window.t?.('replay.frames') || '帧';
      const roundSuffix = window.t?.('replay.round') || '回合';
      const versionPrefix = window.t?.('home.game_version') || '版本';
      const facts = [
        `ID ${item.replayId}`,
        item.sizeText,
        item.frameCount ? `${fmt(item.frameCount)} ${frameSuffix}` : '',
        item.roundCount ? `${item.roundCount} ${roundSuffix}` : '',
        item.gameVersion !== '—' ? `${versionPrefix} ${item.gameVersion}` : ''
      ].filter(Boolean).join(' · ');

      return `
        <div class="replay-entry-row library-row" data-token="${esc(item.token)}" data-replay-id="${esc(item.replayId)}">
          <div class="entry-main">
            <span class="entry-icon">${Icons.svg('play_arrow', 'mi-lg')}</span>
            <div class="entry-meta">
              <strong>${esc(title || item.replayId)}</strong>
              <small>${esc(facts)}</small>
              <div class="entry-badges">${badges.join('')}</div>
            </div>
          </div>
          <div class="entry-side">
            <span class="entry-date">${esc(item.playersText && item.playersText !== '—' ? item.playersText : '')}</span>
            <div class="entry-actions">${actions.join('')}</div>
          </div>
        </div>`;
    },

    onLibraryClick(event) {
      const row = event.target.closest('.library-row');
      if (!row) return;
      const token = row.dataset.token;
      const replayId = row.dataset.replayId;
      const button = event.target.closest('button[data-action]');

      if (!button) {
        if (token) post({ type: 'openRecentReplay', token });
        return;
      }
      event.stopPropagation();

      // 归档 / 放回 期间再点一次会发出第二个请求，第二次必然命中
      // 「游戏目录里没有这个回放」的分支，弹出让人以为操作失败的提示
      if (this.busyButton) return;
      if (button.dataset.action !== 'delete') this.setRowBusy(button);

      switch (button.dataset.action) {
        case 'open':
          post({ type: 'openRecentReplay', token });
          break;
        case 'upgrade':
          post({ type: 'libraryUpgrade', ids: [replayId] });
          break;
        case 'archive':
          post({ type: 'libraryArchive', ids: [replayId] });
          break;
        case 'restore':
          post({ type: 'libraryRestore', ids: [replayId] });
          break;
        case 'delete':
          this.confirmDelete(replayId);
          break;
      }
    },

    /** 行内操作进行中：按钮置灰 + aria-busy（列表回来后由 clearRowBusy 解除）。 */
    setRowBusy(button) {
      this.busyButton = button;
      button.disabled = true;
      button.setAttribute('aria-busy', 'true');
      // 兜底：宿主万一没回音，15 秒后自己解锁，不把按钮永久卡死
      clearTimeout(this.busyTimer);
      this.busyTimer = setTimeout(() => this.clearRowBusy(), 15000);
    },

    clearRowBusy() {
      clearTimeout(this.busyTimer);
      this.busyTimer = null;
      const button = this.busyButton;
      this.busyButton = null;
      if (button) {
        button.disabled = false;
        button.removeAttribute('aria-busy');
      }
    },

    confirmDelete(replayId) {
      const entry = (this.libraryData?.entries || []).find(x => x.replayId === replayId) || {};
      const options = [];
      if (entry.inLibrary) options.push('<button class="secondary-btn" data-delete-target="library">只删库内副本（游戏里仍能看）</button>');
      if (entry.inGame) options.push('<button class="secondary-btn" data-delete-target="game">只删游戏内副本（腾出席位）</button>');
      if (entry.inLibrary && entry.inGame) options.push('<button class="secondary-btn" data-delete-target="both">两边都删</button>');
      if (!options.length) return;

