// replay/performance.js - performance charts and tables
(function () {
  'use strict';
  const { fmtBytes, QUALITY_TONE, qualityTone } = window.ReplayHelpers;
  Object.assign(window.ReplayModule, {
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
  });
})();
