// replay/report.js - match summary and players
(function () {
  'use strict';
  const { fmtBytes, QUALITY_TONE, qualityTone } = window.ReplayHelpers;
  Object.assign(window.ReplayModule, {
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
  });
})();
