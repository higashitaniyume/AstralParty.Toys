// replay/library.js - replay library list and actions
(function () {
  'use strict';
  const { fmtBytes, QUALITY_TONE, qualityTone } = window.ReplayHelpers;
  Object.assign(window.ReplayModule, {
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
  });
})();
