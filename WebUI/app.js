const host = window.chrome?.webview;
const state = { report: null, activeView: 'summary', selectedFrame: -1 };
const titles = {
  summary: '对局摘要', players: '玩家表现', timeline: '事件时间线',
  relics: '筹码记录', technical: '专业信息'
};

const $ = id => document.getElementById(id);
const esc = value => String(value ?? '').replace(/[&<>'"]/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;'}[c]));
const fmt = value => Number(value ?? 0).toLocaleString('zh-CN');
const post = message => host?.postMessage(message);
const image = (url, fallback, className = 'entity-avatar') => url
  ? `<img class="${className}" src="${esc(url)}" alt="">`
  : `<span class="${className} placeholder">${esc(fallback)}</span>`;
const gameIcon = (key, className = 'label-icon') => state.report?.assets?.[key]
  ? `<img class="${className}" src="${esc(state.report.assets[key])}" alt="">` : '';

function playerEntity(playerId, fallbackName = '系统') {
  const player = state.report?.players.find(item => Number(item.id) === Number(playerId));
  if (!player) return `<div class="player-entity">${image('', '✦')}<div><strong>${esc(fallbackName || '系统')}</strong><small>${playerId ? `玩家 ID ${esc(playerId)}` : '系统事件'}</small></div></div>`;
  return `<div class="player-entity">${image(player.avatar, (player.nickname || '?').slice(0, 1))}<div><strong>${esc(player.nickname)}</strong><small>${esc(player.heroName)} · 角色 ${player.heroId}</small></div></div>`;
}

document.querySelectorAll('.open-replay').forEach(x => x.addEventListener('click', () => post({type:'openReplay'})));
$('openBtn').addEventListener('click', () => post({type:'openReplay'}));
$('exportBtn').addEventListener('click', () => post({type:'export'}));
$('classicBtn').addEventListener('click', () => post({type:'openClassic'}));
$('refreshReplaysBtn').addEventListener('click', () => post({type:'refreshReplays'}));
$('libraryBtn').addEventListener('click', showLibrary);
$('technicalView').querySelector('.technical-tabs').addEventListener('click', event => {
  const button = event.target.closest('[data-tech-pane]');
  if (button) showTechnicalPane(button.dataset.techPane);
});

$('nav').addEventListener('click', event => {
  const button = event.target.closest('[data-view]');
  if (!button) return;
  showView(button.dataset.view);
});

function showView(name) {
  if (!state.report) return;
  state.activeView = name;
  document.querySelectorAll('.nav-item').forEach(x => x.classList.toggle('active', x.dataset.view === name));
  document.querySelectorAll('.view').forEach(x => x.classList.add('hidden'));
  $(`${name}View`).classList.remove('hidden');
  $('pageTitle').textContent = titles[name];
}

function showLibrary() {
  state.activeView = 'summary';
  $('workspace').classList.add('hidden');
  $('emptyState').classList.remove('hidden');
  $('pageTitle').textContent = '选择回放';
  $('fileMeta').textContent = '从游戏缓存中选择一局，或手动打开回放文件';
  $('exportBtn').disabled = true;
  $('libraryBtn').classList.add('hidden');
  document.querySelectorAll('.nav-item').forEach(item => item.classList.remove('active'));
  post({type:'refreshReplays'});
}

function showTechnicalPane(id) {
  document.querySelectorAll('.technical-pane').forEach(pane => pane.classList.toggle('hidden', pane.id !== id));
  document.querySelectorAll('.tech-tab').forEach(button => button.classList.toggle('active', button.dataset.techPane === id));
}

