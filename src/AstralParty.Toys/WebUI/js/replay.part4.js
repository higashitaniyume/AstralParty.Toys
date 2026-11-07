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

