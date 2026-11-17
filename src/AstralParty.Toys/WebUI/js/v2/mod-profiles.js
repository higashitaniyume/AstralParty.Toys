// v2/mod-profiles.js — 模组方案（全局共享）+ 历史与回滚
//
// 由宿主推到 modProfiles 的列表渲染两处：
//   · 模组页的「模组方案」卡片（保存 / 更新 / 管理 / 历史）；
//   · 概览页的「模组方案」一行（选中即套用）。
// 套用前先向宿主请求差异预览（modProfilePlan），确认后再发 modProfileApply。
(function () {
  'use strict';

  const S = {
    data: null,        // modProfiles payload
    plan: null,        // 最近一次差异预览
    pendingName: '',   // 等待预览结果的方案名
    history: null,     // 历史列表 payload
    busy: false
  };

  const U = () => window.V2UI;
  const I = () => window.V2UI.ICON;

  const defaultRe = /^[\p{L}\p{N} _-]{1,32}$/u;
  function nameRule() {
    const d = S.data || {};
    const maxLength = Number(d.nameMaxLength) || 32;
    let re = defaultRe;
    if (d.namePattern) {
      try { re = new RegExp(d.namePattern, 'u'); } catch { re = defaultRe; }
    }
    return { re, maxLength };
  }

  const profileByName = name => (S.data?.profiles || []).find(p => p.name === name) || null;
  const activeProfile = () => (S.data?.profiles || []).find(p => p.active) || null;

  function fmtTime(iso) {
    const t = Date.parse(iso || '');
    if (!Number.isFinite(t)) return '';
    const d = new Date(t), pad = n => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  function refresh() {
    try { window.V2ModsModule?.render?.(); } catch { /* 模组页不在 */ }
    try { window.V2Home?.render?.(); } catch { /* 概览不在 */ }
  }

  /* ---------- 模组页卡片 ---------- */

  function card() {
    const d = S.data;
    const profiles = d?.profiles || [];
    const active = activeProfile();
    const disabled = S.busy ? 'disabled' : '';

    const options = profiles.length
      ? ['<option value=""' + (active ? '' : ' selected') + '>不使用方案（保持当前）</option>']
          .concat(profiles.map(p => {
            const suffix = (p.active && p.dirty ? '（已修改）' : '') + (p.missingCount ? `（缺 ${p.missingCount} 个模组）` : '');
            return `<option value="${esc(p.name)}"${p.active ? ' selected' : ''}>${esc(p.name)}${esc(suffix)}</option>`;
          }))
          .join('')
      : '<option value="">还没有保存方案</option>';

    const badges = [];
    if (active) {
      badges.push(active.dirty
        ? '<span class="v2-badge warn" title="当前磁盘状态和这个方案不一致">已修改</span>'
        : '<span class="v2-badge" title="当前状态就是这个方案">已应用</span>');
      if (active.missingCount) {
        badges.push(`<span class="v2-badge warn" title="方案里有 ${active.missingCount} 个模组当前没装，套用时会跳过">缺 ${active.missingCount} 个模组</span>`);
      }
    }

    let hint;
    if (!d) hint = '<span class="v2-muted">正在读取…</span>';
    else if (d.error) hint = `<span style="color:var(--gp-gold)">${esc(d.error)}</span>`;
    else if (!profiles.length) hint = '<span class="v2-muted">把当前启用状态、每个模组的配置和加载器设置存成一套，随时一键切换。</span>';
    else if (active) hint = active.note
      ? esc(active.note)
      : `<span class="v2-muted">${active.dirty ? '当前状态和方案不一致：重新套用，或用「更新当前方案」保存现在的状态。' : '当前状态与方案一致。'}</span>`;
    else hint = '<span class="v2-muted">当前没有使用方案。</span>';

    return `<div class="v2-card" id="v2mProfiles">
      <div class="v2-row"><span class="k">模组方案</span>
        <span class="v">${active ? `<b>${esc(active.name)}</b> ${badges.join(' ')}<br>${hint}` : hint}</span>
        <select id="v2mpSelect" class="v2-select" aria-label="选择模组方案" ${!profiles.length || S.busy ? 'disabled' : ''}>${options}</select>
        <button class="v2-btn line" data-v2mp="capture" ${d && !S.busy && !d.error ? '' : 'disabled'} title="把当前启用状态、模组配置和加载器设置保存成一套">保存当前为新方案…</button>
        <button class="v2-btn ghost" data-v2mp="manage" ${profiles.length ? '' : 'disabled'}>管理…</button>
      </div>
      <div class="v2-row"><span class="k">方案操作</span>
        <span class="v">${active
          ? '<span class="v2-muted">「更新」会把当前状态写回方案；「历史与回滚」可以撤销最近的操作。</span>'
          : '<span class="v2-muted">套用方案会启用 / 停用模组、改写模组配置和加载器设置，重启游戏后生效。</span>'}</span>
        <button class="v2-btn line" data-v2mp="update" ${active && !S.busy ? '' : 'disabled'}>更新当前方案</button>
        <button class="v2-btn ghost" data-v2mp="history">历史与回滚</button>
      </div>
    </div>`;
  }

  /* ---------- 保存 / 更新 ---------- */

  function openCapture() {
    const rule = nameRule();
    U().openModal(`<div class="v2-modal-head"><b>保存当前为新方案</b><button class="v2-icon-btn" data-v2modal="close">${I().x}</button></div>
      <div style="padding:6px 18px 4px;display:grid;gap:12px">
        <label style="display:grid;gap:6px"><span>方案名称</span>
          <input id="v2mpName" class="v2-input" maxlength="${rule.maxLength}" spellcheck="false" autocomplete="off" placeholder="例如：PVE 日常"></label>
        <div id="v2mpNameHint" class="v2-muted" style="font-size:12px">方案名可以用中文、数字、空格、下划线和连字符，最多 ${rule.maxLength} 个字符。</div>
        <label style="display:grid;gap:6px"><span>备注（可选）</span>
          <input id="v2mpNote" class="v2-input" maxlength="${Number(S.data?.maxNoteLength) || 256}" spellcheck="false" placeholder="例如：只开胜率助手和面板"></label>
        <label style="display:flex;gap:8px;align-items:center"><input type="checkbox" id="v2mpCfg" checked> 包含每个模组的配置文件</label>
        <label style="display:flex;gap:8px;align-items:center"><input type="checkbox" id="v2mpLdr" checked> 包含加载器设置（倍速、控制台、绕过、日志等）</label>
        <p class="v2-muted" style="margin:0;font-size:12px;line-height:1.6">方案不包含模组本体（DLL / 资源）；换到别的游戏目录时，缺的模组会被跳过。保存后它会成为当前方案。</p>
      </div>
      <div class="v2-modal-foot"><button class="v2-btn ghost" data-v2modal="close">取消</button><span class="v2-top-spacer"></span>
        <button class="v2-btn primary" data-v2modal="v2mpSave" id="v2mpSaveBtn" disabled>保存</button></div>`, 'narrow');

    const nameInput = $('v2mpName'), hint = $('v2mpNameHint'), save = $('v2mpSaveBtn');
    const validate = () => {
      const name = (nameInput.value || '').trim();
      let error = '';
      if (!name) error = '';
      else if (name.length > rule.maxLength) error = `方案名称最多 ${rule.maxLength} 个字符。`;
      else if (!rule.re.test(name)) error = '方案名称只能用文字、数字、空格、下划线和连字符。';
      else if (profileByName(name)) error = `已经有一个叫「${name}」的方案了。`;
      hint.textContent = error || (name ? '名称可用。' : `方案名可以用中文、数字、空格、下划线和连字符，最多 ${rule.maxLength} 个字符。`);
      hint.style.color = error ? 'var(--gp-gold)' : '';
      save.disabled = !name || !!error;
      return !error && !!name;
    };
    nameInput.addEventListener('input', validate);
    nameInput.addEventListener('keydown', e => { if (e.key === 'Enter' && validate()) U().modalActions.v2mpSave?.(); });
    U().modalActions.v2mpSave = () => {
      if (!validate()) return;
      const name = (nameInput.value || '').trim();
      const note = ($('v2mpNote')?.value || '').trim();
      const includeConfigs = !!$('v2mpCfg')?.checked;
      const includeLoader = !!$('v2mpLdr')?.checked;
      U().closeModal();
      post({ type: 'modProfileCapture', name, note, includeConfigs, includeLoader });
    };
    nameInput.focus();
  }

  function confirmUpdate(profile) {
    U().modalActions.v2mpUpdate = () => {
      U().closeModal();
      post({ type: 'modProfileUpdate', name: profile.name, includeConfigs: true, includeLoader: true });
    };
    U().openModal(`<div class="v2-confirm"><div class="v2-confirm-icon" style="background:rgba(56,189,248,.12);color:var(--gp-blue)">${I().sliders}</div>
      <div><b class="v2-confirm-title">用当前状态更新方案「${esc(profile.name)}」？</b>
        <ul class="v2-confirm-lines"><li>重新读取现在的启用状态、模组配置与加载器设置</li>
          <li>方案里原来的内容会被替换</li>
          <li>这次替换会记进「历史与回滚」，可以还原</li></ul></div></div>
      <div class="v2-modal-foot"><span class="v2-top-spacer"></span><button class="v2-btn ghost" data-v2modal="close">取消</button>
        <button class="v2-btn primary" data-v2modal="v2mpUpdate">更新</button></div>`, 'narrow');
  }

  /* ---------- 管理 ---------- */

  function openManager() {
    const profiles = S.data?.profiles || [];
    const rows = profiles.map(p => {
      const badges = [];
      if (p.active) badges.push('<span class="v2-tag blue">当前</span>');
      if (p.active && p.dirty) badges.push('<span class="v2-badge warn">已修改</span>');
      if (p.missingCount) badges.push(`<span class="v2-badge warn">缺 ${p.missingCount} 个模组</span>`);
      const note = p.note ? `<br>${esc(p.note)}` : '';
      return `<div class="v2-row">
        <span class="k">${esc(p.name)} ${badges.join(' ')}</span>
        <span class="v" style="font-size:12px">${p.modCount} 个模组（${p.enabledCount} 启用）· 更新于 ${esc(fmtTime(p.updatedAtUtc))}${note}</span>
        ${p.active
          ? `<button class="v2-btn line" data-v2mpm="choose" data-name="${esc(p.name)}" ${p.dirty ? '' : 'disabled'} title="${p.dirty ? '重新套用这个方案' : '当前状态已经与方案一致'}">重新套用</button>`
          : `<button class="v2-btn line" data-v2mpm="choose" data-name="${esc(p.name)}">套用</button>`}
        <button class="v2-btn ghost" data-v2mpm="more" data-name="${esc(p.name)}" data-v2menu>更多</button>
      </div>`;
    }).join('');

    U().openModal(`<div class="v2-modal-head"><b>模组方案</b><button class="v2-icon-btn" data-v2modal="close">${I().x}</button></div>
      <div style="padding:4px 0 6px">${rows || '<div class="v2-empty">还没有保存方案。回到模组页点「保存当前为新方案」。</div>'}</div>
      <div class="v2-modal-foot"><span class="v2-top-spacer"></span>
        <button class="v2-btn line" data-v2mpm="history">历史与回滚</button>
        <button class="v2-btn ghost" data-v2modal="close">关闭</button></div>`, 'wide');
  }

  function openProfileMenu(anchor, profile) {
    const items = [
      { label: '用当前状态更新', icon: I().refresh, fn: () => { U().closeModal(); confirmUpdate(profile); } },
      { label: '改名…', icon: I().sliders, fn: () => openRename(profile) },
      { label: '备注…', icon: I().description || I().info, fn: () => openNote(profile) },
      '-',
      { label: '删除…', icon: I().trash, fn: () => confirmDelete(profile) }
    ];
    if (profile.active) {
      items.splice(0, 0, { label: '取消当前方案（不改模组）', icon: I().dot, fn: () => { U().closeModal(); post({ type: 'modProfileClearActive' }); } });
    }
    U().openMenu(anchor, items);
  }

  function openRename(profile) {
    const rule = nameRule();
    U().openModal(`<div class="v2-modal-head"><b>重命名方案</b><button class="v2-icon-btn" data-v2modal="close">${I().x}</button></div>
      <div style="padding:8px 18px 4px;display:grid;gap:8px">
        <input id="v2mpRename" class="v2-input" maxlength="${rule.maxLength}" spellcheck="false" value="${esc(profile.name)}">
        <div id="v2mpRenameHint" class="v2-muted" style="font-size:12px"></div>
      </div>
      <div class="v2-modal-foot"><button class="v2-btn ghost" data-v2modal="close">取消</button><span class="v2-top-spacer"></span>
        <button class="v2-btn primary" data-v2modal="v2mpRenameOk">改名</button></div>`, 'narrow');
    const input = $('v2mpRename'), hint = $('v2mpRenameHint');
    const valid = () => {
      const value = (input.value || '').trim();
      let error = '';
      if (!value) error = '请填写方案名称。';
      else if (value.length > rule.maxLength) error = `方案名称最多 ${rule.maxLength} 个字符。`;
      else if (!rule.re.test(value)) error = '方案名称只能用文字、数字、空格、下划线和连字符。';
      else if (value !== profile.name && profileByName(value)) error = `已经有一个叫「${value}」的方案了。`;
      hint.textContent = error;
      hint.style.color = error ? 'var(--gp-gold)' : '';
      return { value, ok: !error && value !== profile.name };
    };
    input.addEventListener('input', () => valid());
    U().modalActions.v2mpRenameOk = () => {
      const { value, ok } = valid();
      if (!ok) return;
      U().closeModal();
      post({ type: 'modProfileRename', name: profile.name, newName: value });
    };
    input.focus();
    input.select();
  }

  function openNote(profile) {
    U().openModal(`<div class="v2-modal-head"><b>方案备注</b><button class="v2-icon-btn" data-v2modal="close">${I().x}</button></div>
      <div style="padding:8px 18px 4px;display:grid;gap:8px">
        <textarea id="v2mpNoteEdit" class="v2-input" rows="3" maxlength="${Number(S.data?.maxNoteLength) || 256}" spellcheck="false">${esc(profile.note || '')}</textarea>
        <p class="v2-muted" style="margin:0;font-size:12px">只保存在本机的方案文件里。</p>
      </div>
      <div class="v2-modal-foot"><button class="v2-btn ghost" data-v2modal="close">取消</button><span class="v2-top-spacer"></span>
        <button class="v2-btn primary" data-v2modal="v2mpNoteOk">保存</button></div>`, 'narrow');
    U().modalActions.v2mpNoteOk = () => {
      const note = ($('v2mpNoteEdit')?.value || '').trim();
      U().closeModal();
      post({ type: 'modProfileSetNote', name: profile.name, note });
    };
    $('v2mpNoteEdit')?.focus();
  }

  function confirmDelete(profile) {
    U().confirmDelete({
      title: `删除方案「${esc(profile.name)}」？`,
      lines: [
        '只删除这套方案记录，<b>不改动磁盘上的模组与配置</b>',
        profile.active ? '它当前是「当前方案」，删除后当前方案标记会清空' : null,
        '删除可以在「历史与回滚」里恢复'
      ],
      onOk: () => post({ type: 'modProfileDelete', name: profile.name })
    });
  }

  /* ---------- 套用 ---------- */

  function choose(name) {
    if (!name || S.busy) return;
    S.pendingName = name;
    post({ type: 'modProfilePlan', name });
    refresh();   // 下拉先弹回当前方案：确认后才真正套用
  }

  function showPlan(plan) {
    S.plan = plan;
    const lines = (plan.details || []).map(d => `<li>${esc(d)}</li>`).join('');
    if (plan.blocked) {
      U().openModal(`<div class="v2-confirm"><div class="v2-confirm-icon" style="background:rgba(250,204,21,.12);color:var(--gp-gold)">${I().info}</div>
        <div><b class="v2-confirm-title">现在不能套用方案「${esc(plan.name)}」</b>
          <ul class="v2-confirm-lines"><li>${esc(plan.blockReason)}</li></ul></div></div>
        <div class="v2-modal-foot"><span class="v2-top-spacer"></span><button class="v2-btn ghost" data-v2modal="close">知道了</button></div>`, 'narrow');
      return;
    }

    U().modalActions.v2mpApply = () => {
      U().closeModal();
      apply(plan.name);
    };
    U().openModal(`<div class="v2-confirm"><div class="v2-confirm-icon" style="background:rgba(56,189,248,.12);color:var(--gp-blue)">${I().sliders}</div>
      <div><b class="v2-confirm-title">套用方案「${esc(plan.name)}」？</b>
        <ul class="v2-confirm-lines">${lines}</ul>
        <p class="v2-muted" style="margin:6px 0 0;font-size:12px;line-height:1.6">会改写模组目录、模组配置和加载器设置，重启游戏后生效。套用前的状态会记进「历史与回滚」。</p></div></div>
      <div class="v2-modal-foot"><span class="v2-top-spacer"></span><button class="v2-btn ghost" data-v2modal="close">取消</button>
        <button class="v2-btn primary" data-v2modal="v2mpApply">${plan.hasChanges ? '套用' : '设为当前'}</button></div>`, 'narrow');
  }

  function apply(name) {
    S.busy = true;
    refresh();
    post({ type: 'modProfileApply', name });
  }

  function showApplyResult(payload) {
    S.busy = false;
    refresh();
    if (!payload) return;
    if (payload.ok) return;   // 成功的提示由宿主统一发 toast
    const failed = (payload.failed || []).map(x => `<li>${esc(x)}</li>`).join('');
    const skipped = (payload.skipped || []).map(x => `<li>${esc(x)}</li>`).join('');
    U().modalActions.v2mpOpenHistory = () => { U().closeModal(); requestHistory(); };
    U().openModal(`<div class="v2-confirm"><div class="v2-confirm-icon" style="background:rgba(250,204,21,.12);color:var(--gp-gold)">${I().info}</div>
      <div><b class="v2-confirm-title">方案没有完全套用</b>
        <p class="v2-muted" style="margin:0 0 6px;font-size:12px">${esc(payload.message || '')}</p>
        <ul class="v2-confirm-lines">${failed || '<li>没有失败项</li>'}${skipped}</ul>
        <p class="v2-muted" style="margin:6px 0 0;font-size:12px">可以用「历史与回滚」把这次改动还原。</p></div></div>
      <div class="v2-modal-foot"><span class="v2-top-spacer"></span><button class="v2-btn ghost" data-v2modal="close">关闭</button>
        <button class="v2-btn line" data-v2modal="v2mpOpenHistory">历史与回滚</button></div>`, 'narrow');
  }

  /* ---------- 历史与回滚 ---------- */

  function requestHistory() {
    S.history = null;
    post({ type: 'modHistoryList' });
  }

  function showHistory() {
    const entries = S.history?.entries || [];
    const gameRunning = !!S.history?.gameRunning;
    const rows = entries.map(e => {
      const status = e.rolledBack
        ? '<span class="v2-badge">已回滚</span>'
        : e.completed ? '' : '<span class="v2-badge warn">未完成</span>';
      const canRollback = !e.rolledBack && (!e.hasMods || !gameRunning);
      const reason = e.rolledBack ? '这条已经回滚过了'
        : (e.hasMods && gameRunning) ? '游戏正在运行：先退出游戏再回滚模组文件' : '';
      return `<div class="v2-row">
        <span class="k">${esc(e.title)} ${status}</span>
        <span class="v" style="font-size:12px">${esc(fmtTime(e.createdAtUtc))}${e.modCount ? ` · 涉及 ${e.modCount} 个模组` : ''}${e.hasLoaderConfig ? ' · 加载器配置' : ''}${e.hasProfiles ? ' · 方案文件' : ''}</span>
        <button class="v2-btn line" data-v2mph="rollback" data-id="${esc(e.id)}" ${canRollback ? '' : 'disabled'} title="${esc(reason)}">回滚</button>
      </div>`;
    }).join('');

    U().openModal(`<div class="v2-modal-head"><b>历史与回滚</b><button class="v2-icon-btn" data-v2modal="close">${I().x}</button></div>
      <div style="padding:4px 0 6px">${rows || '<div class="v2-empty">还没有可回滚的操作。</div>'}</div>
      <p class="v2-muted" style="margin:0;padding:0 18px 10px;font-size:12px;line-height:1.6">每个操作前的文件都按原始字节保存，回滚会把模组目录位置、模组配置和加载器配置还原到那一刻。保留最近 20 条 / 30 天。</p>
      <div class="v2-modal-foot"><span class="v2-top-spacer"></span><button class="v2-btn ghost" data-v2modal="close">关闭</button></div>`, 'wide');
  }

  function confirmRollback(id) {
    const entry = (S.history?.entries || []).find(e => e.id === id);
    if (!entry) return;
    U().modalActions.v2mpRollback = () => {
      U().closeModal();
      S.busy = true;
      refresh();
      post({ type: 'modHistoryRollback', id, force: false });
    };
    U().openModal(`<div class="v2-confirm"><div class="v2-confirm-icon" style="background:rgba(56,189,248,.12);color:var(--gp-blue)">${I().refresh}</div>
      <div><b class="v2-confirm-title">回滚「${esc(entry.title)}」？</b>
        <ul class="v2-confirm-lines"><li>时间：${esc(fmtTime(entry.createdAtUtc))}</li>
          <li>会按当时的原始内容还原模组目录位置、模组配置与加载器配置</li>
          <li>操作之后被改过的文件会提示冲突，不会静默覆盖</li>
          <li>${entry.hasMods ? '游戏正在运行的话需要先退出' : '只改配置文件，不影响正在运行的游戏'}</li></ul></div></div>
      <div class="v2-modal-foot"><span class="v2-top-spacer"></span><button class="v2-btn ghost" data-v2modal="close">取消</button>
        <button class="v2-btn primary" data-v2modal="v2mpRollback">回滚</button></div>`, 'narrow');
  }

  function showRollbackResult(payload) {
    S.busy = false;
    refresh();
    if (!payload) return;
    if (payload.ok) { requestHistory(); return; }

    if (payload.needsForce) {
      const conflicts = (payload.conflicts || []).map(c => `<li>${esc(c)}</li>`).join('');
      U().modalActions.v2mpRollbackForce = () => {
        U().closeModal();
        S.busy = true;
        refresh();
        post({ type: 'modHistoryRollback', id: payload.id, force: true });
      };
      U().openModal(`<div class="v2-confirm"><div class="v2-confirm-icon" style="background:rgba(250,204,21,.12);color:var(--gp-gold)">${I().info}</div>
        <div><b class="v2-confirm-title">有文件在操作之后被改过</b>
          <ul class="v2-confirm-lines">${conflicts}</ul>
          <p class="v2-muted" style="margin:6px 0 0;font-size:12px">强制回滚会用当初的记录覆盖这些改动。</p></div></div>
        <div class="v2-modal-foot"><span class="v2-top-spacer"></span><button class="v2-btn ghost" data-v2modal="close">取消</button>
          <button class="v2-btn danger-fill" data-v2modal="v2mpRollbackForce">强制回滚</button></div>`, 'narrow');
      return;
    }

    const failed = (payload.failed || []).map(f => `<li>${esc(f)}</li>`).join('');
    U().openModal(`<div class="v2-confirm"><div class="v2-confirm-icon" style="background:rgba(250,204,21,.12);color:var(--gp-gold)">${I().info}</div>
      <div><b class="v2-confirm-title">回滚没有完全成功</b>
        <p class="v2-muted" style="margin:0 0 6px;font-size:12px">${esc(payload.message || '')}</p>
        <ul class="v2-confirm-lines">${failed || '<li>没有失败项</li>'}</ul></div></div>
      <div class="v2-modal-foot"><span class="v2-top-spacer"></span><button class="v2-btn ghost" data-v2modal="close">关闭</button></div>`, 'narrow');
  }

  /* ---------- 事件 ---------- */

  document.addEventListener('click', ev => {
    const t = ev.target;
    if (!t.closest) return;

    const action = t.closest('[data-v2mp]');
    if (action && !action.disabled && action.closest('#v2mProfiles')) {
      switch (action.dataset.v2mp) {
        case 'capture': openCapture(); break;
        case 'manage': openManager(); break;
        case 'update': { const a = activeProfile(); if (a) confirmUpdate(a); break; }
        case 'history': requestHistory(); break;
      }
      return;
    }

    const manager = t.closest('[data-v2mpm]');
    if (manager && !manager.disabled) {
      const name = manager.dataset.name || '';
      const profile = profileByName(name);
      if (manager.dataset.v2mpm === 'history') { U().closeModal(); requestHistory(); return; }
      if (manager.dataset.v2mpm === 'more') { if (profile) openProfileMenu(manager, profile); return; }
      if (manager.dataset.v2mpm === 'choose') { U().closeModal(); choose(name); return; }
      return;
    }

    const history = t.closest('[data-v2mph]');
    if (history && !history.disabled && history.dataset.v2mph === 'rollback') {
      confirmRollback(history.dataset.id);
    }
  });

  document.addEventListener('change', ev => {
    const t = ev.target;
    if (!t.closest) return;
    if (t.id === 'v2mpSelect' && t.closest('#v2mProfiles')) {
      const name = t.value;
      if (!name) { post({ type: 'modProfileClearActive' }); return; }
      choose(name);
      refresh();   // 下拉先弹回当前方案，确认后再由宿主回推
    }
  });

  window.chrome?.webview?.addEventListener('message', ev => {
    const msg = ev.data;
    if (!msg?.type) return;
    switch (msg.type) {
      case 'modProfiles':
        S.data = msg.payload || null;
        refresh();
        break;
      case 'modProfilePlan':
        S.pendingName = '';
        showPlan(msg.payload || {});
        break;
      case 'modProfileApplyResult':
        showApplyResult(msg.payload);
        break;
      case 'modHistoryEntries':
        S.history = msg.payload || null;
        showHistory();
        break;
      case 'modHistoryRollbackResult':
        showRollbackResult(msg.payload);
        break;
    }
  });

  document.addEventListener('DOMContentLoaded', () => {
    post({ type: 'modProfilesGet' });
  });

  window.V2ModProfiles = {
    get data() { return S.data; },
    card,
    choose,
    openManager,
    openCapture,
    openHistory: requestHistory,
    refresh
  };
})();