host?.addEventListener('message', event => {
  const message = event.data;
  if (!message?.type) return;
  if (message.type === 'hostReady') {
    $('runtimeText').textContent = message.payload.runtime;
  } else if (message.type === 'replayLibrary') {
    renderReplayLibrary(message.payload);
  } else if (message.type === 'loading') {
    $('loading').classList.toggle('hidden', !message.active);
    if (message.message) $('loadingText').textContent = message.message;
  } else if (message.type === 'report') {
    state.report = message.payload;
    $('emptyState').classList.add('hidden');
    $('workspace').classList.remove('hidden');
    $('exportBtn').disabled = false;
    $('libraryBtn').classList.remove('hidden');
    $('fileMeta').textContent = `${message.payload.file.fileName} · ${message.payload.file.fileSizeText} · ${message.payload.match.frameCount.toLocaleString()} 帧`;
    renderAll();
  } else if (message.type === 'frameDetail') {
    renderFrameDetail(message.payload);
  } else if (message.type === 'toast') {
    toast(message.message);
  } else if (message.type === 'error') {
    $('loading').classList.add('hidden');
    toast(`解析失败：${message.message}`);
  }
});

function renderReplayLibrary(library) {
  const panel = $('recentReplayPanel');
  panel.classList.remove('hidden');
  $('replayDirectory').textContent = library.directory || '默认回放目录不可用';
  const rows = $('recentReplayRows');
  if (!library.available) {
    rows.innerHTML = '<div class="replay-library-empty">没有找到游戏回放目录。仍可使用上方按钮手动选择回放文件。</div>';
    return;
  }
  if (!library.items.length) {
    rows.innerHTML = '<div class="replay-library-empty">这个目录中暂时没有可查看的回放。</div>';
    return;
  }
  rows.innerHTML = library.items.map(item => `<button class="replay-entry" data-replay-token="${esc(item.token)}">
    <span class="replay-file-mark">▶</span><span><strong>${esc(item.name)}</strong><small>${esc(item.fileName)} · ${esc(item.sizeText)}</small></span>
    <span class="replay-time">${esc(item.modifiedText)}</span><span class="replay-arrow">›</span></button>`).join('');
  rows.querySelectorAll('[data-replay-token]').forEach(button => button.addEventListener('click', () =>
    post({type:'openRecentReplay', token:button.dataset.replayToken})));
}

function renderAll() {
  renderSummary(); renderPlayers(); renderTimeline(); renderRelics(); renderProtocol(); renderStatistics();
  showTechnicalPane('statisticsPane');
  showView(state.activeView);
}

function renderSummary() {
  const r = state.report, m = r.match;
  $('summaryView').innerHTML = `
    <div class="hero-grid">
      <article class="panel map-card">
        ${m.mapImage ? `<img src="${esc(m.mapImage)}" alt="">` : ''}
        <div class="map-copy"><small>本局地图</small><h2>${esc(m.mapName)}</h2><p>难度 ${m.difficulty} · 最终首领 ${esc(m.bossName)}</p></div>
      </article>
      <article class="panel result-panel">
        <div class="result-top"><div><span class="label">对局结果</span><div class="result">${esc(m.resultText)}</div><span>${esc(m.winnerText)}</span></div><div class="award"><span class="label">${gameIcon('starCoin')}结算奖励</span><p>${esc(m.awardsText)}</p></div></div>
        <div class="metrics">
          ${metric('时长', m.durationText)}${metric('回合', m.roundCount)}${metric('关卡进度', m.progressText)}${metric('死亡计数', m.playerDeaths)}
          ${metric('开始时间', m.startTimeText, true)}${metric('结束时间', m.finishTimeText, true)}
        </div>
      </article>
    </div>
    <div class="section-title"><h2>参战玩家</h2><span>${r.players.length} 名玩家</span></div>
    <div class="player-cards">${r.players.map(playerCard).join('')}</div>`;
}

function metric(label, value, small = false) {
  return `<div class="metric"><small>${esc(label)}</small><strong${small ? ' style="font-size:13px;margin-top:9px"' : ''}>${esc(value)}</strong></div>`;
}

function playerCard(p) {
  return `<article class="panel player-card">
    <div class="portrait">${p.avatar ? `<img src="${esc(p.avatar)}" alt="">` : '<span>暂无角色图片</span>'}<em>ID ${p.heroId}</em></div>
    <h3>${esc(p.nickname)}${p.finalBossKill ? ' <span class="boss">♛</span>' : ''}</h3>
    <div class="hero">${esc(p.heroName)} · 账号等级 ${p.accountLevel}</div><div class="status">${esc(p.finalStatus)}</div>
    <div class="mini-stats"><div>${gameIcon('damage')}<b>${fmt(p.damage)}</b><small>伤害</small></div><div>${gameIcon('monster')}<b>${fmt(p.kills)}</b><small>击杀</small></div><div>${gameIcon('relicLand')}<b>${fmt(p.selectedRelicCount)}</b><small>筹码</small></div></div>
  </article>`;
}

