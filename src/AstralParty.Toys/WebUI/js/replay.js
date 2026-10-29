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

      openModal(`删除回放 ${replayId}`, `
        <p style="font-size:13px;color:var(--gp-text-sub);line-height:1.75;margin:0 0 14px">
          删除会优先放进 Windows 回收站，仍可还原。只删游戏内副本是腾席位的常用做法；库内副本才是长期存档。
        </p>
        <div style="display:flex;flex-direction:column;gap:8px;align-items:flex-start">${options.join('')}</div>
      `);

      $('modalBody')?.querySelectorAll('button[data-delete-target]').forEach(button => {
        button.addEventListener('click', () => {
          post({ type: 'libraryDelete', ids: [replayId], target: button.dataset.deleteTarget });
          closeModal();
        });
      });
    },

    // ============================== 按 ID 下载（官方回放 CDN → 回放库） ==============================

    /** 区服选项。取值与游戏 ReplayLogic.ChangeReplayCDNUrl(index) 的编号一致。 */
    cdnOptions() {
      return [
        { value: 'CnProd', label: '国服（正式服）' },
        { value: 'CnDev', label: '国服（测试服）' },
        { value: 'JpProd', label: '国际服（正式服）' },
        { value: 'JpDev', label: '国际服（测试服）' }
      ];
    },

    /** 默认区服跟随「当前游戏」的版本：国际服 → 国际服正式服，其余（国服 / TapTap / 自定义）→ 国服正式服。 */
    defaultCdn() {
      const profiles = AppState.gameProfiles?.profiles || [];
      const active = profiles.find(profile => profile.active);
      return active?.edition === 'global' ? 'JpProd' : 'CnProd';
    },

    openDownloadDialog() {
      const options = this.cdnOptions()
        .map(item => `<option value="${item.value}">${esc(item.label)}</option>`)
        .join('');
      openModal('按回放 ID 下载到回放库', `
        <p class="download-intro">
          回放由服务器录制后放在官方回放服务器上，游戏里点「保存回放」也是从那里拉的。
          填上回放 ID 就能直接下载进<strong>回放库</strong>：不用打开游戏，也不占游戏内的 10 个席位。
        </p>
        <div class="download-form">
          <label class="download-field">
            <span>回放 ID</span>
            <input class="library-input wide" id="replayDownloadIdInput" inputmode="numeric" autocomplete="off"
                   placeholder="战绩里那一长串数字" aria-label="回放 ID">
          </label>
          <label class="download-field">
            <span>回放服务器（不确定就按当前游戏版本选）</span>
            <select class="library-select" id="replayDownloadCdnSelect" aria-label="回放服务器">${options}</select>
          </label>
        </div>
        <div class="download-progress hidden" id="replayDownloadProgress">
          <div class="download-bar" id="replayDownloadBarWrap"><i id="replayDownloadBar"></i></div>
          <span id="replayDownloadText"></span>
        </div>
        <p class="download-status hidden" id="replayDownloadStatus"></p>
        <div class="download-actions">
          <button class="primary-btn" id="replayDownloadStartBtn">开始下载</button>
          <button class="secondary-btn hidden" id="replayDownloadCancelBtn">取消下载</button>
        </div>
      `);

      const select = $('replayDownloadCdnSelect');
      if (select) select.value = this.defaultCdn();
      this.downloadId = null;

      const start = () => this.startDownload();
      $('replayDownloadStartBtn')?.addEventListener('click', start);
      $('replayDownloadIdInput')?.addEventListener('keydown', event => {
        if (event.key === 'Enter') { event.preventDefault(); start(); }
      });
      $('replayDownloadCancelBtn')?.addEventListener('click', () => {
        const button = $('replayDownloadCancelBtn');
        if (button) button.disabled = true;
        this.setDownloadStatus('正在取消下载…', false);
        post({ type: 'replayDownloadCancel' });
      });
      $('replayDownloadIdInput')?.focus();
    },

    startDownload() {
      const replayId = ($('replayDownloadIdInput')?.value || '').trim();
      if (!/^\d{1,32}$/.test(replayId)) {
        this.setDownloadStatus('回放 ID 只能是数字——就是游戏战绩里那一长串数字。', true);
        return;
      }
      this.downloadId = replayId;
      this.setDownloadStatus('', false);
      this.setDownloadRunning(true);
      this.updateDownloadProgress({ receivedBytes: 0, totalBytes: null, percent: null });
      post({
        type: 'replayDownload',
        replayId,
        cdn: $('replayDownloadCdnSelect')?.value || 'CnProd'
      });
    },

    setDownloadRunning(running) {
      const start = $('replayDownloadStartBtn');
      const cancel = $('replayDownloadCancelBtn');
      if (start) {
        start.disabled = running;
        start.textContent = running ? '下载中…' : '开始下载';
      }
      if (cancel) {
        cancel.disabled = false;
        cancel.classList.toggle('hidden', !running);
      }
      $('replayDownloadProgress')?.classList.toggle('hidden', !running);
    },

    setDownloadStatus(text, isError) {
      const box = $('replayDownloadStatus');
      if (!box) return;
      box.textContent = text || '';
      box.classList.toggle('hidden', !text);
      box.classList.toggle('ok', !!text && !isError);
    },

    updateDownloadProgress(payload) {
      const percent = payload?.percent;
      const bar = $('replayDownloadBar');
      $('replayDownloadBarWrap')?.classList.toggle('indeterminate', percent == null);
      if (bar && percent != null) bar.style.width = `${percent}%`;

      const text = $('replayDownloadText');
      if (!text) return;
      const received = fmtBytes(payload?.receivedBytes || 0);
      text.textContent = percent != null
        ? `已下载 ${received} / ${fmtBytes(payload.totalBytes)}（${percent}%）`
        : `已下载 ${received}`;
    },

    /** 宿主回推的下载状态：running = 进度，其余三种是收尾。 */
    renderDownloadState(payload) {
      if (!payload) return;
      // 弹窗已经关掉（或被别的弹窗顶掉）时忽略进度：列表刷新另有通道
      if (!$('globalModal')?.classList.contains('open') || !$('replayDownloadIdInput')) return;
      // 只认领本弹窗正在下载的那个 ID
      if (this.downloadId && payload.replayId && payload.replayId !== this.downloadId) return;

      if (payload.state === 'running') {
        this.updateDownloadProgress(payload);
        return;
      }

      this.downloadId = null;
      this.setDownloadRunning(false);
      const messages = (payload.messages || []).filter(Boolean);

      if (payload.state === 'done') {
        closeModal();
        toast(payload.summary || '下载完成。');
        // 下载成功但解析不出结算帧这类提醒别丢掉（它决定"能不能放回游戏"）
        if (messages.length > 1) {
          openModal('下载完成', `
            <div class="download-messages">${messages.map(message => `<div>${esc(message)}</div>`).join('')}</div>
          `);
        }
        return;
      }

      // 失败 / 取消：弹窗留着，ID 可以直接改
      this.setDownloadStatus(messages.join('\n') || payload.summary || '下载失败。', payload.state !== 'canceled');
    },

    renderLibraryResult(payload) {
      if (!payload) return;
      toast(payload.summary || (payload.ok ? '操作完成。' : '操作失败。'));
      const messages = payload.messages || [];

      if (payload.needsConfirmation && (payload.pendingIds || []).length) {
        const ids = payload.pendingIds;
        openModal('这些回放解析不出结算帧', `
          <p style="font-size:13px;color:var(--gp-text-sub);line-height:1.75;margin:0 0 12px">
            本工具读不出它们的结算帧（工具自带的协议版本可能比游戏旧）。如果游戏也读不出来，
            <strong style="color:#fca5a5">游戏在「本地回放」列表里遇到这种目录会把它整个删掉</strong>。
            确认要继续放回，就点下面的按钮；库里的副本无论如何都会保留。
          </p>
          <p style="font-size:12px;color:var(--gp-text-muted);margin:0 0 14px;word-break:break-all">${ids.map(id => esc(id)).join('<br>')}</p>
          <div style="display:flex;gap:10px">
            <button class="primary-btn" id="libraryForceRestoreBtn">仍要放回（${ids.length} 份）</button>
            <button class="secondary-btn" id="libraryCancelRestoreBtn">取消</button>
          </div>
        `);
        $('libraryForceRestoreBtn')?.addEventListener('click', () => {
          post({ type: 'libraryRestore', ids, allowUnparseable: true });
          closeModal();
        });
        $('libraryCancelRestoreBtn')?.addEventListener('click', closeModal);
        return;
      }

      if (messages.length > 1) {
        openModal('回放库操作结果', `
          <div style="display:flex;flex-direction:column;gap:7px;font-size:12.5px;line-height:1.65">
            ${messages.map(message => `<div>${esc(message)}</div>`).join('')}
          </div>
        `);
      }
    },

    renderReport(report) {
      if (!report) return;

      // 换了一局：清掉上一局选中的帧，否则协议表里会有一行"假选中"，
      // 右边详情却是"请在左侧点击任意协议帧"的占位，自相矛盾
      AppState.selectedFrame = -1;

      this.setDetailMode(true);

      if ($('toolFileMeta')) {
        const frameCount = report.match?.frameCount != null ? `${report.match.frameCount.toLocaleString()} 帧` : '';
        const fileName = report.file?.fileName || '对战回放';
        const fileSize = report.file?.fileSizeText || '';
        $('toolFileMeta').removeAttribute('data-i18n');
        $('toolFileMeta').textContent = `${fileName} · ${fileSize} · ${frameCount}`;
      }

      try { this.renderSummary(report); } catch (e) { console.error('Summary render error:', e); }
      try { this.renderPerformance(report); } catch (e) { console.error('Performance render error:', e); }
      try { this.renderPlayers(report); } catch (e) { console.error('Players render error:', e); }
      try { this.renderTimeline(report); } catch (e) { console.error('Timeline render error:', e); }
      try { this.renderRelics(report); } catch (e) { console.error('Relics render error:', e); }
      try { this.renderShops(report); } catch (e) { console.error('Shops render error:', e); }
      try { this.renderProtocol(report); } catch (e) { console.error('Protocol render error:', e); }
      try { this.renderStatistics(report); } catch (e) { console.error('Statistics render error:', e); }

      this.switchReplayTab(AppState.activeReplayTab || 'summary');
      this.switchTechPane('stats');
    },

    renderSummary(report) {
      const m = report.match;
      const root = $('summaryView');
      if (!root) return;

      root.innerHTML = `
        <div class="summary-grid">
          <article class="map-banner-card">
            <div class="map-img-box">
              ${m.mapImage ? `<img src="${esc(m.mapImage)}" alt="">` : ''}
            </div>
            <div class="map-info">
              <small>本局地图</small>
              <h3>${esc(m.mapName)}</h3>
              <p>难度 ${m.difficulty} · 最终首领 ${esc(m.bossName)}</p>
            </div>
          </article>

          <article class="result-banner-card">
            <div class="result-top-bar">
              <div>
                <small style="font-size:11px;color:var(--text-muted);font-weight:600">对局结果</small>
                <div class="result-outcome">${esc(m.resultText)}</div>
                <div class="result-winner">${esc(m.winnerText)}</div>
              </div>
              <div style="text-align:right">
                <small style="font-size:11px;color:var(--text-muted);font-weight:600">结算奖励</small>
                <p style="margin:4px 0 0;font-size:13px;color:var(--accent-gold);font-weight:700">${esc(m.awardsText)}</p>
              </div>
            </div>

            <div class="metrics-row">
              <div class="metric-cell"><small>时长</small><strong>${esc(m.durationText)}</strong></div>
              <div class="metric-cell"><small>回合</small><strong>${esc(m.roundCount)}</strong></div>
              <div class="metric-cell"><small>关卡进度</small><strong>${esc(m.progressText)}</strong></div>
              <div class="metric-cell"><small>死亡计数</small><strong>${esc(m.playerDeaths)}</strong></div>
            </div>
          </article>
        </div>

        ${this.transferSummary(report)}
        ${this.monsterSummary(report)}

        <div style="margin-bottom:14px;display:flex;align-items:center;justify-content:space-between">
          <h3 style="margin:0;font-size:16px;color:var(--text-main)">参战玩家 (${report.players.length} 名)</h3>
        </div>
        <div class="players-cards-grid">
          ${report.players.map(p => this.playerCard(p)).join('')}
        </div>
      `;
    },

    transferSummary(report) {
      const players = report.players || [];
      const total = players.reduce((sum, p) => sum + (p.transferGold || 0), 0);
      return `
        <section class="transfer-summary">
          <header>
            <div>
              <h3>玩家转账贡献</h3>
              <p>来自回合快照中的累计转账；未记录收款对象时不做推断。</p>
            </div>
            <span>合计 <strong>${fmt(total)}</strong> 星币</span>
          </header>
          <div class="transfer-player-grid">
            ${players.map(p => `
              <article title="${esc(p.nickname)} · 累计转账 ${fmt(p.transferGold || 0)} 星币">
                ${p.avatar ? `<img src="${esc(p.avatar)}" alt="">` : ''}
                <div><strong>${esc(p.nickname)}</strong><p>转出 <b>${fmt(p.transferGold || 0)}</b> 星币</p></div>
              </article>`).join('')}
          </div>
        </section>`;
    },

    monsterSummary(report) {
      const monsters = report.monsters || [];
      const steps = report.monsterTileSteps || [];
      const tileMonsters = monsters.filter(m => m.spawnedByMonsterTile);
      const summary = report.monsterSummary || {
        total: monsters.length,
        defeated: monsters.filter(m => m.statusKey === 'defeated').length,
        alive: monsters.filter(m => m.statusKey === 'alive').length,
        spawnTile: monsters.filter(m => m.spawnedOnMonsterTile).length,
        tileSpawned: tileMonsters.length,
        tileDefeated: tileMonsters.filter(m => m.statusKey === 'defeated').length,
        steps: steps.length,
        tileSteps: steps.filter(step => (step.monsterIds || []).length > 0).length
      };
      if (!tileMonsters.length && !steps.length) return '';

      const byPlayer = new Map();
      for (const step of steps) {
        const key = String(step.playerId);
        const entry = byPlayer.get(key) || { name: step.playerName || `玩家 ${key}`, count: 0 };
        entry.count += 1;
        byPlayer.set(key, entry);
      }

      const groups = new Map();
      for (const monster of tileMonsters) {
        const key = String(monster.heroId);
        const entry = groups.get(key) || {
          name: monster.monsterName || `怪物 ${key}`,
          image: monster.image || '',
          count: 0,
          defeated: 0
        };
        entry.count += 1;
        if (monster.statusKey === 'defeated') entry.defeated += 1;
        groups.set(key, entry);
      }

      const monsterName = id => monsters.find(monster => String(monster.id) === String(id))?.monsterName || `怪物 ${id}`;
      const stepRows = steps.map(step => {
        const names = step.monsterNames?.length
          ? step.monsterNames
          : (step.monsterIds || []).map(monsterName);
        return { step, names };
      });
      return `
        <section class="transfer-summary monster-summary">
          <header>
            <div>
              <h3>刷怪地块记录</h3>
              <p>只记录踩中后同一快照内确实刷出怪物的踩踏；经过不停下、或踩中但没有刷出怪物都不计入。</p>
            </div>
            <span>踩中并触发刷新 <strong>${fmt(summary.steps)}</strong> 次</span>
          </header>
          <div class="monster-summary-grid">
            <article class="monster-step-card">
              <small>触发刷新的踩中</small>
              <strong class="monster-step-total">${fmt(summary.steps)}</strong>
              <div class="monster-step-players">
                ${byPlayer.size
                  ? [...byPlayer.values()].map(item => `<span>${esc(item.name)} ×${item.count}</span>`).join('')
                  : '<span class="muted">本局未记录到踩中并触发刷新的情况</span>'}
              </div>
            </article>
            <article class="monster-spawn-card">
              <small>刷怪地块刷出 ${fmt(summary.tileSpawned)} 只 · 已击败 ${fmt(summary.tileDefeated)}</small>
              <div class="monster-spawn-grid">
                ${groups.size
                  ? [...groups.values()].map(item => `
                    <div class="monster-spawn-item" title="${esc(item.name)} · 刷出 ${item.count} 只 · 已击败 ${item.defeated} 只">
                      ${item.image ? `<img src="${esc(item.image)}" alt="">` : `<span class="monster-spawn-placeholder">?</span>`}
                      <div><strong>${esc(item.name)}</strong><span>×${item.count}${item.defeated ? ` · 击败 ${item.defeated}` : ''}</span></div>
                    </div>`).join('')
                  : '<span class="muted">本局未记录到刷怪地块刷出的怪物</span>'}
              </div>
            </article>
          </div>
          ${stepRows.length ? `
            <div class="monster-step-list">
              ${stepRows.filter(({ names }) => names.length > 0).map(({ step, names }) => `
                <div class="monster-step-row">
                  <span class="monster-step-meta">第 ${step.round} 回合 · ${esc(step.playerName)} · 节点 ${step.nodeId}</span>
                  <span class="monster-step-result">${names.map(esc).join('、')}</span>
                </div>`).join('')}
            </div>` : ''}
        </section>`;
    },

    playerCard(p) {
      return `
        <article class="player-card">
          <div class="player-head">
            <div class="player-avatar-box">
              ${p.avatar ? `<img src="${esc(p.avatar)}" alt="">` : `<div style="display:grid;place-items:center;height:100%;font-size:22px;color:var(--primary)">${Icons.svg('person')}</div>`}
            </div>
            <div class="player-titles">
              <h4>${esc(p.nickname)}${p.finalBossKill ? ` <span style="color:var(--accent-gold)">${Icons.svg('military_tech')} 决胜</span>` : ''}</h4>
              <small>${esc(p.heroName)} · 等级 ${p.accountLevel}</small>
            </div>
          </div>
          <div class="player-stat-badges">
            <div class="stat-badge-item"><small>伤害</small><b>${fmt(p.damage)}</b></div>
            <div class="stat-badge-item"><small>击杀</small><b>${fmt(p.kills)}</b></div>
            <div class="stat-badge-item"><small>筹码</small><b>${fmt(p.selectedRelicCount)}</b></div>
          </div>
          <p class="player-transfer-line">转账贡献 <b>${fmt(p.transferGold || 0)}</b> 星币</p>
        </article>
      `;
    },

    renderPerformance(report) {
      const root = $('performanceView');
      if (!root) return;

      const data = report.performance || [];
      const players = report.players || [];
      const colorList = ['#73b8ff', '#ff91bb', '#e8c65e', '#83d8a1', '#ba9bff', '#ffac73', '#67e8f9', '#f472b6', '#fbbf24', '#a3e635'];
      const playerIndex = id => Math.max(0, players.findIndex(p => String(p.id) === String(id)));
      const colorOf = id => colorList[playerIndex(id) % colorList.length];
      const playerName = id => players.find(p => String(p.id) === String(id))?.nickname || `玩家 ${id}`;
      const metrics = {
        damage: { label: '伤害', total: 'damage', delta: 'damageDelta' },
        transfer: { label: '转账星币', total: 'transfer', delta: 'transferDelta' },
        cards: { label: '出牌次数', total: 'cards', delta: 'cardsDelta' },
        earnedGold: { label: '累计星币', total: 'earnedGold', delta: 'earnedGoldDelta' },
        currentGold: { label: '当前星币', total: 'currentGold', delta: 'currentGoldDelta' },
        deaths: { label: '累计死亡', total: 'deaths', delta: 'deathsDelta' },
        kills: { label: '击杀数量（怪物）', total: 'kills', delta: 'killsDelta' }
      };

      if (!data.length) {
        root.innerHTML = '<div class="data-table-card performance-empty"><h3>对局表现分析</h3><p>当前报告没有逐回合数据，请重新打开原始回放。</p></div>';
        return;
      }

      const selected = new Set(data.map(item => String(item.playerId)));
      let metric = 'damage';
      let cumulative = true;
      const rounds = [...new Set(data.flatMap(item => (item.points || []).map(point => point.round)))].sort((a, b) => a - b);
      const totalTransfer = players.reduce((sum, p) => sum + (p.transferGold || 0), 0);

      root.innerHTML = `
        <section class="performance-panel">
          <header class="performance-hero">
            <div>
              <p class="performance-eyebrow">回放复盘</p>
              <h3>对局表现分析</h3>
              <p>用输出、支援和行动数据对照对局过程；不同角色分工不同，不以单一总伤害评定玩家。</p>
            </div>
            <div class="performance-total"><small>全队转账</small><strong>${fmt(totalTransfer)}</strong><span>星币</span></div>
          </header>

          <div class="performance-controls">
            <label>指标<select id="performanceMetric">${Object.entries(metrics).map(([key, value]) => `<option value="${key}">${value.label}</option>`).join('')}</select></label>
            <label>口径<select id="performanceMode"><option value="true">累计</option><option value="false">每回合新增</option></select></label>
            <div class="performance-players">${data.map(item => `
              <label class="performance-player-toggle" style="--player-color:${colorOf(item.playerId)}">
                <input type="checkbox" data-performance-player="${esc(item.playerId)}" checked>
                <span>${esc(playerName(item.playerId))}</span>
              </label>`).join('')}
            </div>
          </div>

          <div class="performance-chart-card">
            <div id="performanceChart"></div>
            <div id="performanceTooltip" class="performance-tooltip" hidden></div>
          </div>

          <div class="performance-player-grid" id="performancePlayerCards"></div>

          <div class="performance-section-head"><h4>玩家数据对照</h4><p>结算快照中的总伤害、承伤、治疗、转账与行动次数。</p></div>
          <div class="table-scroll-wrap performance-table-wrap">
            <table class="light-table">
              <thead><tr><th>玩家</th><th>总伤害</th><th>总承伤</th><th>治疗</th><th>转账星币</th><th>击杀</th><th>移动点数</th><th>卡牌 / 技能</th></tr></thead>
              <tbody>${players.map(p => `
                <tr>
                  <td><strong>${esc(p.nickname)}</strong></td>
                  <td>${fmt(p.damage)}</td>
                  <td>${fmt(p.injured)}</td>
                  <td>${fmt(p.healing)}</td>
                  <td style="color:var(--accent-gold);font-weight:800">${fmt(p.transferGold || 0)}</td>
                  <td>${fmt(p.kills)}</td>
                  <td>${fmt(p.movePoints)}</td>
                  <td>${fmt(p.usedCards)} / ${fmt(p.usedSkills)}</td>
                </tr>`).join('')}
              </tbody>
            </table>
          </div>

          <div class="performance-section-head"><h4>移动骰子统计</h4><p>仅统计回放中实际收到的移动掷骰结果；保留多骰的每颗点数，受控移动单独标记，不混入战斗骰或检定骰。</p></div>
          <div class="performance-dice-grid" id="performanceDice"></div>

          <details class="performance-rounds">
            <summary>逐回合数据表</summary>
            <div class="table-scroll-wrap" id="performanceRoundsTable"></div>
          </details>
        </section>`;

      const activeMetric = () => metrics[metric];
      const key = () => metric === 'currentGold' || cumulative ? activeMetric().total : activeMetric().delta;
      const modeText = () => metric === 'currentGold' ? '当前' : cumulative ? '累计' : '每回合新增';

      const renderChart = () => {
        const field = key();
        const visible = data.filter(item => selected.has(String(item.playerId)));
        const values = visible.flatMap(item => (item.points || []).map(point => Number(point[field]) || 0));
        const min = Math.min(0, ...values);
        const max = Math.max(1, ...values);
        const width = 900, height = 300, left = 64, right = 28, top = 20, bottom = 38;
        const plotWidth = width - left - right, plotHeight = height - top - bottom;
        const maxRound = Math.max(1, ...rounds);
        const x = round => left + (round / maxRound) * plotWidth;
        const y = value => top + plotHeight - ((value - min) / (max - min)) * plotHeight;
        const grid = [0, 1, 2, 3, 4].map(t => {
          const value = min + (max - min) * t / 4;
          return `<g><line class="performance-grid-line" x1="${left}" x2="${left + plotWidth}" y1="${y(value)}" y2="${y(value)}"/><text class="performance-axis-text" x="${left - 10}" y="${y(value) + 4}" text-anchor="end">${Math.round(value)}</text></g>`;
        }).join('');
        const step = Math.max(1, Math.ceil(rounds.length / 12));
        const axis = rounds.map((round, index) => index % step === 0 || index === rounds.length - 1
          ? `<text class="performance-axis-text" x="${x(round)}" y="${height - 14}" text-anchor="middle">${round}</text>`
          : '').join('') + `<text class="performance-axis-text" x="${left + plotWidth}" y="${height - 1}" text-anchor="end">回合</text>`;
        const lines = visible.map(item => {
          const color = colorOf(item.playerId);
          const points = item.points || [];
          const polyline = points.map(point => `${x(point.round)},${y(Number(point[field]) || 0)}`).join(' ');
          const dots = points.map(point => `<circle cx="${x(point.round)}" cy="${y(Number(point[field]) || 0)}" r="4" fill="${color}"><title>${esc(playerName(item.playerId))} · 第${point.round}回合：${fmt(point[field])}</title></circle>`).join('');
          return `<g><polyline fill="none" stroke="${color}" stroke-width="2.5" stroke-linejoin="round" points="${polyline}"/>${dots}</g>`;
        }).join('');
        $('performanceChart').innerHTML = `<svg viewBox="0 0 ${width} ${height}" role="img" aria-label="${esc(modeText())}${esc(activeMetric().label)}曲线" class="performance-chart">${grid}${axis}${lines}</svg>`;
        const svg = $('performanceChart').querySelector('svg');
        const tooltip = $('performanceTooltip');
        if (!svg || !tooltip) return;
        svg.onmousemove = event => {
          const rect = svg.getBoundingClientRect();
          const pointer = (event.clientX - rect.left) / rect.width * width;
          if (!rounds.length) return;
          const round = rounds.reduce((best, value) => Math.abs(x(value) - pointer) < Math.abs(x(best) - pointer) ? value : best, rounds[0]);
          tooltip.hidden = false;
          tooltip.innerHTML = `<strong>第 ${round} 回合 · ${modeText()}</strong>${visible.map(item => {
            const point = (item.points || []).find(value => value.round === round);
            return `<span style="--player-color:${colorOf(item.playerId)}">${esc(playerName(item.playerId))}：${point ? fmt(point[field]) : '未记录'}</span>`;
          }).join('')}`;
        };
        svg.onmouseleave = () => { tooltip.hidden = true; };
      };

      const renderPlayerCards = () => {
        const roundCount = Math.max(1, report.match?.roundCount || 1);
        $('performancePlayerCards').innerHTML = players.map(p => {
          const average = (p.usedCards || 0) / roundCount;
          return `<article class="performance-player-card" style="--player-color:${colorOf(p.id)}"><h4>${esc(p.nickname)}</h4><p>出牌 <b>${fmt(p.usedCards)}</b> 次 · 平均每回合 <b>${average.toFixed(2)}</b> 次</p><p>转账 <b>${fmt(p.transferGold || 0)}</b> 星币 · 击杀 <b>${fmt(p.kills)}</b></p></article>`;
        }).join('');
      };

      const renderDice = () => {
        $('performanceDice').innerHTML = data.map(item => {
          const dice = item.dice || [];
          const values = dice.flatMap(roll => roll.values || []);
          const counts = new Map();
          values.forEach(value => counts.set(value, (counts.get(value) || 0) + 1));
          const peak = Math.max(1, ...counts.values());
          const average = values.length ? (values.reduce((sum, value) => sum + value, 0) / values.length).toFixed(2) : '—';
          const bars = [...counts].sort((a, b) => a[0] - b[0]).map(([point, count]) => `<div class="performance-bar"><span>${point} 点</span><meter min="0" max="${peak}" value="${count}"></meter><span>${count} 次</span></div>`).join('');
          return `<article class="performance-dice-card"><h4>${esc(playerName(item.playerId))}</h4><p>${dice.length} 次掷骰 · ${values.length} 颗骰子 · 平均 ${average} 点 · 受控 ${dice.filter(roll => roll.controlled).length} 次</p>${bars}${values.length ? '' : '<p class="performance-note">未记录骰面点数</p>'}</article>`;
        }).join('');
      };

      const renderRounds = () => {
        $('performanceRoundsTable').innerHTML = `<table class="light-table"><thead><tr><th>玩家</th><th>回合</th><th>新增伤害</th><th>累计伤害</th><th>新增转账</th><th>累计转账</th><th>当前星币</th><th>新增击杀</th><th>累计死亡</th></tr></thead><tbody>${data.flatMap(item => (item.points || []).map(point => `<tr><td>${esc(playerName(item.playerId))}</td><td>${point.round}</td><td>${fmt(point.damageDelta)}</td><td>${fmt(point.damage)}</td><td>${fmt(point.transferDelta)}</td><td>${fmt(point.transfer)}</td><td>${fmt(point.currentGold)}</td><td>${fmt(point.killsDelta)}</td><td>${fmt(point.deaths)}</td></tr>`)).join('')}</tbody></table>`;
      };

      const syncControls = () => {
        const mode = $('performanceMode');
        if (!mode) return;
        mode.disabled = metric === 'currentGold';
        mode.value = metric === 'currentGold' ? 'true' : String(cumulative);
      };

      $('performanceMetric').onchange = event => {
        metric = event.target.value;
        syncControls();
        renderChart();
      };
      $('performanceMode').onchange = event => {
        cumulative = event.target.value === 'true';
        renderChart();
      };
      root.querySelectorAll('[data-performance-player]').forEach(input => {
        input.onchange = () => {
          if (input.checked) selected.add(String(input.dataset.performancePlayer));
          else selected.delete(String(input.dataset.performancePlayer));
          renderChart();
        };
      });

      syncControls();
      renderPlayerCards();
      renderDice();
      renderRounds();
      renderChart();
    },

    renderPlayers(report) {
      const root = $('playersView');
      if (!root) return;

      root.innerHTML = `
        <div style="margin-bottom:16px;color:var(--text-muted);font-size:13px">
          结算快照中的完整属性与行动统计，记录本局关键数据指标。
        </div>
        <div style="display:grid;gap:18px">
          ${report.players.map(p => `
            <article class="player-card">
              <div class="player-head">
                <div class="player-avatar-box">
                  ${p.avatar ? `<img src="${esc(p.avatar)}" alt="">` : ''}
                </div>
                <div class="player-titles">
                  <h4>${esc(p.nickname)} <span class="badge badge-update">${esc(p.finalStatus)}</span></h4>
                  <small>${esc(p.heroName)} · ID: ${p.id}</small>
                </div>
              </div>
              <div style="display:grid;grid-template-columns:repeat(auto-fill, minmax(130px, 1fr));gap:10px;background:rgba(255,255,255,0.05);border:1px solid rgba(255,255,255,0.1);padding:14px;border-radius:var(--gp-radius-md);margin-bottom:12px">
                <div><small style="color:var(--text-muted);font-size:11px">生命</small><div style="font-weight:700">${esc(p.hpText)}</div></div>
                <div><small style="color:var(--text-muted);font-size:11px">星币</small><div style="font-weight:700;color:var(--accent-gold)">${fmt(p.gold)}</div></div>
                <div><small style="color:var(--text-muted);font-size:11px">转账星币</small><div style="font-weight:700;color:var(--accent-gold)">${fmt(p.transferGold || 0)}</div></div>
                <div><small style="color:var(--text-muted);font-size:11px">攻击 / 防御</small><div style="font-weight:700">${p.attack} / ${p.defense}</div></div>
                <div><small style="color:var(--text-muted);font-size:11px">输出伤害</small><div style="font-weight:700;color:var(--accent-red)">${fmt(p.damage)}</div></div>
                <div><small style="color:var(--text-muted);font-size:11px">承受伤害</small><div style="font-weight:700">${fmt(p.injured)}</div></div>
                <div><small style="color:var(--text-muted);font-size:11px">移动点数</small><div style="font-weight:700">${fmt(p.movePoints)}</div></div>
                <div><small style="color:var(--text-muted);font-size:11px">卡牌使用</small><div style="font-weight:700">${fmt(p.usedCards)}</div></div>
                <div><small style="color:var(--text-muted);font-size:11px">技能使用</small><div style="font-weight:700">${fmt(p.usedSkills)}</div></div>
              </div>
              <div style="font-size:12px;color:var(--text-secondary)">
                <b>已持有 ${p.selectedRelicCount} 个筹码：</b>${this.relicList(p.selectedRelics, '无', true)}
              </div>
            </article>
          `).join('')}
        </div>
      `;
    },

    renderTimeline(report) {
      const root = $('timelineView');
      if (!root) return;

      root.innerHTML = `
        <div class="data-table-card">
          <div class="table-filter-bar">
            <span id="timelineCountText" style="font-size:13px;color:var(--text-secondary)">正在统计事件…</span>
            <input id="timelineSearch" class="table-search-input" placeholder="按玩家、回合、技能或动作搜索...">
          </div>
          <div id="timelineContent" class="table-scroll-wrap" style="padding:18px"></div>
        </div>
      `;

      const relicsByFrame = new Map((report.relics || []).map(relic => [relic.frameIndex, relic]));
      const eventRelics = ev => {
        const relic = relicsByFrame.get(ev.frameIndex);
        if (!relic) return '';
        return this.relicList(relic.relicId > 0 ? [relic] : relic.options);
      };

      const update = () => {
        const q = ($('timelineSearch')?.value || '').trim().toLowerCase();
        const filtered = report.events.filter(x => {
          return !q || `${x.frameIndex} ${x.roundText} ${x.playerName} ${x.type} ${x.title} ${x.description}`.toLowerCase().includes(q);
        });

        $('timelineCountText').textContent = `共筛选出 ${filtered.length.toLocaleString()} 条事件记录`;
        const groups = new Map();
        filtered.forEach(ev => {
          const r = ev.round > 0 ? ev.round : 0;
          if (!groups.has(r)) groups.set(r, []);
          groups.get(r).push(ev);
        });

        $('timelineContent').innerHTML = [...groups.entries()].sort((a, b) => a[0] - b[0]).map(([round, evList]) => `
          <div style="margin-bottom:18px">
            <h4 style="margin:0 0 8px;font-size:14px;color:var(--primary);font-weight:700">
              ${round > 0 ? `第 ${round} 回合` : '开局 / 全局事件'} (${evList.length} 条)
            </h4>
            <div style="display:flex;flex-direction:column;gap:6px">
              ${evList.map(ev => `
                <div style="display:flex;align-items:center;gap:10px;padding:9px 14px;background:rgba(255,255,255,0.04);border:1px solid rgba(255,255,255,0.08);border-radius:var(--gp-radius-sm);font-size:13px">
                  <span style="font-family:monospace;font-size:11px;color:var(--gp-gold);flex:0 0 54px">#${ev.frameIndex}</span>
                  <span class="badge badge-notice">${esc(ev.type)}</span>
                  <strong style="color:#ffffff;flex:0 0 110px">${esc(ev.playerName || '系统')}</strong>
                  <span style="color:var(--gp-text-sub);flex:1">${esc(ev.description || ev.title)}${ev.type === '筹码' ? eventRelics(ev) : ''}</span>
                </div>
              `).join('')}
            </div>
          </div>
        `).join('') || '<div style="text-align:center;padding:24px;color:var(--text-muted)">无符合条件的事件记录</div>';
      };

      $('timelineSearch')?.addEventListener('input', update);
      update();
    },

    relicBadge(relic, iconOnly = false) {
      const name = relic.name || relic.relicName || '未知筹码';
      const label = [name, relic.quality].filter(Boolean).join(' · ');
      const rarity = qualityTone(relic.quality);
      return `<span class="replay-relic replay-relic-${rarity}" title="${esc(label)}"${iconOnly ? ` role="img" aria-label="${esc(label)}"` : ''}>
        ${relic.image ? `<img src="${esc(relic.image)}" alt="" width="36" height="36" loading="lazy" onerror="this.hidden=true">` : ''}
        ${iconOnly ? '' : `<span>${esc(name)}</span>`}
      </span>`;
    },

    relicList(relics, fallback = '', iconOnly = false) {
      return relics?.length
        ? `<span class="replay-relic-list">${relics.map(relic => this.relicBadge(relic, iconOnly)).join('')}</span>`
        : esc(fallback);
    },

    renderRelics(report) {
      const root = $('relicsView');
      if (!root) return;

      root.innerHTML = `
        <div class="data-table-card">
          <div class="table-filter-bar">
            <div style="display:flex;align-items:center;gap:14px">
              <span id="relicCountText" style="font-size:13px;color:var(--gp-text-sub);font-weight:700">筹码记录共 ${report.relics.length} 条</span>
              <input id="relicSearchInput" class="table-search-input" placeholder="筛选筹码、玩家、品质、来源...">
            </div>
            <div style="display:flex;align-items:center;gap:8px">
              <button class="secondary-btn" id="exportRelicsTxtBtn" style="padding:6px 14px;font-size:12px;font-weight:800;border-radius:var(--gp-radius-full);color:var(--gp-gold);border-color:rgba(255,215,0,0.3)">
                <span>${Icons.svg('smart_toy')}</span> 导出 AI 文本 (TXT)
              </button>
              <button class="secondary-btn" id="exportRelicsCsvBtn" style="padding:6px 14px;font-size:12px;font-weight:800;border-radius:var(--gp-radius-full)">
                <span>${Icons.svg('table_chart')}</span> 导出表格 (CSV)
              </button>
              <button class="secondary-btn" id="exportRelicsJsonBtn" style="padding:6px 14px;font-size:12px;font-weight:800;border-radius:var(--gp-radius-full)">
                <span>${Icons.svg('data_object')}</span> 导出 JSON
              </button>
            </div>
          </div>
          <div class="table-scroll-wrap">
            <table class="light-table">
              <thead>
                <tr>
                  <th>${window.t?.('replay.th_round') || '回合'}</th>
                  <th>${window.t?.('replay.th_player') || '玩家'}</th>
                  <th>${window.t?.('replay.th_hero') || '角色'}</th>
                  <th>${window.t?.('replay.th_source') || '来源'}</th>
                  <th>${window.t?.('replay.th_action') || '操作'}</th>
                  <th>${window.t?.('replay.th_relic_name') || '筹码名称'}</th>
                  <th>${window.t?.('replay.th_quality') || '品质'}</th>
                  <th>${window.t?.('replay.th_rerolls') || '刷新记录'}</th>
                  <th>${window.t?.('replay.th_options') || '候选选项'}</th>
                </tr>
              </thead>
              <tbody id="relicTableBody"></tbody>
            </table>
          </div>
        </div>
      `;

      const update = () => {
        const q = ($('relicSearchInput')?.value || '').trim().toLowerCase();
        const rows = report.relics.filter(x => {
          return !q || `${x.playerName} ${x.heroName || ''} ${x.relicName} ${x.quality} ${x.source} ${x.kind} ${x.optionsText}`.toLowerCase().includes(q);
        });

        $('relicCountText').textContent = `显示 ${rows.length} / ${report.relics.length} 条筹码流转`;
        $('relicTableBody').innerHTML = rows.map(r => `
          <tr>
            <td>${r.round || '—'}</td>
            <td><strong>${esc(r.playerName)}</strong></td>
            <td><span style="color:var(--gp-text-sub);font-size:12px">${esc(r.heroName || '—')}</span></td>
            <td><span class="badge badge-notice">${esc(r.source)}</span></td>
            <td><span class="badge ${r.kind === '选择' ? 'badge-event' : 'badge-update'}">${esc(r.kind)}</span></td>
            <td>${r.relicId > 0 ? this.relicBadge(r) : '—'}</td>
            <td class="quality-${qualityTone(r.quality)}">${esc(r.quality)}</td>
            <td>${esc(r.refreshText || '—')}</td>
            <td style="color:var(--text-muted);font-size:12px">${this.relicList(r.options, r.optionsText)}</td>
          </tr>
        `).join('');
      };

      $('relicSearchInput')?.addEventListener('input', update);
      $('exportRelicsTxtBtn')?.addEventListener('click', () => post({ type: 'exportRelics', format: 'txt' }));
      $('exportRelicsCsvBtn')?.addEventListener('click', () => post({ type: 'exportRelics', format: 'csv' }));
      $('exportRelicsJsonBtn')?.addEventListener('click', () => post({ type: 'exportRelics', format: 'json' }));
      update();
    },

    renderShops(report) {
      const root = $('shopsView');
      if (!root) return;

      const total = report.shops?.length || 0;
      const bought = report.shops?.filter(s => s.hasPurchase).length || 0;
      const performance = report.performance || [];
      const transferFor = (playerId, round) => {
        const item = performance.find(p => String(p.playerId) === String(playerId));
        const point = (item?.points || []).find(v => v.round === round);
        return point ? point.transferDelta : null;
      };

      root.innerHTML = `
        <div class="data-table-card">
          <div class="table-filter-bar">
            <div style="display:flex;align-items:center;gap:14px">
              <span id="shopCountText" style="font-size:13px;color:var(--gp-text-sub);font-weight:700">商店进店共 ${total} 次（其中 ${bought} 次有购买）</span>
              <input id="shopSearchInput" class="table-search-input" placeholder="筛选商店类型、玩家、卡牌、购买结果...">
            </div>
            <div style="display:flex;align-items:center;gap:8px">
              <button class="secondary-btn" id="exportShopsCsvBtn" style="padding:6px 14px;font-size:12px;font-weight:800;border-radius:var(--gp-radius-full)">
                <span>${Icons.svg('table_chart')}</span> 导出表格 (CSV)
              </button>
            </div>
          </div>
          <div class="table-scroll-wrap">
            <table class="light-table">
              <thead>
                <tr>
                  <th>${window.t?.('replay.th_round') || '回合'}</th>
                  <th>${window.t?.('replay.th_player') || '玩家'}</th>
                  <th>${window.t?.('replay.th_hero') || '角色'}</th>
                  <th>${window.t?.('replay.th_shop_type') || '商店'}</th>
                  <th>${window.t?.('replay.th_shop_cards') || '候选卡牌'}</th>
                  <th>${window.t?.('replay.th_price') || '售价'}</th>
                  <th>${window.t?.('replay.th_purchase_result') || '购买结果'}</th>
                  <th>${window.t?.('replay.col_actions') || '已售出'}</th>
                  <th>本回合转账</th>
                </tr>
              </thead>
              <tbody id="shopTableBody"></tbody>
            </table>
          </div>
        </div>
      `;

      const update = () => {
        const q = ($('shopSearchInput')?.value || '').trim().toLowerCase();
        const rows = report.shops.filter(s => {
          return !q || `${s.playerName} ${s.heroName || ''} ${s.shopType} ${s.optionsText} ${s.boughtText || ''} ${s.purchaseText}`.toLowerCase().includes(q);
        });

        $('shopCountText').textContent = `显示 ${rows.length} / ${total} 次商店进店`;
        $('shopTableBody').innerHTML = rows.map(s => {
          const soldOut = s.soldOutText ? `<span class="badge badge-muted">${esc(s.soldOutText)}</span>` : '';
          const transfer = transferFor(s.playerId, s.round);
          return `
            <tr>
              <td>${s.round || '—'}</td>
              <td><strong>${esc(s.playerName)}</strong></td>
              <td><span style="color:var(--gp-text-sub);font-size:12px">${esc(s.heroName || '—')}</span></td>
              <td><span class="badge ${s.shopType === 'PVE商店' ? 'badge-event' : 'badge-update'}">${esc(s.shopType)}</span></td>
              <td style="color:var(--text-muted);font-size:12px;max-width:340px">${esc(s.optionsText || '—')}</td>
              <td>${esc(s.priceText || '—')}</td>
              <td><span class="badge ${s.hasPurchase ? 'badge-event' : 'badge-muted'}">${esc(s.purchaseText || '—')}</span></td>
              <td>${soldOut}</td>
              <td title="该玩家本回合快照区间内的转账增量；同回合多次进店显示相同值，不重复累计。">${transfer === null ? '<span style="color:var(--text-muted)">未记录</span>' : `<strong style="color:var(--accent-gold)">${fmt(transfer)} 星币</strong>`}</td>
            </tr>
          `;
        }).join('') || '<tr><td colspan="9" style="text-align:center;padding:24px;color:var(--text-muted)">本局没有商店进店记录</td></tr>';
      };

      $('shopSearchInput')?.addEventListener('input', update);
      $('exportShopsCsvBtn')?.addEventListener('click', () => post({ type: 'exportShops', format: 'csv' }));
      update();
    },

    renderProtocol(report) {
      const pane = $('techProtocolPane');
      if (!pane) return;

      pane.innerHTML = `
        <div class="protocol-split-layout">
          <div class="data-table-card">
            <div class="table-filter-bar">
              <span id="protoCountText" style="font-size:13px;color:var(--text-secondary)">协议帧共 ${report.frames.length.toLocaleString()} 帧</span>
              <input id="protoSearch" class="table-search-input" placeholder="搜索帧号、CMD、消息名...">
            </div>
            <div class="table-scroll-wrap">
              <table class="light-table">
                <thead>
                  <tr>
                    <th>帧</th>
                    <th>偏移</th>
                    <th>CMD</th>
                    <th>消息类型</th>
                    <th>载荷字节</th>
                  </tr>
                </thead>
                <tbody id="protoTableBody"></tbody>
              </table>
            </div>
          </div>

          <div class="frame-detail-box">
            <h4>协议载荷解码详情</h4>
            <pre id="protoFrameDetail">请在左侧点击任意协议帧进行查看解码数据</pre>
          </div>
        </div>
      `;

      const update = () => {
        const q = ($('protoSearch')?.value || '').trim().toLowerCase();
        const frames = report.frames.filter(x => !q || `${x.index} ${x.offsetText} ${x.cmdId} ${x.messageName}`.toLowerCase().includes(q));
        const slice = frames.slice(0, 600);

        $('protoCountText').textContent = `显示 ${slice.length.toLocaleString()} / ${frames.length.toLocaleString()} 帧`;
        $('protoTableBody').innerHTML = slice.map(f => `
          <tr class="proto-frame-row ${AppState.selectedFrame === f.index ? 'selected' : ''}" data-frame="${f.index}">
            <td>${f.index}</td>
            <td style="color:var(--text-muted);font-family:monospace">${f.offsetText}</td>
            <td>${f.cmdId}</td>
            <td><strong>${esc(f.messageName)}</strong></td>
            <td>${fmt(f.payloadLength)} B</td>
          </tr>
        `).join('');

        pane.querySelectorAll('.proto-frame-row').forEach(row => {
          row.addEventListener('click', () => {
            AppState.selectedFrame = Number(row.dataset.frame);
            pane.querySelectorAll('.proto-frame-row').forEach(r => r.classList.toggle('selected', r === row));
            $('protoFrameDetail').textContent = '正在解码 Protobuf 载荷…';
            post({ type: 'getFrame', index: AppState.selectedFrame });
          });
        });
      };

      $('protoSearch')?.addEventListener('input', update);
      update();
    },

    renderFrameDetail(frame) {
      const el = $('protoFrameDetail');
      if (!el) return;
      el.textContent = `帧号: ${frame.index} · ${frame.offsetText}\nCMD: ${frame.cmdId} · ${frame.messageName}\n载荷大小: ${fmt(frame.payloadLength)} 字节\n\n${frame.detail}`;
    },

    renderStatistics(report) {
      const pane = $('techStatsPane');
      if (!pane) return;

      const s = report.statistics;
      pane.innerHTML = `
        <div style="display:grid;grid-template-columns:repeat(auto-fit, minmax(320px, 1fr));gap:20px">
          ${this.statBlock('协议命令统计', s.commands)}
          ${this.statBlock('去重玩家动作统计', s.actions)}
          ${this.statBlock('筹码选择品质分布', s.relicQualities)}
        </div>
      `;
    },

    statBlock(title, items) {
      const list = items || [];
      const max = Math.max(1, ...list.map(x => x.count || 0));
      return `
        <div class="data-table-card" style="padding:18px">
          <h4 style="margin:0 0 14px;font-size:15px;color:var(--text-main)">${esc(title)}</h4>
          <div style="display:flex;flex-direction:column;gap:8px">
            ${list.map(x => {
              const count = x.count || 0;
              const pct = ((x.percent || 0) * 100).toFixed(1);
              const barWidth = (count / max * 100).toFixed(2);
              return `
              <div style="display:flex;flex-direction:column;gap:3px">
                <div style="display:flex;justify-content:space-between;font-size:12px">
                  <span style="color:var(--text-main);font-weight:600">${esc(x.name)}</span>
                  <span style="color:var(--text-muted)">${fmt(count)} (${pct}%)</span>
                </div>
                <div style="height:8px;background:rgba(255,255,255,0.08);border-radius:4px;overflow:hidden">
                  <div style="height:100%;width:${barWidth}%;background:linear-gradient(90deg, #ff1776, #a855f7);border-radius:4px"></div>
                </div>
              </div>
            `;}).join('') || '<p style="color:var(--text-muted);font-size:12px">暂无统计数据</p>'}
          </div>
        </div>
      `;
    }
  };

  window.addEventListener('appLanguageChanged', () => {
    if (ReplayModule.libraryData) {
      ReplayModule.renderLibraryRows();
    }
    if (AppState.replayReport) {
      ReplayModule.renderReport(AppState.replayReport);
    }
  });

  window.ReplayModule = ReplayModule;
})();

