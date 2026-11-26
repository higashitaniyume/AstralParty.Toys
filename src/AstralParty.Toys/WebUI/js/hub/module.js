// hub/module.js — 组队大厅（Microsoft OAuth PKCE、1 对 N 绑定、10 项隐私、自动抓房进房、两阶段战报）
(function () {
  'use strict';

  const esc = v => String(v ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const $id = id => document.getElementById(id);
  const imgUrl = v => typeof v === 'string' && /^https:\/\/(assets|materials\d+)\.aspatoys\.hiynet\.localhost\//.test(v) ? v : '';

  const MODES = {
    1: '标准', 2: '新手', 3: '无限火力', 4: 'PVE', 5: '战役 PVP', 6: '战役 PVE',
    7: '非对称对抗', 8: '练习 PVP', 9: '练习 PVE', 10: 'PVE 新手', 11: '幸运星争夺战', 12: '词条 PVE'
  };

  const GAME_STATES = {
    idle: ['大堂待命', ''],
    launching: ['启动游戏中…', 'gold'],
    in_lobby: ['游戏主界面在线', 'green'],
    joining: ['自动进房中…', 'gold'],
    in_game_room: ['已进游戏房', 'green'],
    playing: ['对局中', 'pink'],
    join_failed: ['进房失败', 'danger']
  };

  const PRIVACY_ITEMS = [
    ['showLinkedAccounts', '已绑定的游戏 UID 清单', '向他人展示当前大师账号下绑定的国服/国际服 UID'],
    ['showProfileMeta', '账号等级与活跃时间', '展示游戏等级、经验进度、账号创建与最近登录时间'],
    ['showCreditAndPraise', '信誉分与获赞数', '展示游戏内信誉评分与累计获赞次数'],
    ['showSummaryStats', '战绩总览与收藏数量', '展示总场次、胜场/胜率、角色/皮肤/装扮拥有数'],
    ['showAchievements', '成就徽章', '展示游戏账号已获取的成就标签'],
    ['showWinMaps', '地图胜场分布', '展示各张地图的历史胜场数'],
    ['showCharacters', '角色练度与参战次数', '展示所有已拥有角色的等级、突破与参战次数'],
    ['showRecentMatches', '近期对局历史', '展示最近 20 场对局的摘要卡片'],
    ['showMatchDetails', '同局对局明细', '允许他人展开近期对局内所有同局玩家详情'],
    ['showLobbyPresence', '大厅实时组队状态', '允许在组队大厅中展示你的当前房间状态']
  ];

  const S = {
    initialized: false,
    tab: 'lobby',
    region: 'cn',
    serverUrl: 'https://connect.hiynet.com',
    autoJoinEnabled: true,
    serverConfig: null,
    auth: { signedIn: false, signingIn: false, userId: '', displayName: '', error: '' },
    me: null,
    localAccounts: [],
    bridge: { running: false, port: 0, gameFound: false, modInstalled: false, modEnabled: false, sessions: [] },
    rooms: [],
    roomQuery: '',
    roomStatusFilter: '',
    currentRoom: null,
    roomEvents: [],
    viewedProfile: null,
    lookupInput: '',
    lookupRegion: 'cn',
    stage1: null,
    stage2: null,
    stage2Status: 'idle',
    stage2Message: '',
    pendingJoinRoom: null
  };

  let _bound = false;

  function formatTime(ms) {
    if (!ms || ms <= 0) return '—';
    const v = ms < 1e12 ? ms * 1000 : ms;
    const d = new Date(v);
    if (Number.isNaN(d.getTime())) return '—';
    const pad = n => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  function regionLabel(r) {
    return r === 'int' ? '国际服 INT' : '国服 CN';
  }

  function applyPolyfills() {
    if (!('switch' in HTMLInputElement.prototype)) {
      document.documentElement.classList.add('hub-switch-fallback');
    }
    if (!('closedBy' in HTMLDialogElement.prototype)) {
      const outside = (e, dlg) => {
        const r = dlg.getBoundingClientRect();
        return e.clientX < r.left || e.clientX > r.right || e.clientY < r.top || e.clientY > r.bottom;
      };
      for (const dlg of document.querySelectorAll('#hubView dialog[closedby="any"]')) {
        let downOut = false;
        dlg.addEventListener('pointerdown', e => { downOut = outside(e, dlg); });
        dlg.addEventListener('pointerup', e => { if (downOut && outside(e, dlg)) dlg.close('cancel'); });
      }
    }
    for (const dlg of document.querySelectorAll('#hubView dialog')) {
      dlg.addEventListener('close', () => { dlg.returnValue = ''; });
    }
  }

  function selectTab(nextTab, focusTab = false) {
    S.tab = nextTab;
    const tabs = Array.from(document.querySelectorAll('#hubView [role="tab"]'));
    const panels = { lobby: $id('hubPanelLobby'), profile: $id('hubPanelProfile'), settlement: $id('hubPanelSettlement') };

    for (const t of tabs) {
      const isSelected = t.dataset.hubTab === nextTab;
      t.setAttribute('aria-selected', isSelected ? 'true' : 'false');
      t.toggleAttribute('focusgroupstart', isSelected);
      if (isSelected && focusTab) t.focus();
    }

    for (const [k, panel] of Object.entries(panels)) {
      if (!panel) continue;
      if (k === nextTab) {
        panel.hidden = false;
        panel.tabIndex = 0;
      } else {
        panel.hidden = true;
        panel.removeAttribute('tabindex');
      }
    }

    if (nextTab === 'settlement') {
      const badge = $id('hubSettlementBadge');
      if (badge) badge.hidden = true;
    }
  }

  function bindOnce() {
    if (_bound) return;
    _bound = true;
    applyPolyfills();

    const tablist = document.querySelector('#hubView [role="tablist"], #hubView [focusgroup]');
    const tabs = Array.from(document.querySelectorAll('#hubView [role="tab"]'));
    tabs.forEach(btn => {
      btn.addEventListener('click', () => selectTab(btn.dataset.hubTab, false));
    });

    if (tablist && !Object.hasOwn(HTMLElement.prototype, 'focusGroup')) {
      tablist.addEventListener('keydown', e => {
        const idx = tabs.indexOf(document.activeElement);
        if (idx < 0) return;
        let next = -1;
        if (e.key === 'ArrowRight') next = (idx + 1) % tabs.length;
        else if (e.key === 'ArrowLeft') next = (idx - 1 + tabs.length) % tabs.length;
        else if (e.key === 'Home') next = 0;
        else if (e.key === 'End') next = tabs.length - 1;
        if (next >= 0) {
          e.preventDefault();
          selectTab(tabs[next].dataset.hubTab, true);
        }
      });
    }

    document.querySelectorAll('#hubView .hub-region-btn').forEach(btn => {
      btn.addEventListener('click', () => {
        const reg = btn.dataset.hubRegion === 'int' ? 'int' : 'cn';
        if (S.currentRoom && S.currentRoom.region !== reg) {
          window.toast?.('当前已在其他区服组队房间内，请先退出当前房间再切换区服。');
          return;
        }
        S.region = reg;
        renderHeader();
        post({ type: 'hubSetRegion', region: reg, query: S.roomQuery, status: S.roomStatusFilter });
      });
    });

    $id('hubServerBtn')?.addEventListener('click', () => {
      $id('hubServerUrlInput').value = S.serverUrl;
      renderBridgeDiag();
      $id('hubServerDialog').showModal();
    });

    $id('hubBridgePillBtn')?.addEventListener('click', () => {
      $id('hubServerUrlInput').value = S.serverUrl;
      renderBridgeDiag();
      $id('hubServerDialog').showModal();
    });

    $id('hubServerSaveBtn')?.addEventListener('click', () => {
      const serverUrl = $id('hubServerUrlInput').value.trim();
      $id('hubServerDialog').close('save');
      post({ type: 'hubSaveServerUrl', serverUrl });
    });

    $id('hubCreateSubmitBtn')?.addEventListener('click', () => {
      const title = $id('hubCreateTitle').value.trim();
      if (!title) {
        $id('hubCreateErr').textContent = '请填写房间标题';
        return;
      }
      $id('hubCreateErr').textContent = '';
      post({
        type: 'hubCreateRoom',
        region: S.region,
        title,
        note: $id('hubCreateNote').value.trim(),
        password: $id('hubCreatePassword').value,
        capacity: Number($id('hubCreateCapacity').value || 4),
        activeUid: $id('hubCreateUid').value
      });
      $id('hubCreateRoomDialog').close('ok');
    });

    $id('hubJoinSubmitBtn')?.addEventListener('click', () => {
      if (!S.pendingJoinRoom) return;
      $id('hubJoinErr').textContent = '';
      post({
        type: 'hubJoinRoom',
        roomId: S.pendingJoinRoom.roomId,
        password: $id('hubJoinPassword').value,
        activeUid: $id('hubJoinUid').value
      });
      $id('hubJoinRoomDialog').close('ok');
    });
  }

  function renderBridgeDiag() {
    const box = $id('hubBridgeDiagBox');
    if (!box) return;
    const b = S.bridge;
    const live = (b.sessions || []).find(s => s.isLoggedIn);
    box.innerHTML = `
      <div><strong>本机 IPC 端口：</strong>${b.running ? `127.0.0.1:${b.port}` : '未启动'}</div>
      <div><strong>HubBridgeMod：</strong>${!b.gameFound ? '未选择游戏目录' : !b.modInstalled ? '未安装内置组队桥接模组（可在模组页重装内置加载器获取）' : b.modEnabled ? '已安装并启用' : '已安装但被停用'}</div>
      <div><strong>实时游戏连接：</strong>${live ? `已登录 ${regionLabel(live.region)} · UID ${esc(live.uid)} · 场景 ${esc(live.scene || 'Home')}${live.gameRoomId > 0 ? ` · 游戏房 ${live.gameRoomId}` : ''}` : '等待游戏启动并进入主界面'}</div>
    `;
  }

  function renderHeader() {
    document.querySelectorAll('#hubView .hub-region-btn').forEach(btn => {
      btn.classList.toggle('is-active', btn.dataset.hubRegion === S.region);
    });

    const b = S.bridge;
    const live = (b.sessions || []).find(s => s.isLoggedIn);
    const dot = $id('hubBridgeDot');
    const pillText = $id('hubBridgePillText');
    if (dot && pillText) {
      dot.className = 'hub-dot ' + (live ? 'online' : b.modInstalled && b.modEnabled ? 'warn' : '');
      pillText.textContent = live
        ? `游戏在线 · ${live.uid}`
        : b.modInstalled && b.modEnabled
          ? '桥接就绪（待开游戏）'
          : '未装桥接模组';
    }

    const authEl = $id('hubAuthControl');
    if (!authEl) return;
    if (S.auth.signingIn) {
      authEl.innerHTML = `
        <span class="hub-pill gold">请在浏览器完成 Microsoft 登录…</span>
        <button type="button" class="v2-btn ghost" data-hub-act="cancel-signin">取消</button>
      `;
    } else if (S.me) {
      authEl.innerHTML = `
        <div class="hub-user-chip">
          <span>MS</span>
          <strong>${esc(S.me.displayName)}</strong>
        </div>
        <button type="button" class="v2-btn ghost" data-hub-act="logout">退出</button>
      `;
    } else {
      authEl.innerHTML = `
        <button type="button" class="v2-btn primary" data-hub-act="signin">Microsoft 登录</button>
      `;
    }

    authEl.querySelector('[data-hub-act="signin"]')?.addEventListener('click', () => post({ type: 'hubSignIn' }));
    authEl.querySelector('[data-hub-act="cancel-signin"]')?.addEventListener('click', () => post({ type: 'hubCancelSignIn' }));
    authEl.querySelector('[data-hub-act="logout"]')?.addEventListener('click', () => post({ type: 'hubLogout' }));
  }

  function regionBindings(region = S.region) {
    return (S.me?.bindings || []).filter(b => b.region === region);
  }

  function openCreateDialog() {
    if (!S.me) {
      window.toast?.('请先点击右上角「Microsoft 登录」再创建组队房间。');
      return;
    }
    const bindings = regionBindings(S.region);
    if (!bindings.length) {
      window.toast?.(`你还没有绑定 ${regionLabel(S.region)} 游戏 UID，先前往「个人主页与绑定」绑定。`);
      selectTab('profile', false);
      return;
    }

    $id('hubCreateRegionLabel').textContent = regionLabel(S.region);
    const sel = $id('hubCreateUid');
    sel.innerHTML = bindings.map(b =>
      `<option value="${esc(b.uid)}" ${b.isPrimary ? 'selected' : ''}>${esc(b.nick || b.uid)} (UID: ${esc(b.uid)}${b.level ? ` · Lv.${b.level}` : ''})</option>`
    ).join('');
    $id('hubCreateTitle').value = `${S.me.displayName} 的队伍`;
    $id('hubCreateNote').value = '';
    $id('hubCreatePassword').value = '';
    $id('hubCreateErr').textContent = '';
    $id('hubCreateRoomDialog').showModal();
  }

  function openJoinDialog(room) {
    if (!S.me) {
      window.toast?.('请先使用 Microsoft 账号登录。');
      return;
    }
    const bindings = regionBindings(room.region);
    if (!bindings.length) {
      window.toast?.(`你还没有绑定 ${regionLabel(room.region)} 游戏 UID，先前往「个人主页与绑定」绑定。`);
      selectTab('profile', false);
      return;
    }

    S.pendingJoinRoom = room;
    $id('hubJoinRoomName').textContent = `${room.title} · 房主 ${room.hostDisplayName} (${regionLabel(room.region)})`;
    $id('hubJoinPasswordWrap').hidden = !room.hasPassword;
    $id('hubJoinPassword').value = '';
    $id('hubJoinErr').textContent = '';
    $id('hubJoinUid').innerHTML = bindings.map(b =>
      `<option value="${esc(b.uid)}" ${b.isPrimary ? 'selected' : ''}>${esc(b.nick || b.uid)} (UID: ${esc(b.uid)})</option>`
    ).join('');
    $id('hubJoinRoomDialog').showModal();
  }

  function renderLobby() {
    const body = $id('hubLobbyBody');
    if (!body) return;

    const badge = $id('hubRoomsBadge');
    if (badge) {
      badge.textContent = String(S.rooms.length);
      badge.hidden = S.rooms.length === 0;
    }

    if (S.currentRoom) {
      renderActiveRoom(body, S.currentRoom);
      return;
    }

    const needSignInBanner = !S.me ? `
      <div class="hub-card" style="display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:12px;">
        <div>
          <h3 style="margin:0 0 4px;">使用 Microsoft 账号即可加入组队大厅</h3>
          <p class="hub-muted">采用标准桌面 OAuth PKCE 登录，不保存密码与机密；1 个大厅账号可绑定多个国服 / 国际服游戏 UID。</p>
        </div>
        <button type="button" class="v2-btn primary" id="hubInlineSignInBtn">Microsoft 登录</button>
      </div>` : '';

    const cards = S.rooms.length
      ? S.rooms.map(r => {
        const statusCls = r.status === 'recruiting' ? 'green' : r.status === 'in_game' ? 'pink' : r.status === 'settled' ? 'gold' : '';
        const statusTxt = { recruiting: '招募中', host_launching: '房主启动中', in_game: '游戏内', settled: '已结束' }[r.status] || r.status;
        return `
          <article class="hub-room-card">
            <div class="hub-room-card-top">
              <h4>${esc(r.title)}</h4>
              <span class="hub-pill ${statusCls}">${esc(statusTxt)}</span>
            </div>
            <div class="hub-room-note">${esc(r.note || '暂无备注')}</div>
            <div class="hub-room-meta">
              <span class="hub-pill">${esc(regionLabel(r.region))}</span>
              <span>房主：${esc(r.hostDisplayName)} (${esc(r.hostActiveUid)})</span>
              ${r.hasPassword ? '<span class="hub-pill gold">加密</span>' : ''}
            </div>
            <div class="hub-room-foot">
              <span class="hub-muted">${r.memberCount} / ${r.capacity} 人 · ${formatTime(r.lastActivityAt)}</span>
              <button type="button" class="v2-btn primary" data-hub-join="${esc(r.roomId)}">加入队伍</button>
            </div>
          </article>
        `;
      }).join('')
      : `<div class="hub-card"><p class="hub-muted" style="text-align:center;padding:24px 0;">${esc(regionLabel(S.region))} 当前暂无招募中的队伍，点击右上角「创建组队房间」开一局吧。</p></div>`;

    body.innerHTML = `
      ${needSignInBanner}
      <div class="hub-card">
        <div class="hub-card-head">
          <div class="hub-toolbar">
            <input id="hubSearchInput" class="hub-input" value="${esc(S.roomQuery)}" placeholder="搜索房名、备注、房主或 UID" />
            <select id="hubStatusSelect" class="v2-select">
              <option value="" ${S.roomStatusFilter === '' ? 'selected' : ''}>全部状态</option>
              <option value="recruiting" ${S.roomStatusFilter === 'recruiting' ? 'selected' : ''}>仅看招募中</option>
              <option value="in_game" ${S.roomStatusFilter === 'in_game' ? 'selected' : ''}>游戏中</option>
              <option value="settled" ${S.roomStatusFilter === 'settled' ? 'selected' : ''}>已结算</option>
            </select>
            <button type="button" class="v2-btn ghost" id="hubRefreshRoomsBtn">刷新</button>
          </div>
          <button type="button" class="v2-btn primary" id="hubOpenCreateBtn">+ 创建组队房间</button>
        </div>
        <div class="hub-rooms-grid">${cards}</div>
      </div>
    `;

    $id('hubInlineSignInBtn')?.addEventListener('click', () => post({ type: 'hubSignIn' }));
    $id('hubOpenCreateBtn')?.addEventListener('click', openCreateDialog);
    $id('hubRefreshRoomsBtn')?.addEventListener('click', () => {
      S.roomQuery = $id('hubSearchInput')?.value || '';
      S.roomStatusFilter = $id('hubStatusSelect')?.value || '';
      post({ type: 'hubListRooms', region: S.region, query: S.roomQuery, status: S.roomStatusFilter });
    });
    $id('hubSearchInput')?.addEventListener('keydown', e => {
      if (e.key === 'Enter') $id('hubRefreshRoomsBtn')?.click();
    });
    $id('hubStatusSelect')?.addEventListener('change', () => $id('hubRefreshRoomsBtn')?.click());

    body.querySelectorAll('[data-hub-join]').forEach(btn => {
      btn.addEventListener('click', () => {
        const room = S.rooms.find(x => x.roomId === btn.dataset.hubJoin);
        if (room) openJoinDialog(room);
      });
    });
  }

  function renderActiveRoom(body, room) {
    const isHost = S.me && room.hostUserId === S.me.userId;
    const bindings = regionBindings(room.region);
    const gr = room.gameRoom;
    const statusTxt = { recruiting: '招募中', host_launching: '房主正在启动游戏', in_game: '游戏房间已就绪', settled: '本局已结算' }[room.status] || room.status;

    const gameRoomBanner = gr && gr.gameRoomId > 0 ? `
      <div class="hub-gameroom-banner">
        <div class="hub-gameroom-codes">
          <div><div class="hub-muted">游戏房间号</div><strong>${esc(gr.gameRoomId)}</strong></div>
          <div><div class="hub-muted">房间密码</div><strong>${esc(gr.gamePwd || '无密码')}</strong></div>
          <div><div class="hub-muted">线路 / 地图</div><strong>#${esc(gr.roomServerId)} · 地图 ${esc(gr.mapId)}</strong></div>
        </div>
        <div class="hub-toolbar">
          <button type="button" class="v2-btn ghost" id="hubCopyGameCodeBtn">复制房号密码</button>
          ${!isHost ? `<button type="button" class="v2-btn primary" id="hubTriggerJoinBtn">一键加入游戏房间</button>` : ''}
        </div>
      </div>` : `
      <div class="hub-notice-warn">
        ${isHost
          ? '请先启动游戏并在游戏内手动创建一个组队房间；装有内置 <strong>HubBridgeMod</strong> 的游戏客户端会自动捕获房号与密码并广播给队友。'
          : '等待房主启动游戏并在游戏内创建房间；房主建房后，这里会自动捕获房号并触发队友自动进房。'}
      </div>`;

    const memberCards = room.members.map(m => {
      const [stText, stCls] = GAME_STATES[m.gameState] || [m.gameState, ''];
      return `
        <div class="hub-member-card ${m.self ? 'is-self' : ''}">
          <div class="hub-member-top">
            <div class="hub-avatar">${esc((m.activeNick || m.displayName || '?').slice(0, 1))}</div>
            <div class="hub-member-info">
              <strong>${esc(m.displayName)} ${m.isHost ? '<span class="hub-pill pink">房主</span>' : ''}</strong>
              <small>${esc(m.activeNick || 'UID')} · ${esc(m.activeUid)}${m.activeLevel ? ` · Lv.${m.activeLevel}` : ''}</small>
            </div>
          </div>
          <div class="hub-toolbar">
            <span class="hub-pill ${stCls}">${esc(stText)}</span>
            ${m.online ? '<span class="hub-pill green">在线</span>' : '<span class="hub-pill">离线</span>'}
          </div>
          ${m.joinError ? `<small style="color:#fb7185;">${esc(m.joinError)}</small>` : ''}
          <div class="hub-member-actions">
            <button type="button" class="v2-btn ghost" data-hub-view-user="${esc(m.userId)}">查看资料</button>
            ${isHost && !m.isHost ? `<button type="button" class="v2-btn ghost" data-hub-kick="${esc(m.userId)}">移出</button>` : ''}
          </div>
        </div>
      `;
    }).join('');

    const chatHtml = S.roomEvents.length
      ? S.roomEvents.map(ev => {
        const isChat = ev.kind === 'chat';
        return `<div class="hub-chat-item ${isChat ? '' : 'sys'}"><span class="at">${esc(formatTime(ev.at))}</span>${isChat ? `<span class="who">${esc(ev.displayName)}:</span>` : ''}${esc(ev.text)}</div>`;
      }).join('')
      : '<div class="hub-muted">房间事件与队内聊天将显示在这里…</div>';

    body.innerHTML = `
      <div class="hub-card">
        <div class="hub-card-head">
          <div>
            <h3>${esc(room.title)} <span class="hub-pill">${esc(regionLabel(room.region))}</span> <span class="hub-pill pink">${esc(statusTxt)}</span></h3>
            <p class="hub-muted">${esc(room.note || '无备注')} · 大厅号 ${esc(room.roomId)} · ${room.members.length}/${room.capacity} 人</p>
          </div>
          <div class="hub-toolbar">
            <label class="hub-pill" style="cursor:pointer;">
              <span>自动进房</span>
              <input id="hubAutoJoinSwitch" type="checkbox" switch ${S.autoJoinEnabled ? 'checked' : ''} />
            </label>
            ${bindings.length > 1 ? `<select id="hubRoomSwitchUid" class="v2-select">${bindings.map(b => `<option value="${esc(b.uid)}">${esc(b.nick || b.uid)} (${esc(b.uid)})</option>`).join('')}</select>` : ''}
            ${isHost ? `<button type="button" class="v2-btn primary" id="hubHostLaunchBtn">启动游戏去开房</button>` : ''}
            ${isHost && room.status === 'settled' ? `<button type="button" class="v2-btn ghost" id="hubReopenBtn">开启下一局招募</button>` : ''}
            ${room.latestMatch ? `<button type="button" class="v2-btn ghost" id="hubOpenLatestSettleBtn">查看本局结算</button>` : ''}
            <button type="button" class="v2-btn ghost" id="hubLeaveRoomBtn">退出房间</button>
          </div>
        </div>
        ${gameRoomBanner}
      </div>

      <div class="hub-stage-grid">
        <div class="hub-card">
          <div class="hub-card-head"><h3>队伍成员 (${room.members.length} / ${room.capacity})</h3></div>
          <div class="hub-members-grid">${memberCards}</div>
        </div>

        <div class="hub-card hub-chat-box">
          <div class="hub-card-head"><h3>队内频道</h3></div>
          <div class="hub-chat-log" id="hubChatLog">${chatHtml}</div>
          <div class="hub-chat-compose">
            <input id="hubChatInput" class="hub-input" maxlength="200" placeholder="发一句给队友…" />
            <button type="button" class="v2-btn primary" id="hubSendChatBtn">发送</button>
          </div>
        </div>
      </div>
    `;

    const log = $id('hubChatLog');
    if (log) log.scrollTop = log.scrollHeight;

    $id('hubAutoJoinSwitch')?.addEventListener('change', e => {
      S.autoJoinEnabled = e.target.checked;
      post({ type: 'hubSetAutoJoin', enabled: S.autoJoinEnabled });
    });
    $id('hubLeaveRoomBtn')?.addEventListener('click', () => post({ type: 'hubLeaveRoom' }));
    $id('hubReopenBtn')?.addEventListener('click', () => post({ type: 'hubReopenRoom' }));
    $id('hubHostLaunchBtn')?.addEventListener('click', () => post({ type: 'hubHostLaunchGame', mode: 'bypassSteam' }));
    $id('hubTriggerJoinBtn')?.addEventListener('click', () => {
      if (!gr) return;
      post({ type: 'hubTriggerBridgeJoin', gameRoomId: gr.gameRoomId, gamePwd: gr.gamePwd, roomServerId: gr.roomServerId });
    });
    $id('hubCopyGameCodeBtn')?.addEventListener('click', () => {
      if (!gr) return;
      const text = gr.gamePwd ? `${gr.gameRoomId} 密码:${gr.gamePwd}` : String(gr.gameRoomId);
      navigator.clipboard?.writeText(text);
      window.toast?.(`已复制游戏房间信息：${text}`);
    });
    $id('hubRoomSwitchUid')?.addEventListener('change', e => {
      post({ type: 'hubSwitchActiveUid', activeUid: e.target.value });
    });
    $id('hubOpenLatestSettleBtn')?.addEventListener('click', () => {
      if (!room.latestMatch) return;
      S.stage1 = room.latestMatch;
      renderSettlement();
      selectTab('settlement', false);
    });

    const sendChat = () => {
      const inp = $id('hubChatInput');
      const text = inp?.value.trim();
      if (!text) return;
      inp.value = '';
      post({ type: 'hubSendMessage', text });
    };
    $id('hubSendChatBtn')?.addEventListener('click', sendChat);
    $id('hubChatInput')?.addEventListener('keydown', e => { if (e.key === 'Enter') sendChat(); });

    body.querySelectorAll('[data-hub-kick]').forEach(btn => {
      btn.addEventListener('click', () => post({ type: 'hubKickMember', targetUserId: btn.dataset.hubKick }));
    });
    body.querySelectorAll('[data-hub-view-user]').forEach(btn => {
      btn.addEventListener('click', () => {
        post({ type: 'hubViewProfile', userId: btn.dataset.hubViewUser });
        selectTab('profile', false);
      });
    });
  }

  function renderProfile() {
    const body = $id('hubProfileBody');
    if (!body) return;

    const me = S.me;
    const priv = me?.privacy || {};
    const live = (S.bridge.sessions || []).find(s => s.isLoggedIn);

    const accountSection = !me ? `
      <div class="hub-card">
        <div class="hub-card-head"><h3>Microsoft 大厅账号</h3></div>
        <p class="hub-muted">登录后即可创建/加入组队房间、绑定多个游戏 UID，并在下方管理 10 项公开隐私权限。</p>
        <div class="hub-toolbar" style="margin-top:12px;">
          <button type="button" class="v2-btn primary" id="hubProfileSignInBtn">Microsoft 登录</button>
          <button type="button" class="v2-btn ghost" id="hubOpenLocalAccountBtn">打开本机直连我的账号（好友在线 / 凭据管理）</button>
        </div>
      </div>` : `
      <div class="hub-profile-grid">
        <div class="hub-card">
          <div class="hub-card-head">
            <h3>大师名片与 1 对 N 游戏 UID 绑定</h3>
            <button type="button" class="v2-btn ghost" id="hubPreviewSelfBtn">预览他人视角</button>
          </div>
          <div class="v2-field">
            <label for="hubDisplayNameInput">大厅显示昵称</label>
            <div class="hub-toolbar">
              <input id="hubDisplayNameInput" class="hub-input" maxlength="24" value="${esc(me.displayName)}" />
              <button type="button" class="v2-btn primary" id="hubSaveNameBtn">保存昵称</button>
            </div>
          </div>

          <div class="hub-notice-warn" style="margin:12px 0;">
            安全约定：<strong>Toys 永不上传您的 <code>account.json</code> 登录凭据或好友列表</strong>；仅同步公开游戏档案快照与 UID 归属，供队友组队确认。
          </div>

          <div class="hub-binding-list">
            ${(me.bindings || []).length ? me.bindings.map(b => `
              <div class="hub-binding-row">
                <div class="hub-binding-main">
                  ${imgUrl(b.headImage) ? `<img class="hub-avatar" src="${esc(b.headImage)}" alt="" />` : `<div class="hub-avatar">${esc((b.nick || b.uid).slice(0, 1))}</div>`}
                  <div>
                    <strong>${esc(b.nick || '未同步昵称')} ${b.isPrimary ? '<span class="hub-pill pink">主号</span>' : ''} <span class="hub-pill">${esc(regionLabel(b.region))}</span></strong>
                    <div class="hub-muted">UID: ${esc(b.uid)}${b.level ? ` · Lv.${b.level}` : ''}${b.updatedAt ? ` · 更新于 ${formatTime(b.updatedAt)}` : ''}</div>
                  </div>
                </div>
                <div class="hub-toolbar">
                  ${!b.isPrimary ? `<button type="button" class="v2-btn ghost" data-hub-primary="${esc(b.region)}:${esc(b.uid)}">设为主号</button>` : ''}
                  <button type="button" class="v2-btn ghost" data-hub-sync-uid="${esc(b.uid)}">同步本机战绩快照</button>
                  <button type="button" class="v2-btn ghost" data-hub-unbind="${esc(b.region)}:${esc(b.uid)}">解绑</button>
                </div>
              </div>
            `).join('') : '<p class="hub-muted">尚未绑定任何游戏账号。</p>'}
          </div>

          <div class="hub-toolbar" style="margin-top:14px;padding-top:14px;border-top:1px solid var(--v2-border);">
            <select id="hubLocalUidSelect" class="v2-select">
              ${(S.localAccounts || []).length
                ? S.localAccounts.map(a => `<option value="${esc(a.uid)}">${esc(a.uid)} (${esc(regionLabel(a.region))})</option>`).join('')
                : '<option value="">未发现本机凭据（请先在本机直连模块同意授权并导出凭据）</option>'}
            </select>
            <button type="button" class="v2-btn primary" id="hubBindLocalBtn" ${(S.localAccounts || []).length ? '' : 'disabled'}>从本机凭据绑定并同步</button>
            ${live ? `<button type="button" class="v2-btn ghost" id="hubBindBridgeBtn">直接绑定在线游戏角色 (${esc(live.uid)})</button>` : ''}
            <button type="button" class="v2-btn ghost" id="hubOpenLocalAccountBtn">本机直连我的账号</button>
          </div>
        </div>

        <div class="hub-card">
          <div class="hub-card-head">
            <h3>10 项个人主页隐私开关（服务端强制生效）</h3>
            <button type="button" class="v2-btn primary" id="hubSavePrivacyBtn">保存隐私设置</button>
          </div>
          <div class="hub-switch-list">
            ${PRIVACY_ITEMS.map(([key, title, desc]) => `
              <label class="hub-switch-row">
                <span><strong>${esc(title)}</strong><small>${esc(desc)}</small></span>
                <input type="checkbox" switch data-hub-priv="${esc(key)}" ${priv[key] ? 'checked' : ''} />
              </label>
            `).join('')}
          </div>
        </div>
      </div>`;

    const lookupSection = `
      <div class="hub-card">
        <div class="hub-card-head">
          <h3>玩家公开资料查询</h3>
          <div class="hub-toolbar">
            <select id="hubLookupRegion" class="v2-select">
              <option value="cn" ${S.lookupRegion === 'cn' ? 'selected' : ''}>按国服 UID 查</option>
              <option value="int" ${S.lookupRegion === 'int' ? 'selected' : ''}>按国际服 UID 查</option>
              <option value="user" ${S.lookupRegion === 'user' ? 'selected' : ''}>按大厅 UserID 查</option>
            </select>
            <input id="hubLookupInput" class="hub-input" value="${esc(S.lookupInput)}" placeholder="输入游戏 UID 或 UserID" />
            <button type="button" class="v2-btn primary" id="hubLookupBtn">查询公开名片</button>
          </div>
        </div>
        ${renderViewedProfileHtml(S.viewedProfile)}
      </div>`;

    body.innerHTML = accountSection + lookupSection;

    $id('hubProfileSignInBtn')?.addEventListener('click', () => post({ type: 'hubSignIn' }));
    $id('hubOpenLocalAccountBtn')?.addEventListener('click', () => window.showPage?.('account'));
    $id('hubPreviewSelfBtn')?.addEventListener('click', () => me && post({ type: 'hubViewProfile', userId: me.userId }));
    $id('hubSaveNameBtn')?.addEventListener('click', () => {
      post({ type: 'hubUpdateProfile', displayName: $id('hubDisplayNameInput')?.value.trim() });
    });
    $id('hubSavePrivacyBtn')?.addEventListener('click', () => {
      const next = {};
      body.querySelectorAll('[data-hub-priv]').forEach(inp => { next[inp.dataset.hubPriv] = !!inp.checked; });
      post({ type: 'hubUpdatePrivacy', privacy: next });
    });
    $id('hubBindLocalBtn')?.addEventListener('click', () => {
      const uid = $id('hubLocalUidSelect')?.value;
      if (uid) post({ type: 'hubBindLocalAccount', uid, setPrimary: !(me?.bindings?.length), syncSnapshot: true });
    });
    $id('hubBindBridgeBtn')?.addEventListener('click', () => {
      post({ type: 'hubBindBridgeAccount', setPrimary: !(me?.bindings?.length) });
    });

    body.querySelectorAll('[data-hub-primary]').forEach(btn => {
      btn.addEventListener('click', () => {
        const [region, uid] = btn.dataset.hubPrimary.split(':');
        post({ type: 'hubUpdateProfile', primaryRegion: region, primaryUid: uid });
      });
    });
    body.querySelectorAll('[data-hub-sync-uid]').forEach(btn => {
      btn.addEventListener('click', () => post({ type: 'hubBindLocalAccount', uid: btn.dataset.hubSyncUid, setPrimary: false, syncSnapshot: true }));
    });
    body.querySelectorAll('[data-hub-unbind]').forEach(btn => {
      btn.addEventListener('click', () => {
        const [region, uid] = btn.dataset.hubUnbind.split(':');
        post({ type: 'hubUnbindAccount', region, uid });
      });
    });

    $id('hubLookupBtn')?.addEventListener('click', () => {
      S.lookupRegion = $id('hubLookupRegion')?.value || 'cn';
      S.lookupInput = $id('hubLookupInput')?.value.trim() || '';
      if (!S.lookupInput) return;
      if (S.lookupRegion === 'user') post({ type: 'hubViewProfile', userId: S.lookupInput });
      else post({ type: 'hubViewProfileByUid', region: S.lookupRegion, uid: S.lookupInput });
    });
  }

  function renderViewedProfileHtml(p) {
    if (!p) return '<p class="hub-muted">点击房间队友的「查看资料」或在上方输入游戏 UID / UserID 查看经对方隐私设置裁切后的公开名片。</p>';
    if (p.profileHidden) {
      return `<div class="hub-notice-warn"><strong>${esc(p.displayName)}</strong> 已关闭所有公开档案模块或仅允许好友/队内可见。</div>`;
    }

    const bindingsHtml = (p.bindings || []).map(b => {
      const snap = b.snapshot;
      return `
        <div class="hub-card" style="background:var(--v2-panel-2);margin-top:10px;">
          <div class="hub-card-head">
            <div class="hub-binding-main">
              ${imgUrl(b.headImage) ? `<img class="hub-avatar" src="${esc(b.headImage)}" alt="" />` : `<div class="hub-avatar">${esc((b.nick || p.displayName || '?').slice(0, 1))}</div>`}
              <div>
                <strong>${esc(b.nick || p.displayName)} <span class="hub-pill">${esc(regionLabel(b.region))}</span></strong>
                <div class="hub-muted">UID ${esc(b.uid)}${b.level ? ` · Lv.${b.level}` : ''}${snap?.creditScore ? ` · 信誉分 ${snap.creditScore}` : ''}${snap?.praiseNum ? ` · 获赞 ${snap.praiseNum}` : ''}</div>
              </div>
            </div>
            ${snap ? `<span class="hub-pill">快照 ${formatTime(snap.capturedAt)}</span>` : '<span class="hub-pill">暂无战绩快照</span>'}
          </div>
          ${snap ? `
            <div class="hub-toolbar" style="margin-bottom:10px;">
              ${snap.fightCount ? `<span class="hub-pill">总对局 ${snap.fightCount}</span>` : ''}
              ${snap.winCount ? `<span class="hub-pill pink">胜场 ${snap.winCount}</span>` : ''}
              ${snap.roleCardCount ? `<span class="hub-pill">角色 ${snap.roleCardCount}</span>` : ''}
              ${snap.skinCount ? `<span class="hub-pill">皮肤 ${snap.skinCount}</span>` : ''}
              ${(snap.achievements || []).slice(0, 6).map(a => `<span class="hub-pill gold">${esc(a.name)}</span>`).join('')}
            </div>
            ${(snap.characters || []).length ? `
              <div class="hub-muted" style="margin-bottom:6px;">常用角色</div>
              <div class="hub-toolbar">
                ${snap.characters.slice(0, 8).map(c => `<span class="hub-pill">${esc(c.name || `#${c.id}`)} Lv.${c.level}${c.fightCount != null ? ` · ${c.fightCount}场` : ''}</span>`).join('')}
              </div>` : ''}
            ${(snap.recentMatches || []).length ? `
              <div class="hub-muted" style="margin:10px 0 6px;">近期对局 (${snap.recentMatches.length})</div>
              <div class="hub-toolbar">
                ${snap.recentMatches.slice(0, 6).map(m => `<span class="hub-pill">#${m.rank} ${esc(m.character || '')} · ${esc(MODES[m.mapType] || '对局')} · ${formatTime(m.playedAt)}</span>`).join('')}
              </div>` : ''}
          ` : ''}
        </div>`;
    }).join('');

    return `
      <div>
        <div class="hub-toolbar">
          <strong>${esc(p.displayName)}</strong>
          <span class="hub-muted">UserID: ${esc(p.userId)} · 最近活跃 ${formatTime(p.lastSeenAt)}</span>
        </div>
        ${bindingsHtml || '<p class="hub-muted" style="margin-top:10px;">对方隐藏了绑定的游戏 UID 清单。</p>'}
      </div>`;
  }

  function renderSettlement() {
    const body = $id('hubSettlementBody');
    if (!body) return;

    if (!S.stage1) {
      body.innerHTML = `
        <div class="hub-card">
          <h3>暂无本会话对局结算记录</h3>
          <p class="hub-muted">在游戏内安装 <strong>HubBridgeMod</strong> 并完成一局游戏后，对局结束瞬间将自动弹出「Stage 1 即时战报」，并在后台继续轮询官方回放 CDN 补齐「Stage 2 全量深度数据」。</p>
        </div>`;
      return;
    }

    const s1 = S.stage1;
    const s2 = S.stage2;
    const st2Label = S.stage2Status === 'ready'
      ? ['Stage 2 完整战报就绪', 'green']
      : S.stage2Status === 'polling'
        ? ['Stage 2 官方回放拉取中…', 'gold']
        : S.stage2Status === 'failed'
          ? ['Stage 2 暂未就绪', 'danger']
          : ['等待拉取回放', ''];

    const mergedPlayers = (s1.players || []).map(p => {
      const p2 = s2?.players?.find(x => x.playerId === p.playerId);
      const isWin = p.rank === 1;
      const relics = p2?.selectedRelics || [];
      return `
        <article class="hub-settle-player ${isWin ? 'winner' : ''}">
          <div class="hub-member-top">
            ${imgUrl(p2?.heroImage || p.heroImage) ? `<img class="hub-avatar" src="${esc(p2?.heroImage || p.heroImage)}" alt="" />` : `<div class="hub-avatar">${esc(p.rank || '-')}</div>`}
            <div class="hub-member-info">
              <strong>#${esc(p.rank)} ${esc(p.name || p.playerId)} ${p.isBot ? '<span class="hub-pill">AI</span>' : ''}</strong>
              <small>${esc(p2?.heroName || p.heroName)} · Lv.${esc(p.level)} · UID ${esc(p.playerId)}</small>
            </div>
          </div>
          <div class="hub-settle-kpis">
            <div class="hub-kpi"><span>造成伤害</span><strong>${esc(p2?.damage ?? p.totalDamage ?? 0)}</strong></div>
            <div class="hub-kpi"><span>承受伤害</span><strong>${esc(p2?.injured ?? p.totalInjured ?? 0)}</strong></div>
            <div class="hub-kpi"><span>治疗量</span><strong>${esc(p2?.healing ?? p.treatmentScore ?? 0)}</strong></div>
            <div class="hub-kpi"><span>击败 / 倒地</span><strong>${esc(p2?.kills ?? p.killCount ?? 0)} / ${esc(p.totalDie ?? 0)}</strong></div>
            <div class="hub-kpi"><span>结算星币</span><strong>${esc(p2?.gold ?? p.gold ?? 0)}</strong></div>
            <div class="hub-kpi"><span>步数 / 骰均</span><strong>${esc(p2?.movePoints ?? p.movePoint ?? 0)}${p2?.diceCount ? ` · ${p2.avgDice}` : ''}</strong></div>
          </div>
          ${p2 ? `
            <div class="hub-muted">用牌 ${p2.usedCards} 张 · 技能 ${p2.usedSkills} 次 · 购买筹码 ${p2.boughtRelics} 个</div>
            ${relics.length ? `<div class="hub-relic-chips">${relics.map(r => `<span class="hub-relic-chip">${imgUrl(r.icon) ? `<img src="${esc(r.icon)}" alt="" />` : ''}${esc(r.name)}</span>`).join('')}</div>` : ''}
          ` : ''}
        </article>
      `;
    }).join('');

    const stage2Details = s2 ? `
      <div class="hub-card">
        <div class="hub-card-head">
          <h3>Stage 2 官方回放深度拆解</h3>
          <span class="hub-muted">耗时 ${esc(s2.durationText)} · ${esc(s2.roundCount)} 轮 (${esc(s2.turnCount)} 行动) · ${esc(s2.resultText)}</span>
        </div>
        <div class="hub-toolbar">
          ${(s2.actionStats || []).slice(0, 6).map(a => `<span class="hub-pill">${esc(a.name)}: ${a.count} (${esc(a.ratioText)})</span>`).join('')}
        </div>
      </div>` : '';

    body.innerHTML = `
      <div class="hub-settle-hero">
        <div class="hub-settle-map">
          ${imgUrl(s2?.mapImage || s1.mapImage) ? `<img src="${esc(s2?.mapImage || s1.mapImage)}" alt="" />` : ''}
          <div>
            <h3 style="margin:0 0 4px;">${esc(s2?.mapName || s1.mapName || '对局结算')} · ${esc(MODES[s1.mapType] || '标准对局')}</h3>
            <div class="hub-muted">
              ${esc(regionLabel(s1.region))} · 回合 ${esc(s1.round)} · 回放 ID <code style="user-select:text;">${esc(s1.replayId || '无')}</code> · ${formatTime(s1.finishTime)}
            </div>
          </div>
        </div>
        <div class="hub-toolbar">
          <span class="hub-pill ${st2Label[1]}">${esc(st2Label[0])}</span>
          ${s1.replayId && S.stage2Status !== 'ready' ? `<button type="button" class="v2-btn ghost" id="hubRetryStage2Btn">重试拉取 CDN</button>` : ''}
          ${s2 ? `<button type="button" class="v2-btn primary" id="hubOpenFullReplayBtn">在回放分析器中打开完整时间线</button>` : ''}
        </div>
      </div>
      ${S.stage2Message ? `<div class="hub-muted">${esc(S.stage2Message)}</div>` : ''}
      <div class="hub-settle-players">${mergedPlayers}</div>
      ${stage2Details}
    `;

    $id('hubRetryStage2Btn')?.addEventListener('click', () => {
      post({ type: 'hubPollSettlement', region: s1.region, replayId: s1.replayId });
    });
    $id('hubOpenFullReplayBtn')?.addEventListener('click', () => {
      post({ type: 'hubOpenSettlementReplay', filePath: s2?.filePath || '' });
    });
  }

  function renderAll() {
    renderHeader();
    renderLobby();
    renderProfile();
    renderSettlement();
  }

  function receive(msg) {
    if (!msg?.type) return;
    switch (msg.type) {
      case 'hubInitState': {
        const p = msg.payload || {};
        S.serverUrl = p.serverUrl || S.serverUrl;
        S.region = p.activeRegion || S.region;
        S.autoJoinEnabled = p.autoJoinEnabled !== false;
        S.serverConfig = p.serverConfig || null;
        S.auth = p.auth || S.auth;
        S.me = p.me || null;
        S.localAccounts = p.localAccounts || [];
        S.bridge = p.bridge || S.bridge;
        S.initialized = true;
        renderAll();
        break;
      }
      case 'hubAuthState':
        S.auth = msg.payload || S.auth;
        if (!S.auth.signedIn) S.me = null;
        renderHeader();
        renderLobby();
        renderProfile();
        break;
      case 'hubMe':
        S.me = msg.payload?.me || null;
        S.localAccounts = msg.payload?.localAccounts || S.localAccounts;
        renderHeader();
        renderLobby();
        renderProfile();
        break;
      case 'hubViewedProfile':
        S.viewedProfile = msg.payload || null;
        renderProfile();
        break;
      case 'hubBridgeSessions':
        S.bridge = msg.payload || S.bridge;
        renderHeader();
        renderBridgeDiag();
        renderProfile();
        break;
      case 'hubAutoJoinChanged':
        S.autoJoinEnabled = !!msg.payload?.enabled;
        break;
      case 'hubRoomsList':
        S.rooms = msg.payload?.rooms || [];
        renderLobby();
        break;
      case 'hubRoomState':
        S.currentRoom = msg.payload || null;
        renderLobby();
        break;
      case 'hubRoomReset':
        S.roomEvents = [];
        break;
      case 'hubRoomEvents': {
        const incoming = msg.payload?.events || [];
        const known = new Set(S.roomEvents.map(e => e.id));
        for (const ev of incoming) {
          if (!known.has(ev.id)) S.roomEvents.push(ev);
        }
        if (S.roomEvents.length > 150) S.roomEvents.splice(0, S.roomEvents.length - 150);
        if (S.currentRoom) renderLobby();
        break;
      }
      case 'hubRoomLeft':
        S.currentRoom = null;
        S.roomEvents = [];
        renderLobby();
        break;
      case 'hubSettlementStage1':
        S.stage1 = msg.payload;
        S.stage2 = null;
        S.stage2Status = msg.payload?.stage2Status || 'pending';
        S.stage2Message = msg.payload?.stage2Message || '';
        renderSettlement();
        const badge = $id('hubSettlementBadge');
        if (badge && S.tab !== 'settlement') badge.hidden = false;
        window.showPage?.('hub');
        selectTab('settlement', false);
        break;
      case 'hubSettlementStage2Status':
        if (S.stage1 && S.stage1.replayId === msg.payload?.replayId) {
          S.stage2Status = msg.payload.status;
          S.stage2Message = msg.payload.message || '';
          renderSettlement();
        }
        break;
      case 'hubSettlementStage2':
        if (S.stage1 && S.stage1.replayId === msg.payload?.replayId) {
          S.stage2Status = 'ready';
          S.stage2Message = msg.payload.message || '';
          S.stage2 = msg.payload.report || null;
          renderSettlement();
        }
        break;
    }
  }

  window.HubModule = {
    onShow() {
      bindOnce();
      if (!S.initialized) {
        renderAll();
      }
      post({ type: 'hubInit' });
    },
    selectTab,
    receive
  };
})();