function renderPlayers() {
  const r = state.report;
  $('playersView').innerHTML = `<div class="toolbar"><p>结算快照中的最终数据；无英雄数据的玩家保留服务器占位状态。</p></div>
    <div class="performance-grid">${r.players.map(p => `<article class="panel performance-card">
      <div class="performance-identity"><div class="portrait">${p.avatar ? `<img src="${esc(p.avatar)}" alt="">` : `<span>角色 ${p.heroId}</span>`}</div><div><h3>${esc(p.nickname)}</h3><p>${esc(p.heroName)} · ID ${p.id}</p><p>${esc(p.finalStatus)}</p></div></div>
      <div class="performance-body"><div class="performance-metrics">
        ${performance('生命', p.hpText, 'hp')}${performance('星币', p.gold, 'starCoin')}${performance('攻击', p.attack, 'attack')}${performance('防御', p.defense, 'defense')}
        ${performance('伤害', p.damage, 'damage')}${performance('承伤', p.injured, 'damage')}${performance('击杀', p.kills, 'monster')}${performance('治疗', p.healing, 'healing')}
        ${performance('移动', p.movePoints, 'move')}${performance('卡牌', p.usedCards, 'card')}${performance('技能', p.usedSkills)}${performance('转移星币', p.transferGold, 'starCoin')}
        ${performance('获得筹码', p.boughtRelics, 'relicLand')}${performance('最终一击', p.finalBossKill ? '是' : '否', 'monster')}
      </div><div class="relic-line"><b>已选择 ${p.selectedRelicCount} 个筹码：</b>${esc(p.selectedRelicsText)}</div></div>
    </article>`).join('')}</div>`;
}
function performance(label, value, assetKey = '') { return `<div>${assetKey ? gameIcon(assetKey) : ''}<b>${esc(value)}</b><small>${esc(label)}</small></div>`; }

function renderTimeline() {
  const root = $('timelineView');
  root.classList.remove('table-view');
  root.innerHTML = `<div class="toolbar"><p id="eventCount"></p><input id="eventSearch" class="search" placeholder="筛选回合、玩家、角色或事件"></div><div id="roundRows" class="round-list"></div>`;
  const update = () => {
    const q = $('eventSearch').value.trim().toLowerCase();
    const all = state.report.events.filter(x => {
      const player = state.report.players.find(player => Number(player.id) === Number(x.playerId));
      return !q || `${x.frameIndex} ${x.roundText} ${x.playerName} ${player?.heroName || ''} ${player?.heroId || ''} ${x.type} ${x.title} ${x.description}`.toLowerCase().includes(q);
    });
    const groups = new Map();
    all.forEach(item => {
      const key = item.round > 0 ? item.round : 0;
      if (!groups.has(key)) groups.set(key, []);
      const row = groups.get(key), previous = row[row.length - 1];
      const signature = `${item.playerId}|${item.type}|${item.title}`;
      if (previous?.signature === signature) previous.count++;
      else row.push({ item, signature, count: 1 });
    });
    $('eventCount').textContent = `${groups.size} 个回合分区 · ${all.length.toLocaleString()} 条文字记录`;
    $('roundRows').innerHTML = [...groups.entries()].sort((a, b) => a[0] - b[0]).map(([round, events]) => `
      <article class="panel round-row"><div class="round-heading"><span>${round > 0 ? `第 ${round} 回合` : '开局 / 全局'}</span><small>${events.reduce((sum, x) => sum + x.count, 0)} 个事件</small></div>
      <ol class="round-text-events">${events.map(({item, count}, index) => timelineEvent(item, count, index + 1)).join('')}</ol></article>`).join('') || '<div class="replay-library-empty">没有符合条件的事件</div>';
  };
  $('eventSearch').addEventListener('input', update); update();
}

