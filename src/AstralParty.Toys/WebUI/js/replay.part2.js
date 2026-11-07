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

