// replay.js - Full Replay Tool Logic Integrated within Tools Page (Light Theme)
(function () {
  const ReplayModule = {
    init() {
      this.bindEvents();
    },

    bindEvents() {
      // Sub-nav tab switcher
      document.querySelectorAll('.subnav-tab').forEach(tab => {
        tab.addEventListener('click', () => {
          this.switchReplayTab(tab.dataset.tab);
        });
      });

      // Actions in Tools Header
      $('toolOpenBtn')?.addEventListener('click', () => post({ type: 'openReplay' }));
      $('toolExportBtn')?.addEventListener('click', () => post({ type: 'export' }));
      $('toolLibraryBtn')?.addEventListener('click', () => this.showLibrary());
      $('toolsBackHomeBtn')?.addEventListener('click', () => showPage('home'));

      // Empty state buttons
      $('emptySelectReplayBtn')?.addEventListener('click', () => post({ type: 'openReplay' }));
      $('refreshLibraryBtn')?.addEventListener('click', () => post({ type: 'refreshReplays' }));

      // Technical sub tabs
      $('techTabStatistics')?.addEventListener('click', () => this.switchTechPane('stats'));
      $('techTabProtocol')?.addEventListener('click', () => this.switchTechPane('proto'));
    },

    onShowTools() {
      // If we already have a report, show report workspace, else show empty / library
      if (AppState.replayReport) {
        $('replayEmptyBox')?.classList.add('hidden');
        $('replayWorkspaceBox')?.classList.remove('hidden');
        $('replaySubnav')?.classList.remove('hidden');
        $('toolExportBtn')?.removeAttribute('disabled');
        $('toolLibraryBtn')?.classList.remove('hidden');
      } else {
        this.showLibrary();
      }
    },

    showLibrary() {
      $('replayEmptyBox')?.classList.remove('hidden');
      $('replayWorkspaceBox')?.classList.add('hidden');
      $('replaySubnav')?.classList.add('hidden');
      if ($('toolExportBtn')) $('toolExportBtn').disabled = true;
      if ($('toolLibraryBtn')) $('toolLibraryBtn').classList.add('hidden');
      if ($('toolFileMeta')) $('toolFileMeta').textContent = '从游戏缓存中选择一局，或手动打开回放文件';
      post({ type: 'refreshReplays' });
    },

    switchReplayTab(tabName) {
      AppState.activeReplayTab = tabName;
      document.querySelectorAll('#replaySubnav .subnav-tab').forEach(tab => {
        tab.classList.toggle('active', tab.dataset.tab === tabName);
      });

      document.querySelectorAll('.replay-view').forEach(view => {
        view.classList.add('hidden');
      });
      $(`${tabName}View`)?.classList.remove('hidden');
    },

    switchTechPane(paneName) {
      const isStats = paneName === 'stats';
      $('techStatsPane')?.classList.toggle('hidden', !isStats);
      $('techProtocolPane')?.classList.toggle('hidden', isStats);
      $('techTabStatistics')?.classList.toggle('active', isStats);
      $('techTabProtocol')?.classList.toggle('active', !isStats);
    },

    renderLibrary(library) {
      const dirEl = $('libraryDirectoryText');
      if (dirEl) dirEl.textContent = library.directory || '默认回放目录不可用';

      const listContainer = $('recentReplayList');
      if (!listContainer) return;

      if (!library.available) {
        listContainer.innerHTML = '<div style="padding:16px;color:var(--text-muted);font-size:13px;text-align:center;">未检测到本地回放缓存目录。请使用上方按钮手动选择回放文件。</div>';
        return;
      }

      if (!library.items || !library.items.length) {
        listContainer.innerHTML = '<div style="padding:16px;color:var(--text-muted);font-size:13px;text-align:center;">暂无近期对战回放。在游戏内完成对战后点击刷新即可查看。</div>';
        return;
      }

      listContainer.innerHTML = library.items.map(item => `
        <div class="replay-entry-row" data-replay-token="${esc(item.token)}">
          <div class="entry-main">
            <span class="entry-icon">▶</span>
            <div class="entry-meta">
              <strong>${esc(item.name)}</strong>
              <small>${esc(item.fileName)} · ${esc(item.sizeText)}</small>
            </div>
          </div>
          <span class="entry-date">${esc(item.modifiedText)}</span>
        </div>
      `).join('');

      listContainer.querySelectorAll('.replay-entry-row').forEach(row => {
        row.addEventListener('click', () => {
          const token = row.dataset.replayToken;
          if (token) {
            row.style.background = 'var(--primary-light)';
            post({ type: 'openRecentReplay', token });
          }
        });
      });
    },

    renderReport(report) {
      if (!report) return;

      $('replayEmptyBox')?.classList.add('hidden');
      $('replayWorkspaceBox')?.classList.remove('hidden');
      $('replaySubnav')?.classList.remove('hidden');
      $('toolExportBtn')?.removeAttribute('disabled');
      $('toolLibraryBtn')?.classList.remove('hidden');

      if ($('toolFileMeta')) {
        const frameCount = report.match?.frameCount != null ? `${report.match.frameCount.toLocaleString()} 帧` : '';
        const fileName = report.file?.fileName || '对战回放';
        const fileSize = report.file?.fileSizeText || '';
        $('toolFileMeta').textContent = `${fileName} · ${fileSize} · ${frameCount}`;
      }

      try { this.renderSummary(report); } catch (e) { console.error('Summary render error:', e); }
      try { this.renderPlayers(report); } catch (e) { console.error('Players render error:', e); }
      try { this.renderTimeline(report); } catch (e) { console.error('Timeline render error:', e); }
      try { this.renderRelics(report); } catch (e) { console.error('Relics render error:', e); }
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

        <div style="margin-bottom:14px;display:flex;align-items:center;justify-content:space-between">
          <h3 style="margin:0;font-size:16px;color:var(--text-main)">参战玩家 (${report.players.length} 名)</h3>
        </div>
        <div class="players-cards-grid">
          ${report.players.map(p => this.playerCard(p)).join('')}
        </div>
      `;
    },

    playerCard(p) {
      return `
        <article class="player-card">
          <div class="player-head">
            <div class="player-avatar-box">
              ${p.avatar ? `<img src="${esc(p.avatar)}" alt="">` : '<div style="display:grid;place-items:center;height:100%;font-size:20px;color:var(--primary)">✦</div>'}
            </div>
            <div class="player-titles">
              <h4>${esc(p.nickname)}${p.finalBossKill ? ' <span style="color:var(--accent-gold)">♛ 决胜</span>' : ''}</h4>
              <small>${esc(p.heroName)} · 等级 ${p.accountLevel}</small>
            </div>
          </div>
          <div class="player-stat-badges">
            <div class="stat-badge-item"><small>伤害</small><b>${fmt(p.damage)}</b></div>
            <div class="stat-badge-item"><small>击杀</small><b>${fmt(p.kills)}</b></div>
            <div class="stat-badge-item"><small>筹码</small><b>${fmt(p.selectedRelicCount)}</b></div>
          </div>
        </article>
      `;
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
              <div style="display:grid;grid-template-columns:repeat(auto-fill, minmax(130px, 1fr));gap:10px;background:#f8fafd;padding:14px;border-radius:var(--radius-md);margin-bottom:12px">
                <div><small style="color:var(--text-muted);font-size:11px">生命</small><div style="font-weight:700">${esc(p.hpText)}</div></div>
                <div><small style="color:var(--text-muted);font-size:11px">星币</small><div style="font-weight:700;color:var(--accent-gold)">${fmt(p.gold)}</div></div>
                <div><small style="color:var(--text-muted);font-size:11px">攻击 / 防御</small><div style="font-weight:700">${p.attack} / ${p.defense}</div></div>
                <div><small style="color:var(--text-muted);font-size:11px">输出伤害</small><div style="font-weight:700;color:var(--accent-red)">${fmt(p.damage)}</div></div>
                <div><small style="color:var(--text-muted);font-size:11px">承受伤害</small><div style="font-weight:700">${fmt(p.injured)}</div></div>
                <div><small style="color:var(--text-muted);font-size:11px">移动点数</small><div style="font-weight:700">${fmt(p.movePoints)}</div></div>
                <div><small style="color:var(--text-muted);font-size:11px">卡牌使用</small><div style="font-weight:700">${fmt(p.usedCards)}</div></div>
                <div><small style="color:var(--text-muted);font-size:11px">技能使用</small><div style="font-weight:700">${fmt(p.usedSkills)}</div></div>
              </div>
              <div style="font-size:12px;color:var(--text-secondary)">
                <b>已持有 ${p.selectedRelicCount} 个筹码：</b>${esc(p.selectedRelicsText || '无')}
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
                <div style="display:flex;align-items:center;gap:10px;padding:8px 12px;background:#f8fafd;border-radius:var(--radius-sm);font-size:13px">
                  <span style="font-family:monospace;font-size:11px;color:var(--text-muted);flex:0 0 54px">#${ev.frameIndex}</span>
                  <span class="badge badge-notice">${esc(ev.type)}</span>
                  <strong style="color:var(--text-main);flex:0 0 110px">${esc(ev.playerName || '系统')}</strong>
                  <span style="color:var(--text-secondary);flex:1">${esc(ev.description || ev.title)}</span>
                </div>
              `).join('')}
            </div>
          </div>
        `).join('') || '<div style="text-align:center;padding:24px;color:var(--text-muted)">无符合条件的事件记录</div>';
      };

      $('timelineSearch')?.addEventListener('input', update);
      update();
    },

    renderRelics(report) {
      const root = $('relicsView');
      if (!root) return;

      root.innerHTML = `
        <div class="data-table-card">
          <div class="table-filter-bar">
            <span id="relicCountText" style="font-size:13px;color:var(--text-secondary)">筹码记录共 ${report.relics.length} 条</span>
            <input id="relicSearchInput" class="table-search-input" placeholder="筛选筹码、玩家、品质、来源...">
          </div>
          <div class="table-scroll-wrap">
            <table class="light-table">
              <thead>
                <tr>
                  <th>回合</th>
                  <th>玩家</th>
                  <th>来源</th>
                  <th>操作</th>
                  <th>筹码名称</th>
                  <th>品质</th>
                  <th>刷新记录</th>
                  <th>候选选项</th>
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
          return !q || `${x.playerName} ${x.relicName} ${x.quality} ${x.source} ${x.kind} ${x.optionsText}`.toLowerCase().includes(q);
        });

        $('relicCountText').textContent = `显示 ${rows.length} / ${report.relics.length} 条筹码流转`;
        $('relicTableBody').innerHTML = rows.map(r => `
          <tr>
            <td>${r.round || '—'}</td>
            <td><strong>${esc(r.playerName)}</strong></td>
            <td><span class="badge badge-notice">${esc(r.source)}</span></td>
            <td><span class="badge ${r.kind === '选择' ? 'badge-event' : 'badge-update'}">${esc(r.kind)}</span></td>
            <td><strong>${esc(r.relicName)}</strong></td>
            <td class="quality-${esc(r.quality)}">${esc(r.quality)}</td>
            <td>${esc(r.refreshText || '—')}</td>
            <td style="color:var(--text-muted);font-size:12px">${esc(r.optionsText)}</td>
          </tr>
        `).join('');
      };

      $('relicSearchInput')?.addEventListener('input', update);
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
            <div class="table-scroll-wrap" style="height:460px">
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
            $('protoFrameDetail').textContent = '正在请求宿主 .NET 进行 Protobuf 解码…';
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
                <div style="height:6px;background:#eef2fb;border-radius:4px;overflow:hidden">
                  <div style="height:100%;width:${barWidth}%;background:var(--primary);border-radius:4px"></div>
                </div>
              </div>
            `;}).join('') || '<p style="color:var(--text-muted);font-size:12px">暂无统计数据</p>'}
          </div>
        </div>
      `;
    }
  };

  window.ReplayModule = ReplayModule;
})();