function timelineEvent(item, count, order) {
  const player = state.report.players.find(player => Number(player.id) === Number(item.playerId));
  const iconUrl = item.icon || player?.avatar;
  const subject = player?.nickname || item.playerName || '游戏';
  let sentence = String(item.description || '').trim();
  if (!sentence || /(^|\s)(Action|CMD|C2S|S2C|SN)(\s|$)/i.test(sentence)) {
    sentence = item.playerId ? `${subject}：${item.title}` : item.title;
  }
  return `<li class="round-text-item" title="第 ${item.frameIndex} 帧">
    <span class="event-order">${order}</span>${image(iconUrl, '✦', 'timeline-text-icon')}
    <div><small>${esc(subject)} · ${esc(item.type)}</small><p>${esc(sentence)}</p></div>${count > 1 ? `<b>连续 ${count} 次</b>` : ''}
  </li>`;
}

function renderRelics() {
  const root = $('relicsView');
  root.classList.add('table-view');
  root.innerHTML = `<div class="relic-summary" id="relicSummary"></div><div class="toolbar"><p id="relicCount"></p><input id="relicSearch" class="search" placeholder="筛选玩家、角色、来源、品质或筹码"></div><div class="panel table-wrap"><table style="min-width:1260px"><thead><tr><th>图片</th><th>回合</th><th>玩家 / 角色</th><th>来源</th><th>操作</th><th>档位</th><th>筹码</th><th>品质</th><th>刷新</th><th>候选内容</th></tr></thead><tbody id="relicRows"></tbody></table></div>`;
  const picked = state.report.relics.filter(x => x.kind === '选择');
  const refreshes = state.report.relics.filter(x => x.isRefresh);
  $('relicSummary').innerHTML = `<div><span class="star-symbol">★</span><span><b>${picked.filter(x => x.source === '升星').length}</b><small>升星获得</small></span></div><div>${gameIcon('mission')}<span><b>${picked.filter(x => x.source === '任务').length}</b><small>任务获得</small></span></div><div>${gameIcon('relicLand')}<span><b>${picked.filter(x => x.source === '筹码地块购买').length}</b><small>筹码地块购买</small></span></div><div><span class="refresh-symbol">↻</span><span><b>${refreshes.length}</b><small>刷新次数</small></span></div>`;
  const update = () => {
    const q = $('relicSearch').value.trim().toLowerCase();
    const rows = state.report.relics.filter(x => {
      const player = state.report.players.find(player => Number(player.id) === Number(x.playerId));
      return !q || `${x.playerName} ${player?.heroName || ''} ${player?.heroId || ''} ${x.source} ${x.kind} ${x.relicId} ${x.relicName} ${x.quality} ${x.refreshText} ${x.optionsText}`.toLowerCase().includes(q);
    });
    $('relicCount').textContent = `${rows.length} 条记录 · “首次候选 → 选择”表示未刷新，“刷新候选”每出现一次计一次刷新`;
    $('relicRows').innerHTML = rows.map(x => `<tr><td>${image(x.image || x.sourceIcon, x.relicId || '◆', 'relic-icon')}</td><td>${x.round || '—'}</td><td>${playerEntity(x.playerId, x.playerName)}</td><td><span class="source-badge source-${x.source === '升星' ? 'upgrade' : x.source === '任务' ? 'task' : x.source === '筹码地块购买' ? 'purchase' : 'unknown'}">${x.sourceIcon ? `<img src="${esc(x.sourceIcon)}" alt="">` : ''}${esc(x.source)}</span></td><td><span class="tag relic">${esc(x.kind)}</span></td><td>${esc(x.levelText)}</td><td>${esc(x.relicName || '—')}</td><td class="quality-${esc(x.quality)}">${esc(x.quality || '—')}</td><td>${esc(x.refreshText)}</td><td class="muted">${esc(x.optionsText)}</td></tr>`).join('');
  };
  $('relicSearch').addEventListener('input', update); update();
}

