(function () {
  'use strict';
  const t = key => window.i18n?.t(key) || key;
  const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const imageUrl = value => typeof value === 'string' && /^https:\/\/(assets|materials\d+)\.aspatoys\.hiynet\.localhost\//.test(value) ? value : '';
  const el = id => document.getElementById(id);
  const state = { visible: false, consent: false, consentChecked: false, privacyOpen: false, tutorialChecking: false, requestId: 0, availability: {}, uid: '', player: null, stats: null, statsCapturedAt: null, details: {}, detailLoading: {}, friends: null, matches: null, matchesCapturedAt: null, matchesLoading: false, matchesFailed: false, matchesAttempt: 0, stale: true, status: 'disconnected', diagnostic: '', capturedAt: null, cooldownTimer: null, bound: false };
  const active = () => ['connected', 'refreshing', 'connecting'].includes(state.status);
  const date = value => fmtMinute(value);
  function clearData() { state.player = null; state.stats = null; state.statsCapturedAt = null; state.details = {}; state.detailLoading = {}; state.friends = null; state.matches = null; state.matchesCapturedAt = null; state.matchesLoading = false; state.matchesFailed = false; state.matchesAttempt = 0; state.capturedAt = null; state.stale = true; }
  function credentialLocation() {
    const root = state.availability.credentialRoot;
    return root ? `${root}\\${state.uid || '<UID>'}\\account.json` : t('account.credentialPath');
  }
  function closePrivacyModal() {
    state.privacyOpen = false;
    const modal = el('globalModal');
    if (!el('modalBody')?.querySelector('.account-consent')) return;
    modal._onClose = null;
    closeModal();
  }
  function openPrivacyModal() {
    state.privacyOpen = true;
    openModal(t('account.privacy'), `
      <div class="workshop-consent account-consent" data-i18n-skip>
        <p class="workshop-consent-warn">${esc(t('account.warning'))}</p>
        <p class="workshop-consent-lead">${esc(t('account.safety'))}</p>
        <ul class="workshop-consent-points"><li>${esc(t('account.direct'))}</li><li>${esc(t('account.conflict'))}</li></ul>
        <p class="workshop-consent-url">${esc(t('account.savedLocation'))}</p>
        <p class="workshop-consent-url account-consent-path">${esc(credentialLocation())}</p>
        <p class="workshop-consent-url-note">${esc(t('account.exportLocationNote'))}</p>
        <div class="workshop-consent-actions">
          <button class="secondary-btn" id="accountDeclineBtn">${esc(t('account.decline'))}</button>
          <button class="primary-btn" id="accountAcceptBtn">${esc(t(state.consent ? 'common.close' : 'account.accept'))}</button>
        </div>
      </div>`, 'warning', 'modal-wide');
    el('globalModal')._onClose = () => {
      state.privacyOpen = false;
      if (!state.consent && state.visible) showPage('home');
    };
    el('accountDeclineBtn').onclick = () => closeModal();
    el('accountAcceptBtn').onclick = () => {
      if (state.consent) { closePrivacyModal(); return; }
      el('accountAcceptBtn').disabled = true;
      post({ type: 'myAccountAccept' });
    };
  }
  const tutorialOpen = () => el('globalModal')?.classList.contains('open') && !!el('accountTutorial');
  function openTutorial(refresh = true) {
    state.privacyOpen = false;
    if (refresh) state.tutorialChecking = true;
    const a = state.availability;
    const known = state.consentChecked && state.consent && a.gameFound && !state.tutorialChecking;
    const unknown = state.tutorialChecking ? 'account.checking' : 'account.unknown';
    const installStatus = known ? t(a.installed ? 'account.installed' : 'account.notInstalled') : t(unknown);
    const enabledStatus = known ? t(!a.installed ? 'account.notApplicable' : a.enabled ? 'account.enabled' : 'account.notEnabled') : t(unknown);
    const action = !state.consent ? 'privacy' : !a.gameFound ? 'settings' : !a.installed ? 'install' : 'mods';
    const labels = { privacy: 'account.privacy', settings: 'nav.settings', install: 'account.install', mods: 'account.enable' };
    openModal(t('account.tutorial'), `<div id="accountTutorial" class="account-tutorial" data-i18n-skip>
      <div class="account-tutorial-status" role="status" aria-live="polite">
        <p><span>${esc(t('account.modName'))}</span><strong>${esc(t('account.installStatus'))} · ${esc(installStatus)}</strong></p>
        <p><span>${esc(t('account.enableStatus'))}</span><strong>${esc(enabledStatus)}</strong></p>
      </div>
      ${!state.consent ? `<p>${esc(t('account.tutorialConsent'))}</p>` : !a.gameFound ? `<p>${esc(t('account.noGame'))}</p>` : ''}
      <ol>${['account.tutorialInstall','account.tutorialEnable','account.tutorialExport','account.tutorialConnect'].map(key => `<li>${esc(t(key))}</li>`).join('')}</ol>
      <p class="account-tutorial-path">${esc(t('account.savedLocation'))}<br>${esc(credentialLocation())}</p>
      <div class="account-tutorial-actions">
        <button class="secondary-btn" id="accountTutorialRefresh" ${state.tutorialChecking ? 'disabled' : ''}>${esc(t('account.scan'))}</button>
        <button class="primary-btn" id="accountTutorialAction" ${state.tutorialChecking ? 'disabled' : ''}>${esc(t(labels[action]))}</button>
        <button class="secondary-btn" id="accountTutorialClose">${esc(t('common.close'))}</button>
      </div>
    </div>`, 'help', 'modal-wide');
    el('accountTutorialClose').onclick = () => closeModal();
    el('accountTutorialRefresh').onclick = () => openTutorial();
    el('accountTutorialAction').onclick = () => {
      closeModal();
      if (action === 'privacy') openPrivacyModal();
      else if (action === 'install') post({ type: 'myAccountInstallMod' });
      else showPage(action);
    };
    if (refresh) post({ type: 'myAccountDiscover' });
  }
  function render() {
    el('accountSafety').hidden = state.consent;
    el('accountContent').hidden = !state.consent;
    if (!state.consent) return;
    const a = state.availability;
    el('accountCredentialPath').textContent = `${t('account.savedLocation')} ${credentialLocation()}`;
    let reason = !a.gameFound ? 'noGame' : !a.installed ? 'noMod' : !(a.accounts || []).length ? (!a.enabled ? 'disabledMod' : 'noCredentials') : '';
    el('accountPrerequisite').textContent = reason ? t('account.' + reason) : '';
    el('accountInstallBtn').hidden = reason !== 'noMod';
    el('accountModsBtn').hidden = reason !== 'disabledMod';
    el('accountConnectBtn').disabled = !state.uid || a.gameRunning || active() || !a.gameFound || !a.installed;
    el('accountDisconnectBtn').disabled = !active();
    el('accountDeleteBtn').disabled = !state.uid;
    el('accountScanBtn').disabled = active();
    el('accountRefreshBtn').disabled = state.status !== 'connected';
    el('accountConnectionStatus').textContent = t('account.status.' + state.status) + state.diagnostic;
    renderOverview(); renderStats();
    renderCharacters(); renderFriends(); renderRecent();
  }
  function renderOverview() {
    const node = el('accountOverview');
    const p = state.player;
    node.hidden = !p;
    if (!p) return;
    const need = Number(p.expNeed) || 0, exp = Number(p.exp) || 0;
    const percent = need > 0 ? Math.max(0, Math.min(100, exp / need * 100)) : 0;
    const privacy = p.privacy || {};
    const flag = value => t(value ? 'account.privacyYes' : 'account.privacyNo');
    node.innerHTML = `<div class="account-profile-main">
      <div class="account-profile-title"><strong>${esc(p.name)}</strong><span class="account-uid">UID ${esc(p.uid)}</span></div>
      <div class="account-profile-level"><span>${esc(t('account.level'))} ${esc(p.level)}</span>${need > 0 ? `<div class="account-exp" role="img" aria-label="${esc(exp + ' / ' + need)}"><i style="width:${percent.toFixed(1)}%"></i></div><small>${esc(exp)} / ${esc(need)}</small>` : `<small>${esc(exp)} EXP</small>`}</div>
    </div>
    <div class="account-profile-meta">
      <span class="account-badge">${esc(t('account.privacyStats'))} · ${esc(flag(privacy.stats))}</span>
      <span class="account-badge">${esc(t('account.privacyMatches'))} · ${esc(flag(privacy.matches))}</span>
    </div>`;
  }
  function renderStats() {
    const node = el('accountStats');
    const p = state.player;
    node.hidden = !p;
    if (!p) return;
    const s = state.stats?.statistics || null;
    const characters = p.characters || [];
    const top = [...characters].sort((a, b) => (Number(b.fightCount) || 0) - (Number(a.fightCount) || 0))[0];
    const fights = s?.fightCount ?? null, wins = s?.winCount ?? p.winCount ?? null;
    const winRate = fights > 0 && wins != null ? `${(wins * 100 / fights).toFixed(1)}%` : '—';
    const value = (label, data, sub) => `<article class="account-stat"><span>${esc(label)}</span><strong>${esc(data)}</strong>${sub ? `<small>${esc(sub)}</small>` : ''}</article>`;
    const maps = p.winMaps || [];
    el('accountStatGrid').innerHTML = [
      value(t('account.totalFights'), fights ?? '—'),
      value(t('account.wins'), wins ?? '—'),
      value(t('account.winRate'), winRate),
      value(t('account.praise'), state.stats?.praiseNum ?? p.praiseNum ?? '—'),
      value(t('account.roleCards'), s?.roleCardCount ?? characters.length),
      value(t('account.skins'), s?.skinCount ?? '—'),
      value(t('account.adorns'), s?.adornCount ?? '—'),
      value(t('account.clearedMaps'), maps.length),
      value(t('account.mostUsed'), s?.useHeroName || top?.name || '—'),
      value(t('account.credit'), p.creditScore ?? '—'),
      value(t('account.createdAt'), date(p.createdAt)),
      value(t('account.lastLogin'), date(p.lastLoginAt))
    ].join('');
    el('accountStatsFreshness').textContent = state.statsCapturedAt ? `${t('account.updated')} · ${date(state.statsCapturedAt)}` : '';
    const achievements = (state.stats?.achievements || p.achievements || []).filter(a => a && a.name);
    el('accountAchievements').innerHTML = `<span class="account-section-label">${esc(t('account.achievements'))}</span>${achievements.length ? `<div class="account-chips">${achievements.map(a => `<span class="account-chip">${esc(a.name)}</span>`).join('')}</div>` : `<span class="account-muted">${esc(t('account.noAchievements'))}</span>`}`;
    el('accountWinMaps').innerHTML = `<span class="account-section-label">${esc(t('account.clearedMaps'))} · ${esc(maps.length)}</span>${maps.length ? `<div class="account-chips">${maps.map(m => `<span class="account-chip">${imageUrl(m.image) ? `<img class="account-map-thumb" src="${esc(m.image)}" alt="" loading="lazy" />` : ''}${esc(m.name || t('account.unknownMap'))}<b>${esc(m.wins)}</b><small>${esc(t('account.wins'))}</small></span>`).join('')}</div>` : `<span class="account-muted">${esc(t('account.noStats'))}</span>`}`;
  }
  function renderCharacters() {
    const query = (el('accountCharacterSearch').value || '').trim().toLowerCase();
    const items = (state.player?.characters || []).filter(c => (!el('accountPlayedOnly').checked || c.fightCount > 0) && `${c.name} ${c.id}`.toLowerCase().includes(query));
    el('accountCharacters').innerHTML = items.length ? items.map(c => `<article class="account-character">${imageUrl(c.image) ? `<img src="${esc(c.image)}" alt="" loading="lazy" />` : '<div class="account-avatar" aria-hidden="true"></div>'}<div><strong>${esc(c.name || c.id)}</strong><span>${c.level > 0 ? `<em class="account-tag">Lv.${esc(c.level)}</em>` : ''}${c.breakthrough ? `<em class="account-tag breakthrough">${esc(t('account.breakthrough'))}</em>` : ''}<b>${c.fightCount == null ? '—' : esc(c.fightCount)}</b>${esc(t('account.count'))}</span></div></article>`).join('') : `<p class="account-empty">${esc(t(state.player ? 'account.empty' : 'account.noSelection'))}</p>`;
    window.Motion?.content(el('accountCharacters'), JSON.stringify([state.uid, state.player?.capturedAt, el('accountPlayedOnly').checked]));
  }
  function freshness() {
    const node = el('accountFriendFreshness');
    node.classList.toggle('account-stale', state.stale);
    el('accountRefreshBtn').disabled = state.status !== 'connected' || Date.now() - Date.parse(state.capturedAt) < 3000;
    node.textContent = `${state.stale ? t('account.stale') + ' ' : ''}${state.capturedAt ? t('account.updated') + ' · ' + date(state.capturedAt) : ''}`;
    el('accountRecentRefreshBtn').disabled = state.status !== 'connected' || state.matchesLoading || Date.now() - state.matchesAttempt < 3000;
    el('accountStatsRefreshBtn').disabled = el('accountRecentRefreshBtn').disabled;
    const recentNode = el('accountRecentFreshness');
    recentNode.classList.toggle('account-stale', !active() || state.matchesFailed);
    recentNode.textContent = [state.matchesLoading ? t('account.loading') : state.matchesFailed ? t('account.recentFailed') : '',
      !active() && state.matchesCapturedAt ? t('account.stale') : '',
      state.matchesCapturedAt ? t('account.updated') + ' · ' + date(state.matchesCapturedAt) : ''].filter(Boolean).join(' · ');
  }
  function renderFriends() {
    const query = (el('accountFriendSearch').value || '').trim().toLowerCase();
    const items = (state.friends || []).filter(f => (!el('accountOnlineOnly').checked || f.status !== 'offline') && `${f.name} ${f.note} ${f.uid}`.toLowerCase().includes(query));
    const statuses = new Set(['offline','online','busy','room','playing','unknown']);
    el('accountFriends').innerHTML = items.length ? items.map(f => {
      const status = statuses.has(f.status) ? f.status : 'unknown';
      const image = imageUrl(f.image) ? `<img class="account-avatar" src="${esc(f.image)}" alt="" loading="lazy" />` : '<div class="account-avatar" aria-hidden="true"></div>';
      return `<article class="account-friend">${image}<div class="account-friend-main"><strong>${esc(f.name)}</strong>${f.note ? `<small>${esc(f.note)}</small>` : ''}<small>UID ${esc(f.uid)} · ${esc(t('account.level'))} ${esc(f.level)}</small>${status === 'offline' && f.offlineAt ? `<small>${esc(t('account.lastOffline'))} · ${esc(date(f.offlineAt))}</small>` : ''}</div><span class="account-badge ${status}">${esc(t('account.friend.' + status))}</span></article>`;
    }).join('') : `<p class="account-empty">${esc(t(state.friends ? 'account.empty' : 'account.noSelection'))}</p>`;
    window.Motion?.content(el('accountFriends'), JSON.stringify([state.uid, state.capturedAt, el('accountOnlineOnly').checked]));
    freshness();
  }
  function matchDetailKey(m) { return m.replayId || 'i' + m.index; }
  function renderMatchDetail(m) {
    const key = matchDetailKey(m);
    const detail = state.details[key];
    if (state.detailLoading[key]) return `<div class="account-match-detail"><p class="account-muted">${esc(t('account.matchDetailLoading'))}</p></div>`;
    if (!detail) return '';
    if (detail.failed) return `<div class="account-match-detail"><p class="account-muted">${esc(t('account.matchDetailFailed'))}</p></div>`;
    const players = detail.players || [];
    if (!players.length) return `<div class="account-match-detail"><p class="account-muted">${esc(t('account.noStats'))}</p></div>`;
    const rows = players.map(p => `<tr class="${p.isSelf ? 'self' : ''}"><td>${p.rank > 0 ? esc(p.rank) : '—'}</td><td><div class="account-detail-player">${imageUrl(p.image) ? `<img class="account-detail-avatar" src="${esc(p.image)}" alt="" loading="lazy" />` : '<span class="account-detail-avatar placeholder" aria-hidden="true"></span>'}<div><strong>${esc(p.name || p.playerId)}</strong>${p.isSelf ? ` <em class="account-tag">${esc(t('account.self'))}</em>` : ''}<small>${esc(t('account.level'))} ${esc(p.playerLevel ?? '—')}</small></div></div></td><td>${esc(p.character || p.heroId)}</td><td>${esc(p.level)}</td><td>${esc(p.gold)}</td><td>${p.gaveUp ? esc(t('account.gaveUp')) : '—'}</td></tr>`).join('');
    return `<div class="account-match-detail"><table class="account-detail-table"><thead><tr><th>${esc(t('account.rankInMatch'))}</th><th>${esc(t('account.matchPlayers'))}</th><th>${esc(t('account.character'))}</th><th>${esc(t('account.matchLevel'))}</th><th>${esc(t('account.gold'))}</th><th>${esc(t('account.result'))}</th></tr></thead><tbody>${rows}</tbody></table></div>`;
  }
  function renderRecent() {
    el('accountRecent').innerHTML = state.matches?.length ? state.matches.map(m => {
      const downloadable = m.canDownload === true && /^\d{1,32}$/.test(m.replayId);
      const mode = Number.isInteger(m.mapType) && m.mapType >= 1 && m.mapType <= 12 ? t('account.mode.' + m.mapType) : `${t('account.mode')} ${m.mapType ?? '—'}`;
      const detail = state.details[matchDetailKey(m)];
      const detailOpen = !!detail && !detail.failed;
      return `<article class="account-match"><div class="account-match-main"><strong data-account-map="${esc(m.replayId)}">${imageUrl(m.mapImage) ? `<img class="account-match-map" src="${esc(m.mapImage)}" alt="" loading="lazy" />` : ''}<span>${esc(m.mapName || t(m.mapLoading ? 'account.mapLoading' : 'account.unknownMap'))}</span></strong><span>${esc(m.character)} · ${esc(t('account.rank'))} ${m.rank > 0 ? esc(m.rank) : '—'}</span><small>${esc(mode)} · ${esc(date(m.playedAt))}${m.version ? ' · ' + esc(m.version) : ''}</small><small class="account-replay-id">${esc(t('account.replayId'))} ${esc(m.replayId || '—')}</small></div><div class="account-match-actions">${downloadable ? `<button class="v2-btn" data-account-replay="${esc(m.replayId)}">${esc(t('account.download'))}</button>` : `<span class="account-muted">${esc(t('account.noReplay'))}</span>`}<button class="v2-btn account-detail-btn" data-account-detail="${esc(matchDetailKey(m))}" data-account-detail-index="${esc(m.index)}" data-account-detail-replay="${esc(m.replayId || '')}">${esc(t(detailOpen ? 'account.matchDetailHide' : 'account.matchDetail'))}</button></div>${renderMatchDetail(m)}</article>`;
    }).join('') : `<p class="account-empty">${esc(t(state.matchesLoading ? 'account.loading' : state.matches ? 'account.noRecent' : 'account.noSelection'))}</p>`;
    window.Motion?.content(el('accountRecent'), JSON.stringify([state.uid, state.matchesCapturedAt]));
    freshness();
  }
  const tabs = ['accountCharacters', 'accountFriends', 'accountRecent'];
  function selectTab(tab) {
    for (const id of tabs) {
      const selected = id === tab;
      const entering = selected && el(id + 'Panel').hidden;
      el(id + 'Tab').setAttribute('aria-selected', String(selected)); el(id + 'Tab').tabIndex = selected ? 0 : -1; el(id + 'Panel').hidden = !selected;
      if (entering) window.Motion?.reveal(el(id + 'Panel'));
    }
  }
  function refreshMatches() {
    if (el('accountRecentRefreshBtn').disabled) return;
    state.matchesAttempt = Date.now(); state.matchesLoading = true; state.matchesFailed = false; renderRecent();
    post({ type: 'myAccountRecentRefresh' });
  }
  function bind() {
    if (state.bound) return;
    state.bound = true;
    el('accountReviewBtn').onclick = openPrivacyModal;
    el('accountPrivacyBtn').onclick = openPrivacyModal;
    el('accountTutorialBtn').onclick = () => openTutorial();
    el('accountScanBtn').onclick = () => post({ type: 'myAccountDiscover' });
    el('accountUid').onchange = () => { post({ type: 'myAccountDisconnect', requestId: ++state.requestId }); state.uid = el('accountUid').value; state.status = 'disconnected'; clearData(); render(); };
    el('accountConnectBtn').onclick = () => { if (!state.uid || active()) return; clearData(); state.status = 'connecting'; render(); post({ type: 'myAccountConnect', uid: state.uid, requestId: ++state.requestId }); };
    el('accountDisconnectBtn').onclick = () => post({ type: 'myAccountDisconnect', requestId: ++state.requestId });
    el('accountDeleteBtn').onclick = () => { if (state.uid) post({ type: 'myAccountDeleteRequest', uid: state.uid }); };
    el('accountRefreshBtn').onclick = () => post({ type: 'myAccountRefresh' });
    el('accountInstallBtn').onclick = () => post({ type: 'myAccountInstallMod' });
    el('accountModsBtn').onclick = () => showPage('mods');
    el('accountRecentRefreshBtn').onclick = refreshMatches;
    el('accountStatsRefreshBtn').onclick = refreshMatches;
    el('accountRecent').onclick = e => {
      const detailButton = e.target.closest('[data-account-detail]');
      if (detailButton) {
        const key = detailButton.dataset.accountDetail;
        const index = Number(detailButton.dataset.accountDetailIndex);
        const replayId = detailButton.dataset.accountDetailReplay || '';
        if (state.details[key] && !state.details[key].failed) { delete state.details[key]; renderRecent(); return; }
        if (state.detailLoading[key] || !Number.isInteger(index) || index < 0 || !active()) return;
        state.detailLoading[key] = true;
        renderRecent();
        post({ type: 'myAccountMatchDetail', index, replayId });
        return;
      }
      const id = e.target.closest('[data-account-replay]')?.dataset.accountReplay;
      if (id && state.matches?.some(m => m.replayId === id && m.canDownload) && /^\d{1,32}$/.test(id))
        window.ReplayModule?.openDownloadDialog({ replayId: id, cdn: 'CnProd', autoStart: true });
    };
    for (const id of tabs) el(id + 'Tab').onclick = () => selectTab(id);
    for (const id of tabs) el(id + 'Tab').onkeydown = e => {
      if (!['ArrowLeft','ArrowRight','Home','End'].includes(e.key)) return;
      e.preventDefault();
      const index = e.key === 'Home' ? 0 : e.key === 'End' ? tabs.length - 1 : (tabs.indexOf(id) + (e.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length;
      selectTab(tabs[index]); el(tabs[index] + 'Tab').focus();
    };
    ['accountCharacterSearch','accountPlayedOnly'].forEach(id => el(id).addEventListener('input', renderCharacters));
    ['accountFriendSearch','accountOnlineOnly'].forEach(id => el(id).addEventListener('input', renderFriends));
    window.addEventListener('appLanguageChanged', () => { if (state.visible) { render(); if (state.privacyOpen) openPrivacyModal(); else if (tutorialOpen()) openTutorial(false); post({ type: 'myAccountDiscover' }); } });
  }
  window.MyAccountModule = {
    // The timer only updates the manual-refresh button cooldown; it never requests friend data.
    onShow() { bind(); if (state.visible) return; state.visible = true; state.consentChecked = false; state.status = 'disconnected'; state.stale = true; render(); post({ type: 'myAccountEnter' }); state.cooldownTimer = setInterval(freshness, 1000); },
    onHide() { state.visible = false; closePrivacyModal(); if (el('accountDeleteConfirm') || tutorialOpen()) closeModal(); clearInterval(state.cooldownTimer); state.cooldownTimer = null; state.status = 'disconnected'; state.matchesLoading = false; state.stale = true; post({ type: 'myAccountLeave', requestId: ++state.requestId }); },
    receive(message) {
      if (!state.visible) return;
      const p = message.payload || {};
      if (Number.isInteger(p.requestId) && p.requestId !== state.requestId) return;
      if (message.type === 'myAccountDeleteConfirm') {
        if (p.uid !== state.uid) { post({ type: 'myAccountDeleteCancel' }); return; }
        openModal(t('account.delete'), `<div id="accountDeleteConfirm" class="workshop-consent" data-i18n-skip>
          <p class="workshop-consent-warn">${esc(t('account.deleteConfirm'))}</p>
          <p class="workshop-consent-url">UID ${esc(p.uid)}</p>
          <p class="workshop-consent-url account-consent-path">${esc(p.path)}</p>
          <p class="workshop-consent-lead">${esc(t('account.deleteScope'))}</p>
          <p class="workshop-consent-url-note">${esc(t('account.deleteRegenerate'))}</p>
          <div class="workshop-consent-actions"><button class="secondary-btn" id="accountDeleteCancelBtn">${esc(t('common.cancel'))}</button><button class="danger-btn" id="accountDeleteAgreeBtn">${esc(t('account.delete'))}</button></div>
        </div>`, 'warning', 'modal-wide');
        el('globalModal')._onClose = () => post({ type: 'myAccountDeleteCancel' });
        el('accountDeleteCancelBtn').onclick = () => closeModal();
        el('accountDeleteAgreeBtn').onclick = () => {
          el('globalModal')._onClose = null;
          closeModal();
          post({ type: 'myAccountDelete', token: p.token, requestId: ++state.requestId });
        };
        el('accountDeleteCancelBtn').focus();
        return;
      }
      if (message.type === 'myAccountDeleteResult') {
        if (p.success && p.uid === state.uid) { clearData(); state.uid = ''; state.status = 'disconnected'; }
        toast(t(p.success ? 'account.deleteSuccess' : 'account.deleteFailed'));
        render();
        return;
      }
      if (message.type === 'myAccountAvailability') {
        state.tutorialChecking = false;
        const firstCheck = !state.consentChecked;
        state.consentChecked = true;
        state.consent = p.consent === true; state.availability = p;
        if (state.consent) closePrivacyModal();
        else if (firstCheck) openPrivacyModal();
        const accounts = Array.isArray(p.accounts) ? p.accounts : [];
        if (!accounts.includes(state.uid)) { if (state.uid) clearData(); state.uid = ''; }
        el('accountUid').innerHTML = `<option value="">${esc(t('account.select'))}</option>` + accounts.map(uid => `<option value="${esc(uid)}">${esc(uid)}</option>`).join('');
        el('accountUid').value = state.uid;
        if (p.gameRunning && !active()) state.status = 'game_running';
        if (tutorialOpen()) openTutorial(false);
      } else if (message.type === 'myAccountState') {
        state.status = p.status || 'disconnected'; if (typeof p.stale === 'boolean') state.stale = p.stale;
        if (!active()) state.matchesLoading = false;
        if (state.status === 'game_changed') { clearData(); state.uid = ''; post({ type: 'myAccountDiscover' }); }
        const reasons = new Set(['server_kick','server_rejected','resource_version','rpc_timeout','transport_closed','repeated_friend_page','missing_friend_end','friend_page_limit']);
        state.diagnostic = reasons.has(p.reason) ? ` [${p.reason}${Number.isInteger(p.command) ? ' · RPC ' + p.command : ''}${Number.isInteger(p.serverError) ? ' · #' + p.serverError : ''}]` : '';
        if (state.status === 'unavailable' && tutorialOpen()) { state.tutorialChecking = false; state.availability = {}; openTutorial(false); }
      }
      else if (message.type === 'myAccountPlayer') { if (p.uid !== state.uid || !active()) return; state.player = p; }
      else if (message.type === 'myAccountStats') { if (p.uid !== state.uid || !active()) return; state.stats = p; state.statsCapturedAt = p.capturedAt; }
      else if (message.type === 'myAccountFriends') { if (p.uid !== state.uid || !active()) return; state.friends = p.friends || []; state.capturedAt = p.capturedAt; state.stale = false; }
      else if (message.type === 'myAccountRecent') { if (p.uid !== state.uid || !active()) return; state.matches = p.matches || []; state.matchesCapturedAt = p.capturedAt; state.matchesFailed = false; }
      else if (message.type === 'myAccountRecentMap') {
        if (p.uid !== state.uid || !active()) return;
        for (const match of state.matches || []) if (match.replayId === p.replayId) { match.mapName = p.mapName; match.mapImage = p.mapImage; match.mapLoading = false; }
        renderRecent();
        return;
      }
      else if (message.type === 'myAccountMatchDetail') {
        if (p.uid !== state.uid || !active()) return;
        const key = p.replayId || ('i' + p.index);
        delete state.detailLoading[key];
        state.details[key] = p.failed ? { failed: true } : { players: p.players || [] };
      }
      else if (message.type === 'myAccountRecentState') {
        if (!active()) return;
        state.matchesLoading = p.loading === true;
        if (p.loading) { state.matchesAttempt = Date.now(); state.matchesFailed = false; }
        if (p.failed) state.matchesFailed = true;
      }
      else if (message.type === 'myAccountMarket') {
        if (p.id) window.WorkshopModule?.openExternalMod({ id: p.id });
        else { toast(t('account.marketPending')); showPage('workshop'); }
      }
      render();
    }
  };
})();
