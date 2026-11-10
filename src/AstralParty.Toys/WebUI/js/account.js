(function () {
  'use strict';
  const t = key => window.i18n?.t(key) || key;
  const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const el = id => document.getElementById(id);
  const state = { visible: false, consent: false, consentChecked: false, privacyOpen: false, tutorialChecking: false, requestId: 0, availability: {}, uid: '', player: null, friends: null, stale: true, status: 'disconnected', diagnostic: '', capturedAt: null, cooldownTimer: null, bound: false };
  const active = () => ['connected', 'refreshing', 'connecting'].includes(state.status);
  const date = value => value ? new Date(value).toLocaleString(window.i18n?.currentLocale?.()) : '—';
  function clearData() { state.player = null; state.friends = null; state.capturedAt = null; state.stale = true; }
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
    el('accountOverview').hidden = !state.player;
    if (state.player) {
      const p = state.player;
      el('accountOverview').innerHTML = `<strong>${esc(p.name)}</strong><span>UID ${esc(p.uid)}</span><span>${esc(t('account.level'))} ${esc(p.level)}</span><small>${esc(t('account.updated'))} · ${esc(date(p.capturedAt))}</small>`;
    }
    renderCharacters(); renderFriends();
  }
  function renderCharacters() {
    const query = (el('accountCharacterSearch').value || '').trim().toLowerCase();
    const items = (state.player?.characters || []).filter(c => (!el('accountPlayedOnly').checked || c.fightCount > 0) && `${c.name} ${c.id}`.toLowerCase().includes(query));
    el('accountCharacters').innerHTML = items.length ? items.map(c => `<article class="account-character">${c.image && /^https:\/\/(assets|materials\d+)\.aspatoys\.hiynet\.localhost\//.test(c.image) ? `<img src="${esc(c.image)}" alt="" loading="lazy" />` : '<div class="account-avatar" aria-hidden="true"></div>'}<div><strong>${esc(c.name || c.id)}</strong><span><b>${c.fightCount == null ? '—' : esc(c.fightCount)}</b>${esc(t('account.count'))}</span></div></article>`).join('') : `<p class="account-empty">${esc(t(state.player ? 'account.empty' : 'account.noSelection'))}</p>`;
    window.Motion?.content(el('accountCharacters'), JSON.stringify([state.uid, state.player?.capturedAt, el('accountPlayedOnly').checked]));
  }
  function freshness() {
    const node = el('accountFriendFreshness');
    node.classList.toggle('account-stale', state.stale);
    el('accountRefreshBtn').disabled = state.status !== 'connected' || Date.now() - Date.parse(state.capturedAt) < 3000;
    node.textContent = `${state.stale ? t('account.stale') + ' ' : ''}${state.capturedAt ? t('account.updated') + ' · ' + date(state.capturedAt) : ''}`;
  }
  function renderFriends() {
    const query = (el('accountFriendSearch').value || '').trim().toLowerCase();
    const items = (state.friends || []).filter(f => (!el('accountOnlineOnly').checked || f.status !== 'offline') && `${f.name} ${f.note} ${f.uid}`.toLowerCase().includes(query));
    const statuses = new Set(['offline','online','busy','room','playing','unknown']);
    el('accountFriends').innerHTML = items.length ? items.map(f => {
      const status = statuses.has(f.status) ? f.status : 'unknown';
      const image = f.image && /^https:\/\/(assets|materials\d+)\.aspatoys\.hiynet\.localhost\//.test(f.image) ? `<img class="account-avatar" src="${esc(f.image)}" alt="" loading="lazy" />` : '<div class="account-avatar" aria-hidden="true"></div>';
      return `<article class="account-friend">${image}<div class="account-friend-main"><strong>${esc(f.name)}</strong>${f.note ? `<small>${esc(f.note)}</small>` : ''}<small>UID ${esc(f.uid)} · ${esc(t('account.level'))} ${esc(f.level)}</small>${status === 'offline' && f.offlineAt ? `<small>${esc(t('account.lastOffline'))} · ${esc(date(f.offlineAt))}</small>` : ''}</div><span class="account-badge ${status}">${esc(t('account.friend.' + status))}</span></article>`;
    }).join('') : `<p class="account-empty">${esc(t(state.friends ? 'account.empty' : 'account.noSelection'))}</p>`;
    window.Motion?.content(el('accountFriends'), JSON.stringify([state.uid, state.capturedAt, el('accountOnlineOnly').checked]));
    freshness();
  }
  function selectTab(friends) {
    for (const [id, selected] of [['accountCharacters', !friends], ['accountFriends', friends]]) {
      const entering = selected && el(id + 'Panel').hidden;
      el(id + 'Tab').setAttribute('aria-selected', String(selected)); el(id + 'Tab').tabIndex = selected ? 0 : -1; el(id + 'Panel').hidden = !selected;
      if (entering) window.Motion?.reveal(el(id + 'Panel'));
    }
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
    el('accountCharactersTab').onclick = () => selectTab(false);
    el('accountFriendsTab').onclick = () => selectTab(true);
    for (const id of ['accountCharactersTab','accountFriendsTab']) el(id).onkeydown = e => {
      if (!['ArrowLeft','ArrowRight','Home','End'].includes(e.key)) return;
      e.preventDefault(); const friends = e.key === 'End' || (e.key !== 'Home' && id === 'accountCharactersTab'); selectTab(friends); el(friends ? 'accountFriendsTab' : 'accountCharactersTab').focus();
    };
    ['accountCharacterSearch','accountPlayedOnly'].forEach(id => el(id).addEventListener('input', renderCharacters));
    ['accountFriendSearch','accountOnlineOnly'].forEach(id => el(id).addEventListener('input', renderFriends));
    window.addEventListener('appLanguageChanged', () => { if (state.visible) { render(); if (state.privacyOpen) openPrivacyModal(); else if (tutorialOpen()) openTutorial(false); post({ type: 'myAccountDiscover' }); } });
  }
  window.MyAccountModule = {
    // The timer only updates the manual-refresh button cooldown; it never requests friend data.
    onShow() { bind(); if (state.visible) return; state.visible = true; state.consentChecked = false; state.status = 'disconnected'; state.stale = true; render(); post({ type: 'myAccountEnter' }); state.cooldownTimer = setInterval(freshness, 1000); },
    onHide() { state.visible = false; closePrivacyModal(); if (el('accountDeleteConfirm') || tutorialOpen()) closeModal(); clearInterval(state.cooldownTimer); state.cooldownTimer = null; state.status = 'disconnected'; state.stale = true; post({ type: 'myAccountLeave', requestId: ++state.requestId }); },
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
        if (state.status === 'game_changed') { clearData(); state.uid = ''; post({ type: 'myAccountDiscover' }); }
        const reasons = new Set(['server_kick','server_rejected','resource_version','rpc_timeout','transport_closed','repeated_friend_page','missing_friend_end','friend_page_limit']);
        state.diagnostic = reasons.has(p.reason) ? ` [${p.reason}${Number.isInteger(p.command) ? ' · RPC ' + p.command : ''}${Number.isInteger(p.serverError) ? ' · #' + p.serverError : ''}]` : '';
        if (state.status === 'unavailable' && tutorialOpen()) { state.tutorialChecking = false; state.availability = {}; openTutorial(false); }
      }
      else if (message.type === 'myAccountPlayer') { if (p.uid !== state.uid || !active()) return; state.player = p; }
      else if (message.type === 'myAccountFriends') { if (p.uid !== state.uid || !active()) return; state.friends = p.friends || []; state.capturedAt = p.capturedAt; state.stale = false; }
      else if (message.type === 'myAccountMarket') {
        if (p.id) window.WorkshopModule?.openExternalMod({ id: p.id });
        else { toast(t('account.marketPending')); showPage('workshop'); }
      }
      render();
    }
  };
})();
