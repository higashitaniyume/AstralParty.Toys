// replay/timeline.js - timeline and relics
(function () {
  'use strict';
  const { fmtBytes, QUALITY_TONE, qualityTone } = window.ReplayHelpers;
  Object.assign(window.ReplayModule, {
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
  });
})();
