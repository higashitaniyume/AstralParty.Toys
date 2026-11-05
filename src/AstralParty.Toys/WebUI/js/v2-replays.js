// v2-replays.js — 新界面：回放页
// 列表整页重做；详情页只换外框（标题、操作、页签），各页签的内容仍由原版 ReplayModule 渲染进 #replayWorkspaceBox。
// 字段与消息见 astralparty-redesign/回放接口.md。
(function () {
  const R = { filter: 'all', sort: 'time', q: '', busyUntil: 0 };
  const U = () => window.V2UI;
  const I = () => window.V2UI.ICON;
  const RM = () => window.ReplayModule;

  const lib = () => AppState.replayLibrary || RM()?.libraryData || null;
  const entries = () => lib()?.entries || [];
  const split = s => (s && s !== '—' ? String(s).split(' · ') : []);
  const shown = v => v && v !== '—';
  const busy = () => Date.now() < R.busyUntil;

  /* ---------- 列表 ---------- */
  function badges(e) {
    const b = [];
    if (e.inGame) b.push('<span class="v2-tag">游戏内</span>');
    if (e.inLibrary) b.push('<span class="v2-tag">回放库</span>');
    if (!e.healthy) b.push(`<span class="v2-badge warn" title="${esc(e.error || '')}">无法解析</span>`);
    else if (e.canUpgrade) b.push(`<span class="v2-badge off" title="可升级到 ${esc(lib()?.upgradeTargetVersion || '')}">旧版本 ${esc(e.gameVersion)}</span>`);
    return b.join('');
  }

  function listHtml() {
    const L = lib();
    const head = `<div class="v2-head"><h1>回放</h1><span class="v2-top-spacer"></span>
      <button class="v2-btn primary" data-v2r="addMenu" data-v2menu>${I().plus}添加回放${I().down}</button></div>`;
    if (!L) return `<div class="v2-page">${head}<div class="v2-card v2-empty">正在读取回放列表…</div></div>`;

    const cap = L.capacity || 10, n = L.gameCount || 0;
    const full = n >= cap, near = n >= cap - 1;
    const take = Math.max(n - cap, 0) || 3;
    const q = R.q.trim().toLowerCase();
    let list = entries().filter(e => (R.filter === 'all' || (R.filter === 'game' ? e.inGame : e.inLibrary)) &&
      (!q || [e.replayId, e.mapName, e.playersText, e.heroesText, e.finishText, e.gameVersion].some(v => String(v ?? '').toLowerCase().includes(q))));
    if (R.sort === 'size') list.sort((a, b) => (b.sizeBytes || 0) - (a.sizeBytes || 0));
    else if (R.sort === 'id') list.sort((a, b) => String(b.replayId).localeCompare(String(a.replayId)));
    else list.sort((a, b) => (b.sortTime || 0) - (a.sortTime || 0));
    const seg = (k, l) => `<button class="${R.filter === k ? 'on' : ''}" data-v2r-filter="${k}">${l}</button>`;
    const sortLabel = { time: '按时间', size: '按大小', id: '按 ID' }[R.sort];

    const status = `<div class="v2-card">
      <div class="v2-row" style="gap:16px">
        <span class="k">游戏内席位</span>
        <div class="v2-bar"><div style="width:${Math.min(100, Math.round(n / cap * 100))}%;${near ? 'background:var(--gp-gold)' : ''}"></div></div>
        <span style="${near ? 'color:var(--gp-gold)' : ''}" class="${near ? '' : 'v2-muted'}">${n} / ${cap}${full ? ' 已满' : near ? ' 快满了' : ''}</span>
        <span class="v"></span>
        ${near && !L.sameFolder && L.available ? `<button class="v2-btn ghost" data-v2r="archiveOldest" ${busy() ? 'disabled' : ''}>${I().archive}归档最旧 ${take} 局</button>` : ''}
      </div>
      <div class="v2-row"><span class="k">回放库</span>
        <span class="v">${L.libraryAvailable ? `${fmt(L.libraryCount)} 局 · ${esc(L.librarySizeText || '')}` : '<span style="color:var(--gp-gold)">回放库目录不可用</span>'}
          <span class="v2-muted">${L.autoMaintain ? `自动托管：游戏内保留 ${L.keepInGame || cap} 局` : '自动托管未开启'}</span></span>
        ${!L.autoMaintain && near ? '<button class="v2-btn ghost" data-v2r="autoOn">开启自动托管</button>' : ''}
        <button class="v2-btn ghost" data-v2r="openFolder">${I().folder}打开</button></div>
    </div>`;

    const notices = [
      L.gameRunning && '游戏正在运行：归档 / 放回随时可用，但别在游戏里正播着回放时操作',
      L.sameFolder && '回放库和游戏回放目录是同一个文件夹，不能归档 / 放回，请在设置里换一个回放库位置',
      ...(L.warnings || []),
    ].filter(Boolean).map(t => `<div class="v2-notice">${I().info}<span>${esc(t)}</span></div>`).join('');

    const cards = list.map(e => {
      const players = split(e.playersText), heroes = split(e.heroesText);
      const title = e.healthy && shown(e.mapName) ? e.mapName : `回放 #${e.replayId}`;
      return `<div class="v2-card v2-replay ${e.healthy ? '' : 'bad'}" data-v2r-open="${esc(e.token)}">
        <div class="v2-map">${e.healthy && shown(e.mapName) ? esc(e.mapName.slice(0, 1)) : '?'}</div>
        <div class="body">
          <div class="title">${esc(title)}${badges(e)}</div>
          <div class="meta">${e.healthy
            ? [e.finishText, e.durationText, e.roundCount ? e.roundCount + ' 回合' : '', '#' + e.replayId].filter(shown).map(v => `<span>${esc(v)}</span>`).join('')
            : `<span>${esc(e.error || '解析不出结算帧，文件可能已损坏或来自不兼容的版本。')}</span>`}</div>
          ${players.length ? `<div class="players">${players.map((p, i) =>
            `<span class="v2-player ${p === e.winnerText ? 'winner' : ''}" title="${p === e.winnerText ? '获胜方' : ''}">${esc(p)}<small>${esc(heroes[i] || '')}</small></span>`).join('')}</div>` : ''}
        </div>
        <button class="v2-icon-btn" data-v2r-menu="${esc(e.replayId)}" data-v2menu title="更多">${I().more}</button>
      </div>`;
    }).join('');

    const empty = !entries().length
      ? (L.available ? '还没有回放。在游戏「战绩」里对一局点「保存回放」，再回来刷新。' : '未检测到游戏回放目录。仍然可以用「添加回放 → 打开任意回放文件分析」。')
      : '没有符合条件的回放';

    return `<div class="v2-page">${head}${status}${notices}
      <div class="v2-toolbar">
        <label class="v2-search">${I().search}<input id="v2rq" placeholder="搜索回放 ID、地图、玩家、角色" value="${esc(R.q)}"></label>
        <div class="v2-seg">${seg('all', '全部')}${seg('game', '游戏内')}${seg('lib', '回放库')}</div>
        <button class="v2-btn line" data-v2r="sortMenu" data-v2menu>${sortLabel}${I().down}</button>
      </div>
      <div class="v2-replay-list">${cards || `<div class="v2-card v2-empty">${empty}</div>`}</div>
    </div>`;
  }

  function entryMenu(anchor, e, inDetail) {
    const items = [];
    if (!inDetail) items.push({ label: e.healthy ? '查看分析' : '无法解析', icon: I().search, disabled: !e.healthy, fn: () => openEntry(e) });
    if (e.inGame && !e.inLibrary) items.push({ label: '归档到回放库', icon: I().archive, fn: () => act('libraryArchive', e) });
    if (e.inLibrary && !e.inGame) items.push({ label: '放回游戏', icon: I().play, fn: () => act('libraryRestore', e) });
    if (e.canUpgrade) items.push({ label: `升级到 ${lib()?.upgradeTargetVersion || '当前版本'}`, icon: I().refresh, fn: () => act('libraryUpgrade', e) });
    items.push('-', { label: '删除…', icon: I().trash, fn: () => deleteDialog(e) });
    U().openMenu(anchor, items);
  }

  // 归档 / 放回 / 升级：宿主处理完会推新的 replayLibrary；期间再点会命中「目录里没有这个回放」，所以先锁住
  function act(type, e) {
    if (busy()) return toast('上一个操作还在处理…');
    R.busyUntil = Date.now() + 15000;
    post({ type, ids: [e.replayId] });
    render();
  }

  function openEntry(e) {
    if (!e.healthy) return toast('这局无法解析：' + (e.error || ''));
    post({ type: 'openRecentReplay', token: e.token });   // 宿主回 loading → report，原版会切到详情
  }

  function deleteDialog(e) {
    const opts = [];
    if (e.inLibrary) opts.push(['library', '只删回放库里的', '游戏里仍能看']);
    if (e.inGame) opts.push(['game', '只删游戏内的', '腾出一个游戏内席位']);
    if (e.inLibrary && e.inGame) opts.push(['both', '两边都删', '这局回放彻底删除，无法恢复']);
    if (!opts.length) return;
    U().modalActions.v2rDelete = b => {
      U().closeModal();
      if (busy()) return toast('上一个操作还在处理…');
      R.busyUntil = Date.now() + 15000;
      post({ type: 'libraryDelete', ids: [e.replayId], target: b.dataset.target });
      // 删的是正在看的这局：回到列表
      if (RM()?.detailOpen && AppState.replayReport?.match?.replayId === e.replayId && b.dataset.target === 'both') RM().showLibrary();
      render();
    };
    const m = U().openModal(`
      <div class="v2-modal-head"><b>删除回放 #${esc(e.replayId)}</b><button class="v2-icon-btn" data-v2modal="close">${I().x}</button></div>
      <p class="v2-muted" style="margin:12px 18px 4px">${esc([e.finishText, e.mapName].filter(shown).join(' · '))}　选择删哪一边：</p>
      ${opts.map(o => `<div class="v2-row"><span class="v">${o[1]}<br><span class="v2-muted">${o[2]}</span></span>
        <button class="v2-btn danger" data-v2modal="v2rDelete" data-target="${o[0]}">${I().trash}删除</button></div>`).join('')}
      <div class="v2-modal-foot"><span class="v2-top-spacer"></span><button class="v2-btn ghost" data-v2modal="close">取消</button></div>`, 'narrow');
    U().armDanger(m);
  }

  /* ---------- 详情（外框） ---------- */
  const TABS = [['summary', '摘要'], ['performance', '表现'], ['players', '玩家'], ['timeline', '时间线'], ['relics', '筹码'], ['shops', '商店']];

  function detailHtml() {
    const rep = AppState.replayReport, m = rep?.match || {};
    const e = entries().find(x => String(x.replayId) === String(m.replayId));
    const active = AppState.activeReplayTab || 'summary';
    const title = shown(m.mapName) ? m.mapName : (rep?.file?.fileName || '回放');
    const sub = [e?.finishText, m.durationText, m.roundCount ? m.roundCount + ' 回合' : '', m.replayId && '#' + m.replayId, !e && rep?.file?.fileName].filter(shown).join(' · ');
    const tabs = TABS.concat(active === 'technical' ? [['technical', '专业信息']] : [])
      .map(([k, l]) => `<button class="${active === k ? 'on' : ''}" data-v2r-tab="${k}">${l}</button>`).join('');
    return `<div class="v2-page v2-detail-head">
      <div class="v2-head">
        <div class="v2-crumb"><button data-v2r="back">回放</button>${I().right}<span class="cur">${esc(title)}</span></div>
        <span class="v2-top-spacer"></span>
        ${e && e.inLibrary && !e.inGame ? `<button class="v2-btn primary" data-v2r="restoreOpen" ${busy() ? 'disabled' : ''}>${I().play}放回游戏观看</button>`
          : e?.inGame ? '<span class="v2-tag">已在游戏内，可在游戏里观看</span>' : ''}
        <button class="v2-icon-btn" data-v2r="detailMenu" data-v2menu title="更多">${I().more}</button>
      </div>
      ${sub ? `<p class="v2-muted" style="margin:-10px 0 12px">${esc(sub)}</p>` : ''}
      <div class="v2-tabs">${tabs}</div>
    </div>`;
  }

  function exportDialog() {
    const rows = [['完整报告', 'JSON', { type: 'export' }], ['筹码记录（给 AI 看）', 'TXT', { type: 'exportRelics', format: 'txt' }],
      ['筹码记录', 'CSV', { type: 'exportRelics', format: 'csv' }], ['筹码记录', 'JSON', { type: 'exportRelics', format: 'json' }],
      ['商店记录', 'CSV', { type: 'exportShops', format: 'csv' }]];
    U().modalActions.v2rExport = b => { U().closeModal(); post(rows[Number(b.dataset.i)][2]); };
    U().openModal(`
      <div class="v2-modal-head"><b>导出这局回放</b><button class="v2-icon-btn" data-v2modal="close">${I().x}</button></div>
      ${rows.map((r, i) => `<div class="v2-row"><span class="v">${r[0]}</span><span class="v2-tag">${r[1]}</span>
        <button class="v2-btn line" data-v2modal="v2rExport" data-i="${i}">${I().download}导出</button></div>`).join('')}
      <div class="v2-modal-foot"><span class="v2-muted">会弹出保存位置</span><span class="v2-top-spacer"></span><button class="v2-btn ghost" data-v2modal="close">关闭</button></div>`, 'narrow');
  }

  /* ---------- 渲染与事件 ---------- */
  function render() {
    const root = $('v2Replays'), tv = $('toolsView');
    if (!root || !tv || U().isComposing()) return;
    const detail = !!RM()?.detailOpen;
    tv.classList.toggle('v2-list-mode', !detail);
    root.innerHTML = detail ? detailHtml() : listHtml();
    U().bindSearch('v2rq', v => { R.q = v; render(); });
  }

  const actions = {
    addMenu: el => U().openMenu(el, [
      { label: '导入回放文件…', icon: I().upload, fn: () => post({ type: 'libraryImport' }) },
      { label: '按回放 ID 下载…', icon: I().download, fn: () => RM()?.openDownloadDialog() },
      { label: '打开任意回放文件分析…', icon: I().search, fn: () => post({ type: 'openReplay' }) },
      '-',
      { label: '刷新列表', icon: I().refresh, fn: () => post({ type: 'refreshReplays' }) },
    ]),
    sortMenu: el => U().openMenu(el, [['time', '按对局时间'], ['size', '按文件大小'], ['id', '按回放 ID']].map(([k, l]) =>
      ({ label: l, icon: R.sort === k ? I().check : I().dot, fn: () => { R.sort = k; render(); } }))),
    archiveOldest: () => {
      if (busy()) return;
      const L = lib(); R.busyUntil = Date.now() + 15000;
      post({ type: 'libraryArchiveOldest', count: Math.max((L.gameCount || 0) - (L.capacity || 10), 0) || 3 });
      render();
    },
    autoOn: () => {
      const L = lib(), keep = L.keepInGame || L.capacity || 10;
      // 与原版横幅按钮相同：保留局数沿用设置里的值
      post({ type: 'librarySaveSettings', libraryRoot: L.libraryRoot || null, autoMaintain: true, keepInGame: keep });
      toast(`已开启自动托管：以后每次打开都会把超出的旧回放归档进库（游戏内保留最近 ${keep} 局）`);
    },
    openFolder: () => post({ type: 'libraryOpenFolder' }),
    back: () => RM()?.showLibrary(),
    restoreOpen: () => {
      const e = entries().find(x => String(x.replayId) === String(AppState.replayReport?.match?.replayId));
      if (e) act('libraryRestore', e);
    },
    detailMenu: el => {
      const e = entries().find(x => String(x.replayId) === String(AppState.replayReport?.match?.replayId));
      U().openMenu(el, [
        { label: '导出…', icon: I().download, fn: exportDialog },
        { label: '专业信息（协议帧、统计）', icon: I().info, fn: () => RM()?.switchReplayTab('technical') },
        { label: '打开其他回放文件…', icon: I().search, fn: () => post({ type: 'openReplay' }) },
        ...(e ? ['-',
          ...(e.inGame && !e.inLibrary ? [{ label: '归档到回放库', icon: I().archive, fn: () => act('libraryArchive', e) }] : []),
          ...(e.canUpgrade ? [{ label: `升级到 ${lib()?.upgradeTargetVersion || '当前版本'}`, icon: I().refresh, fn: () => act('libraryUpgrade', e) }] : []),
          { label: '删除…', icon: I().trash, fn: () => deleteDialog(e) }] : []),
      ]);
    },
  };

  document.addEventListener('click', ev => {
    const t = ev.target;
    if (!t.closest?.('#toolsView')) return;
    const a = t.closest('[data-v2r]'), f = t.closest('[data-v2r-filter]'), tab = t.closest('[data-v2r-tab]');
    const menu = t.closest('[data-v2r-menu]'), card = t.closest('[data-v2r-open]');
    if (a) { if (!a.disabled) actions[a.dataset.v2r]?.(a); return; }
    if (f) { R.filter = f.dataset.v2rFilter; return render(); }
    if (tab) { RM()?.switchReplayTab(tab.dataset.v2rTab); return; }
    if (menu) { const e = entries().find(x => String(x.replayId) === menu.dataset.v2rMenu); if (e) entryMenu(menu, e, false); return; }
    if (card) { const e = entries().find(x => x.token === card.dataset.v2rOpen); if (e) openEntry(e); }
  });

  // 原版这几个方法跑完后，重画新界面（不改它们的行为）
  document.addEventListener('DOMContentLoaded', () => {
    const m = RM();
    if (!m) return;
    ['setDetailMode', 'renderLibrary', 'renderLibraryResult', 'switchReplayTab'].forEach(name => {
      const orig = m[name];
      if (typeof orig !== 'function') return;
      m[name] = function (...args) {
        const r = orig.apply(this, args);
        if (name === 'renderLibrary' || name === 'renderLibraryResult') R.busyUntil = 0;   // 宿主回话了，解锁
        render();
        return r;
      };
    });
    render();
  });
})();
