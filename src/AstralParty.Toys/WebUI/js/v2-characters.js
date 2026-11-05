// v2-characters.js — 新界面：自定义 → 角色
// 两层：方案（哪个人物用哪套皮肤，CharacterSchemesModule）和人物皮肤包（图片 / 动画 / 文案 / 布局，CharacterSkinsModule）。
// 重做：方案列表、方案页、添加 / 编辑人物的三步向导、皮肤包编辑器。
// 沿用原版：新建皮肤包的表单（可从游戏复制模板）、图片裁切窗口。
// 注意：清单（manifest）的键名可能是大写开头（Images / images），读写都走 field()，保存时整份按原样送回。
(function () {
  const C = { scheme: null, pkg: null, pkgTab: 'images', action: 'Idle', frame: 0, timer: null, wiz: null, pendingScheme: null };
  const U = () => window.V2UI;
  const I = () => window.V2UI.ICON;
  const VS = () => window.V2Skins;
  const CSM = () => window.CharacterSchemesModule;
  const CSK = () => window.CharacterSkinsModule;
  const field = (o, k) => o?.[k] ?? o?.[k[0].toUpperCase() + k.slice(1)];
  // 取对象里的子对象；没有就按清单现有的大小写风格建一个挂上去（否则改了也存不回去）
  function ensure(o, k) {
    const cur = field(o, k);
    if (cur && typeof cur === 'object') return cur;
    const pascal = Object.keys(o).some(x => /^[A-Z]/.test(x));
    return (o[pascal ? k[0].toUpperCase() + k.slice(1) : k] = {});
  }

  const data = () => CSM()?.data || CSK()?.data || null;
  const heroes = () => data()?.heroes || [];
  const heroName = id => heroes().find(h => h.id === id)?.name || `角色 ${id}`;
  const targetsOf = hero => (data()?.targets || []).filter(t => field(t, 'heroId') === hero);
  const skinName = (hero, item) => field(targetsOf(hero).find(t => field(t, 'itemId') === item), 'name') || '未识别的皮肤';
  const packages = () => data()?.profiles || [];
  const pkgOf = id => packages().find(p => field(p, 'id') === id);
  const pkgName = p => field(field(p, 'manifest'), 'name') || field(p, 'id') || '自定义皮肤包';
  const schemes = () => data()?.schemeProfiles || [];
  const schemeOf = id => schemes().find(p => field(p, 'id') === id);
  const rulesOf = p => field(field(p, 'scheme'), 'rules') || [];
  const peopleCount = p => new Set(rulesOf(p).map(r => field(r, 'heroId'))).size;
  const imageUrl = (pkgId, path) => 'https://characters.astral.local/' + encodeURIComponent(pkgId) + '/' + String(path).split(/[\\/]/).map(encodeURIComponent).join('/');
  const sendPkg = (type, extra = {}) => { if (CSK()) CSK().selected = C.pkg; post({ type, id: C.pkg, ...extra }); };

  /* ---------- 方案列表 ---------- */
  function listHtml() {
    const d = data();
    const head = VS().frameHead(`<button class="v2-btn ${d && (!d.installed || d.updateRequired) ? 'line' : 'primary'}" data-v2c="addMenu" data-v2menu>${I().plus}新建方案${I().down}</button>`);
    if (!d) return head + '<div class="v2-card v2-empty">正在读取角色皮肤…</div>';
    const rt = d.runtimeStatus;
    return head + `
      ${!d.installed || d.updateRequired ? `<div class="v2-card"><div class="v2-hero" style="padding:14px 18px"><div class="v2-hero-text">
          <div class="v2-hero-title" style="font-size:15px">角色皮肤模组 <span class="v2-badge ${d.installed ? 'warn' : 'off'}">${d.installed ? '需要更新' : '未安装'}</span></div>
          <div class="v2-hero-sub">${d.installed ? `游戏里是 ${esc(d.installedVersion || '未知版本')}，请更新到 ${esc(d.bundledVersion || '')} 并重启游戏` : '先安装角色皮肤模组，方案才能在游戏里生效'}</div></div>
          <button class="v2-btn primary" data-v2c="install">${d.installed ? '更新' : '安装'}</button></div></div>` : ''}
      ${field(rt, 'state') === 'Failed' ? `<div class="v2-notice">${I().info}<span>上次进游戏时方案没能应用：${esc(field(rt, 'error') || '原因未知')}</span></div>` : ''}
      <div class="v2-grid">${schemes().map(p => {
        const id = field(p, 'id'), s = field(p, 'scheme'), on = !!field(p, 'active'), err = field(p, 'error');
        return `<button class="v2-scheme ${on ? 'active' : ''}" data-v2c-scheme="${esc(id)}">
          <div class="thumb" style="background:${err ? 'linear-gradient(135deg,#3a2f1a,#1f2438)' : VS().thumbBg(id)}">${err ? '!' : esc(String(field(s, 'name') || '?').slice(0, 1))}${on ? '<span class="v2-badge pink">已启用</span>' : ''}</div>
          <div class="info"><b title="${esc(field(s, 'name') || '未命名方案')}">${esc(field(s, 'name') || '未命名方案')}</b><small>${err ? '<span style="color:var(--gp-gold)">需要修复</span>' : `${peopleCount(p)} 个人物`}</small></div></button>`;
      }).join('') || '<div class="v2-card v2-empty" style="grid-column:1/-1">还没有方案。新建一个，把喜欢的人物皮肤放进去，之后可以一键切换整套搭配。</div>'}</div>
      <p class="v2-muted" style="margin-top:12px">同一时间只能启用一个角色方案</p>
      <details class="v2-card" style="margin-top:14px"><summary><div class="t"><b>人物皮肤包</b><small>${packages().length} 个 · 导入、打开目录</small></div>${I().down}</summary>
        <div class="v2-adv">
          ${packages().map(p => `<div class="v2-row link" data-v2c-pkg="${esc(field(p, 'id'))}"><span class="n">${esc(pkgName(p))}</span>
            <span class="d">${field(p, 'error') ? `<span style="color:var(--gp-gold)">${esc(field(p, 'error'))}</span>` : esc(heroName(field(field(p, 'manifest'), 'heroId')))}</span>${I().right}</div>`).join('')}
          <div class="v2-row"><span class="n">导入皮肤包</span><span class="d">ZIP 或旧式皮肤包</span><button class="v2-btn line" data-v2c="pkgImportMenu" data-v2menu>导入…</button></div>
          <div class="v2-row"><span class="n">打开目录</span><span class="d">方案 / 皮肤包存放的位置</span><button class="v2-btn line" data-v2c="folderMenu" data-v2menu>选择…</button></div>
          ${d.installed && !d.updateRequired ? '<div class="v2-row"><span class="n">重装角色皮肤模组</span><span class="d">把游戏里的角色皮肤模组装回程序附带的版本</span><button class="v2-btn line" data-v2c="install">重装</button></div>' : ''}
        </div></details>`;
  }

  /* ---------- 方案页 ---------- */
  function schemeHtml() {
    const p = schemeOf(C.scheme);
    if (!p) { C.scheme = null; return listHtml(); }
    const s = field(p, 'scheme'), on = !!field(p, 'active'), err = field(p, 'error');
    const meta = [`${peopleCount(p)} 个人物`, field(s, 'author') && '作者 ' + field(s, 'author'), field(s, 'description')].filter(Boolean).map(esc).join(' · ');
    // 面包屑要用本文件的 data-v2c（VS().crumb 生成的是卡牌分区的 data-v2s，点了没反应）
    const crumb = `<div class="v2-crumb"><button data-v2c="v2cBack">自定义</button>${I().right}<span class="cur">${esc(field(s, 'name') || '未命名方案')}</span></div>`;
    return `<div class="v2-head">${crumb}<span class="v2-top-spacer"></span>
        ${on ? '<span class="v2-badge pink">已启用 · 重启游戏生效</span><button class="v2-btn line" data-v2c="toggleActive">停用</button>'
          : `<button class="v2-btn primary" data-v2c="toggleActive" ${err ? 'disabled' : ''}>${I().check}应用此方案</button>`}
        <button class="v2-icon-btn" data-v2c="schemeMenu" data-v2menu title="更多">${I().more}</button></div>
      <p class="v2-muted" style="margin:-10px 0 14px">${meta}${on ? '　添加或修改人物后，完全退出并重启游戏才会生效' : ''}</p>
      ${err ? `<div class="v2-notice">${I().info}<span style="flex:1">${esc(err)}</span><button class="v2-btn line" data-v2c="openSchemeFolder">打开目录修复</button></div>` : `
      <div class="v2-card">
        <div class="v2-card-title"><span>方案中的人物</span><button class="v2-btn ghost" data-v2c="addHero">${I().plus}添加人物</button></div>
        ${rulesOf(p).map(r => {
          const hero = field(r, 'heroId'), off = field(r, 'officialSourceSkinItemId'), cp = field(r, 'customPackageId'), tgt = field(r, 'targetSkinItemId');
          const pk = cp && pkgOf(cp);
          const source = cp ? (off ? '自定义 + 官方基础' : '自定义皮肤') : '官方皮肤';
          const summary = cp ? `${pk ? pkgName(pk) : '找不到皮肤包'}${off ? ' + ' + skinName(hero, off) : ''}` : skinName(hero, off);
          return `<div class="v2-row link" ${cp ? `data-v2c-pkg="${esc(cp)}"` : `data-v2c-rule="${esc(field(r, 'id'))}"`}>
            <span class="k" style="color:var(--v2-text);font-weight:500">${esc(heroName(hero))}</span>
            <span class="v" style="white-space:normal">${esc(summary)} <span class="v2-tag">${source}</span><br>
              <span class="v2-muted">${tgt ? '只替换「' + esc(skinName(hero, tgt)) + '」' : '替换这个人物的全部原皮肤'}</span></span>
            ${cp ? `<button class="v2-icon-btn" data-v2c-rule="${esc(field(r, 'id'))}" title="来源与替换范围">${I().sliders}</button><span class="v2-muted">编辑</span>` : ''}
            ${I().right}</div>`;
        }).join('') || '<div class="v2-empty">还没有人物。点「添加人物」开始搭配：可以用官方皮肤、已有的皮肤包，或者自己做一个。</div>'}
      </div>`}`;
  }

  /* ---------- 三步向导：添加 / 编辑人物 ---------- */
  function openWizard(ruleId) {
    const r = ruleId && rulesOf(schemeOf(C.scheme)).find(x => field(x, 'id') === ruleId);
    C.wiz = r ? { step: 2, ruleId, heroId: field(r, 'heroId'), mode: field(r, 'customPackageId') ? 'package' : 'official',
      official: field(r, 'officialSourceSkinItemId') || 0, pkg: field(r, 'customPackageId') || '', target: field(r, 'targetSkinItemId') || 0, error: '' }
      : { step: 1, ruleId: null, heroId: null, mode: null, official: 0, pkg: '', target: 0, error: '' };
    drawWizard();
  }

  function drawWizard() {
    const W = C.wiz, rules = rulesOf(schemeOf(C.scheme));
    const steps = ['选人物', '选外观', '替换范围'];
    const bar = steps.map((s, i) => `<span class="v2-wz-step ${W.step === i + 1 ? 'on' : W.step > i + 1 ? 'done' : ''}">${W.step > i + 1 ? I().check : i + 1} ${s}</span>`).join('<span class="v2-wz-line"></span>');
    let body = '';
    if (W.step === 1) {
      body = `<p class="v2-muted" style="margin:0 0 12px">给哪个人物换皮肤？</p><div class="v2-choices">${heroes().map(h => {
        const n = rules.filter(r => field(r, 'heroId') === h.id).length;
        return `<button class="v2-choice ${W.heroId === h.id ? 'on' : ''}" data-v2c-wz="hero" data-v="${h.id}"><b>${esc(h.name)}</b><small>${n ? `方案里已有 ${n} 条` : '还没配置'}</small></button>`;
      }).join('')}</div>`;
    } else if (W.step === 2) {
      const pk = packages().filter(p => !field(p, 'error') && field(field(p, 'manifest'), 'heroId') === W.heroId), tg = targetsOf(W.heroId);
      const opt = (k, t, d) => `<button class="v2-choice row ${W.mode === k ? 'on' : ''}" data-v2c-wz="mode" data-v="${k}"><b>${t}</b><small>${d}</small></button>`;
      body = `<p class="v2-muted" style="margin:0 0 12px">「${esc(heroName(W.heroId))}」用什么外观？</p>
        ${opt('official', '官方皮肤', '直接换成游戏里已有的另一款皮肤')}
        ${W.mode === 'official' ? `<div class="v2-subpick">${tg.map(t => `<button class="v2-chip ${W.official === field(t, 'itemId') ? 'on' : ''}" data-v2c-wz="official" data-v="${field(t, 'itemId')}">${esc(field(t, 'name'))}</button>`).join('') || '<span class="v2-muted">没有可选的官方皮肤</span>'}</div>` : ''}
        ${opt('package', '已有的皮肤包', pk.length ? `这个人物有 ${pk.length} 个自定义皮肤包` : '这个人物还没有皮肤包，可以从 ZIP 导入')}
        ${W.mode === 'package' ? `<div class="v2-subpick">${pk.map(p => `<button class="v2-chip ${W.pkg === field(p, 'id') ? 'on' : ''}" data-v2c-wz="pkg" data-v="${esc(field(p, 'id'))}">${esc(pkgName(p))}</button>`).join('')}
          <button class="v2-chip ghost" data-v2c-wz="importPkg">${I().upload}从 ZIP 导入…</button></div>` : ''}
        ${W.ruleId ? '' : opt('create', '自己做一个', '新建皮肤包：下一步之后会打开创建表单，可以从游戏复制原皮肤当模板')}`;
    } else {
      const tg = targetsOf(W.heroId), custom = W.mode !== 'official';
      body = `<p class="v2-muted" style="margin:0 0 12px">替换「${esc(heroName(W.heroId))}」的哪些原皮肤？</p>
        <div class="v2-radios">
          <label><input type="radio" name="v2wzt" value="0" ${W.target === 0 ? 'checked' : ''}> 全部原皮肤 <span class="v2-muted">（推荐）</span></label>
          ${tg.map(t => `<label><input type="radio" name="v2wzt" value="${field(t, 'itemId')}" ${W.target === field(t, 'itemId') ? 'checked' : ''}> 只替换「${esc(field(t, 'name'))}」</label>`).join('')}
        </div>
        ${custom ? `<details class="v2-mini" ${W.official ? 'open' : ''}><summary>混合搭配（可选）</summary>
          <p class="v2-muted" style="margin:6px 0">自定义素材优先，没改的部分沿用：</p>
          <select id="v2wzBase" class="v2-select"><option value="0">原皮肤</option>${tg.map(t => `<option value="${field(t, 'itemId')}" ${W.official === field(t, 'itemId') ? 'selected' : ''}>官方「${esc(field(t, 'name'))}」</option>`).join('')}</select></details>` : ''}
        <div class="v2-wz-summary">${esc(heroName(W.heroId))} → ${W.mode === 'official' ? `官方「${esc(skinName(W.heroId, W.official))}」` : W.mode === 'create' ? '新的自定义皮肤包' : esc(pkgName(pkgOf(W.pkg)))}</div>`;
    }
    const canNext = W.step === 1 ? !!W.heroId : W.step === 2 ? (W.mode === 'official' ? W.official > 0 : W.mode === 'package' ? !!W.pkg : W.mode === 'create') : true;
    const finish = W.mode === 'create' ? '下一步：创建皮肤包' : W.ruleId ? '保存' : '添加到方案';
    U().openModal(`
      <div class="v2-modal-head"><b>${W.ruleId ? '编辑人物皮肤' : '添加人物皮肤'}</b><button class="v2-icon-btn" data-v2modal="close">${I().x}</button></div>
      <div class="v2-wz-bar">${bar}</div>
      <div class="v2-wz-body">${body}${W.error ? `<p class="v2-err" style="margin-top:12px">${esc(W.error)}</p>` : ''}</div>
      <div class="v2-modal-foot">
        ${W.step > 1 && !(W.ruleId && W.step === 2) ? '<button class="v2-btn ghost" data-v2c-wz="back">上一步</button>' : ''}
        ${W.ruleId ? '<button class="v2-btn danger" data-v2c-wz="remove">从方案移除</button>' : ''}
        <span class="v2-top-spacer"></span>
        ${W.step < 3 ? `<button class="v2-btn primary" data-v2c-wz="next" ${canNext ? '' : 'disabled'}>下一步</button>`
          : `<button class="v2-btn primary" data-v2c-wz="finish">${finish}</button>`}
      </div>`, 'wide');
  }

  function wizardFinish() {
    const W = C.wiz, csm = CSM();
    W.target = Number(document.querySelector('input[name="v2wzt"]:checked')?.value || 0);
    const base = $('v2wzBase'); if (base) W.official = Number(base.value);
    if (W.mode === 'official') W.pkg = '';
    csm.selected = C.scheme;
    const copy = csm.copyScheme();
    if (copy.Rules.some(r => r.Id !== W.ruleId && r.HeroId === W.heroId && r.TargetSkinItemId === W.target)) {
      W.error = '这个人物的同一款原皮肤已经配置过了，请编辑原来那一条。'; return drawWizard();
    }
    if (W.mode === 'create') {
      // 新建皮肤包走原版表单：先把原版绑定表单的字段填好，再调用它的 createPackageFromBinding
      U().closeModal();
      csm.bindingSchemeId = C.scheme; csm.editing = null;
      // 人物下拉框的选项原本由原版 openBinding 填，新界面不走那里，要先填好，否则赋值无效、人物变成 0
      $('characterBindingHero').innerHTML = heroes().map(h => `<option value="${h.id}">${esc(h.name)}</option>`).join('');
      $('characterBindingHero').value = String(W.heroId); csm.bindingOptions();
      $('characterBindingTarget').value = String(W.target);
      $('characterBindingOfficial').value = String(W.official || 0);
      $('characterBindingPackage').value = '';
      csm.createPackageFromBinding();
      // createPackageFromBinding 里的 form.reset() 会清空缓存路径；留空时导出找不到游戏后来下载的皮肤资源
      if (!$('characterTemplateCache').value) $('characterTemplateCache').value = data()?.templateCache || '';
      C.wiz = null; return;
    }
    const rule = { Id: W.ruleId || 'rule-' + crypto.randomUUID(), HeroId: W.heroId, TargetSkinItemId: W.target,
      OfficialSourceSkinItemId: W.mode === 'official' ? W.official : (W.official || 0), CustomPackageId: W.mode === 'official' ? '' : W.pkg };
    copy.Rules = copy.Rules.filter(r => r.Id !== rule.Id).concat(rule);
    C.wiz.saving = true;
    post({ type: 'characterSchemesSave', id: C.scheme, scheme: copy });   // 结果由 saved() 回来
  }

  const wizActs = {
    hero: v => Object.assign(C.wiz, { heroId: Number(v), mode: null, official: 0, pkg: '', target: 0, error: '' }),
    mode: v => { if (C.wiz.mode !== v) Object.assign(C.wiz, { mode: v, official: 0, pkg: '' }); },
    official: v => { C.wiz.official = Number(v); },
    pkg: v => { C.wiz.pkg = v; },
    next: () => { C.wiz.step++; C.wiz.error = ''; },
    back: () => { C.wiz.step--; C.wiz.error = ''; },
    finish: () => { wizardFinish(); return 'stop'; },
    importPkg: () => {
      U().closeModal();
      post({ type: 'characterSchemesImportPackage', id: C.scheme, gameDirectory: data()?.gameDirectory,
        binding: { Id: 'rule-' + crypto.randomUUID(), HeroId: C.wiz.heroId, TargetSkinItemId: 0, OfficialSourceSkinItemId: 0, CustomPackageId: '' } });
      C.wiz = null; return 'stop';
    },
    remove: () => {
      const W = C.wiz, r = rulesOf(schemeOf(C.scheme)).find(x => field(x, 'id') === W.ruleId);
      U().confirmDelete({
        title: `把「${esc(heroName(W.heroId))}」从方案移除？`, ok: '移除',
        lines: ['只删掉方案里的这一条配置', field(r, 'customPackageId') && '用到的人物皮肤包不会被删除，以后还能再加回来',
          field(schemeOf(C.scheme), 'active') && '这个方案正在启用，完全退出并重启游戏后生效'],
        onOk: () => { post({ type: 'characterSchemesRemoveRule', id: C.scheme, ruleId: W.ruleId }); C.wiz = null; },
        onCancel: () => drawWizard(),
      });
      return 'stop';
    },
  };

  /* ---------- 皮肤包编辑器 ---------- */
  const LABELS = { Character: '完整立绘', SfwCharacter: 'SFW 立绘', ProfilePhoto: '角色头像', CharacterMap: '倒计时 / 地图像素头像', InGameCharacter: 'HUD 角色图',
    InGameCharacterSFW: 'SFW HUD', RolePhoto: '角色缩略图', SfwRolePhoto: 'SFW 缩略图', CharacterReady: '准备界面', SfwCharacterReady: 'SFW 准备图',
    CharacterThin: '窄幅角色图', SfwCharacterThin: 'SFW 窄幅图', LandIcon: '地块标志', Bust0: '战斗半身图 1', Bust1: '战斗半身图 2', SfwBust0: 'SFW 半身图 1',
    SfwBust1: 'SFW 半身图 2', Name: '角色名字', Nick: '称号', Biography: '角色简介', Lines: '台词', SkinLong: '皮肤故事', SkinShort: '皮肤短描述' };
  function slotLabel(slot) {
    if (LABELS[slot]) return LABELS[slot];
    const a = slot.startsWith('asset:') ? slot.slice(6) : slot;
    if (a.startsWith('UT_AccountBackground_')) return '账号背景';
    if (a.startsWith('UT_Item_AccountBackground_')) return '背景物品图标';
    if (a.startsWith('UT_Item_StandingPainting_')) return '立绘物品图标';
    return a;
  }
  const ACTIONS = [['Idle', '待机'], ['Walk', '正面行走'], ['Walk-back', '背面行走'], ['Show', ''], ['Talent', ''], ['Hit', ''], ['Hit-01', ''], ['Hit-02', ''],
    ['Cheer', ''], ['Cry', ''], ['Die', ''], ['Eat', ''], ['Hospitalized', ''], ['Vomit', ''], ['Lose', ''], ['Electricshock', '']];
  const stopAnim = () => { clearTimeout(C.timer); C.timer = null; };

  function pkgHtml() {
    const p = pkgOf(C.pkg);
    if (!p) { C.pkg = null; return C.scheme ? schemeHtml() : listHtml(); }
    const m = field(p, 'manifest') || {}, back = C.scheme ? schemeOf(C.scheme) : null;
    const tabs = [['images', '图片'], ['anim', '动画'], ['text', '文案'], ['layout', '布局']]
      .map(([k, l]) => `<button class="${C.pkgTab === k ? 'on' : ''}" data-v2c-ptab="${k}">${l}</button>`).join('');
    const crumb = `<div class="v2-crumb"><button data-v2c="v2cBack">自定义</button>${I().right}${back ? `<button data-v2c="pkgBack">${esc(field(field(back, 'scheme'), 'name'))}</button>${I().right}` : ''}<span class="cur">${esc(pkgName(p))}</span></div>`;
    let body;
    if (field(p, 'error')) body = `<div class="v2-notice">${I().info}<span style="flex:1">${esc(field(p, 'error'))}</span><button class="v2-btn line" data-v2c="pkgOpen">打开目录修复</button></div>`;
    else if (C.pkgTab === 'images') {
      const images = field(m, 'images') || {};
      const slots = [...(data()?.imageSlots || []), ...Object.keys(images).filter(s => s.startsWith('asset:'))];
      const tile = s => `<button class="v2-slot ${images[s] ? 'set' : ''}" data-v2c-slot="${esc(s)}" data-v2menu>
        <div class="img">${images[s] ? `<img src="${esc(imageUrl(C.pkg, images[s]))}" alt="" loading="lazy">` : I().plus}</div>
        <b>${esc(slotLabel(s))}</b><small>${images[s] ? '已自定义' : '使用原版'}</small></button>`;
      const main = slots.filter(s => !/sfw/i.test(s)), sfw = slots.filter(s => /sfw/i.test(s));
      body = `<div class="v2-slots">${main.map(tile).join('')}</div>
        ${sfw.length ? `<details class="v2-card" style="margin-top:14px"><summary><div class="t"><b>SFW 替代图</b><small>${sfw.filter(s => images[s]).length} / ${sfw.length} 已自定义 · 游戏开启 SFW 时使用</small></div>${I().down}</summary>
          <div class="v2-slots" style="padding:0 18px 16px">${sfw.map(tile).join('')}</div></details>` : ''}`;
    } else if (C.pkgTab === 'anim') {
      const anims = field(m, 'animations') || {};
      const list = ACTIONS.concat(Object.keys(anims).filter(k => !ACTIONS.some(a => a[0] === k)).map(k => [k, '']));
      const a = anims[C.action], frames = field(a, 'frames') || [];
      if (C.frame >= frames.length) C.frame = 0;
      body = `<div class="v2-anim">
        <div class="v2-card v2-anim-list">${list.map(([k, zh]) => `<button class="${C.action === k ? 'on' : ''}" data-v2c-action="${esc(k)}">
          <span class="v2-dot-s ${anims[k] ? '' : 'off'}"></span>${esc(k)}${zh ? ` <span class="v2-muted">${zh}</span>` : ''}</button>`).join('')}</div>
        <div style="flex:1;min-width:0">
          <div class="v2-card v2-anim-preview">${frames.length ? `<img id="v2cFrame" src="${esc(imageUrl(C.pkg, frames[C.frame]))}" alt="${esc(C.action)} 第 ${C.frame + 1} 帧">`
            : `<div class="v2-empty">「${esc(C.action)}」还没有自定义帧，游戏里沿用原版动作</div>`}</div>
          ${frames.length ? `<div class="v2-frames">${frames.map((f, i) => `<button class="${C.frame === i ? 'on' : ''}" data-v2c-frame="${i}"><img src="${esc(imageUrl(C.pkg, f))}" alt="" loading="lazy"><span>${i + 1}</span></button>`).join('')}</div>` : ''}
          <div class="v2-toolbar" style="margin-top:12px">
            <button class="v2-btn ${frames.length ? 'line' : 'primary'}" data-v2c="animImport">${I().upload}导入帧序列…</button>
            ${frames.length ? `<button class="v2-btn line" data-v2c="animPlay">${I().play}${C.timer ? '停止' : '播放'}</button>
              <span class="v2-muted">FPS</span><input id="v2cFps" class="v2-num" type="number" min="1" max="120" value="${Number(field(a, 'fps')) || 15}">
              <label class="v2-muted" style="display:flex;align-items:center;gap:6px">循环 <label class="v2-switch"><input type="checkbox" id="v2cLoop" ${field(a, 'loop') !== false ? 'checked' : ''}><span></span></label></label>
              <span class="v2-top-spacer"></span><button class="v2-icon-btn" data-v2c="animMenu" data-v2menu title="更多">${I().more}</button>` : ''}
          </div>
          <p class="v2-muted" style="margin-top:4px">PNG / JPG 帧按文件名排序。点某一帧可以单独替换。FPS 和循环改了就自动保存。</p>
        </div></div>`;
    } else if (C.pkgTab === 'text') {
      const texts = field(m, 'texts') || {};
      body = `<div class="v2-card" style="padding:16px 18px">${(data()?.textSlots || []).map(s => `<div class="v2-field"><label>${esc(slotLabel(s))} <span class="v2-muted">${esc(s)}</span></label>
        <textarea data-v2c-text="${esc(s)}" rows="${/Biography|Lines|SkinLong/.test(s) ? 3 : 1}" placeholder="留空使用原版">${esc(texts[s] || '')}</textarea>
        ${/^(Name|Biography)$/.test(s) ? '<small style="margin-top:4px">这是人物共用文案，可能也会影响这个人物的其他皮肤</small>' : ''}</div>`).join('')}
        <button class="v2-btn primary" data-v2c="saveTexts">保存文案</button></div>`;
    } else {
      const L = field(m, 'layout') || {};
      const f = (k, label, hint, min, max, step, def) => `<div class="v2-field"><label>${label}</label><small>${hint}</small>
        <input type="number" data-v2c-layout="${k}" min="${min}" max="${max}" step="${step}" value="${esc(field(L, k[0].toLowerCase() + k.slice(1)) ?? def)}"></div>`;
      body = `<div class="v2-card" style="padding:16px 18px">
        <p class="v2-muted" style="margin:0 0 14px">控制人物在地图上的位置和大小，不影响碰撞和移动。</p>
        ${f('PivotX', '脚底锚点 X', '0～1，0.5 是正中', 0, 1, 0.01, 0.5)}${f('PivotY', '脚底锚点 Y', '0～1，从画布底部往上算', 0, 1, 0.01, 0.1)}
        ${f('PixelsPerUnit', '每单位像素 PPU', '图越大这个值越大', 1, 10000, 1, 100)}${f('Scale', '人物缩放', '乘在 PPU 上，1 = 原大小', 0.01, 20, 0.01, 1)}
        <button class="v2-btn primary" data-v2c="saveLayout">保存布局</button></div>
        <details class="v2-card"><summary><div class="t"><b>完整清单（高级）</b><small>直接编辑 JSON：原精灵名映射、自定义资源地址等</small></div>${I().down}</summary>
          <div style="padding:0 18px 16px"><textarea id="v2cManifest" class="v2-code" spellcheck="false">${esc(JSON.stringify(m, null, 2))}</textarea>
          <button class="v2-btn line" data-v2c="saveManifest" style="margin-top:8px">保存清单</button></div></details>`;
    }
    return `<div class="v2-head">${crumb}<span class="v2-top-spacer"></span>
        <button class="v2-icon-btn" data-v2c="pkgMenu" data-v2menu title="更多">${I().more}</button></div>
      <p class="v2-muted" style="margin:-10px 0 14px">${esc(heroName(field(m, 'heroId')))}${field(m, 'author') ? ' · 作者 ' + esc(field(m, 'author')) : ''} · 改完后完全退出并重启游戏生效</p>
      <div class="v2-tabs" style="margin-bottom:14px">${tabs}</div>${body}`;
  }

  function saveManifest(msg) {
    const p = pkgOf(C.pkg); if (!p) return;
    sendPkg('characterSkinsSave', { manifest: field(p, 'manifest') });
    if (msg) toast(msg);
  }

  // 按每帧时长（durationsMs，没有就按 FPS）逐帧换图；不循环的动作播到最后一帧停
  function playTick() {
    const anims = field(field(pkgOf(C.pkg), 'manifest'), 'animations') || {}, an = anims[C.action], frames = field(an, 'frames') || [];
    if (!frames.length) return stopAnim();
    const ms = (field(an, 'durationsMs') || [])[C.frame] || 1000 / (Number(field(an, 'fps')) || 15);
    C.timer = setTimeout(() => {
      if (C.frame === frames.length - 1 && field(an, 'loop') === false) { stopAnim(); return VS().render(); }
      C.frame = (C.frame + 1) % frames.length;
      const img = $('v2cFrame'); if (img) img.src = imageUrl(C.pkg, frames[C.frame]);
      document.querySelectorAll('[data-v2c-frame]').forEach(b => b.classList.toggle('on', Number(b.dataset.v2cFrame) === C.frame));
      playTick();
    }, ms);
  }

  /* ---------- 操作 ---------- */
  function openPkg(id) {
    stopAnim();
    C.pkg = id; C.pkgTab = 'images'; C.action = 'Idle'; C.frame = 0;
    if (CSK()) CSK().selected = id;
    VS().render();
  }

  const actions = {
    addMenu: el => U().openMenu(el, [
      { label: '新建方案…', icon: I().plus, fn: () => VS().metaForm({ title: '新建角色方案', ok: '创建',
        fields: [{ key: 'name', label: '方案名称', required: true, hint: '先给搭配起个名字，之后再添加人物皮肤' }],
        onOk: v => { const id = 'scheme-' + crypto.randomUUID(); C.pendingScheme = id; if (CSM()) CSM().pending = id; post({ type: 'characterSchemesCreate', id, name: v.name }); } }) },
      { label: '导入方案 ZIP…', icon: I().upload, fn: () => post({ type: 'characterSchemesImport' }) },
    ]),
    install: () => post({ type: 'characterSkinsInstall' }),
    pkgImportMenu: el => U().openMenu(el, [
      { label: '导入皮肤包 ZIP…', icon: I().upload, fn: () => post({ type: 'characterSkinsImport', id: '' }) },
      { label: '导入旧式皮肤包…', icon: I().upload, fn: () => post({ type: 'characterSkinsImportLegacy', id: '' }) },
    ]),
    folderMenu: el => U().openMenu(el, [
      { label: '方案目录', icon: I().folder, fn: () => post({ type: 'characterSchemesOpen', id: '' }) },
      { label: '皮肤包目录', icon: I().folder, fn: () => post({ type: 'characterSkinsOpen', id: '' }) },
    ]),
    v2cBack: () => { stopAnim(); C.scheme = null; C.pkg = null; VS().render(); },
    pkgBack: () => { stopAnim(); C.pkg = null; VS().render(); },
    toggleActive: () => { const p = schemeOf(C.scheme); post({ type: 'characterSchemesActive', id: field(p, 'active') ? '' : C.scheme }); },
    openSchemeFolder: () => post({ type: 'characterSchemesOpen', id: C.scheme }),
    addHero: () => openWizard(null),
    schemeMenu: el => {
      const p = schemeOf(C.scheme), s = field(p, 'scheme'), err = field(p, 'error');
      U().openMenu(el, [
        { label: '编辑方案信息…', icon: I().sliders, disabled: !!err, fn: () => VS().metaForm({ title: '编辑方案信息', ok: '保存',
          fields: [{ key: 'name', label: '方案名称', required: true, value: field(s, 'name') }, { key: 'author', label: '作者', value: field(s, 'author') }, { key: 'description', label: '简介', multiline: true, value: field(s, 'description') }],
          onOk: v => post({ type: 'characterSchemesUpdateMetadata', id: C.scheme, name: v.name, author: v.author, description: v.description }) }) },
        { label: '导入人物包…', icon: I().upload, disabled: !!err, fn: () => post({ type: 'characterSchemesImportPackage', id: C.scheme }) },
        { label: '导出方案 ZIP', icon: I().download, disabled: !!err, fn: () => post({ type: 'characterSchemesExport', id: C.scheme }) },
        { label: '打开方案目录', icon: I().folder, fn: () => post({ type: 'characterSchemesOpen', id: C.scheme }) },
        '-',
        { label: '删除方案…', icon: I().trash, fn: () => U().confirmDelete({
          title: `删除角色方案「${esc(field(s, 'name') || C.scheme)}」？`,
          lines: [`方案和里面 <b>${peopleCount(p)} 个人物</b>的搭配会被移到方案目录的 <b>.deleted</b> 文件夹`, '方案用到的自定义皮肤包会保留',
            field(p, 'active') && '这是<b>已启用</b>的方案，删除后重启游戏生效'],
          onOk: () => { post({ type: 'characterSchemesDelete', id: C.scheme }); C.scheme = null; VS().render(); } }) },
      ]);
    },
    pkgOpen: () => sendPkg('characterSkinsOpen'),
    pkgMenu: el => {
      const p = pkgOf(C.pkg), users = schemes().filter(s => rulesOf(s).some(r => field(r, 'customPackageId') === C.pkg));
      U().openMenu(el, [
        { label: '导出皮肤包 ZIP', icon: I().download, disabled: !!field(p, 'error'), fn: () => sendPkg('characterSkinsExport') },
        { label: '打开目录', icon: I().folder, fn: () => sendPkg('characterSkinsOpen') },
        '-',
        { label: '删除皮肤包…', icon: I().trash, fn: () => U().confirmDelete({
          title: `删除人物皮肤包「${esc(pkgName(p))}」？`,
          lines: ['皮肤包会被停用，并移到皮肤目录的 <b>.deleted</b> 文件夹', '想留个备份，先在 ⋯ 里「导出皮肤包 ZIP」',
            users.length && `有 <b>${users.length} 个方案</b>用到它：${users.map(s => '「' + esc(field(field(s, 'scheme'), 'name')) + '」').join('')}，这些方案里的对应人物要重新配置`],
          onOk: () => { stopAnim(); post({ type: 'characterSkinsDelete', id: C.pkg }); C.pkg = null; VS().render(); } }) },
      ]);
    },
    animImport: () => {
      const a = (field(field(pkgOf(C.pkg), 'manifest'), 'animations') || {})[C.action];
      sendPkg('characterSkinsFrames', { action: C.action, fps: Number(field(a, 'fps')) || 15, loop: field(a, 'loop') !== false });
    },
    animPlay: () => { if (C.timer) stopAnim(); else playTick(); VS().render(); },
    animMenu: el => {
      U().openMenu(el, [
        { label: `替换第 ${C.frame + 1} 帧…`, icon: I().upload, fn: () => sendPkg('characterSkinsFrame', { action: C.action, index: C.frame }) },
        '-',
        { label: '恢复原版动作…', icon: I().refresh, fn: () => U().confirmDelete({ title: `恢复「${esc(C.action)}」原版动作？`, ok: '恢复',
          lines: ['会移除这个动作的自制帧和原精灵映射', '重启游戏后恢复原动作'], onOk: () => { stopAnim(); sendPkg('characterSkinsClearAction', { action: C.action }); } }) },
      ]);
    },
    saveTexts: () => {
      const m = field(pkgOf(C.pkg), 'manifest'), texts = ensure(m, 'texts');
      document.querySelectorAll('[data-v2c-text]').forEach(t => { const v = t.value; if (v) texts[t.dataset.v2cText] = v; else delete texts[t.dataset.v2cText]; });
      saveManifest('文案已保存');
    },
    saveLayout: () => {
      const inputs = [...document.querySelectorAll('[data-v2c-layout]')];
      const bad = inputs.find(i => !(Number(i.value) >= Number(i.min) && Number(i.value) <= Number(i.max)));
      if (bad) return toast(`${bad.closest('.v2-field').querySelector('label').textContent} 超出范围（${bad.min}～${bad.max}）`);
      const L = ensure(field(pkgOf(C.pkg), 'manifest'), 'layout');
      // 与原版一致：已有键按原样写，没有就用小写开头
      inputs.forEach(i => { const k = i.dataset.v2cLayout; L[Object.hasOwn(L, k) ? k : k[0].toLowerCase() + k.slice(1)] = Number(i.value); });
      saveManifest('布局已保存');
    },
    saveManifest: () => {
      try { sendPkg('characterSkinsSave', { manifest: JSON.parse($('v2cManifest').value) }); toast('清单已保存'); }
      catch (e) { toast('清单 JSON 无效：' + e.message); }
    },
  };

  function slotMenu(anchor, slot) {
    const images = field(field(pkgOf(C.pkg), 'manifest'), 'images') || {};
    U().openMenu(anchor, [
      { label: '选择图片…（会打开裁切）', icon: I().upload, disabled: slot.startsWith('asset:'), fn: () => sendPkg('characterSkinsImage', { slot }) },
      { label: '恢复原版', icon: I().refresh, disabled: !images[slot], fn: () => { delete images[slot]; saveManifest(`「${slotLabel(slot)}」已恢复原版`); } },
    ]);
  }

  document.addEventListener('click', ev => {
    const t = ev.target;
    // 向导在弹窗里
    const wz = t.closest?.('[data-v2c-wz]');
    if (wz && C.wiz) {
      if (wz.disabled) return;
      const r = wizActs[wz.dataset.v2cWz]?.(wz.dataset.v);
      if (r !== 'stop') drawWizard();
      return;
    }
    if (!t.closest?.('#v2Skins')) return;
    let el;
    if ((el = t.closest('[data-v2c]'))) { if (!el.disabled) actions[el.dataset.v2c]?.(el); return; }
    if ((el = t.closest('[data-v2c-rule]'))) { ev.stopPropagation(); return openWizard(el.dataset.v2cRule); }
    if ((el = t.closest('[data-v2c-scheme]'))) { C.scheme = el.dataset.v2cScheme; if (CSM()) CSM().selected = C.scheme; return VS().render(); }
    if ((el = t.closest('[data-v2c-pkg]'))) return openPkg(el.dataset.v2cPkg);
    if ((el = t.closest('[data-v2c-ptab]'))) { stopAnim(); C.pkgTab = el.dataset.v2cPtab; return VS().render(); }
    if ((el = t.closest('[data-v2c-action]'))) { stopAnim(); C.action = el.dataset.v2cAction; C.frame = 0; return VS().render(); }
    if ((el = t.closest('[data-v2c-frame]'))) { stopAnim(); C.frame = Number(el.dataset.v2cFrame); return VS().render(); }
    if ((el = t.closest('[data-v2c-slot]'))) return slotMenu(el, el.dataset.v2cSlot);
  });
  document.addEventListener('change', ev => {
    const t = ev.target;
    if (!t.closest?.('#v2Skins') || !C.pkg) return;
    const anims = ensure(field(pkgOf(C.pkg), 'manifest') || {}, 'animations'), a = anims[C.action];
    if (!a) return;
    if (t.id === 'v2cFps') {
      const v = Number(t.value);
      if (!(v >= 1 && v <= 120)) { t.value = Number(field(a, 'fps')) || 15; return toast('FPS 要在 1～120 之间'); }
      a[Object.hasOwn(a, 'Fps') ? 'Fps' : 'fps'] = v; saveManifest(`FPS ${v} 已保存`);
    }
    if (t.id === 'v2cLoop') { a[Object.hasOwn(a, 'Loop') ? 'Loop' : 'loop'] = t.checked; saveManifest(t.checked ? '已设为循环播放' : '已设为单次播放'); }
  });

  // 接管原版：数据回来时重画；新建的皮肤包要打开编辑器时，打开新编辑器而不是原版的
  document.addEventListener('DOMContentLoaded', () => {
    const csm = CSM(), csk = CSK();
    if (!csm || !csk || !window.V2Skins) return;
    // 原版的「新建皮肤包」表单放在角色面板里；面板被新界面隐藏了，挪到 body 下才能弹出来
    const dlg = $('characterCreateDialog'); if (dlg) document.body.appendChild(dlg);
    const origRender = csm.render;
    csm.render = function (payload) {
      const r = origRender.call(this, payload);
      if (C.pendingScheme && schemeOf(C.pendingScheme)) { C.scheme = C.pendingScheme; C.pendingScheme = null; }
      VS().render();
      return r;
    };
    const origSaved = csm.saved;
    csm.saved = function (payload) {
      const r = origSaved.call(this, payload);
      if (C.wiz?.saving) {
        if (payload?.success) { U().closeModal(); C.wiz = null; toast('已保存，完全退出并重启游戏后生效'); }
        else { C.wiz.saving = false; C.wiz.error = payload?.error || '保存失败'; drawWizard(); }
      }
      return r;
    };
    csm.editPackage = function (id) { this.selected = C.scheme || this.selected; openPkg(id); };
    window.V2Chars = { html: () => (C.pkg ? pkgHtml() : C.scheme ? schemeHtml() : listHtml()) };
    VS().render();
  });
})();