function renderProtocol() {
  const root = $('protocolPane');
  root.classList.add('table-view');
  root.innerHTML = `<div class="toolbar"><p id="frameCount"></p><input id="frameSearch" class="search" placeholder="筛选帧、CMD 或消息名称"></div><div class="protocol-layout"><div class="panel table-wrap"><table><thead><tr><th>帧</th><th>偏移</th><th>CMD</th><th>消息</th><th>字节</th></tr></thead><tbody id="frameRows"></tbody></table></div><div class="panel detail"><h3>协议帧详情</h3><pre id="frameDetail">选择左侧帧。已注册消息显示 protobuf JSON，未知命令显示十六进制。</pre></div></div>`;
  const update = () => {
    const q = $('frameSearch').value.trim().toLowerCase();
    const all = state.report.frames.filter(x => !q || `${x.index} ${x.offsetText} ${x.cmdId} ${x.messageName}`.toLowerCase().includes(q));
    const rows = all.slice(0, 700);
    $('frameCount').textContent = `显示 ${rows.length.toLocaleString()} / ${all.length.toLocaleString()} 帧`;
    $('frameRows').innerHTML = rows.map(x => `<tr class="frame-row ${state.selectedFrame === x.index ? 'selected' : ''}" data-frame="${x.index}"><td>${x.index}</td><td class="muted">${x.offsetText}</td><td>${x.cmdId}</td><td>${esc(x.messageName)}</td><td class="muted">${fmt(x.payloadLength)}</td></tr>`).join('');
    document.querySelectorAll('.frame-row').forEach(row => row.addEventListener('click', () => {
      state.selectedFrame = Number(row.dataset.frame); document.querySelectorAll('.frame-row').forEach(x => x.classList.toggle('selected', x === row));
      $('frameDetail').textContent = '正在向 .NET 请求解码…'; post({type:'getFrame', index:state.selectedFrame});
    }));
  };
  $('frameSearch').addEventListener('input', update); update();
}

function renderFrameDetail(frame) {
  const target = $('frameDetail');
  if (!target) return;
  target.textContent = `帧 ${frame.index} · ${frame.offsetText}\nCMD ${frame.cmdId} · ${frame.messageName}\n载荷 ${frame.payloadLength.toLocaleString()} 字节\n\n${frame.detail}`;
}

function renderStatistics() {
  const r = state.report;
  $('statisticsPane').innerHTML = `<div class="stat-columns">
    ${statPanel('协议命令频率', r.statistics.commands, '')}
    ${statPanel('去重行动频率', r.statistics.actions, 'accent-cyan')}
    <div>${statPanel('筹码选择品质', r.statistics.relicQualities, 'accent-gold')}<article class="panel stat-panel" style="margin-top:14px"><h2>文件与解析</h2><div class="file-facts"><div><small>文件</small><b>${esc(r.file.fileName)} · ${esc(r.file.fileSizeText)}</b></div><div><small>回放 / 游戏版本</small><b>${esc(r.match.replayId)} · ${esc(r.match.gameVersion)}</b></div><div><small>协议规模</small><b>${fmt(r.match.frameCount)} 帧 · ${r.match.commandTypeCount} 种命令</b></div><div><small>SHA-256</small><b style="font:10px Consolas">${esc(r.file.sha256)}</b></div></div></article></div>
  </div>`;
}

function statPanel(title, items, cls) {
  const max = Math.max(1, ...items.map(x => x.count));
  return `<article class="panel stat-panel ${cls}"><h2>${esc(title)}</h2>${items.map(x => `<div class="stat-row"><span title="${esc(x.name)}">${esc(x.name)}</span><div class="bar"><i style="width:${(x.count / max * 100).toFixed(2)}%"></i></div><output>${fmt(x.count)} · ${(x.percent * 100).toFixed(1)}%</output></div>`).join('') || '<p class="muted">没有数据</p>'}</article>`;
}

function toast(message) {
  const node = $('toast'); node.textContent = message; node.classList.add('show');
  clearTimeout(toast.timer); toast.timer = setTimeout(() => node.classList.remove('show'), 4200);
}

post({type:'ready'});
