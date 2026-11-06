// v2-skins.js — 新界面：自定义页（卡牌 / 音效 / 资源包 / 背景）
// 外框、卡牌、音效、资源包在这里；角色分区在 v2-characters.js（通过 window.V2Chars 接进来）。
// 数据仍由原版模块保存（SkinsModule / AudioSkinsModule / ResourceModsModule），这里包一层方法、跑完后重画。
// 裁切窗口、按编号导入动态卡面、动态卡面转换进度沿用原版。
(function () {
  const K = {
    cardScheme: null, cardCat: 'all', cardQ: '',
    audioScheme: null, aCat: 'all', aStatus: 'all', aChar: 'all', aQ: '', aLimit: 200, extract: null,
    resP: null,
  };
  const U = () => window.V2UI;
  const I = () => window.V2UI.ICON;
  const SK = () => window.SkinsModule;
  const CS = () => window.CharacterSkinsModule;
  const AU = () => window.AudioSkinsModule;
  const RS = () => window.ResourceModsModule;
  const tab = () => CS()?.tab || 'cards';
  // 角色分区由 v2-characters.js 接管后，四个分区都由新界面画；没加载到它时退回原版面板
  const ownTab = () => tab() !== 'characters' || !!window.V2Chars;
  const fmtBytes = b => !b ? '0 B' : b >= 1073741824 ? (b / 1073741824).toFixed(1) + ' GB' : b >= 1048576 ? (b / 1048576).toFixed(1) + ' MB' : Math.max(1, Math.round(b / 1024)) + ' KB';
  const hue = s => [...String(s)].reduce((a, c) => a + c.charCodeAt(0), 0) % 360;
  const thumbBg = s => `linear-gradient(135deg, hsl(${hue(s)},60%,45%), hsl(${(hue(s) + 50) % 360},55%,25%))`;
  const segBtns = (cur, list, attr) => list.map(([k, l]) => `<button class="${cur === k ? 'on' : ''}" ${attr}="${k}">${l}</button>`).join('');
  const skelSchemes = () => `<div class="v2-skel-grid" aria-hidden="true">${Array.from({ length: 6 }, () => `<div class="v2-skel-card"><div class="v2-skel v2-skel-thumb"></div><div class="v2-skel-body"><div class="v2-skel v2-skel-block" style="width:60%"></div><div class="v2-skel v2-skel-block" style="width:85%"></div></div></div>`).join('')}</div>`;
  const skelCards = () => `<div class="v2-cards" aria-hidden="true">${Array.from({ length: 12 }, () => `<div class="v2-skel-card"><div class="v2-skel" style="aspect-ratio:404/400;border-radius:0"></div><div class="v2-skel-body"><div class="v2-skel v2-skel-block" style="width:75%"></div></div></div>`).join('')}</div>`;

  /* ---------- 外框 ---------- */
  // 角色皮肤功能目前不稳定，入口暂时隐藏；恢复时把 ['characters', '角色'] 加回来即可，v2-characters.js 还在
  const TABS = [['cards', '卡牌'], ['audio', '音效'], ['resource', '资源包'], ['background', '背景']];
  function frameHead(action = '') {
    return `<div class="v2-head"><h1>自定义</h1><span class="v2-top-spacer"></span>${action}</div>
      ${tab() === 'background' ? '' : U().gamePicker('v2sGame', 'data-v2s="manageGames"', rRunning())}
      <div class="v2-toolbar"><div class="v2-seg">${segBtns(tab(), TABS, 'data-v2s-tab')}</div></div>`;
  }
  const crumb = (name, back) => `<div class="v2-crumb"><button data-v2s="${back}">自定义</button>${I().right}<span class="cur">${esc(name)}</span></div>`;

  /* ---------- 卡牌 ---------- */
  const CATS = ['手牌卡', '命运卡', '事件卡', '地图事件', '异画道具'];
  const profiles = () => SK()?.profiles || [];
  const activeSkin = () => (SK()?.activeSkin || '').toLowerCase();
  const special = n => n === '__game_default__' || String(n).toLowerCase() === 'default';
  const pName = p => p.name === '__game_default__' ? '游戏默认' : (p.displayName || p.name);

  function cardsList() {
    const action = `<button class="v2-btn primary" data-v2s="cardAddMenu" data-v2menu>${I().plus}新建方案${I().down}</button>`;
    if (!SK()) return frameHead(action) + skelSchemes();
    const ps = profiles();
    const inner = ps.length ? `<div class="v2-grid">${ps.map(p => {
      const on = p.name.toLowerCase() === activeSkin();
      const sub = p.name === '__game_default__' ? '使用游戏内置卡面' : `${p.cardCount || 0} 张卡面${p.author ? ' · ' + esc(p.author) : ''}`;
      return `<button class="v2-scheme ${on ? 'active' : ''}" data-v2s-card="${esc(p.name)}">
        <div class="thumb" style="background:${p.name === '__game_default__' ? 'linear-gradient(135deg,#2d3558,#1f2438)' : thumbBg(p.name)}">${esc(pName(p).slice(0, 1))}${on ? '<span class="v2-badge pink">使用中</span>' : ''}</div>
        <div class="info"><b title="${esc(pName(p))}">${esc(pName(p))}</b><small>${sub}</small></div></button>`;
    }).join('')}</div>` : skelSchemes();
    return frameHead(action) + inner
    + `<details class="v2-card" style="margin-top:14px"><summary><div class="t"><b>更多</b><small>皮肤目录</small></div>${I().down}</summary>
      <div class="v2-adv"><div class="v2-row"><span class="n">打开皮肤目录</span><span class="d">所有卡牌方案存放的位置</span><button class="v2-btn line" data-v2s="cardFolder">打开</button></div></div></details>`;
  }

  function cardScheme() {
    const p = profiles().find(x => x.name === K.cardScheme);
    if (!p) { K.cardScheme = null; return cardsList(); }
    const on = p.name.toLowerCase() === activeSkin();
    const loaded = SK()?.selectedSkin === p.name;
    const cards = loaded ? (SK().cards || []) : [];
    const q = K.cardQ.trim().toLowerCase();
    const list = cards.filter(c => (K.cardCat === 'all' || c.category === K.cardCat) && (!q || (q.startsWith('#')
      ? String(c.cardId || '').includes(q.slice(1)) : [c.cardName, c.fileName, c.assetKey, String(c.cardId || '')].some(v => String(v ?? '').toLowerCase().includes(q)))));
    const cat = (k, l) => `<button class="${K.cardCat === k ? 'on' : ''}" data-v2s-cat="${k}">${l} <span class="v2-muted">${k === 'all' ? cards.length : cards.filter(c => c.category === k).length}</span></button>`;
    const custom = cards.filter(c => c.isCustomized).length;
    const meta = [p.description, p.author && '作者 ' + p.author, p.lastModified && '修改于 ' + new Date(p.lastModified).toLocaleDateString('zh-CN')].filter(Boolean).map(esc).join(' · ');
    return `<div class="v2-head">${crumb(pName(p), 'cardBack')}<span class="v2-top-spacer"></span>
        ${on ? '<span class="v2-badge pink">当前方案 · 重启游戏生效</span>' : `<button class="v2-btn primary" data-v2s="cardApply">${I().check}设为当前皮肤</button>`}
        <button class="v2-icon-btn" data-v2s="cardSchemeMenu" data-v2menu title="更多">${I().more}</button></div>
      ${meta ? `<p class="v2-muted" style="margin:-10px 0 14px">${meta}</p>` : ''}
      ${p.name === '__game_default__' ? '<div class="v2-card v2-empty">游戏默认方案只使用游戏内部卡面，不需要外部图片。</div>' : !loaded ? skelCards() : `
      <div class="v2-toolbar"><div class="v2-seg">${cat('all', '全部')}${CATS.map(c => cat(c, c)).join('')}</div>
        <label class="v2-search">${I().search}<input id="v2sCardQ" placeholder="卡名 / #编号" value="${esc(K.cardQ)}"></label></div>
      <p class="v2-muted" style="margin:0 0 10px"><span class="v2-dot"></span>已自定义 ${custom} 张　点卡牌可以更换或恢复</p>
      <div class="v2-cards">${list.map(c => `<button class="v2-tcard ${c.isCustomized ? 'custom' : ''}" data-v2s-tcard="${esc(c.fileName)}" title="${esc(c.fileName)}">
          <img src="${esc(c.previewUrl)}" loading="lazy" alt="" onerror="this.style.opacity='0.15'">
          ${c.isCustomized ? '<span class="v2-dot"></span>' : ''}${c.isAnimated ? '<span class="v2-tag anim">动态</span>' : ''}
          <span class="cap">${esc(c.cardName || c.assetKey || c.fileName)}${c.cardId > 0 ? `<small>#${c.cardId}</small>` : ''}</span></button>`).join('')
        || '<div class="v2-empty" style="grid-column:1/-1">没有找到匹配的卡牌</div>'}</div>`}`;
  }

  function cardModal(c) {
    const s = SK().selectedSkin;
    const file = c.animationBinding ? c.animationBinding + '.png' : c.fileName;
    Object.assign(U().modalActions, {
      v2sReplace: () => { U().closeModal(); post({ type: 'skinsPickAndReplaceCard', skinName: s, fileName: c.fileName }); },
      v2sAnim: () => { U().closeModal(); post({ type: 'skinsPickCardAnimation', skinName: s, fileName: file }); },
      v2sAnimRemove: () => { U().closeModal(); post({ type: 'skinsRemoveCardAnimation', skinName: s, fileName: file }); },
      v2sRevert: () => U().confirmDelete({
        title: `把「${esc(c.cardName || c.fileName)}」恢复成内置卡面？`, ok: '恢复',
        lines: ['会删除这个方案里替换的图片，改用程序内置的卡面', c.isAnimated && '动态覆盖也会一起移除'],
        onOk: () => post({ type: 'skinsRevertCard', skinName: s, fileName: c.fileName }),
      }),
    });
    U().openModal(`
      <div class="v2-modal-head"><b>${esc(c.cardName || c.fileName)}</b>${c.cardId > 0 ? `<span class="v2-tag">#${c.cardId}</span>` : ''}<button class="v2-icon-btn" data-v2modal="close">${I().x}</button></div>
      <div class="v2-cardmodal">
        <img src="${esc(c.previewUrl)}" alt="">
        <div class="side">
          <p class="v2-muted" style="margin:0 0 6px">${esc(c.category || '')} · ${fmtBytes(c.fileSizeBytes)}<br><span style="font-family:var(--v2-mono);font-size:12px">${esc(c.fileName)}</span></p>
          <p style="margin:0 0 12px">${c.isCustomized ? '已使用自定义卡面' : '当前是内置卡面'}${c.isAnimated ? '，带动态覆盖' : ''}</p>
          <button class="v2-btn primary" data-v2modal="v2sReplace">${I().upload}更换图片…</button>
          <button class="v2-btn line" data-v2modal="v2sAnim">导入视频 / GIF…</button>
          ${c.isAnimated ? '<button class="v2-btn ghost" data-v2modal="v2sAnimRemove">移除动态覆盖</button>' : ''}
          ${c.canRestore ? '<button class="v2-btn ghost" data-v2modal="v2sRevert">恢复内置卡面</button>' : ''}
          <p class="v2-muted" style="font-size:12px;margin:8px 0 0">选好文件后会打开裁切窗口。视频 / GIF 会转换成动态卡面（404 × 400）。</p>
        </div>
      </div>`);
  }

  function metaForm({ title, fields, ok, onOk }) {
    U().modalActions.v2sMetaOk = () => {
      const val = {};
      fields.forEach(f => { val[f.key] = ($('v2sf_' + f.key)?.value || '').trim(); });
      const bad = fields.find(f => f.required && !val[f.key]) || fields.find(f => f.pattern && val[f.key] && !f.pattern.test(val[f.key]));
      if (bad) { $('v2sMetaErr').textContent = bad.required && !val[bad.key] ? `请填写「${bad.label}」` : `「${bad.label}」${bad.hint}`; return; }
      U().closeModal(); onOk(val);
    };
    U().openModal(`
      <div class="v2-modal-head"><b>${title}</b><button class="v2-icon-btn" data-v2modal="close">${I().x}</button></div>
      <div style="padding:16px 18px">${fields.map(f => `<div class="v2-field"><label for="v2sf_${f.key}">${f.label}${f.required ? '' : ' <span class="v2-muted">（可选）</span>'}</label>
        ${f.hint ? `<small>${f.hint}</small>` : ''}
        ${f.multiline ? `<textarea id="v2sf_${f.key}" rows="2">${esc(f.value || '')}</textarea>` : `<input id="v2sf_${f.key}" value="${esc(f.value || '')}" spellcheck="false">`}</div>`).join('')}
        <p class="v2-err" id="v2sMetaErr"></p></div>
      <div class="v2-modal-foot"><span class="v2-top-spacer"></span><button class="v2-btn ghost" data-v2modal="close">取消</button>
        <button class="v2-btn primary" data-v2modal="v2sMetaOk">${ok}</button></div>`);
    $('v2sf_' + fields[0].key)?.focus();
  }
  const ID_RULE = { pattern: /^[A-Za-z0-9_-]+$/, hint: '只能用英文、数字、下划线和减号' };

  /* ---------- 音效 ---------- */
  const ACATS = [['UI', '界面交互'], ['Dice', '掷骰与地图'], ['Combat', '战斗对拍'], ['Card', '卡牌与技能'], ['Voice', '角色语音'], ['BGM', '背景音乐'], ['Misc', '其它特效']];
  const aProfiles = () => AU()?.profiles || [];
  const aItems = () => AU()?.items || [];
  const aActive = () => AU()?.activeSchemeId || 'default';
  const extracted = () => aItems().some(i => i.hasOriginal);

  function prepBanner() {
    const X = K.extract;
    if (X) return `<div class="v2-card" style="padding:14px 18px"><b style="font-weight:500">正在准备原版音频</b>
      <span class="v2-muted">${Math.round((X.progress || 0) * 100)}%</span><br><span class="v2-muted">${esc(X.message || '')}</span>
      <div class="v2-bar" style="max-width:none;margin-top:8px"><div style="width:${(X.progress || 0) * 100}%"></div></div></div>`;
    if (!aItems().length || extracted()) return '';
    // 实测（1.3.2）：「从游戏提取」并不真的解包游戏，只从事先导出的 audio-export 文件夹复制；找不到就是 0 个
    if (K.extractEmpty) return `<div class="v2-notice">${I().info}<span style="flex:1">刚才提取到 <b>0 个</b>：这台电脑上没有事先导出的 audio-export 文件夹，
      所以暂时<b>听不了原版</b>。替换、静音、调音量都不受影响。</span>
      <button class="v2-btn line" data-v2s="audioPrep">了解详情…</button></div>`;
    return `<div class="v2-notice">${I().info}<span style="flex:1">暂时<b>听不了原版</b>：需要事先导出好的原版音频（替换、静音、调音量都不受影响）</span>
      <button class="v2-btn line" data-v2s="audioPrep">准备原版音频…</button></div>`;
  }

  function audioList() {
    const action = `<button class="v2-btn primary" data-v2s="audioAddMenu" data-v2menu>${I().plus}新建方案${I().down}</button>`;
    const ap = aProfiles();
    const inner = ap.length ? `<div class="v2-grid">${ap.map(p => {
      const on = p.id === aActive();
      return `<button class="v2-scheme ${on ? 'active' : ''}" data-v2s-audio="${esc(p.id)}">
        <div class="thumb" style="background:${p.id === 'default' ? 'linear-gradient(135deg,#2d3558,#1f2438)' : thumbBg(p.id)}">${I().sliders}${on ? '<span class="v2-badge pink">使用中</span>' : ''}</div>
        <div class="info"><b title="${esc(p.name || p.id)}">${esc(p.name || p.id)}</b><small>${p.id === 'default' ? '官方原版，只能试听' : `${p.ruleCount || 0} 项替换 / 静音${p.author ? ' · ' + esc(p.author) : ''}`}</small></div></button>`;
    }).join('')}</div>` : skelSchemes();
    return frameHead(action) + prepBanner() + inner + `
    <details class="v2-card" style="margin-top:14px"><summary><div class="t"><b>更多</b><small>原版音频包、方案目录</small></div>${I().down}</summary>
      <div class="v2-adv">
        <div class="v2-row"><span class="n">导出原版音频包</span><span class="d">把整理好的原版音频打成 ZIP，换电脑时直接导入</span><button class="v2-btn line" data-v2s="audioExportOriginal" ${extracted() ? '' : 'disabled'}>导出</button></div>
        <div class="v2-row"><span class="n">重新整理原版音频</span><span class="d">游戏更新后，原版音频可能有变化</span><button class="v2-btn line" data-v2s="audioPrep">整理…</button></div>
        <div class="v2-row"><span class="n">打开方案目录</span><span class="d">所有音效方案存放的位置</span><button class="v2-btn line" data-v2s="audioFolder">打开</button></div>
      </div></details>`;
  }

  function audioScheme() {
    const p = aProfiles().find(x => x.id === K.audioScheme);
    if (!p) { K.audioScheme = null; return audioList(); }
    const isDefault = p.id === 'default', on = p.id === aActive();
    const loaded = AU()?.selectedSchemeId === p.id;
    const items = loaded ? aItems() : [];
    const voice = K.aCat === 'Voice';
    const chars = [...new Set(items.filter(i => i.category === 'Voice' && i.characterName).map(i => i.characterName))].sort();
    const q = K.aQ.trim().toLowerCase();
    const list = items.filter(i => (K.aCat === 'all' || i.category === K.aCat)
      && (!voice || K.aChar === 'all' || i.characterName === K.aChar)
      && (K.aStatus === 'all' || (K.aStatus === 'changed' ? !!i.action : i.action === K.aStatus))
      && (!q || ('#' + i.id).includes(q) || [i.name, i.label, i.characterName, i.skinName].some(v => String(v ?? '').toLowerCase().includes(q))));
    const count = k => k === 'all' ? items.length : items.filter(i => i.category === k).length;
    const cat = (k, l) => `<button class="${K.aCat === k ? 'on' : ''}" data-v2s-acat="${k}">${l} <span class="v2-muted">${count(k)}</span></button>`;
    const tag = i => i.action === 'replace' ? `<span class="v2-badge">已替换：${esc(i.customFile || '自定义音频')}</span>`
      : i.action === 'mute' ? '<span class="v2-badge off">已静音</span>' : i.action === 'redirect' ? `<span class="v2-tag">重定向 #${esc(i.redirectId)}</span>` : '';
    const meta = [p.description, p.author && '作者 ' + p.author, p.lastModified && '更新于 ' + String(p.lastModified).slice(0, 10)].filter(Boolean).map(esc).join(' · ');
    return `<div class="v2-head">${crumb(p.name || p.id, 'audioBack')}<span class="v2-top-spacer"></span>
        ${on ? '<span class="v2-badge pink">当前方案 · 重启游戏生效</span>' : `<button class="v2-btn primary" data-v2s="audioApply">${I().check}设为当前方案</button>`}
        <button class="v2-icon-btn" data-v2s="audioSchemeMenu" data-v2menu title="更多">${I().more}</button></div>
      ${meta ? `<p class="v2-muted" style="margin:-10px 0 14px">${meta}</p>` : ''}
      ${isDefault ? `<div class="v2-notice">${I().info}<span style="flex:1">这是官方原版，只能试听。想替换或静音，先复制一份再改</span>
        <button class="v2-btn line" data-v2s="audioDuplicate">复制为新方案</button></div>` : ''}
      ${prepBanner()}
      ${!loaded ? `<div aria-hidden="true" style="display:grid;gap:8px">${Array.from({ length: 6 }, () => `<div class="v2-skel-row" style="border:1px solid var(--v2-border);border-radius:10px"><div class="v2-skel" style="width:28px;height:28px;border-radius:50%"></div><div style="flex:1;display:grid;gap:6px"><div class="v2-skel v2-skel-block" style="width:40%"></div><div class="v2-skel v2-skel-block" style="width:65%"></div></div></div>`).join('')}</div>` : `
      <div class="v2-toolbar"><div class="v2-seg">${cat('all', '全部')}${ACATS.map(([k, l]) => cat(k, l)).join('')}</div></div>
      <div class="v2-toolbar">
        ${isDefault ? '' : `<div class="v2-seg">${segBtns(K.aStatus, [['all', '全部'], ['changed', '已改动'], ['mute', '已静音']], 'data-v2s-astatus')}</div>`}
        ${voice ? `<select id="v2sAChar" class="v2-select"><option value="all">全部角色</option>${chars.map(c => `<option ${K.aChar === c ? 'selected' : ''}>${esc(c)}</option>`).join('')}</select>` : ''}
        <label class="v2-search">${I().search}<input id="v2sAQ" placeholder="音效名、#编号、角色" value="${esc(K.aQ)}"></label>
      </div>
      <div class="v2-card">${list.slice(0, K.aLimit).map(i => `
        <div class="v2-mod v2-audio ${i.action === 'mute' ? 'off' : ''}">
          <span class="aid">#${i.id}</span>
          <div class="body">
            <div class="name">${esc(i.label || i.name)} ${tag(i)}
              ${i.category === 'Voice' && i.characterName ? `<span class="v2-tag">${esc(i.characterName)}</span><span class="v2-tag">${esc(i.skinName || '常规默认')}</span>${i.slotName ? `<span class="v2-tag">${esc(i.slotName)}</span>` : ''}` : ''}</div>
            <div class="desc" style="font-family:var(--v2-mono);font-size:11px">${esc(i.name)}</div>
          </div>
          ${i.action === 'replace' ? `<label class="v2-muted vol" title="这一条的音量倍率">音量 <input type="range" min="0" max="2" step="0.05" value="${i.volume || 1}" data-v2s-avol="${i.id}"> <span>${Math.round((i.volume || 1) * 100)}%</span></label>` : ''}
          <button class="v2-btn ghost play" data-v2s-play="${esc(i.originalPreviewUrl || '')}" ${i.hasOriginal ? '' : 'disabled title="先「准备原版音频」才能试听"'}>${I().play}原版</button>
          ${i.action === 'replace' && i.previewUrl ? `<button class="v2-btn ghost play" data-v2s-play="${esc(i.previewUrl)}">${I().play}新的</button>` : ''}
          ${isDefault ? '' : `<button class="v2-icon-btn" data-v2s-aitem="${i.id}" data-v2menu title="更多">${I().more}</button>`}
        </div>`).join('') || '<div class="v2-empty">没有符合条件的音效</div>'}
        ${list.length > K.aLimit ? `<div class="v2-row" style="justify-content:center"><button class="v2-btn ghost" data-v2s="audioMore">再显示 200 条（共 ${list.length} 条）</button></div>` : ''}
      </div>`}`;
  }

  function audioPrepDialog() {
    Object.assign(U().modalActions, {
      v2sExtract: () => { U().closeModal(); post({ type: 'audioSkinsExtractGameAudio' }); },
      v2sImportOrig: () => { U().closeModal(); post({ type: 'audioSkinsImportOriginalZip' }); },
    });
    U().openModal(`
      <div class="v2-modal-head"><b>准备原版音频</b><button class="v2-icon-btn" data-v2modal="close">${I().x}</button></div>
      <div style="padding:16px 18px">
        <p style="margin:0 0 10px">想「试听原版」，要先把约 1,300 个原版音频放进游戏目录的 <span style="font-family:var(--v2-mono)">AstralParty_ModLoader\\audio-cache\\</span>。</p>
        <div class="v2-notice" style="margin:0 0 10px">${I().info}<span><b>目前的限制：</b>「从游戏提取」其实<b>不会解包游戏</b>，
          只会从一个事先导出好的 <span style="font-family:var(--v2-mono)">audio-export</span> 文件夹里复制。这个文件夹和原版音频包目前都没有公开，
          所以在大多数电脑上会提取到 0 个。</span></div>
        <ul class="v2-confirm-lines" style="margin:0">
          <li>如果你从别处拿到了别人导出的 <b>audio-export</b> 文件夹，放进游戏目录（或 AstralParty_ModLoader 里）再点「尝试提取」</li>
          <li>如果你拿到了<b>原版音频包 ZIP</b>，点「导入音频包 ZIP」</li>
          <li>两样都没有也没关系：替换、静音、调音量都能正常用，只是听不了原版对比</li></ul></div>
      <div class="v2-modal-foot"><button class="v2-btn ghost" data-v2modal="close">关闭</button><span class="v2-top-spacer"></span>
        <button class="v2-btn line" data-v2modal="v2sImportOrig">${I().upload}导入音频包 ZIP…</button>
        <button class="v2-btn line" data-v2modal="v2sExtract">尝试提取</button></div>`);
  }

  function selectAudio(id) {
    K.audioScheme = id; K.aCat = 'all'; K.aStatus = 'all'; K.aChar = 'all'; K.aQ = ''; K.aLimit = 200;
    if (AU()) { AU().selectedSchemeId = id; post({ type: 'audioSkinsGetItems', schemeId: id }); }
    render();
  }

  /* ---------- 资源包 ---------- */
  const rStatus = () => RS()?.status || null;
  const rRunning = () => !!(K.resP && !['done', 'cancelled'].includes(K.resP.phase));

  function resourceView() {
    const s = rStatus() || {}, mods = s.mods || [], P = K.resP, running = rRunning();
    const canApply = s.catalogAvailable && !s.gameRunning && mods.some(m => m.enabled && m.matchedBundles > 0) && !running;
    const canRestore = !!s.appliedVersion && !s.gameRunning && !running;
    const action = `<button class="v2-btn ${s.needsApply ? 'line' : 'primary'}" data-v2s="resImport" ${running ? 'disabled' : ''}>${I().upload}导入资源包</button>`;
    const phase = P ? ({ import: '正在导入', apply: '正在应用', done: '完成', cancelled: '已取消并还原' }[P.phase] || '处理中') : '';
    const msgs = s.appliedMessages || [];
    return frameHead(action) + `
      ${s.catalogError ? `<div class="v2-notice">${I().info}<span>${esc(s.catalogError)}</span></div>` : ''}
      ${s.gameRunning ? `<div class="v2-notice">${I().info}<span>游戏正在运行：要先关掉游戏，才能应用或还原资源包</span></div>` : ''}
      ${s.needsApply && !running ? `<div class="v2-notice">${I().info}<span style="flex:1">有未应用的更改，点「应用」后才会写入游戏</span>
        <button class="v2-btn primary" data-v2s="resApply" ${canApply ? '' : 'disabled'}>应用</button></div>` : ''}
      ${P && running ? `<div class="v2-card" style="padding:14px 18px">
        <div style="display:flex;align-items:center;gap:10px"><div style="flex:1"><b style="font-weight:500">${phase}</b>
          <span class="v2-muted">${P.paused ? '已暂停 · ' : ''}${P.percent || 0}%</span><br><span class="v2-muted">${esc(P.task || P.detail || '')}</span></div>
          ${P.canPause !== false ? `<button class="v2-btn ghost" data-v2s="resPause">${P.paused || RS()?.progressPaused ? '继续' : '暂停'}</button>` : ''}
          ${P.canCancel !== false ? '<button class="v2-btn ghost" data-v2s="resCancel">取消并还原</button>' : ''}</div>
        <div class="v2-bar" style="max-width:none;margin-top:8px"><div style="width:${P.percent || 0}%"></div></div></div>` : ''}
      <div class="v2-card">
        <div class="v2-row"><span class="k">状态</span><span class="v">${s.catalogError ? '<span class="v2-muted">资源目录读取失败</span>'
          : `资源目录 ${esc(s.catalogVersion || '—')} · ${s.appliedVersion ? `已应用 ${s.appliedRedirects ?? 0} 条` : '尚未应用'}`}
          ${s.appliedErrors > 0 ? `<span class="v2-badge warn">${s.appliedErrors} 条未生效</span>` : s.appliedWarnings > 0 ? `<span class="v2-badge warn">${s.appliedWarnings} 条警告</span>` : ''}</span></div>
        ${mods.map(m => `<div class="v2-mod ${m.enabled ? '' : 'off'}">
          <div class="body"><div class="name">${esc(m.name)}<span class="v2-tag">${fmtBytes(m.size)}</span>
            ${m.unmatchedBundles > 0 ? `<span class="v2-badge warn" title="${esc((m.unmatchedNames || []).join('\n'))}">${m.unmatchedBundles} 个未匹配</span>` : ''}
            ${m.hasMojibakeNames ? '<span class="v2-badge warn" title="压缩包里的文件名有乱码">文件名乱码</span>' : ''}</div>
            <div class="desc">${m.matchedBundles || 0} / ${m.bundleCount || 0} 个资源匹配当前游戏</div></div>
          <button class="v2-icon-btn" data-v2s-rmenu="${esc(m.id)}" data-v2menu title="更多">${I().more}</button>
          <label class="v2-switch"><input type="checkbox" data-v2s-rtoggle="${esc(m.id)}" ${m.enabled ? 'checked' : ''} ${running ? 'disabled' : ''}><span></span></label>
        </div>`).join('') || '<div class="v2-empty">还没有资源包。点「导入资源包」添加 RML 格式的 ZIP。</div>'}
      </div>
      ${msgs.length ? `<details class="v2-card"><summary><div class="t"><b>应用结果</b><small>${s.appliedErrors || 0} 条未生效 / ${s.appliedWarnings || 0} 条警告</small></div>${I().down}</summary>
        <pre class="v2-log">${esc(msgs.join('\n'))}</pre></details>` : ''}
      <details class="v2-card"><summary><div class="t"><b>更多</b><small>应用、还原官方资源、打开目录</small></div>${I().down}</summary>
        <div class="v2-adv">
          <div class="v2-row"><span class="n">应用</span><span class="d">把开着的资源包写入游戏</span><button class="v2-btn line" data-v2s="resApply" ${canApply ? '' : 'disabled'}>应用</button></div>
          <div class="v2-row"><span class="n">还原官方资源</span><span class="d">撤销资源包对游戏文件的全部修改</span><button class="v2-btn line" data-v2s="resRestore" ${canRestore ? '' : 'disabled'}>还原</button></div>
          <div class="v2-row"><span class="n">打开目录</span><span class="d">资源包存放的位置</span><button class="v2-btn line" data-v2s="resFolder">打开</button></div>
          <div class="v2-row"><span class="n">刷新</span><span class="d">重新读取资源包状态</span><button class="v2-btn line" data-v2s="resRefresh">刷新</button></div>
        </div></details>`;
  }

  /* ---------- 渲染 ---------- */
  function render() {
    const root = $('v2Skins'), view = $('skinsView');
    if (!root || !view || U().isComposing()) return;
    view.classList.toggle('v2-own', ownTab());
    const t = tab();
    root.innerHTML = `<div class="v2-page ${t === 'characters' && !window.V2Chars ? 'v2-frame-only' : ''}">${
      t === 'cards' ? (K.cardScheme ? cardScheme() : cardsList())
      : t === 'audio' ? (K.audioScheme ? audioScheme() : audioList())
      : t === 'resource' ? resourceView()
      : t === 'background' && window.V2Bg ? window.V2Bg.html()
      : window.V2Chars ? window.V2Chars.html()
      : frameHead()}</div>`;
    // 搜索框：重画后把焦点和光标放回去
    U().bindSearch('v2sCardQ', v => { K.cardQ = v; render(); });
    U().bindSearch('v2sAQ', v => { K.aQ = v; K.aLimit = 200; render(); });
  }

  // 给 v2-characters.js 用
  window.V2Skins = { frameHead, crumb, metaForm, render, thumbBg, ID_RULE };

  /* ---------- 操作 ---------- */
  const confirm = o => U().confirmDelete(o);
  const curCard = () => profiles().find(x => x.name === K.cardScheme);
  const curAudio = () => aProfiles().find(x => x.id === K.audioScheme);

  const actions = {
    manageGames: () => showPage('settings'),
    // 卡牌
    cardAddMenu: el => U().openMenu(el, [
      { label: '新建方案（从内置默认包）…', icon: I().plus, fn: () => metaForm({
        title: '新建卡牌方案', ok: '创建',
        fields: [{ key: 'newName', label: '方案目录名', required: true, value: 'my_skin', ...ID_RULE },
          { key: 'displayName', label: '显示名称', value: '我的卡面' }, { key: 'author', label: '作者' }, { key: 'description', label: '简介', multiline: true }],
        // 原版：新方案总是从程序内置的默认包复制，不用磁盘上改过的图片
        onOk: v => post({ type: 'skinsDuplicate', source: 'default', newName: v.newName, displayName: v.displayName || v.newName, author: v.author, description: v.description }),
      }) },
      { label: '导入方案 ZIP…', icon: I().upload, fn: () => post({ type: 'skinsImportZip' }) },
      { label: '导入旧式皮肤包…', icon: I().upload, fn: () => post({ type: 'skinsImportLegacyZip' }) },
    ]),
    cardFolder: () => post({ type: 'skinsOpenFolder' }),
    cardBack: () => { K.cardScheme = null; render(); },
    cardApply: () => post({ type: 'skinsSetActive', name: K.cardScheme }),
    cardSchemeMenu: el => {
      const p = curCard(); if (!p) return;
      const gd = p.name === '__game_default__', sp = special(p.name), isDefault = p.name.toLowerCase() === 'default';
      U().openMenu(el, [
        { label: '编辑方案信息…', icon: I().sliders, disabled: sp, fn: () => metaForm({
          title: '编辑方案信息', ok: '保存',
          fields: [{ key: 'displayName', label: '显示名称', required: true, value: p.displayName }, { key: 'author', label: '作者', value: p.author }, { key: 'description', label: '简介', multiline: true, value: p.description }],
          onOk: v => post({ type: 'skinsUpdateMeta', name: p.name, displayName: v.displayName, author: v.author, description: v.description }),
        }) },
        { label: '按编号导入动态卡面…', icon: I().upload, disabled: gd, fn: () => SK()?.openAnimationBinding() },
        { label: '导出 ZIP', icon: I().download, disabled: gd || !p.cardCount, fn: () => post({ type: 'skinsExportZip', name: p.name }) },
        { label: '打开方案目录', icon: I().folder, disabled: gd, fn: () => post({ type: 'skinsOpenFolder', name: p.name }) },
        ...(isDefault ? ['-',
          { label: '从 GitHub 下载最新默认包…', icon: I().download, fn: () => confirm({ title: '下载最新默认包？', ok: '下载',
            lines: ['从最新 Release 下载默认包，<b>覆盖 default 里的同名图片</b>', '不会切换当前方案'], onOk: () => post({ type: 'skinsDownloadDefault' }) }) },
          { label: '恢复内置默认包…', icon: I().refresh, fn: () => confirm({ title: '恢复内置默认包？', ok: '恢复',
            lines: ['用程序内置的资源恢复 default 方案，<b>覆盖同名图片并移除动态覆盖</b>', '不会改动其他方案，也不会切换当前方案'], onOk: () => post({ type: 'skinsRestoreDefault' }) }) }] : []),
        '-',
        { label: '删除方案…', icon: I().trash, disabled: sp, fn: () => confirm({
          title: `删除卡牌方案「${esc(pName(p))}」？`,
          lines: [`方案和目录下的 <b>${p.cardCount || 0} 张卡面图片</b>会一起删除，无法恢复`, p.cardCount && '想留个备份，先在 ⋯ 里「导出 ZIP」',
            p.name.toLowerCase() === activeSkin() && '这是<b>当前正在用</b>的方案，删除后会切回「游戏默认」'],
          onOk: () => { post({ type: 'skinsDelete', name: p.name }); K.cardScheme = null; render(); },
        }) },
      ]);
    },
    // 音效
    audioAddMenu: el => U().openMenu(el, [
      { label: '新建空白方案…', icon: I().plus, fn: () => metaForm({
        title: '新建音效方案', ok: '创建',
        fields: [{ key: 'id', label: '方案 ID', required: true, value: 'my_audio', ...ID_RULE }, { key: 'name', label: '显示名称', value: '我的音效' },
          { key: 'author', label: '作者' }, { key: 'description', label: '简介', multiline: true }],
        onOk: v => post({ type: 'audioSkinsCreate', id: v.id, name: v.name || v.id, author: v.author, description: v.description }),
      }) },
      { label: '导入方案 ZIP…', icon: I().upload, fn: () => post({ type: 'audioSkinsImportZip' }) },
    ]),
    audioPrep: audioPrepDialog,
    audioExportOriginal: () => post({ type: 'audioSkinsExportOriginalZip' }),
    audioFolder: () => post({ type: 'audioSkinsOpenFolder' }),
    audioBack: () => { K.audioScheme = null; render(); },
    audioApply: () => post({ type: 'audioSkinsSetActive', id: K.audioScheme }),
    audioMore: () => { K.aLimit += 200; render(); },
    audioDuplicate: () => {
      const p = curAudio(); if (!p) return;
      metaForm({ title: `复制「${esc(p.name || p.id)}」`, ok: '复制',
        fields: [{ key: 'newId', label: '新方案 ID', required: true, value: `${p.id}_copy`, ...ID_RULE }, { key: 'name', label: '显示名称', value: `${p.name || p.id} 副本` }],
        onOk: v => post({ type: 'audioSkinsDuplicate', sourceId: p.id, newId: v.newId, name: v.name || v.newId }) });
    },
    audioSchemeMenu: el => {
      const p = curAudio(); if (!p) return;
      const isDefault = p.id === 'default';
      U().openMenu(el, [
        { label: '编辑方案信息…', icon: I().sliders, disabled: isDefault, fn: () => metaForm({
          title: '编辑方案信息', ok: '保存',
          fields: [{ key: 'name', label: '显示名称', required: true, value: p.name }, { key: 'author', label: '作者', value: p.author }, { key: 'description', label: '简介', multiline: true, value: p.description }],
          onOk: v => post({ type: 'audioSkinsUpdateMeta', id: p.id, name: v.name, author: v.author, description: v.description }),
        }) },
        { label: '复制为新方案…', icon: I().copy, fn: actions.audioDuplicate },
        { label: '导出方案 ZIP', icon: I().download, disabled: isDefault, fn: () => post({ type: 'audioSkinsExportZip', id: p.id }) },
        '-',
        { label: '删除方案…', icon: I().trash, disabled: isDefault, fn: () => confirm({
          title: `删除音效方案「${esc(p.name || p.id)}」？`,
          lines: [p.ruleCount ? `方案里 <b>${p.ruleCount} 项替换 / 静音</b>和导入的音频文件会一起删除，无法恢复` : '这个方案里还没有改动',
            p.ruleCount && '想留个备份，先在 ⋯ 里「导出方案 ZIP」', p.id === aActive() && '这是<b>当前正在用</b>的方案'],
          onOk: () => { post({ type: 'audioSkinsDelete', id: p.id }); K.audioScheme = null; render(); },
        }) },
      ]);
    },
    // 资源包
    resImport: () => post({ type: 'resourceModsImport' }),
    resApply: () => post({ type: 'resourceModsApply' }),
    resRestore: () => confirm({ title: '还原官方资源？', ok: '还原', lines: ['撤销所有资源包对游戏文件的修改', '资源包本身不会删除，之后可以重新应用'], onOk: () => post({ type: 'resourceModsRestore' }) }),
    resFolder: () => post({ type: 'resourceModsOpenFolder' }),
    resRefresh: () => RS()?.refresh(),
    resPause: () => post({ type: 'resourceModsPause', paused: !RS()?.progressPaused }),
    resCancel: () => confirm({ title: '取消并还原？', ok: '取消并还原',
      lines: ['停止当前操作，并把游戏资源恢复到操作前的状态', '已经解压的临时文件会被删除'], onOk: () => post({ type: 'resourceModsCancel' }) }),
  };

  function audioItemMenu(anchor, i) {
    const base = { schemeId: K.audioScheme, configId: i.id, eventName: i.name };
    U().openMenu(anchor, [
      { label: '替换音频（.wav）…', icon: I().upload, fn: () => post({ type: 'audioSkinsPickAndReplace', ...base, volume: 1.0 }) },
      { label: i.action === 'mute' ? '取消静音' : '静音', icon: I().dot, fn: () => post({ type: 'audioSkinsSetMute', ...base, mute: i.action !== 'mute' }) },
      { label: '导出这条原版音频', icon: I().download, disabled: !i.hasOriginal, fn: () => post({ type: 'audioSkinsExportSingleOriginal', id: i.id, name: i.name }) },
      '-',
      { label: '恢复原版', icon: I().refresh, disabled: !i.action, fn: () => post({ type: 'audioSkinsRevertAudio', ...base }) },
    ]);
  }

  document.addEventListener('click', ev => {
    const t = ev.target;
    if (!t.closest?.('#skinsView') || !t.closest('#v2Skins')) return;
    const q = sel => t.closest(sel);
    let el;
    if ((el = q('[data-v2s]'))) { if (!el.disabled) actions[el.dataset.v2s]?.(el); return; }
    if ((el = q('[data-v2s-tab]'))) { CS()?.switchTab(el.dataset.v2sTab); return; }
    if ((el = q('[data-v2s-card]'))) {
      K.cardScheme = el.dataset.v2sCard; K.cardCat = 'all'; K.cardQ = '';
      if (SK()) { SK().selectedSkin = K.cardScheme; post({ type: 'skinsGetCards', name: K.cardScheme }); }
      return render();
    }
    if ((el = q('[data-v2s-cat]'))) { K.cardCat = el.dataset.v2sCat; return render(); }
    if ((el = q('[data-v2s-tcard]'))) { const c = (SK()?.cards || []).find(x => x.fileName === el.dataset.v2sTcard); if (c) cardModal(c); return; }
    if ((el = q('[data-v2s-audio]'))) return selectAudio(el.dataset.v2sAudio);
    if ((el = q('[data-v2s-acat]'))) { K.aCat = el.dataset.v2sAcat; K.aChar = 'all'; K.aLimit = 200; return render(); }
    if ((el = q('[data-v2s-astatus]'))) { K.aStatus = el.dataset.v2sAstatus; K.aLimit = 200; return render(); }
    if ((el = q('[data-v2s-play]'))) { if (!el.disabled && el.dataset.v2sPlay) AU()?.playAudio(el.dataset.v2sPlay, el); return; }
    if ((el = q('[data-v2s-aitem]'))) { const i = aItems().find(x => String(x.id) === el.dataset.v2sAitem); if (i) audioItemMenu(el, i); return; }
    if ((el = q('[data-v2s-rmenu]'))) {
      const m = (rStatus()?.mods || []).find(x => x.id === el.dataset.v2sRmenu); if (!m) return;
      U().openMenu(el, [{ label: '删除资源包…', icon: I().trash, disabled: rRunning(), fn: () => confirm({
        title: `删除资源包「${esc(m.name)}」？`,
        lines: [`会删除导入的资源包文件（${fmtBytes(m.size)}），之后只能重新导入`, m.enabled && '它现在是开启状态。删除后点一次「应用」，游戏才会撤掉它的修改'],
        onOk: () => post({ type: 'resourceModsDelete', id: m.id, name: m.name }),
      }) }]);
    }
  });
  document.addEventListener('input', ev => {
    const t = ev.target;
    if (t.dataset?.v2sAvol) t.nextElementSibling.textContent = Math.round(t.value * 100) + '%';
  });
  document.addEventListener('change', ev => {
    const t = ev.target;
    if (!t.closest?.('#v2Skins')) return;
    if (t.id === 'v2sGame') {
      if (!t.value) return;
      post({ type: 'gameProfileSetActive', id: t.value });
      // 卡牌由 GameLibModule 自动刷新；音效、资源包要重新向宿主要一次新游戏的数据
      if (tab() === 'audio' || tab() === 'resource') CS()?.switchTab(tab());
      return;
    }
    if (t.dataset.v2sAvol) { const i = aItems().find(x => String(x.id) === t.dataset.v2sAvol); if (i) post({ type: 'audioSkinsSetVolume', schemeId: K.audioScheme, configId: i.id, eventName: i.name, volume: Number(t.value) }); return; }
    if (t.id === 'v2sAChar') { K.aChar = t.value; K.aLimit = 200; return render(); }
    if (t.dataset.v2sRtoggle) {
      const m = (rStatus()?.mods || []).find(x => x.id === t.dataset.v2sRtoggle);
      if (m) post({ type: 'resourceModsToggle', id: m.id, enabled: t.checked, name: m.name });
    }
  });

  // 包一层原版方法：跑完后重画
  function wrap(obj, names, before) {
    if (!obj) return;
    names.forEach(name => {
      const orig = obj[name]; if (typeof orig !== 'function') return;
      obj[name] = function (...args) { before?.(name, args); const r = orig.apply(this, args); render(); return r; };
    });
  }
  // 宿主提取完不管拿到几个都提示「已同步并就绪 N 个」；N = 0 时这句话会让人以为成功了，换成实话
  const origToast = window.toast;
  if (typeof origToast === 'function') {
    window.toast = function (msg, ...rest) {
      if (/^已同步并就绪 0 个原版音频/.test(String(msg))) msg = '没有提取到原版音频：电脑上没有事先导出的 audio-export 文件夹（替换、静音不受影响）';
      return origToast.call(this, msg, ...rest);
    };
  }

  document.addEventListener('DOMContentLoaded', () => {
    wrap(window.GameLibModule, ['renderProfiles']);
    wrap(SK(), ['renderProfiles', 'renderCards', 'onCardUpdated', 'legacyImported', 'onAnimationState']);
    wrap(CS(), ['switchTab']);
    wrap(AU(), ['renderProfiles', 'renderItems', 'renderExtractProgress', 'renderExtractDone'], (name, args) => {
      if (name === 'renderExtractProgress') K.extract = args[0] || K.extract;
      if (name === 'renderExtractDone') { K.extract = null; K.extractChecking = true; if (AU()?.selectedSchemeId) post({ type: 'audioSkinsGetItems', schemeId: AU().selectedSchemeId }); }
      // 提取完重新拿到条目后：一个原版都没有，就说明是「0 个」那种情况
      if (name === 'renderItems' && K.extractChecking) { K.extractChecking = false; K.extractEmpty = !(args[0]?.items || []).some(i => i.hasOriginal); }
    });
    wrap(RS(), ['render', 'renderProgress', 'renderPaused', 'setBusy'], (name, args) => { if (name === 'renderProgress') K.resP = args[0]; });
    render();
  });
})();
