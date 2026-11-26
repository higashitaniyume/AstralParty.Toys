// hub/module.js — 组队大厅（对战平台全局顶栏、Microsoft 头像、弹窗隐私、完整个人账号面板、房主解散）
(function () {
  'use strict';

  const esc = v => String(v ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const $id = id => document.getElementById(id);
  const imgUrl = v => typeof v === 'string' && /^https:\/\/(assets|materials\d+|hubavatars)\.aspatoys\.hiynet\.localhost\//.test(v) ? v : '';

  const MODES = {
    1: '标准', 2: '新手', 3: '无限火力', 4: 'PVE', 5: '战役 PVP', 6: '战役 PVE',
    7: '非对称对抗', 8: '练习 PVP', 9: '练习 PVE', 10: 'PVE 新手', 11: '幸运星争夺战', 12: '词条 PVE'
  };

  const ROOM_STATUS_TEXT = {
    recruiting: '招募中',
    launching: '房主启动中',
    game_ready: '游戏房就绪',
    playing: '对局中',
    finished: '已结束',
    closed: '已关闭'
  };

  const GAME_STATES = {
    idle: ['待命', ''],
    launching: ['启动游戏中…', 'gold'],
    in_lobby: ['游戏主界面', 'green'],
    joining: ['自动进房中…', 'gold'],
    in_game_room: ['已进游戏房', 'green'],
    playing: ['对局中', 'pink'],
    join_failed: ['进房失败', 'danger']
  };

  const FRIEND_STATUS_TEXT = {
    offline: '离线',
    online: '在线',
    busy: '忙碌',
    room: '房间中',
    playing: '对局中',
    unknown: '未知'
  };

  const PRIVACY_ITEMS = [
    ['showLinkedAccounts', '已绑定的游戏 UID 清单', '向他人展示名下绑定的国服 / 国际服 UID'],
    ['showProfileMeta', '账号等级与活跃时间', '展示游戏等级、经验进度、创建与最近登录时间'],
    ['showCreditAndPraise', '信誉分与获赞数', '展示信誉评分与累计获赞次数'],
    ['showSummaryStats', '战绩总览与收藏数量', '展示总场次、胜场/胜率、角色/皮肤/装扮数'],
    ['showAchievements', '成就徽章', '展示已获取的成就标签'],
    ['showWinMaps', '地图胜场分布', '展示各张地图的历史胜场数'],
    ['showCharacters', '角色练度与参战次数', '展示已拥有角色的等级、突破与参战次数'],
    ['showRecentMatches', '近期对局历史', '展示最近 20 场对局摘要'],
    ['showMatchDetails', '同局对局明细', '允许他人展开对局内所有同局玩家详情'],
    ['showLobbyPresence', '公开个人名片与组队状态', '关闭后他人无法查看你的公开名片']
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
    selectedBindingKey: '',
    profileSubTab: 'characters',
    charSearch: '',
    charPlayedOnly: false,
    friendSearch: '',
    friendOnlineOnly: false,
    expandedMatches: {},
    liveByUid: {},
    liveLoadingUid: '',
    viewedProfile: null,
    lookupInput: '',
    lookupRegion: 'cn',
    stage1: null,
    stage2: null,
    stage2Status: 'idle',
    stage2Message: '',
    pendingJoinRoom: null,
    gameMapType: 4,
    inspectState: null,
    authDialogMode: 'login',
    twoFactorTicket: '',
    twoFactorUser: '',
    security: null,
    totpSetup: null,
    recoveryCodes: null
  };

  let _bound = false;
  let _ctxBound = false;
  const _enterOnOpen = new WeakSet();

  function formatTime(ms) {
    if (!ms) return '—';
    const v = typeof ms === 'string' ? Date.parse(ms) : (ms < 1e12 ? ms * 1000 : ms);
    if (!v || Number.isNaN(v)) return '—';
    const d = new Date(v);
    if (Number.isNaN(d.getTime())) return '—';
    const pad = n => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  function regionLabel(r) {
    return r === 'int' ? '国际服 INT' : '国服 CN';
  }

  function renderAvatar(url, fallbackName, size = 40, cls = 'hub-avatar', extraAttrs = '') {
    const safe = imgUrl(url);
    const initial = (fallbackName || '?').trim().slice(0, 1).toUpperCase();
    const attrs = extraAttrs ? ` ${extraAttrs}` : '';
    if (safe) {
      return `<img class="${cls}" src="${esc(safe)}" alt="" width="${size}" height="${size}" style="width:${size}px;height:${size}px;"${attrs} />`;
    }
    return `<span class="${cls}" style="width:${size}px;height:${size}px;font-size:${Math.max(11, Math.round(size * 0.44))}px;" aria-hidden="true"${attrs}>${esc(initial)}</span>`;
  }

  function inspectAttrs(info) {
    const uid = info.activeUid || info.uid || '';
    const name = info.activeNick || info.displayName || uid || '玩家';
    const level = info.activeLevel || info.level || 0;
    const region = info.region || S.region || 'cn';
    const title = `右键查成分 · ${name}${uid ? ` (UID ${uid})` : ''}`;
    return `data-hub-inspect-user="${esc(info.userId || '')}" data-hub-inspect-name="${esc(name)}" data-hub-inspect-uid="${esc(uid)}" data-hub-inspect-avatar="${esc(info.avatarUrl || '')}" data-hub-inspect-level="${esc(level)}" data-hub-inspect-region="${esc(region)}" title="${esc(title)}"`;
  }

  function hideContextMenu() {
    const menu = $id('hubPlayerContextMenu');
    if (!menu) return;
    if (typeof menu.hidePopover === 'function') {
      try { if (menu.matches(':popover-open')) menu.hidePopover(); } catch { }
    }
    menu.classList.remove(':popover-open');
  }

  function renderInspectDialog() {
    const st = S.inspectState;
    const titleEl = $id('hubInspectDialogTitle');
    const bodyEl = $id('hubInspectBody');
    const copyBtn = $id('hubInspectCopyUidBtn');
    const openBtn = $id('hubInspectOpenProfileBtn');
    if (!st || !bodyEl) return;

    if (titleEl) {
      titleEl.textContent = `查成分 · ${st.profile?.displayName || st.name || '玩家档案'}`;
    }

    const uid = st.uid || st.profile?.primaryUid || st.profile?.bindings?.[0]?.uid || '';
    if (copyBtn) {
      copyBtn.hidden = !uid;
      copyBtn.dataset.uid = uid;
    }
    if (openBtn) {
      openBtn.hidden = !st.profile;
    }

    if (st.loading) {
      bodyEl.innerHTML = `<p class="hub-muted" style="padding:20px 0;text-align:center;">正在拉取 ${esc(st.name || '该玩家')} 的公开战绩与档案…</p>`;
      return;
    }

    if (st.error) {
      bodyEl.innerHTML = `<div class="hub-notice-warn">${esc(st.error)}</div>`;
      return;
    }

    bodyEl.innerHTML = renderViewedProfileHtml(st.profile);
  }

  function openInspectDialog(target) {
    if (!target || (!target.userId && !target.uid)) return;
    S.inspectState = {
      loading: true,
      userId: target.userId || '',
      uid: target.uid || '',
      region: target.region || S.region || 'cn',
      name: target.name || target.uid || '玩家',
      profile: null,
      error: ''
    };
    renderInspectDialog();
    const dlg = $id('hubInspectDialog');
    if (dlg && !dlg.open) dlg.showModal();
    post({
      type: 'hubInspectProfile',
      userId: target.userId || '',
      region: target.region || S.region || 'cn',
      uid: target.uid || ''
    });
  }

  function openPlayerContextMenu(el, clientX, clientY) {
    const menu = $id('hubPlayerContextMenu');
    const anchor = $id('hubContextAnchor');
    const head = $id('hubContextMenuHead');
    const list = $id('hubContextMenuList');
    if (!menu || !anchor || !head || !list) return;

    const target = {
      userId: el.dataset.hubInspectUser || '',
      name: el.dataset.hubInspectName || '玩家',
      uid: el.dataset.hubInspectUid || '',
      avatarUrl: el.dataset.hubInspectAvatar || '',
      level: Number(el.dataset.hubInspectLevel || 0),
      region: el.dataset.hubInspectRegion || S.region || 'cn'
    };
    if (!target.userId && !target.uid) return;

    const metaParts = [];
    if (target.uid) metaParts.push(`UID ${target.uid}`);
    if (target.level > 0) metaParts.push(`Lv.${target.level}`);
    head.innerHTML = `
      ${renderAvatar(target.avatarUrl, target.name, 28, 'hub-ms-avatar')}
      <div class="hub-context-menu-head-text">
        <strong>${esc(target.name)}</strong>
        <small>${esc(metaParts.join(' · ') || regionLabel(target.region))}</small>
      </div>
    `;

    list.innerHTML = `
      <button type="button" role="menuitem" class="hub-context-menu-item" data-ctx-act="inspect">
        <span>查成分（战绩与档案）</span>
        <small>弹窗</small>
      </button>
      ${target.uid ? `
        <button type="button" role="menuitem" class="hub-context-menu-item" data-ctx-act="copy-uid">
          <span>复制游戏 UID</span>
          <small>${esc(target.uid)}</small>
        </button>` : ''}
      ${target.userId ? `
        <button type="button" role="menuitem" class="hub-context-menu-item" data-ctx-act="open-profile">
          <span>在个人主页打开</span>
        </button>` : ''}
    `;

    list.querySelector('[data-ctx-act="inspect"]')?.addEventListener('click', () => {
      hideContextMenu();
      openInspectDialog(target);
    });
    list.querySelector('[data-ctx-act="copy-uid"]')?.addEventListener('click', () => {
      hideContextMenu();
      navigator.clipboard?.writeText(target.uid);
      window.toast?.(`已复制游戏 UID：${target.uid}`);
    });
    list.querySelector('[data-ctx-act="open-profile"]')?.addEventListener('click', () => {
      hideContextMenu();
      window.showPage?.('hub');
      selectTab('profile', false);
      post({ type: 'hubViewProfile', userId: target.userId });
    });

    const hasAnchor = typeof CSS !== 'undefined' && typeof CSS.supports === 'function' && CSS.supports('anchor-name: --a');
    if (hasAnchor) {
      anchor.style.left = `${clientX}px`;
      anchor.style.top = `${clientY}px`;
      menu.style.removeProperty('left');
      menu.style.removeProperty('top');
      menu.style.removeProperty('right');
    } else {
      const clampedX = Math.max(8, Math.min(clientX, window.innerWidth - 250));
      const clampedY = Math.max(8, Math.min(clientY, window.innerHeight - 170));
      menu.style.removeProperty('right');
      menu.style.left = `${clampedX}px`;
      menu.style.top = `${clampedY}px`;
    }

    _enterOnOpen.add(menu);
    if (typeof menu.showPopover === 'function') {
      try {
        if (menu.matches(':popover-open')) menu.hidePopover();
        if (hasAnchor) menu.showPopover({ source: anchor });
        else menu.showPopover();
      } catch {
        try { menu.showPopover(); } catch { menu.classList.add(':popover-open'); }
      }
    } else {
      menu.classList.add(':popover-open');
      list.querySelector('button:not(:disabled)')?.focus();
    }
  }

  function bindContextMenuOnce() {
    if (_ctxBound) return;
    _ctxBound = true;

    const menu = $id('hubPlayerContextMenu');
    const list = $id('hubContextMenuList');

    document.addEventListener('contextmenu', e => {
      const target = e.target?.closest?.('[data-hub-inspect-user]');
      if (!target) return;
      e.preventDefault();
      // 键盘 ContextMenu / Shift+F10 触发时（无鼠标坐标）以元素左下角为锚点打开
      if (e.clientX === 0 && e.clientY === 0) {
        const rect = target.getBoundingClientRect();
        openPlayerContextMenu(target, rect.left + rect.width / 2, rect.bottom);
      }
    });

    document.addEventListener('pointerup', e => {
      if (!(e.button === 2 || (e.button === 0 && e.ctrlKey))) return;
      const target = e.target?.closest?.('[data-hub-inspect-user]');
      if (!target) return;
      openPlayerContextMenu(target, e.clientX, e.clientY);
    });

    menu?.addEventListener('toggle', e => {
      if (e.newState === 'open' && _enterOnOpen.has(menu)) {
        _enterOnOpen.delete(menu);
        list?.querySelector('button:not(:disabled)')?.focus();
      }
    });

    if (list && !Object.hasOwn(HTMLElement.prototype, 'focusGroup')) {
      list.addEventListener('keydown', e => {
        const items = Array.from(list.querySelectorAll('button:not(:disabled)'));
        if (!items.length) return;
        const idx = items.indexOf(document.activeElement);
        if (e.key === 'ArrowDown') {
          e.preventDefault();
          items[(idx + 1 + items.length) % items.length].focus();
        } else if (e.key === 'ArrowUp') {
          e.preventDefault();
          items[(idx - 1 + items.length) % items.length].focus();
        } else if (e.key === 'Home') {
          e.preventDefault();
          items[0].focus();
        } else if (e.key === 'End') {
          e.preventDefault();
          items[items.length - 1].focus();
        } else if (e.key === 'Escape') {
          hideContextMenu();
        }
      });
    }

    if (!Object.hasOwn(HTMLElement.prototype, 'popover')) {
      document.addEventListener('pointerdown', e => {
        if (menu && menu.classList.contains(':popover-open') && !menu.contains(e.target)) {
          hideContextMenu();
        }
      });
    }

    $id('hubInspectCopyUidBtn')?.addEventListener('click', e => {
      const uid = e.currentTarget?.dataset?.uid;
      if (!uid) return;
      navigator.clipboard?.writeText(uid);
      window.toast?.(`已复制游戏 UID：${uid}`);
    });
    $id('hubInspectOpenProfileBtn')?.addEventListener('click', () => {
      const profile = S.inspectState?.profile;
      if (!profile) return;
      $id('hubInspectDialog')?.close('open-profile');
      S.viewedProfile = profile;
      window.showPage?.('hub');
      selectTab('profile', false);
      renderProfile();
    });
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
      for (const dlg of document.querySelectorAll('dialog.hub-dialog[closedby="any"]')) {
        let downOut = false;
        dlg.addEventListener('pointerdown', e => { downOut = outside(e, dlg); });
        dlg.addEventListener('pointerup', e => { if (downOut && outside(e, dlg)) dlg.close('cancel'); });
      }
    }
    for (const dlg of document.querySelectorAll('dialog.hub-dialog')) {
      dlg.addEventListener('close', () => { dlg.returnValue = ''; });
    }
  }

  function syncRoomTabVisibility() {
    const tabRoom = $id('hubTabRoom');
    const badge = $id('hubCurrentRoomBadge');
    if (!tabRoom) return;
    const hasRoom = !!S.currentRoom;
    tabRoom.hidden = !hasRoom;
    if (hasRoom) {
      tabRoom.removeAttribute('focusgroup');
      if (badge) badge.textContent = `${S.currentRoom.members.length}/${S.currentRoom.capacity}`;
    } else {
      tabRoom.setAttribute('focusgroup', 'none');
      if (S.tab === 'room') selectTab('lobby', false);
    }
  }

  function selectTab(nextTab, focusTab = false) {
    if (nextTab === 'room' && !S.currentRoom) nextTab = 'lobby';
    S.tab = nextTab;
    const tabs = Array.from(document.querySelectorAll('#hubView .hub-tablist [role="tab"]'));
    const panels = {
      lobby: $id('hubPanelLobby'),
      room: $id('hubPanelRoom'),
      profile: $id('hubPanelProfile'),
      settlement: $id('hubPanelSettlement')
    };

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

  function setAuthDialogMode(mode) {
    S.authDialogMode = mode === 'register' ? 'register' : 'login';
    const isReg = S.authDialogMode === 'register';
    const title = $id('hubAuthDialogTitle');
    if (title) title.textContent = isReg ? '注册组队大厅账号' : '登录组队大厅';
    document.querySelectorAll('#hubAuthModeTabs [data-auth-mode]').forEach(btn => {
      const active = btn.dataset.authMode === S.authDialogMode;
      btn.classList.toggle('is-active', active);
      btn.setAttribute('aria-selected', active ? 'true' : 'false');
    });
    const pwdInput = $id('hubAuthPassword');
    if (pwdInput) pwdInput.setAttribute('autocomplete', isReg ? 'new-password' : 'current-password');
    const confirmWrap = $id('hubAuthConfirmWrap');
    if (confirmWrap) confirmWrap.hidden = !isReg;
    const warn = $id('hubAuthRegisterWarn');
    if (warn) warn.hidden = !isReg;
    const submitBtn = $id('hubAuthSubmitBtn');
    if (submitBtn) submitBtn.textContent = isReg ? '注册并登录' : '登录';
    const err = $id('hubAuthErr');
    if (err) err.textContent = '';
  }

  function showAuthCredPane() {
    S.twoFactorTicket = '';
    S.twoFactorUser = '';
    const tabs = $id('hubAuthModeTabs');
    const cred = $id('hubAuthCredPane');
    const twofa = $id('hubAuth2faPane');
    if (tabs) tabs.hidden = false;
    if (cred) cred.hidden = false;
    if (twofa) twofa.hidden = true;
    setAuthDialogMode(S.authDialogMode);
  }

  function showAuthTwoFactorStep(ticket, username) {
    S.twoFactorTicket = ticket || '';
    S.twoFactorUser = username || '';
    const title = $id('hubAuthDialogTitle');
    const tabs = $id('hubAuthModeTabs');
    const cred = $id('hubAuthCredPane');
    const twofa = $id('hubAuth2faPane');
    const userEl = $id('hubAuth2faUser');
    const codeEl = $id('hubAuth2faCode');
    const recEl = $id('hubAuth2faIsRecovery');
    const err = $id('hubAuthErr');
    if (title) title.textContent = '二次验证（TOTP）';
    if (tabs) tabs.hidden = true;
    if (cred) cred.hidden = true;
    if (twofa) twofa.hidden = false;
    if (userEl) userEl.textContent = S.twoFactorUser;
    if (recEl) recEl.checked = false;
    if (codeEl) {
      codeEl.value = '';
      codeEl.placeholder = '输入 6 位数字';
      codeEl.setAttribute('inputmode', 'numeric');
    }
    if (err) err.textContent = '';
    const dlg = $id('hubAuthDialog');
    if (dlg && !dlg.open) dlg.showModal();
    codeEl?.focus();
  }

  function openAuthDialog(mode = 'login') {
    bindOnce();
    S.authDialogMode = mode === 'register' ? 'register' : 'login';
    showAuthCredPane();
    const oauthSec = $id('hubAuthOAuthSection');
    if (oauthSec) oauthSec.hidden = S.serverConfig ? !S.serverConfig.microsoftAuthEnabled : false;
    const uEl = $id('hubAuthUsername');
    const pEl = $id('hubAuthPassword');
    const cEl = $id('hubAuthConfirmPassword');
    const showEl = $id('hubAuthShowPwd');
    if (pEl) { pEl.value = ''; pEl.type = 'password'; }
    if (cEl) { cEl.value = ''; cEl.type = 'password'; }
    if (showEl) showEl.checked = false;
    const dlg = $id('hubAuthDialog');
    if (dlg && !dlg.open) dlg.showModal();
    uEl?.focus();
  }

  function openSecurityDialog() {
    bindOnce();
    if (!S.me) return;
    S.totpSetup = null;
    S.recoveryCodes = null;
    const body = $id('hubSecurityBody');
    if (body) body.innerHTML = '<p class="hub-muted" style="text-align:center;padding:24px 0;">正在加载账号安全状态…</p>';
    const dlg = $id('hubSecurityDialog');
    if (dlg && !dlg.open) dlg.showModal();
    post({ type: 'hubLoadSecurity' });
  }

  function renderSecurityDialog() {
    const body = $id('hubSecurityBody');
    if (!body) return;
    const sec = S.security;
    if (!sec) {
      body.innerHTML = '<p class="hub-muted" style="text-align:center;padding:24px 0;">正在加载账号安全状态…</p>';
      return;
    }

    const msLinked = (sec.linkedProviders || []).some(p => p.provider === 'microsoft');
    const msConfigured = S.serverConfig ? !!S.serverConfig.microsoftAuthEnabled : true;
    const canUnlinkMs = sec.hasPassword || (sec.linkedProviders || []).length > 1;
    const isOAuthRecovery = !!sec.canResetPasswordWithoutCurrent && sec.hasPassword;

    const recoveryBlock = (S.recoveryCodes && S.recoveryCodes.length) ? `
      <div class="hub-notice-warn" style="margin-top:12px;">
        <strong>请立即保存以下 8 个一次性备用恢复码（关闭后不再显示）：</strong>
        <div class="hub-recovery-grid">
          ${S.recoveryCodes.map(c => `<div class="hub-recovery-code">${esc(c)}</div>`).join('')}
        </div>
        <div class="hub-toolbar" style="margin-top:10px;">
          <button type="button" class="v2-btn primary" id="hubCopyRecoveryBtn">一键复制全部恢复码</button>
        </div>
      </div>` : '';

    const totpSetupBlock = (!sec.twoFactorEnabled && S.totpSetup) ? `
      <div class="hub-totp-setup-box">
        <div class="hub-totp-qr">${S.totpSetup.qrSvg || ''}</div>
        <div>
          <p class="hub-muted" style="margin-bottom:8px;">请使用 Microsoft Authenticator / Google Authenticator / Bitwarden 等验证器扫描左侧二维码，或手动输入密钥：</p>
          <div style="display:flex;align-items:center;gap:8px;flex-wrap:wrap;margin-bottom:10px;">
            <code class="hub-totp-secret">${esc(S.totpSetup.formattedKey || S.totpSetup.sharedKey)}</code>
            <button type="button" class="v2-btn ghost" id="hubCopyTotpKeyBtn">复制密钥</button>
          </div>
          <div class="hub-toolbar">
            <input id="hubTotpEnableCode" class="hub-input" style="min-width:140px;width:160px;" inputmode="numeric" maxlength="10" autocomplete="one-time-code" placeholder="输入 6 位动态码" />
            <button type="button" class="v2-btn primary" id="hubTotpEnableConfirmBtn">验证并开启</button>
          </div>
        </div>
      </div>` : '';

    body.innerHTML = `
      <div class="hub-sec-grid">
        <section class="hub-sec-card">
          <h3>账号与密码 ${sec.hasPassword ? '<span class="hub-pill green">已设置</span>' : '<span class="hub-pill">未设置</span>'}</h3>
          <p class="hub-muted">${sec.username ? `当前登录用户名：<strong>${esc(sec.username)}</strong>` : '绑定用户名与密码后，无需第三方登录即可直接登录大厅。'}</p>
          ${isOAuthRecovery ? '<p class="hub-muted" style="color:#4ade80;margin-top:4px;">当前为 Microsoft 恢复会话，忘记旧密码时无需输入原密码即可直接重置。</p>' : ''}
          <div class="hub-dialog-row" style="margin-top:12px;">
            ${!sec.username ? `
              <div class="v2-field">
                <label for="hubSecUsername">设置用户名</label>
                <input id="hubSecUsername" name="username" type="text" maxlength="24" autocomplete="username" spellcheck="false" autocorrect="off" autocapitalize="none" placeholder="3–24 位字母、数字或 _ . -" />
              </div>` : ''}
            ${sec.hasPassword && !sec.canResetPasswordWithoutCurrent ? `
              <div class="v2-field">
                <label for="hubSecCurPwd">当前密码</label>
                <input id="hubSecCurPwd" type="password" maxlength="128" autocomplete="current-password" placeholder="输入当前密码" />
              </div>` : ''}
            <div class="v2-field">
              <label for="hubSecNewPwd">${sec.hasPassword ? '新密码' : '设置密码'}</label>
              <input id="hubSecNewPwd" type="password" maxlength="128" autocomplete="new-password" placeholder="至少 8 位密码" />
            </div>
            <div class="v2-field">
              <label for="hubSecConfirmPwd">确认新密码</label>
              <input id="hubSecConfirmPwd" type="password" maxlength="128" autocomplete="new-password" placeholder="再次输入新密码" />
            </div>
          </div>
          <div class="hub-toolbar" style="justify-content:space-between;">
            <label class="hub-auth-recovery-toggle" style="margin:0;">
              <input id="hubSecRevokeOthers" type="checkbox" />
              <span>修改后注销其它设备会话</span>
            </label>
            <button type="button" class="v2-btn primary" id="hubSecSavePwdBtn">${sec.hasPassword ? (isOAuthRecovery ? '重置密码' : '修改密码') : '绑定用户名和密码'}</button>
          </div>
          <p id="hubSecPwdErr" class="v2-err" style="margin-top:6px;"></p>
        </section>

        <section class="hub-sec-card">
          <h3>第三方登录渠道</h3>
          <p class="hub-muted">绑定 Microsoft 后，忘记密码时可通过 Microsoft 登录并直接重置密码。</p>
          <div class="hub-sec-provider-row">
            <div>
              <strong>Microsoft</strong>
              <div class="hub-muted">${msLinked ? '已绑定（可用于一键登录与忘记密码恢复）' : '未绑定'}</div>
            </div>
            <div>
              ${msLinked
                ? `<button type="button" class="v2-btn ghost" id="hubSecUnlinkMsBtn" ${canUnlinkMs ? '' : 'disabled title="至少保留一种登录方式，请先设置账号密码"'}>解绑</button>`
                : `<button type="button" class="v2-btn primary" id="hubSecLinkMsBtn" ${msConfigured ? '' : 'disabled'}>绑定 Microsoft</button>`}
            </div>
          </div>
          <div class="hub-sec-provider-row" style="opacity:.68;">
            <div>
              <strong>Google / GitHub</strong>
              <div class="hub-muted">多渠道扩展已预留，将在下一阶段接入</div>
            </div>
            <span class="hub-pill">即将开放</span>
          </div>
        </section>

        <section class="hub-sec-card">
          <h3>二次验证（TOTP 验证器） ${sec.twoFactorEnabled ? '<span class="hub-pill green">已开启</span>' : '<span class="hub-pill">未开启</span>'}</h3>
          <p class="hub-muted">${sec.twoFactorEnabled
            ? `账号密码登录需要 6 位动态验证码保护；当前剩余 <strong>${sec.recoveryCodesLeft}</strong> 个一次性备用恢复码。`
            : '开启后使用账号密码登录需输入验证器 App 的 6 位动态码，显著提升账号安全性。'}</p>
          ${!sec.twoFactorEnabled ? `
            <div class="hub-toolbar" style="margin-top:10px;">
              <button type="button" class="v2-btn primary" id="hubTotpStartSetupBtn">${S.totpSetup ? '刷新二维码' : '开启二次验证'}</button>
            </div>
            ${totpSetupBlock}
          ` : `
            <div class="hub-toolbar" style="margin-top:10px;">
              ${sec.sessionAuthMethod === 'password'
                ? '<input id="hubTotpManageCode" class="hub-input" style="min-width:160px;width:180px;" inputmode="numeric" maxlength="20" placeholder="输入 6 位验证码确认" />'
                : '<span class="hub-pill green">Microsoft 恢复会话免码操作</span>'}
              <button type="button" class="v2-btn ghost" id="hubTotpRegenBtn">重新生成恢复码</button>
              <button type="button" class="v2-btn hub-btn-danger" id="hubTotpDisableBtn">关闭二次验证</button>
            </div>
          `}
          ${recoveryBlock}
        </section>

        <section class="hub-sec-card">
          <div class="hub-toolbar" style="justify-content:space-between;">
            <div>
              <h3 style="margin-bottom:4px;">登录设备会话</h3>
              <p class="hub-muted">当前共有 <strong>${sec.activeSessionsCount}</strong> 个活跃登录会话。</p>
            </div>
            <button type="button" class="v2-btn ghost" id="hubSecRevokeSessionsBtn" ${sec.activeSessionsCount <= 1 ? 'disabled' : ''}>注销其它设备</button>
          </div>
        </section>
      </div>
    `;

    $id('hubSecSavePwdBtn')?.addEventListener('click', () => {
      const err = $id('hubSecPwdErr');
      const username = $id('hubSecUsername')?.value.trim() || null;
      const currentPassword = $id('hubSecCurPwd')?.value || null;
      const newPassword = $id('hubSecNewPwd')?.value || '';
      const confirmPassword = $id('hubSecConfirmPwd')?.value || '';
      const revokeOtherSessions = !!$id('hubSecRevokeOthers')?.checked;
      if (!sec.username && (!username || !/^[a-zA-Z0-9_.-]{3,24}$/.test(username))) {
        if (err) err.textContent = '用户名需为 3–24 位字母、数字或 _ . -';
        return;
      }
      if (newPassword.length < 8) {
        if (err) err.textContent = '新密码长度至少为 8 位';
        return;
      }
      if (newPassword !== confirmPassword) {
        if (err) err.textContent = '两次输入的新密码不一致';
        return;
      }
      if (err) err.textContent = '';
      post({ type: 'hubSavePassword', username, currentPassword, newPassword, revokeOtherSessions });
    });

    $id('hubSecLinkMsBtn')?.addEventListener('click', () => post({ type: 'hubLinkMicrosoft' }));
    $id('hubSecUnlinkMsBtn')?.addEventListener('click', () => post({ type: 'hubUnlinkProvider', provider: 'microsoft' }));
    $id('hubTotpStartSetupBtn')?.addEventListener('click', () => post({ type: 'hubTotpSetup', reset: !!S.totpSetup }));
    $id('hubCopyTotpKeyBtn')?.addEventListener('click', () => {
      if (!S.totpSetup?.sharedKey) return;
      navigator.clipboard?.writeText(S.totpSetup.sharedKey);
      window.toast?.('已复制 TOTP 密钥');
    });
    $id('hubTotpEnableConfirmBtn')?.addEventListener('click', () => {
      const code = $id('hubTotpEnableCode')?.value.trim() || '';
      if (!code) { window.toast?.('请输入验证器里的 6 位动态码'); return; }
      post({ type: 'hubTotpEnable', code });
    });
    $id('hubTotpRegenBtn')?.addEventListener('click', () => {
      const code = $id('hubTotpManageCode')?.value.trim() || null;
      post({ type: 'hubTotpRegenerateCodes', code });
    });
    $id('hubTotpDisableBtn')?.addEventListener('click', () => {
      const code = $id('hubTotpManageCode')?.value.trim() || null;
      post({ type: 'hubTotpDisable', code, isRecoveryCode: false });
    });
    $id('hubCopyRecoveryBtn')?.addEventListener('click', () => {
      if (!S.recoveryCodes?.length) return;
      navigator.clipboard?.writeText(S.recoveryCodes.join('\n'));
      window.toast?.('已复制全部 8 个备用恢复码，请妥善保存');
    });
    $id('hubSecRevokeSessionsBtn')?.addEventListener('click', () => post({ type: 'hubRevokeOtherSessions' }));
  }

  function openPrivacyDialog() {
    if (!S.me) return;
    const priv = S.me.privacy || {};
    const list = $id('hubPrivacySwitchList');
    if (!list) return;
    list.innerHTML = PRIVACY_ITEMS.map(([key, title, desc]) => `
      <label class="hub-switch-row">
        <span><strong>${esc(title)}</strong><small>${esc(desc)}</small></span>
        <input type="checkbox" switch data-hub-priv="${esc(key)}" ${priv[key] ? 'checked' : ''} />
      </label>
    `).join('');
    $id('hubPrivacyDialog')?.showModal();
  }

  function openDisbandDialog() {
    if (!S.currentRoom) return;
    const desc = $id('hubDisbandDesc');
    if (desc) {
      desc.textContent = `确认解散房间「${S.currentRoom.title}」？当前房间内共 ${S.currentRoom.members.length} 名成员，解散后所有成员将立即离开。`;
    }
    $id('hubDisbandDialog')?.showModal();
  }

  function bindOnce() {
    if (_bound) return;
    _bound = true;
    applyPolyfills();

    const tablist = document.querySelector('#hubView .hub-tablist');
    const getVisibleTabs = () => Array.from(document.querySelectorAll('#hubView .hub-tablist [role="tab"]')).filter(t => !t.hidden);
    document.querySelectorAll('#hubView .hub-tablist [role="tab"]').forEach(btn => {
      btn.addEventListener('click', () => selectTab(btn.dataset.hubTab, false));
    });

    if (tablist && !Object.hasOwn(HTMLElement.prototype, 'focusGroup')) {
      tablist.addEventListener('keydown', e => {
        const tabs = getVisibleTabs();
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

    $id('hubSavePrivacyBtn')?.addEventListener('click', () => {
      const next = {};
      $id('hubPrivacySwitchList')?.querySelectorAll('[data-hub-priv]').forEach(inp => {
        next[inp.dataset.hubPriv] = !!inp.checked;
      });
      $id('hubPrivacyDialog')?.close('save');
      post({ type: 'hubUpdatePrivacy', privacy: next });
    });

    $id('hubConfirmDisbandBtn')?.addEventListener('click', () => {
      $id('hubDisbandDialog')?.close('disband');
      post({ type: 'hubDisbandRoom' });
    });

    document.querySelectorAll('#hubAuthModeTabs [data-auth-mode]').forEach(btn => {
      btn.addEventListener('click', () => setAuthDialogMode(btn.dataset.authMode));
    });

    const submitAuthCreds = () => {
      const err = $id('hubAuthErr');
      const username = $id('hubAuthUsername')?.value.trim() || '';
      const password = $id('hubAuthPassword')?.value || '';
      if (!/^[a-zA-Z0-9_.-]{3,24}$/.test(username)) {
        if (err) err.textContent = '用户名需为 3–24 位字母、数字或 _ . -';
        return;
      }
      if (password.length < 8) {
        if (err) err.textContent = '密码长度至少为 8 位';
        return;
      }
      if (S.authDialogMode === 'register') {
        const confirmPwd = $id('hubAuthConfirmPassword')?.value || '';
        if (password !== confirmPwd) {
          if (err) err.textContent = '两次输入的密码不一致';
          return;
        }
        if (err) err.textContent = '';
        post({ type: 'hubRegisterPassword', username, password });
      } else {
        if (err) err.textContent = '';
        post({ type: 'hubLoginPassword', username, password });
      }
    };

    $id('hubAuthSubmitBtn')?.addEventListener('click', submitAuthCreds);
    $id('hubAuthShowPwd')?.addEventListener('change', e => {
      const nextType = e.target.checked ? 'text' : 'password';
      const pEl = $id('hubAuthPassword');
      const cEl = $id('hubAuthConfirmPassword');
      if (pEl) pEl.type = nextType;
      if (cEl) cEl.type = nextType;
    });
    $id('hubAuthPassword')?.addEventListener('keydown', e => { if (e.key === 'Enter' && S.authDialogMode === 'login') { e.preventDefault(); submitAuthCreds(); } });
    $id('hubAuthConfirmPassword')?.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); submitAuthCreds(); } });
    $id('hubAuthMsBtn')?.addEventListener('click', () => {
      $id('hubAuthDialog')?.close('oauth');
      post({ type: 'hubSignIn' });
    });
    $id('hubAuth2faIsRecovery')?.addEventListener('change', e => {
      const codeEl = $id('hubAuth2faCode');
      const lbl = $id('hubAuth2faLabel');
      if (lbl) lbl.textContent = e.target.checked ? '一次性备用恢复码' : '6 位动态验证码';
      if (codeEl) {
        codeEl.placeholder = e.target.checked ? '输入备用恢复码' : '输入 6 位数字';
        codeEl.setAttribute('inputmode', e.target.checked ? 'text' : 'numeric');
        codeEl.focus();
      }
    });
    const submit2fa = () => {
      const code = $id('hubAuth2faCode')?.value.trim() || '';
      const isRecoveryCode = !!$id('hubAuth2faIsRecovery')?.checked;
      const err = $id('hubAuthErr');
      if (!code) {
        if (err) err.textContent = isRecoveryCode ? '请输入备用恢复码' : '请输入 6 位动态验证码';
        return;
      }
      if (err) err.textContent = '';
      post({ type: 'hubCompleteTwoFactor', twoFactorTicket: S.twoFactorTicket, code, isRecoveryCode });
    };
    $id('hubAuth2faSubmitBtn')?.addEventListener('click', submit2fa);
    $id('hubAuth2faCode')?.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); submit2fa(); } });
    $id('hubAuth2faBackBtn')?.addEventListener('click', showAuthCredPane);

    $id('hubCreateSubmitBtn')?.addEventListener('click', () => {
      const title = $id('hubCreateTitle').value.trim();
      if (!title) {
        $id('hubCreateErr').textContent = '请填写房间标题';
        return;
      }
      $id('hubCreateErr').textContent = '';
      S.gameMapType = Number($id('hubCreateGameMode')?.value || 4) === 1 ? 1 : 4;
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

  function hasLiveGameForRoom(room) {
    if (!room) return false;
    const selfUid = room.members?.find(m => m.self)?.activeUid || '';
    return (S.bridge.sessions || []).some(s =>
      s.isLoggedIn &&
      (!room.region || !s.region || s.region === room.region) &&
      (!selfUid || !s.uid || String(s.uid) === String(selfUid)));
  }

  function copyGameRoomInfo(gr) {
    if (!gr || !(gr.gameRoomId > 0)) return;
    const text = gr.gamePwd ? `${gr.gameRoomId} 密码:${gr.gamePwd}` : String(gr.gameRoomId);
    navigator.clipboard?.writeText(text);
    window.toast?.(`已复制游戏房间信息：${text}`);
  }

  // 全局顶部车队状态条：在任何页面都能看到房间人数、成员头像、游戏房号与密码、一键开房/进房与房主解散
  function renderGlobalRoomBar() {
    const bar = $id('hubGlobalRoomBar');
    if (!bar) return;
    const room = S.currentRoom;
    if (!room) {
      bar.hidden = true;
      bar.innerHTML = '';
      return;
    }

    const isHost = S.me && room.hostUserId === S.me.userId;
    const gr = room.gameRoom;
    const hasGameRoom = !!(gr && gr.gameRoomId > 0);
    const liveGame = hasLiveGameForRoom(room);
    const modeTag = S.gameMapType === 1 ? 'PVP' : 'PVE';
    const statusTxt = ROOM_STATUS_TEXT[room.status] || room.status;
    const avatarsHtml = (room.members || []).slice(0, 4).map(m => {
      const safe = imgUrl(m.avatarUrl);
      const name = m.activeNick || m.displayName || m.activeUid || '?';
      const initial = name.slice(0, 1).toUpperCase();
      const attrs = inspectAttrs({ ...m, region: room.region });
      return safe
        ? `<img class="hub-bar-avatar hub-inspectable-avatar" src="${esc(safe)}" alt="" width="24" height="24" ${attrs} />`
        : `<span class="hub-bar-avatar hub-inspectable-avatar" ${attrs}>${esc(initial)}</span>`;
    }).join('');

    bar.hidden = false;
    bar.innerHTML = `
      <div class="hub-global-bar-left">
        <span class="hub-dot online"></span>
        <span class="hub-pill">${esc(regionLabel(room.region))}</span>
        <span class="hub-global-bar-title" title="${esc(room.title)}">${esc(room.title)}</span>
        <span class="hub-pill pink">已进 ${room.members.length} / ${room.capacity} 人</span>
        <span class="hub-bar-avatars">${avatarsHtml}</span>
        ${hasGameRoom
          ? `<span class="hub-bar-code-group">
               <span class="hub-pill green hub-bar-code-pill">房号 <strong>${esc(gr.gameRoomId)}</strong></span>
               <span class="hub-pill gold hub-bar-code-pill">密码 <strong>${esc(gr.gamePwd || '无')}</strong></span>
               <button type="button" class="v2-btn ghost hub-bar-copy-btn" data-bar-act="copy-code">复制</button>
             </span>`
          : `<span class="hub-pill">${esc(statusTxt)}</span>`}
      </div>
      <div class="hub-global-bar-right">
        <button type="button" class="v2-btn ghost hub-bar-btn" data-bar-act="return">回到房间</button>
        ${!liveGame ? `<button type="button" class="v2-btn ghost hub-bar-btn" data-bar-act="launch-game" title="给 Steam / TapTap 用户快捷启动游戏；其他平台请自行启动">启动游戏</button>` : ''}
        ${isHost
          ? (!hasGameRoom ? `<button type="button" class="v2-btn primary hub-bar-btn" data-bar-act="create-game-room">自动建 ${modeTag} 房</button>` : '')
          : (hasGameRoom ? `<button type="button" class="v2-btn primary hub-bar-btn" data-bar-act="join-game">一键进房</button>` : '')}
        ${isHost
          ? `<button type="button" class="v2-btn hub-btn-danger hub-bar-btn" data-bar-act="disband">解散房间</button>`
          : `<button type="button" class="v2-btn ghost hub-bar-btn" data-bar-act="leave">退出</button>`}
      </div>
    `;

    bar.querySelector('[data-bar-act="copy-code"]')?.addEventListener('click', () => copyGameRoomInfo(gr));
    bar.querySelector('[data-bar-act="return"]')?.addEventListener('click', () => {
      window.showPage?.('hub');
      selectTab('room', false);
    });
    bar.querySelector('[data-bar-act="launch-game"]')?.addEventListener('click', () => {
      post({ type: 'hubLaunchGameOnly', mode: 'bypassSteam' });
    });
    bar.querySelector('[data-bar-act="create-game-room"]')?.addEventListener('click', () => {
      post({ type: 'hubHostCreateGameRoom', mapType: S.gameMapType });
    });
    bar.querySelector('[data-bar-act="join-game"]')?.addEventListener('click', () => {
      if (!gr) return;
      post({ type: 'hubTriggerBridgeJoin', gameRoomId: gr.gameRoomId, gamePwd: gr.gamePwd, roomServerId: gr.roomServerId });
    });
    bar.querySelector('[data-bar-act="disband"]')?.addEventListener('click', () => {
      bindOnce();
      openDisbandDialog();
    });
    bar.querySelector('[data-bar-act="leave"]')?.addEventListener('click', () => {
      post({ type: 'hubLeaveRoom' });
    });
  }

  function renderBridgeDiag() {
    const box = $id('hubBridgeDiagBox');
    if (!box) return;
    const b = S.bridge;
    const liveList = (b.sessions || []).filter(s => s.isLoggedIn);
    const liveHtml = liveList.length
      ? liveList.map(live =>
        `<div>· ${regionLabel(live.region)} · UID ${esc(live.uid)} · ${esc(live.scene || 'Home')}${live.gameRoomId > 0 ? ` · 游戏房 ${live.gameRoomId}` : ''}</div>`
      ).join('')
      : '等待游戏进入主界面';
    box.innerHTML = `
      <div><strong>本机 IPC 端口：</strong>${b.running ? `127.0.0.1:${b.port}` : '未启动'}</div>
      <div><strong>HubBridgeMod：</strong>${!b.gameFound ? '未选择游戏目录' : !b.modInstalled ? '未安装内置桥接模组（可在模组页重装内置加载器获取）' : b.modEnabled ? '已安装并启用' : '已安装但已停用'}</div>
      <div><strong>游戏连接（${liveList.length} 个在线实例）：</strong>${liveList.length ? '' : liveHtml}</div>
      ${liveList.length ? liveHtml : ''}
    `;
  }

  function renderHeader() {
    document.querySelectorAll('#hubView .hub-region-btn').forEach(btn => {
      btn.classList.toggle('is-active', btn.dataset.hubRegion === S.region);
    });

    const b = S.bridge;
    const liveList = (b.sessions || []).filter(s => s.isLoggedIn);
    const dot = $id('hubBridgeDot');
    const pillText = $id('hubBridgePillText');
    if (dot && pillText) {
      dot.className = 'hub-dot ' + (liveList.length ? 'online' : b.modInstalled && b.modEnabled ? 'warn' : '');
      pillText.textContent = liveList.length > 1
        ? `游戏在线 · ${liveList.length} 个实例`
        : liveList.length === 1
          ? `游戏在线 · ${liveList[0].uid}`
          : b.modInstalled && b.modEnabled
            ? '桥接就绪'
            : '未装桥接模组';
    }

    const authEl = $id('hubAuthControl');
    if (!authEl) return;
    if (S.auth.signingIn) {
      authEl.innerHTML = `
        <span class="hub-pill gold">请在浏览器完成登录…</span>
        <button type="button" class="v2-btn ghost" data-hub-act="cancel-signin">取消</button>
      `;
    } else if (S.me) {
      authEl.innerHTML = `
        <div class="hub-user-chip" title="${esc(S.me.displayName)}">
          ${renderAvatar(S.me.avatarUrl, S.me.displayName, 24, 'hub-ms-avatar')}
          <strong>${esc(S.me.displayName)}</strong>
        </div>
        <button type="button" class="v2-btn ghost" data-hub-act="logout">退出</button>
      `;
    } else {
      authEl.innerHTML = `<button type="button" class="v2-btn primary" data-hub-act="signin">登录 / 注册</button>`;
    }

    authEl.querySelector('[data-hub-act="signin"]')?.addEventListener('click', () => openAuthDialog('login'));
    authEl.querySelector('[data-hub-act="cancel-signin"]')?.addEventListener('click', () => post({ type: 'hubCancelSignIn' }));
    authEl.querySelector('[data-hub-act="logout"]')?.addEventListener('click', () => post({ type: 'hubLogout' }));
  }

  function regionBindings(region = S.region) {
    return (S.me?.bindings || []).filter(b => b.region === region);
  }

  function openCreateDialog() {
    if (!S.me) {
      window.toast?.('请先点击右上角「Microsoft 登录」。');
      return;
    }
    const bindings = regionBindings(S.region);
    if (!bindings.length) {
      window.toast?.(`请先在「个人主页」绑定一个 ${regionLabel(S.region)} 游戏账号。`);
      selectTab('profile', false);
      return;
    }

    $id('hubCreateRegionLabel').textContent = regionLabel(S.region);
    const sel = $id('hubCreateUid');
    sel.innerHTML = bindings.map(b =>
      `<option value="${esc(b.uid)}" ${b.isPrimary ? 'selected' : ''}>${esc(b.nick || b.uid)} (UID: ${esc(b.uid)}${b.level ? ` · Lv.${b.level}` : ''})</option>`
    ).join('');
    const primaryInRegion = bindings.find(b => b.isPrimary) || bindings[0];
    $id('hubCreateTitle').value = `${primaryInRegion?.nick || S.me.displayName} 的队伍`;
    const modeSel = $id('hubCreateGameMode');
    if (modeSel) modeSel.value = String(S.gameMapType || 4);
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
    if (S.currentRoom && S.currentRoom.roomId === room.roomId) {
      selectTab('room', false);
      return;
    }
    const bindings = regionBindings(room.region);
    if (!bindings.length) {
      window.toast?.(`请先在「个人主页」绑定一个 ${regionLabel(room.region)} 游戏账号。`);
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

  // 房间大厅始终显示全部车队列表（即使已在房间内也可自由查看）
  function renderLobby() {
    const body = $id('hubLobbyBody');
    if (!body) return;

    const badge = $id('hubRoomsBadge');
    if (badge) {
      badge.textContent = String(S.rooms.length);
      badge.hidden = S.rooms.length === 0;
    }

    const needSignInBanner = !S.me ? `
      <div class="hub-card" style="display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:12px;">
        <div>
          <h3 style="margin:0 0 4px;">登录或注册开始组队</h3>
          <p class="hub-muted">支持账号密码（可开启 TOTP 二次验证）或 Microsoft 一键登录，绑定多个国服 / 国际服游戏账号。</p>
        </div>
        <button type="button" class="v2-btn primary" id="hubInlineSignInBtn">登录 / 注册</button>
      </div>` : '';

    const cards = S.rooms.length
      ? S.rooms.map(r => {
        const isMine = S.currentRoom && S.currentRoom.roomId === r.roomId;
        const statusCls = r.status === 'recruiting' ? 'green' : r.status === 'playing' || r.status === 'game_ready' ? 'pink' : r.status === 'finished' ? 'gold' : '';
        const statusTxt = ROOM_STATUS_TEXT[r.status] || r.status;
        const previewMembers = (r.members && r.members.length)
          ? r.members
          : [{
            userId: r.hostUserId,
            displayName: r.hostDisplayName,
            avatarUrl: r.hostAvatarUrl,
            activeUid: r.hostActiveUid,
            activeLevel: 0,
            isHost: true
          }];
        const membersHtml = previewMembers.map(m => {
          const attrs = inspectAttrs({ ...m, region: r.region });
          return `
            <span class="hub-lobby-member-chip ${m.isHost ? 'is-host' : ''}" ${attrs}>
              ${renderAvatar(m.avatarUrl, m.displayName || m.activeUid, 22, 'hub-ms-avatar')}
              <span class="hub-chip-name">${esc(m.displayName || m.activeUid)}</span>
            </span>`;
        }).join('');
        return `
          <article class="hub-room-card ${isMine ? 'is-mine' : ''}">
            <div class="hub-room-card-top">
              <h4>${esc(r.title)} ${isMine ? '<span class="hub-pill pink">我的房间</span>' : ''}</h4>
              <span class="hub-pill ${statusCls}">${esc(statusTxt)}</span>
            </div>
            <div class="hub-room-note">${esc(r.note || '暂无备注')}</div>
            <div class="hub-room-meta">
              <span class="hub-pill">${esc(regionLabel(r.region))}</span>
              ${r.hasPassword ? '<span class="hub-pill gold">加密</span>' : ''}
              <span class="hub-muted">右键头像查成分</span>
            </div>
            <div class="hub-lobby-members">
              <div class="hub-lobby-member-strip">${membersHtml}</div>
            </div>
            <div class="hub-room-foot">
              <span class="hub-muted">${r.memberCount} / ${r.capacity} 人 · ${formatTime(r.lastActivityAt)}</span>
              ${isMine
                ? `<button type="button" class="v2-btn primary" data-hub-enter-mine="1">回到我的房间</button>`
                : `<button type="button" class="v2-btn primary" data-hub-join="${esc(r.roomId)}">加入队伍</button>`}
            </div>
          </article>
        `;
      }).join('')
      : `<div class="hub-card"><p class="hub-muted" style="text-align:center;padding:24px 0;">${esc(regionLabel(S.region))} 暂无活跃房间，点击右上角「创建组队房间」开一局吧。</p></div>`;

    body.innerHTML = `
      ${needSignInBanner}
      <div class="hub-card">
        <div class="hub-card-head">
          <div class="hub-toolbar">
            <input id="hubSearchInput" class="hub-input" value="${esc(S.roomQuery)}" placeholder="搜索房名、备注或房间号" />
            <select id="hubStatusSelect" class="v2-select">
              <option value="" ${S.roomStatusFilter === '' ? 'selected' : ''}>全部状态</option>
              <option value="recruiting" ${S.roomStatusFilter === 'recruiting' ? 'selected' : ''}>招募中</option>
              <option value="game_ready" ${S.roomStatusFilter === 'game_ready' ? 'selected' : ''}>游戏房就绪</option>
              <option value="playing" ${S.roomStatusFilter === 'playing' ? 'selected' : ''}>对局中</option>
              <option value="finished" ${S.roomStatusFilter === 'finished' ? 'selected' : ''}>已结束</option>
            </select>
            <button type="button" class="v2-btn ghost" id="hubRefreshRoomsBtn">刷新</button>
          </div>
          <div class="hub-toolbar">
            ${S.currentRoom ? `<button type="button" class="v2-btn ghost" id="hubBackToRoomBtn">回到当前房间 (${S.currentRoom.members.length}/${S.currentRoom.capacity})</button>` : ''}
            <button type="button" class="v2-btn primary" id="hubOpenCreateBtn">+ 创建组队房间</button>
          </div>
        </div>
        <div class="hub-rooms-grid">${cards}</div>
      </div>
    `;

    $id('hubInlineSignInBtn')?.addEventListener('click', () => openAuthDialog('login'));
    $id('hubOpenCreateBtn')?.addEventListener('click', openCreateDialog);
    $id('hubBackToRoomBtn')?.addEventListener('click', () => selectTab('room', false));
    $id('hubRefreshRoomsBtn')?.addEventListener('click', () => {
      S.roomQuery = $id('hubSearchInput')?.value || '';
      S.roomStatusFilter = $id('hubStatusSelect')?.value || '';
      post({ type: 'hubListRooms', region: S.region, query: S.roomQuery, status: S.roomStatusFilter });
    });
    $id('hubSearchInput')?.addEventListener('keydown', e => {
      if (e.key === 'Enter') $id('hubRefreshRoomsBtn')?.click();
    });
    $id('hubStatusSelect')?.addEventListener('change', () => $id('hubRefreshRoomsBtn')?.click());

    body.querySelectorAll('[data-hub-enter-mine]').forEach(btn => {
      btn.addEventListener('click', () => selectTab('room', false));
    });
    body.querySelectorAll('[data-hub-join]').forEach(btn => {
      btn.addEventListener('click', () => {
        const room = S.rooms.find(x => x.roomId === btn.dataset.hubJoin);
        if (room) openJoinDialog(room);
      });
    });
  }

  function renderRoomPanel() {
    const body = $id('hubRoomBody');
    if (!body) return;
    const room = S.currentRoom;
    if (!room) {
      body.innerHTML = '<div class="hub-card"><p class="hub-muted">当前未加入任何组队房间。</p></div>';
      return;
    }

    const isHost = S.me && room.hostUserId === S.me.userId;
    const bindings = regionBindings(room.region);
    const gr = room.gameRoom;
    const hasGameRoom = !!(gr && gr.gameRoomId > 0);
    const modeTag = S.gameMapType === 1 ? 'PVP' : 'PVE';
    const statusTxt = ROOM_STATUS_TEXT[room.status] || room.status;
    const selfMember = room.members.find(m => m.self);
    const hostCreateLabel = hasGameRoom ? `重开 ${modeTag} 房` : `自动建 ${modeTag} 房`;

    const gameRoomBanner = hasGameRoom ? `
      <div class="hub-gameroom-banner">
        <div class="hub-gameroom-codes">
          <div><div class="hub-muted">游戏房间号</div><strong>${esc(gr.gameRoomId)}</strong></div>
          <div><div class="hub-muted">房间密码</div><strong>${esc(gr.gamePwd || '无密码')}</strong></div>
          <div><div class="hub-muted">模式 / 地图</div><strong>${esc(MODES[gr.mapType] || '自定义')} · 地图 ${esc(gr.mapId)}</strong></div>
        </div>
        <div class="hub-toolbar">
          <button type="button" class="v2-btn ghost" id="hubCopyGameCodeBtn">复制房号密码</button>
          ${!isHost ? `<button type="button" class="v2-btn primary" id="hubTriggerJoinBtn">一键加入游戏房间</button>` : ''}
        </div>
      </div>` : `
      <div class="hub-notice-warn">
        ${isHost
          ? `请自行启动游戏（Steam / TapTap 用户也可点「启动游戏」），点击「${esc(hostCreateLabel)}」后，内置 <strong>HubBridgeMod</strong> 会在对应出战账号进入主界面时自动建房，并把房号与密码同步到顶部栏与队友。`
          : '请自行启动游戏（Steam / TapTap 用户也可点「启动游戏」），等待房主建房后房号与密码将自动同步到顶部栏并通知进房。'}
      </div>`;

    const memberCards = room.members.map(m => {
      const [stText, stCls] = GAME_STATES[m.gameState] || [m.gameState, ''];
      const memberName = m.activeNick || m.displayName || m.activeUid || '玩家';
      const attrs = inspectAttrs({ ...m, region: room.region });
      return `
        <div class="hub-member-card ${m.self ? 'is-self' : ''}">
          <div class="hub-member-top">
            ${renderAvatar(m.avatarUrl, memberName, 42, 'hub-avatar round hub-inspectable-avatar', attrs)}
            <div class="hub-member-info">
              <strong><span>${esc(memberName)}</span> ${m.isHost ? '<span class="hub-pill pink">房主</span>' : ''}</strong>
              <small>UID ${esc(m.activeUid)}${m.activeLevel ? ` · Lv.${m.activeLevel}` : ''}</small>
            </div>
          </div>
          <div class="hub-toolbar">
            <span class="hub-pill ${stCls}">${esc(stText)}</span>
            ${m.online ? '<span class="hub-pill green">在线</span>' : '<span class="hub-pill">离线</span>'}
          </div>
          ${m.joinError ? `<small style="color:#fb7185;">${esc(m.joinError)}</small>` : ''}
          <div class="hub-member-actions">
            <button type="button" class="v2-btn ghost" data-hub-view-user="${esc(m.userId)}">查看主页</button>
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
            ${bindings.length > 1 ? `<select id="hubRoomSwitchUid" class="v2-select">${bindings.map(b => `<option value="${esc(b.uid)}" ${selfMember?.activeUid === b.uid ? 'selected' : ''}>${esc(b.nick || b.uid)} (${esc(b.uid)})</option>`).join('')}</select>` : ''}
            ${isHost ? `<select id="hubHostMapTypeSelect" class="v2-select" title="自动建房游戏模式">
              <option value="4" ${S.gameMapType === 4 ? 'selected' : ''}>PVE 模式</option>
              <option value="1" ${S.gameMapType === 1 ? 'selected' : ''}>PVP 模式</option>
            </select>` : ''}
            ${isHost ? `<button type="button" class="v2-btn primary" id="hubHostCreateGameBtn">${esc(hostCreateLabel)}</button>` : ''}
            <button type="button" class="v2-btn ghost" id="hubLaunchGameOnlyBtn" title="给 Steam / TapTap 用户快捷启动游戏；其他平台请自行启动">启动游戏</button>
            ${isHost && room.status === 'finished' ? `<button type="button" class="v2-btn ghost" id="hubReopenBtn">开启下一局</button>` : ''}
            ${room.latestMatch ? `<button type="button" class="v2-btn ghost" id="hubOpenLatestSettleBtn">查看战报</button>` : ''}
            <button type="button" class="v2-btn ghost" id="hubBrowseLobbyBtn">逛房间大厅</button>
            ${isHost
              ? `<button type="button" class="v2-btn hub-btn-danger" id="hubDisbandRoomBtn">解散房间</button>`
              : `<button type="button" class="v2-btn ghost" id="hubLeaveRoomBtn">退出房间</button>`}
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
    $id('hubBrowseLobbyBtn')?.addEventListener('click', () => selectTab('lobby', false));
    $id('hubLeaveRoomBtn')?.addEventListener('click', () => post({ type: 'hubLeaveRoom' }));
    $id('hubDisbandRoomBtn')?.addEventListener('click', openDisbandDialog);
    $id('hubReopenBtn')?.addEventListener('click', () => post({ type: 'hubReopenRoom' }));
    $id('hubHostMapTypeSelect')?.addEventListener('change', e => {
      S.gameMapType = Number(e.target.value) === 1 ? 1 : 4;
      renderGlobalRoomBar();
      renderRoomPanel();
    });
    $id('hubHostCreateGameBtn')?.addEventListener('click', () => post({ type: 'hubHostCreateGameRoom', mapType: S.gameMapType }));
    $id('hubLaunchGameOnlyBtn')?.addEventListener('click', () => post({ type: 'hubLaunchGameOnly', mode: 'bypassSteam' }));
    $id('hubTriggerJoinBtn')?.addEventListener('click', () => {
      if (!gr) return;
      post({ type: 'hubTriggerBridgeJoin', gameRoomId: gr.gameRoomId, gamePwd: gr.gamePwd, roomServerId: gr.roomServerId });
    });
    $id('hubCopyGameCodeBtn')?.addEventListener('click', () => copyGameRoomInfo(gr));
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

  function pickSelectedBinding(bindings) {
    if (!bindings?.length) return null;
    const found = bindings.find(b => `${b.region}:${b.uid}` === S.selectedBindingKey);
    if (found) return found;
    const primary = bindings.find(b => b.isPrimary) || bindings[0];
    S.selectedBindingKey = `${primary.region}:${primary.uid}`;
    return primary;
  }

  function renderMatchDetailTable(players) {
    if (!players?.length) return '<div class="account-match-detail"><p class="hub-muted">暂无同局玩家明细</p></div>';
    const rows = players.map(p => `
      <tr class="${p.isSelf ? 'self' : ''}">
        <td>${p.rank > 0 ? esc(p.rank) : '—'}</td>
        <td>
          <div class="account-detail-player">
            ${imgUrl(p.image) ? `<img class="account-detail-avatar" src="${esc(p.image)}" alt="" loading="lazy" />` : '<span class="account-detail-avatar placeholder" aria-hidden="true"></span>'}
            <div>
              <strong>${esc(p.name || p.playerId)}</strong>${p.isSelf ? ' <em class="account-tag">我</em>' : ''}
              <small>Lv.${esc(p.playerLevel ?? '—')}</small>
            </div>
          </div>
        </td>
        <td>${esc(p.character || p.heroId || '—')}</td>
        <td>${esc(p.level)}</td>
        <td>${esc(p.gold)}</td>
        <td>${p.gaveUp ? '放弃' : '—'}</td>
      </tr>
    `).join('');
    return `
      <div class="account-match-detail">
        <table class="account-detail-table">
          <thead>
            <tr><th>排名</th><th>玩家</th><th>角色</th><th>局内等级</th><th>星币</th><th>状态</th></tr>
          </thead>
          <tbody>${rows}</tbody>
        </table>
      </div>`;
  }

  function renderBoundShowcaseHtml(binding) {
    if (!binding) {
      return `<div class="hub-card"><p class="hub-muted">尚未绑定游戏账号，在上方选择一个本机 UID 或在线游戏角色即可展示完整战绩与好友状态。</p></div>`;
    }

    const snap = binding.snapshot;
    const hasLocalCred = (S.localAccounts || []).some(a => a.uid === binding.uid);
    const isRefreshing = S.liveLoadingUid === binding.uid;
    const liveData = S.liveByUid[binding.uid] || null;
    const need = Number(snap?.expNeed) || 0;
    const exp = Number(snap?.exp) || 0;
    const expPct = need > 0 ? Math.max(0, Math.min(100, exp / need * 100)) : 0;

    const characters = snap?.characters || [];
    const topChar = [...characters].sort((a, b) => (Number(b.fightCount) || 0) - (Number(a.fightCount) || 0))[0];
    const fights = snap?.fightCount ?? 0;
    const wins = snap?.winCount ?? 0;
    const winRate = fights > 0 ? `${(wins * 100 / fights).toFixed(1)}%` : '—';
    const winMaps = snap?.winMaps || [];
    const achievements = (snap?.achievements || []).filter(a => a && a.name);
    const matches = snap?.recentMatches || [];

    const statCard = (label, val) => `<article class="account-stat"><span>${esc(label)}</span><strong>${esc(val)}</strong></article>`;
    const statsGrid = snap ? [
      statCard('总对局', fights || '—'),
      statCard('胜场', wins || '—'),
      statCard('胜率', winRate),
      statCard('获赞', snap.praiseNum ?? '—'),
      statCard('角色卡', snap.roleCardCount || characters.length || '—'),
      statCard('皮肤', snap.skinCount ?? '—'),
      statCard('装扮', snap.adornCount ?? '—'),
      statCard('通关地图', winMaps.length),
      statCard('最常用角色', snap.useHeroName || topChar?.name || '—'),
      statCard('信誉分', snap.creditScore ?? '—'),
      statCard('注册时间', formatTime(snap.createdAtGame)),
      statCard('最近登录', formatTime(snap.lastLoginAtGame))
    ].join('') : '';

    // 角色使用子面板
    const charQ = S.charSearch.trim().toLowerCase();
    const filteredChars = characters.filter(c => (!S.charPlayedOnly || (Number(c.fightCount) || 0) > 0) && `${c.name || ''} ${c.id}`.toLowerCase().includes(charQ));
    const charsHtml = `
      <div class="hub-toolbar">
        <input id="hubCharSearch" class="hub-input" value="${esc(S.charSearch)}" placeholder="筛选角色名或 ID…" />
        <label style="display:inline-flex;align-items:center;gap:6px;font-size:13px;cursor:pointer;">
          <input id="hubCharPlayedOnly" type="checkbox" ${S.charPlayedOnly ? 'checked' : ''} /> 仅看使用过
        </label>
        <span class="hub-muted">共 ${filteredChars.length} / ${characters.length} 名角色</span>
      </div>
      <div class="account-character-grid">
        ${filteredChars.length ? filteredChars.map(c => `
          <article class="account-character">
            ${imgUrl(c.image) ? `<img src="${esc(c.image)}" alt="" loading="lazy" />` : '<div class="account-avatar" aria-hidden="true"></div>'}
            <div>
              <strong>${esc(c.name || `#${c.id}`)}</strong>
              <span>${c.level > 0 ? `<em class="account-tag">Lv.${esc(c.level)}</em>` : ''}${c.breakthrough ? '<em class="account-tag breakthrough">已突破</em>' : ''}<b>${c.fightCount == null ? '—' : esc(c.fightCount)}</b>场</span>
            </div>
          </article>
        `).join('') : '<p class="account-empty">暂无匹配的角色数据</p>'}
      </div>`;

    // 最近对局子面板
    const matchesHtml = `
      <div class="account-recent">
        ${matches.length ? matches.map(m => {
          const key = `${binding.uid}:${m.replayId || ('i' + m.index)}`;
          const open = !!S.expandedMatches[key];
          const downloadable = m.canDownload === true && /^\d{1,32}$/.test(m.replayId || '');
          const modeTxt = MODES[m.mapType] || `模式 ${m.mapType ?? '—'}`;
          return `
            <article class="account-match">
              <div class="account-match-main">
                <strong>
                  ${imgUrl(m.mapImage) ? `<img class="account-match-map" src="${esc(m.mapImage)}" alt="" loading="lazy" />` : (imgUrl(m.heroImage) ? `<img class="account-match-map" src="${esc(m.heroImage)}" alt="" loading="lazy" />` : '')}
                  <span>${esc(m.mapName || modeTxt)}</span>
                </strong>
                <span>${esc(m.character || '未知角色')} · 第 ${m.rank > 0 ? esc(m.rank) : '—'} 名</span>
                <small>${esc(modeTxt)} · ${esc(formatTime(m.playedAt))}${m.version ? ` · ${esc(m.version)}` : ''}</small>
                <small class="account-replay-id">回放 ID ${esc(m.replayId || '—')}</small>
              </div>
              <div class="account-match-actions">
                ${downloadable ? `<button type="button" class="v2-btn" data-hub-dl-replay="${esc(m.replayId)}" data-hub-dl-region="${esc(binding.region)}">下载回放</button>` : '<span class="hub-muted">无回放</span>'}
                ${m.players?.length ? `<button type="button" class="v2-btn" data-hub-toggle-detail="${esc(key)}">${open ? '收起详情' : '查看详情'}</button>` : ''}
              </div>
              ${open ? renderMatchDetailTable(m.players) : ''}
            </article>
          `;
        }).join('') : '<p class="account-empty">暂无近期对局记录，点击上方「直连刷新最新战绩与好友」同步。</p>'}
      </div>`;

    // 好友状态子面板
    const friendQ = S.friendSearch.trim().toLowerCase();
    const friendsList = liveData?.friends || null;
    const filteredFriends = (friendsList || []).filter(f =>
      (!S.friendOnlineOnly || f.status !== 'offline') &&
      `${f.name || ''} ${f.note || ''} ${f.uid || ''}`.toLowerCase().includes(friendQ)
    );
    const friendsHtml = `
      <div class="hub-toolbar">
        <input id="hubFriendSearch" class="hub-input" value="${esc(S.friendSearch)}" placeholder="搜索好友昵称、备注或 UID…" />
        <label style="display:inline-flex;align-items:center;gap:6px;font-size:13px;cursor:pointer;">
          <input id="hubFriendOnlineOnly" type="checkbox" ${S.friendOnlineOnly ? 'checked' : ''} /> 仅看在线
        </label>
        ${hasLocalCred ? `<button type="button" class="v2-btn ghost" id="hubRefreshFriendsBtn" ${isRefreshing ? 'disabled' : ''}>${isRefreshing ? '刷新中…' : '刷新好友状态'}</button>` : ''}
        ${liveData?.capturedAt ? `<span class="hub-muted">更新于 ${esc(formatTime(liveData.capturedAt))}</span>` : ''}
      </div>
      <div class="account-friends">
        ${friendsList === null
          ? `<p class="account-empty">${hasLocalCred ? '点击「刷新好友状态」或上方「直连刷新最新战绩与好友」查看实时好友状态。' : '当前机器未检测到该 UID 的本机游戏凭据。'}</p>`
          : filteredFriends.length
            ? filteredFriends.map(f => {
              const st = FRIEND_STATUS_TEXT[f.status] ? f.status : 'unknown';
              return `
                <article class="account-friend">
                  ${imgUrl(f.image) ? `<img class="account-avatar" src="${esc(f.image)}" alt="" loading="lazy" />` : '<div class="account-avatar" aria-hidden="true"></div>'}
                  <div class="account-friend-main">
                    <strong>${esc(f.name)}</strong>
                    ${f.note ? `<small>${esc(f.note)}</small>` : ''}
                    <small>UID ${esc(f.uid)} · Lv.${esc(f.level)}</small>
                    ${st === 'offline' && f.offlineAt ? `<small>上次离线 · ${esc(formatTime(f.offlineAt))}</small>` : ''}
                  </div>
                  <span class="account-badge ${esc(st)}">${esc(FRIEND_STATUS_TEXT[st] || '未知')}</span>
                </article>`;
            }).join('')
            : '<p class="account-empty">暂无匹配的好友</p>'}
      </div>`;

    return `
      <div class="hub-card">
        <div class="hub-showcase-head">
          <div class="hub-showcase-id">
            ${renderAvatar(binding.headImage || snap?.headImage, binding.nick || binding.uid, 56, 'hub-showcase-avatar')}
            <div class="account-profile-main">
              <div class="account-profile-title">
                <strong>${esc(snap?.nick || binding.nick || binding.uid)}</strong>
                <span class="hub-pill">${esc(regionLabel(binding.region))}</span>
                ${binding.isPrimary ? '<span class="hub-pill pink">主号</span>' : ''}
                <span class="account-uid">UID ${esc(binding.uid)}</span>
              </div>
              <div class="account-profile-level">
                <span>Lv.${esc(snap?.level ?? binding.level ?? 0)}</span>
                ${need > 0
                  ? `<div class="account-exp" role="img" aria-label="${esc(exp + ' / ' + need)}"><i style="width:${expPct.toFixed(1)}%"></i></div><small>${esc(exp)} / ${esc(need)}</small>`
                  : (snap ? `<small>${esc(exp)} EXP</small>` : '<small>未同步详细快照</small>')}
                ${snap?.capturedAt ? `<small class="hub-muted">· 同步于 ${esc(formatTime(snap.capturedAt))}</small>` : ''}
              </div>
            </div>
          </div>

          <div class="hub-toolbar">
            ${hasLocalCred ? `<button type="button" class="v2-btn primary" id="hubRefreshLiveBtn" ${isRefreshing ? 'disabled' : ''}>${isRefreshing ? '正在刷新…' : '直连刷新最新战绩与好友'}</button>` : ''}
            ${!binding.isPrimary ? `<button type="button" class="v2-btn ghost" data-hub-primary="${esc(binding.region)}:${esc(binding.uid)}">设为主号</button>` : ''}
            <button type="button" class="v2-btn ghost" data-hub-unbind="${esc(binding.region)}:${esc(binding.uid)}">解绑</button>
          </div>
        </div>

        ${snap ? `
          <div class="account-stat-grid">${statsGrid}</div>
          <div class="account-achievements">
            <span class="account-section-label">成就徽章</span>
            ${achievements.length
              ? `<div class="account-chips">${achievements.map(a => `<span class="account-chip">${esc(a.name)}</span>`).join('')}</div>`
              : '<span class="hub-muted">暂无成就展示</span>'}
          </div>
          <div class="account-winmaps">
            <span class="account-section-label">通关地图 · ${winMaps.length}</span>
            ${winMaps.length
              ? `<div class="account-chips">${winMaps.map(m => `<span class="account-chip">${imgUrl(m.image) ? `<img class="account-map-thumb" src="${esc(m.image)}" alt="" loading="lazy" />` : ''}${esc(m.name || `地图 ${m.mapId}`)}<b>${esc(m.wins)}</b><small>胜</small></span>`).join('')}</div>`
              : '<span class="hub-muted">暂无地图胜场记录</span>'}
          </div>
        ` : `<div class="hub-notice-warn">该账号当前仅绑定了 UID，点击「直连刷新最新战绩与好友」即可拉取完整等级、角色、战绩与好友列表。</div>`}

        <div class="hub-subtabs" role="tablist" aria-label="游戏账号详情切换">
          <button type="button" role="tab" aria-selected="${S.profileSubTab === 'characters'}" data-hub-subtab="characters">角色使用 (${characters.length})</button>
          <button type="button" role="tab" aria-selected="${S.profileSubTab === 'matches'}" data-hub-subtab="matches">最近对局 (${matches.length})</button>
          <button type="button" role="tab" aria-selected="${S.profileSubTab === 'friends'}" data-hub-subtab="friends">好友状态${friendsList ? ` (${friendsList.length})` : ''}</button>
        </div>

        <div role="tabpanel" ${S.profileSubTab === 'characters' ? '' : 'hidden'}>${charsHtml}</div>
        <div role="tabpanel" ${S.profileSubTab === 'matches' ? '' : 'hidden'}>${matchesHtml}</div>
        <div role="tabpanel" ${S.profileSubTab === 'friends' ? '' : 'hidden'}>${friendsHtml}</div>
      </div>`;
  }

  function renderProfile() {
    const body = $id('hubProfileBody');
    if (!body) return;

    const me = S.me;
    const live = (S.bridge.sessions || []).find(s => s.isLoggedIn);
    const selectedBinding = me ? pickSelectedBinding(me.bindings) : null;

    const masterSection = !me ? `
      <div class="hub-card">
        <div class="hub-card-head"><h3>大厅账号</h3></div>
        <p class="hub-muted">支持账号密码注册登录、Microsoft 登录与 TOTP 二次验证；登录后可绑定游戏账号、查看个人战绩与好友状态。</p>
        <div class="hub-toolbar" style="margin-top:12px;">
          <button type="button" class="v2-btn primary" id="hubProfileSignInBtn">登录 / 注册</button>
          <button type="button" class="v2-btn ghost" id="hubOpenLocalAccountBtn">本机凭据管理</button>
        </div>
      </div>` : `
      <div class="hub-card">
        <div class="hub-profile-master">
          <div class="hub-profile-identity">
            ${renderAvatar(me.avatarUrl, me.displayName, 52, 'hub-ms-avatar')}
            <div>
              <strong style="font-size:18px;display:block;">${esc(me.displayName)}</strong>
              <div class="hub-muted" style="margin-top:4px;display:flex;align-items:center;gap:6px;flex-wrap:wrap;">
                <span>ID: ${esc(me.userId)}</span>
                ${me.username ? `<span>· @${esc(me.username)}</span>` : ''}
                <span>· 已绑定 ${(me.bindings || []).length} 个游戏账号</span>
                ${me.twoFactorEnabled ? '<span class="hub-pill green">2FA 已开启</span>' : ''}
              </div>
            </div>
          </div>

          <div class="hub-toolbar">
            <button type="button" class="v2-btn primary" id="hubOpenSecurityBtn">账号与安全</button>
            <button type="button" class="v2-btn ghost" id="hubOpenPrivacyBtn">隐私设置</button>
            <button type="button" class="v2-btn ghost" id="hubPreviewSelfBtn">预览他人视角</button>
            <button type="button" class="v2-btn ghost" id="hubOpenLocalAccountBtn">凭据管理</button>
          </div>
        </div>

        <div class="hub-binding-strip">
          ${(me.bindings || []).map(b => {
            const key = `${b.region}:${b.uid}`;
            const isSel = selectedBinding && key === `${selectedBinding.region}:${selectedBinding.uid}`;
            return `
              <button type="button" class="hub-binding-pill ${isSel ? 'is-active' : ''}" data-hub-select-binding="${esc(key)}">
                ${imgUrl(b.headImage) ? `<img src="${esc(b.headImage)}" alt="" />` : ''}
                <span>${esc(b.nick || b.uid)}</span>
                <span class="hub-pill">${esc(b.region === 'int' ? 'INT' : 'CN')}</span>
              </button>`;
          }).join('')}
          <select id="hubLocalUidSelect" class="v2-select" aria-label="选择本机游戏 UID">
            ${(S.localAccounts || []).length
              ? S.localAccounts.map(a => `<option value="${esc(a.uid)}">UID ${esc(a.uid)}${a.region ? ` (${esc(regionLabel(a.region))})` : ''}</option>`).join('')
              : '<option value="">未检测到本机游戏凭据</option>'}
          </select>
          <button type="button" class="v2-btn ghost" id="hubBindLocalBtn" ${(S.localAccounts || []).length ? '' : 'disabled'}>+ 绑定并同步本机账号</button>
          ${live ? `<button type="button" class="v2-btn ghost" id="hubBindBridgeBtn">+ 绑定在线角色 (${esc(live.uid)})</button>` : ''}
        </div>
      </div>
      ${renderBoundShowcaseHtml(selectedBinding)}`;

    const lookupSection = `
      <details class="hub-details-box" ${S.viewedProfile ? 'open' : ''}>
        <summary>
          <span>玩家公开名片查询</span>
          <span class="hub-muted">按游戏 UID 或大厅 ID 查他人资料</span>
        </summary>
        <div class="hub-details-body">
          <div class="hub-toolbar" style="margin-bottom:12px;">
            <select id="hubLookupRegion" class="v2-select">
              <option value="cn" ${S.lookupRegion === 'cn' ? 'selected' : ''}>国服 UID</option>
              <option value="int" ${S.lookupRegion === 'int' ? 'selected' : ''}>国际服 UID</option>
              <option value="user" ${S.lookupRegion === 'user' ? 'selected' : ''}>大厅 UserID</option>
            </select>
            <input id="hubLookupInput" class="hub-input" value="${esc(S.lookupInput)}" placeholder="输入游戏 UID 或 UserID" />
            <button type="button" class="v2-btn primary" id="hubLookupBtn">查询</button>
          </div>
          ${renderViewedProfileHtml(S.viewedProfile)}
        </div>
      </details>`;

    body.innerHTML = masterSection + lookupSection;

    $id('hubProfileSignInBtn')?.addEventListener('click', () => openAuthDialog('login'));
    $id('hubOpenLocalAccountBtn')?.addEventListener('click', () => window.showPage?.('account'));
    $id('hubOpenSecurityBtn')?.addEventListener('click', openSecurityDialog);
    $id('hubOpenPrivacyBtn')?.addEventListener('click', openPrivacyDialog);
    $id('hubPreviewSelfBtn')?.addEventListener('click', () => me && post({ type: 'hubViewProfile', userId: me.userId }));
    $id('hubBindLocalBtn')?.addEventListener('click', () => {
      const uid = $id('hubLocalUidSelect')?.value;
      if (uid) post({ type: 'hubBindLocalAccount', uid, setPrimary: !(me?.bindings?.length), syncSnapshot: true });
    });
    $id('hubBindBridgeBtn')?.addEventListener('click', () => {
      post({ type: 'hubBindBridgeAccount', setPrimary: !(me?.bindings?.length) });
    });

    body.querySelectorAll('[data-hub-select-binding]').forEach(btn => {
      btn.addEventListener('click', () => {
        S.selectedBindingKey = btn.dataset.hubSelectBinding;
        renderProfile();
      });
    });

    const triggerLiveRefresh = () => {
      if (!selectedBinding) return;
      post({ type: 'hubRefreshLiveAccount', uid: selectedBinding.uid, syncToHub: true });
    };
    $id('hubRefreshLiveBtn')?.addEventListener('click', triggerLiveRefresh);
    $id('hubRefreshFriendsBtn')?.addEventListener('click', triggerLiveRefresh);

    body.querySelectorAll('[data-hub-subtab]').forEach(btn => {
      btn.addEventListener('click', () => {
        S.profileSubTab = btn.dataset.hubSubtab;
        renderProfile();
      });
    });

    $id('hubCharSearch')?.addEventListener('input', e => {
      S.charSearch = e.target.value;
      const pos = e.target.selectionStart;
      renderProfile();
      const el = $id('hubCharSearch');
      if (el) { el.focus(); el.setSelectionRange(pos, pos); }
    });
    $id('hubCharPlayedOnly')?.addEventListener('change', e => {
      S.charPlayedOnly = e.target.checked;
      renderProfile();
    });
    $id('hubFriendSearch')?.addEventListener('input', e => {
      S.friendSearch = e.target.value;
      const pos = e.target.selectionStart;
      renderProfile();
      const el = $id('hubFriendSearch');
      if (el) { el.focus(); el.setSelectionRange(pos, pos); }
    });
    $id('hubFriendOnlineOnly')?.addEventListener('change', e => {
      S.friendOnlineOnly = e.target.checked;
      renderProfile();
    });

    body.querySelectorAll('[data-hub-toggle-detail]').forEach(btn => {
      btn.addEventListener('click', () => {
        const key = btn.dataset.hubToggleDetail;
        S.expandedMatches[key] = !S.expandedMatches[key];
        renderProfile();
      });
    });
    body.querySelectorAll('[data-hub-dl-replay]').forEach(btn => {
      btn.addEventListener('click', () => {
        const id = btn.dataset.hubDlReplay;
        const reg = btn.dataset.hubDlRegion;
        if (/^\d{1,32}$/.test(id || '')) {
          window.ReplayModule?.openDownloadDialog({
            replayId: id,
            cdn: reg === 'int' ? 'GlobalProd' : 'CnProd',
            autoStart: true
          });
        }
      });
    });

    body.querySelectorAll('[data-hub-primary]').forEach(btn => {
      btn.addEventListener('click', () => {
        const [region, uid] = btn.dataset.hubPrimary.split(':');
        post({ type: 'hubUpdateProfile', primaryRegion: region, primaryUid: uid });
      });
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
    if (!p) return '<p class="hub-muted">点击房间队友的「查看主页」或在上方输入 UID 查看公开名片。</p>';
    if (p.profileHidden) {
      return `<div class="hub-notice-warn"><strong>${esc(p.displayName)}</strong> 已关闭公开档案展示。</div>`;
    }

    const bindingsHtml = (p.bindings || []).map(b => {
      const snap = b.snapshot;
      const fights = Number(snap?.fightCount) || 0;
      const wins = Number(snap?.winCount) || 0;
      const winRate = fights > 0 ? `${(wins * 100 / fights).toFixed(1)}%` : '—';
      const winMaps = snap?.winMaps || [];
      return `
        <div class="hub-card" style="background:var(--v2-panel-2);margin-top:12px;">
          <div class="hub-card-head">
            <div class="hub-member-top">
              ${renderAvatar(b.headImage || snap?.headImage, b.nick || p.displayName, 44, 'hub-avatar')}
              <div>
                <strong>${esc(snap?.nick || b.nick || p.displayName)} <span class="hub-pill">${esc(regionLabel(b.region))}</span>${b.isPrimary ? ' <span class="hub-pill pink">主号</span>' : ''}</strong>
                <div class="hub-muted">UID ${esc(b.uid)}${(snap?.level ?? b.level) ? ` · Lv.${snap?.level ?? b.level}` : ''}${snap?.useHeroName ? ` · 常用 ${esc(snap.useHeroName)}` : ''}</div>
              </div>
            </div>
            ${snap ? `<span class="hub-pill">更新于 ${formatTime(snap.capturedAt)}</span>` : '<span class="hub-pill">暂无战绩快照</span>'}
          </div>
          ${snap ? `
            <div class="hub-inspect-kpis">
              <div class="hub-inspect-kpi"><span>总对局</span><strong>${fights || '—'}</strong></div>
              <div class="hub-inspect-kpi"><span>胜场</span><strong>${wins || '—'}</strong></div>
              <div class="hub-inspect-kpi"><span>胜率</span><strong>${esc(winRate)}</strong></div>
              <div class="hub-inspect-kpi"><span>信誉分</span><strong>${snap.creditScore || '—'}</strong></div>
              <div class="hub-inspect-kpi"><span>获赞</span><strong>${snap.praiseNum || '—'}</strong></div>
              <div class="hub-inspect-kpi"><span>角色 / 皮肤</span><strong>${snap.roleCardCount || '—'} / ${snap.skinCount || '—'}</strong></div>
            </div>
            ${(snap.achievements || []).length ? `
              <div class="hub-muted" style="margin-bottom:6px;">成就徽章</div>
              <div class="hub-toolbar" style="margin-bottom:10px;">
                ${snap.achievements.slice(0, 8).map(a => `<span class="hub-pill gold">${esc(a.name)}</span>`).join('')}
              </div>` : ''}
            ${winMaps.length ? `
              <div class="hub-muted" style="margin-bottom:6px;">通关地图 (${winMaps.length})</div>
              <div class="hub-toolbar" style="margin-bottom:10px;">
                ${winMaps.slice(0, 8).map(m => `<span class="hub-pill">${esc(m.name || `地图 ${m.mapId}`)} · ${m.wins}胜</span>`).join('')}
              </div>` : ''}
            ${(snap.characters || []).length ? `
              <div class="hub-muted" style="margin-bottom:6px;">常用角色</div>
              <div class="hub-toolbar">
                ${snap.characters.slice(0, 10).map(c => `<span class="hub-pill">${esc(c.name || `#${c.id}`)} Lv.${c.level}${c.fightCount != null ? ` · ${c.fightCount}场` : ''}</span>`).join('')}
              </div>` : ''}
            ${(snap.recentMatches || []).length ? `
              <div class="hub-muted" style="margin:10px 0 6px;">近期对局 (${snap.recentMatches.length})</div>
              <div class="hub-toolbar">
                ${snap.recentMatches.slice(0, 8).map(m => `<span class="hub-pill">#${m.rank} ${esc(m.character || '')} · ${esc(MODES[m.mapType] || '对局')} · ${formatTime(m.playedAt)}</span>`).join('')}
              </div>` : ''}
          ` : ''}
        </div>`;
    }).join('');

    return `
      <div>
        <div class="hub-toolbar">
          ${renderAvatar(p.avatarUrl, p.displayName, 36, 'hub-ms-avatar')}
          <strong>${esc(p.displayName)}</strong>
          <span class="hub-muted">ID: ${esc(p.userId)}${p.lastSeenAt ? ` · 最近活跃 ${formatTime(p.lastSeenAt)}` : ''}</span>
        </div>
        ${bindingsHtml || '<p class="hub-muted" style="margin-top:10px;">对方未公开绑定的游戏 UID。</p>'}
      </div>`;
  }

  function renderSettlement() {
    const body = $id('hubSettlementBody');
    if (!body) return;

    if (!S.stage1) {
      body.innerHTML = `
        <div class="hub-card">
          <h3>暂无本会话对局战报</h3>
          <p class="hub-muted">完成一局游戏后，这里会自动展示即时战报并拉取官方完整回放数据。</p>
        </div>`;
      return;
    }

    const s1 = S.stage1;
    const s2 = S.stage2;
    const st2Label = S.stage2Status === 'ready'
      ? ['完整战报就绪', 'green']
      : S.stage2Status === 'polling'
        ? ['官方回放拉取中…', 'gold']
        : S.stage2Status === 'failed'
          ? ['完整回放暂未就绪', 'danger']
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
          <h3>官方回放深度拆解</h3>
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
          ${s1.replayId && S.stage2Status !== 'ready' ? `<button type="button" class="v2-btn ghost" id="hubRetryStage2Btn">重试拉取回放</button>` : ''}
          ${s2 ? `<button type="button" class="v2-btn primary" id="hubOpenFullReplayBtn">打开完整回放时间线</button>` : ''}
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
    syncRoomTabVisibility();
    renderGlobalRoomBar();
    renderHeader();
    renderLobby();
    renderRoomPanel();
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
        renderGlobalRoomBar();
        renderHeader();
        renderLobby();
        renderProfile();
        break;
      case 'hubAuthSuccess':
        S.twoFactorTicket = '';
        S.twoFactorUser = '';
        $id('hubAuthDialog')?.close('ok');
        break;
      case 'hubAuthDialogError': {
        const err = $id('hubAuthErr');
        if (err) err.textContent = msg.payload?.message || '认证失败，请重试';
        break;
      }
      case 'hubTwoFactorRequired':
        showAuthTwoFactorStep(msg.payload?.twoFactorTicket, msg.payload?.username);
        break;
      case 'hubSecurityState':
        S.security = msg.payload || null;
        if (S.security?.twoFactorEnabled) S.totpSetup = null;
        renderSecurityDialog();
        break;
      case 'hubTotpSetupState':
        S.totpSetup = msg.payload || null;
        renderSecurityDialog();
        break;
      case 'hubTotpEnabled':
      case 'hubTotpRecoveryCodes':
        S.recoveryCodes = msg.payload?.recoveryCodes || [];
        S.totpSetup = null;
        renderSecurityDialog();
        break;
      case 'hubMe':
        S.me = msg.payload?.me || null;
        S.localAccounts = msg.payload?.localAccounts || S.localAccounts;
        renderGlobalRoomBar();
        renderHeader();
        renderLobby();
        renderRoomPanel();
        renderProfile();
        break;
      case 'hubAccountLiveState': {
        const p = msg.payload || {};
        S.liveLoadingUid = p.loading ? (p.uid || '') : '';
        renderProfile();
        break;
      }
      case 'hubAccountLiveData': {
        const p = msg.payload || {};
        if (p.uid) {
          S.liveByUid[p.uid] = { friends: p.friends || [], capturedAt: p.capturedAt, region: p.region || 'cn' };
        }
        renderProfile();
        break;
      }
      case 'hubViewedProfile':
        S.viewedProfile = msg.payload || null;
        renderProfile();
        break;
      case 'hubInspectProfileResult': {
        const p = msg.payload || {};
        if (!S.inspectState) break;
        S.inspectState.loading = false;
        if (p.ok && p.profile) {
          S.inspectState.profile = p.profile;
          S.inspectState.error = '';
        } else {
          S.inspectState.profile = null;
          S.inspectState.error = p.error || '未能获取该玩家的公开档案。';
        }
        renderInspectDialog();
        break;
      }
      case 'hubBridgeSessions':
        S.bridge = msg.payload || S.bridge;
        renderGlobalRoomBar();
        renderHeader();
        renderBridgeDiag();
        if (S.currentRoom) renderRoomPanel();
        renderProfile();
        break;
      case 'hubAutoJoinChanged':
        S.autoJoinEnabled = !!msg.payload?.enabled;
        break;
      case 'hubRoomsList':
        S.rooms = msg.payload?.rooms || [];
        renderLobby();
        break;
      case 'hubRoomState': {
        const wasInRoom = !!S.currentRoom;
        S.currentRoom = msg.payload || null;
        syncRoomTabVisibility();
        renderGlobalRoomBar();
        renderLobby();
        renderRoomPanel();
        if (!wasInRoom && S.currentRoom) {
          selectTab('room', false);
        }
        break;
      }
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
        if (S.currentRoom) renderRoomPanel();
        break;
      }
      case 'hubRoomLeft':
        S.currentRoom = null;
        S.roomEvents = [];
        syncRoomTabVisibility();
        renderGlobalRoomBar();
        renderLobby();
        renderRoomPanel();
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

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', bindContextMenuOnce, { once: true });
  } else {
    bindContextMenuOnce();
  }

  window.HubModule = {
    onShow() {
      bindOnce();
      bindContextMenuOnce();
      if (!S.initialized) {
        renderAll();
      }
      post({ type: 'hubInit' });
    },
    selectTab,
    receive
  };
})();
