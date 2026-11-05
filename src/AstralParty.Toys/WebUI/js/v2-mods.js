// v2-mods.js — 新界面：模组页
// 页面整体重做；模组配置表单、加载器设置、导入发布包这三个弹窗暂时沿用原版 ModsModule（按键捕获等逻辑较多）。
// 字段与消息见 astralparty-redesign/模组与皮肤接口.md。
(function () {
  const M = { q: '', speedCfg: null };
  const U = () => window.V2UI;
  const I = () => window.V2UI.ICON;
  const MM = () => window.ModsModule;

  const status = () => MM()?.modStatus || null;
  const mods = () => status()?.mods || [];
  const busy = () => !!(MM()?.busy || MM()?.recoveryBusy || MM()?.packageImportPending);
  const latest = () => MM()?.updateInfo?.latest || '';
  const checking = () => MM()?.updateInfo?.state === 'checking';

  function cmpVer(a, b) {
    const pa = String(a).replace(/^v/i, '').split(/[.-]/).map(n => parseInt(n, 10) || 0);
    const pb = String(b).replace(/^v/i, '').split(/[.-]/).map(n => parseInt(n, 10) || 0);
    for (let i = 0; i < Math.max(pa.length, pb.length); i++) { const d = (pa[i] || 0) - (pb[i] || 0); if (d) return d > 0 ? 1 : -1; }
    return 0;
  }
  const isBuiltIn = m => {
    const ids = MM()?.builtInMods || [], file = String(m.fileName || '').replace(/\.dll$/i, '');
    return ids.some(id => id === m.name || id === file);
  };
  const speedMod = () => mods().find(m => m.name === 'SpeedHackMod' || /^SpeedHackMod\.dll$/i.test(m.fileName || ''));
  const baseSpeed = () => {
    const raw = Number(M.speedCfg?.speedhackBaseSpeed ?? 1);
    return Number.isFinite(raw) && raw >= 1 ? raw : 1;
  };

  // 加载器状态（与原版 renderStatusHeader / renderVerdict 的分支一致）
  function loaderView(s) {
    const newer = latest() && s.installedVersion && cmpVer(latest(), s.installedVersion) > 0 ? latest() : '';
    if (s.bundleLoaderPresent === false) return { badge: '缺少内置文件', cls: 'warn', text: '本程序缺少内置加载器文件，无法安装，请使用完整发布包', act: null };
    if (!s.gameDirectory) return { badge: '未找到游戏', cls: 'warn', text: '还没找到游戏目录，在下面「高级操作」里自动检测或手动选择', act: 'adv' };
    if (s.loaderMissing) return { badge: s.loaderDisabled ? '已停用' : 'DLL 缺失', cls: 'off',
      text: s.canRestoreLoader ? `模组与配置都还在，可以恢复加载器（保留版本 ${s.installedVersion ? 'v' + s.installedVersion : '未知'}）` : '保留的组件读不出加载器版本，请重新安装或更新', act: 'restore' };
    if (!s.installed) return { badge: '未安装', cls: 'off', text: '安装后加载器和内置模组会一起装进游戏目录', act: 'install' };
    if (!s.loaderManaged) return { badge: '文件不属于本工具', cls: 'warn', text: '游戏目录里的 version.dll 不是本工具装的（可能是旧版变速器或第三方文件）', act: 'adv' };
    if (newer) return { badge: '可更新', cls: 'warn', text: `已装 v${s.installedVersion}，最新 v${newer}`, act: 'update', newer };
    const note = latest() ? (cmpVer(latest(), s.installedVersion) === 0 ? '已是最新' : '比 GitHub 上的更新')
      : s.embeddedVersion && cmpVer(s.installedVersion, s.embeddedVersion) === 0 ? '与内置版本一致'
      : s.embeddedVersion && cmpVer(s.installedVersion, s.embeddedVersion) > 0 ? `比程序内置的 v${s.embeddedVersion} 新` : '';
    return { badge: '已安装', cls: '', text: `v${s.installedVersion || '?'}${note ? ' · ' + note : ''}`, act: 'check' };
  }

  function html() {
    const s = status();
    const head = (primary) => `<div class="v2-head"><h1>模组</h1><span class="v2-top-spacer"></span>
      <button class="v2-btn ${primary ? 'primary' : 'line'}" data-v2m="import" ${s && (s.installed || s.loaderRootExists) ? '' : 'disabled'}>${I().upload}导入模组</button></div>`;
    if (!s) return `<div class="v2-page">${head(true)}<div class="v2-card v2-empty">正在读取加载器状态…</div></div>`;

    const lv = loaderView(s), b = busy();
    const usable = s.installed || s.loaderRootExists;
    const btn = {
      install: `<button class="v2-btn primary" data-v2m="install" ${b ? 'disabled' : ''}>${I().download}安装加载器</button>`,
      restore: `<button class="v2-btn primary" data-v2m="restore" ${b || !s.canRestoreLoader ? 'disabled' : ''}>恢复加载器</button>`,
      update: `<button class="v2-btn primary" data-v2m="update" ${b ? 'disabled' : ''}>${I().download}更新到 v${esc(lv.newer || '')}</button>`,
      check: `<button class="v2-btn ghost" data-v2m="check" ${b || checking() ? 'disabled' : ''}>${I().refresh}${checking() ? '检查中…' : '检查更新'}</button>`,
      adv: '<button class="v2-btn line" data-v2m="adv">查看处理办法</button>',
    }[lv.act] || '';
    const loaderIsPrimary = ['install', 'restore', 'update'].includes(lv.act);

    const sp = baseSpeed(), on = sp > 1.0001, sm = speedMod(), hk = sm && sm.enabled !== false;
    const enabled = mods().filter(m => m.enabled !== false).length;
    const q = M.q.trim().toLowerCase();
    const list = mods().filter(m => !q || [m.displayName, m.name, m.fileName, m.description, m.author].some(v => String(v ?? '').toLowerCase().includes(q)))
      .sort((a, b) => isBuiltIn(a) - isBuiltIn(b));   // 自己装的排前面：内置有 21 个，从市场新装的混在中间很难找

    return `<div class="v2-page">${head(!loaderIsPrimary)}
      ${s.gameRunning ? `<div class="v2-notice">${I().info}<span>游戏正在运行：改动会保存，重启游戏后生效</span></div>` : ''}

      <div class="v2-card">
        <div class="v2-hero" style="padding:14px 18px">
          <div class="v2-hero-art" style="width:40px;height:40px;font-size:18px">${I().sliders}</div>
          <div class="v2-hero-text">
            <div class="v2-hero-title" style="font-size:15px">CesiumLoader <span class="v2-badge ${lv.cls}">${lv.badge}</span>${b ? ' <span class="v2-muted" style="font-size:12px;font-weight:400">处理中…</span>' : ''}</div>
            <div class="v2-hero-sub">${esc(lv.text)}</div>
          </div>
          ${btn}
          <button class="v2-icon-btn" data-v2m="loaderSettings" title="加载器设置" ${usable ? '' : 'disabled'}>${I().sliders}</button>
        </div>
        <div class="v2-row"><span class="k">游戏变速</span>
          <span class="v">${!usable ? '<span class="v2-muted">先安装加载器</span>' : on
            ? `启动时 <input id="v2mSpeed" type="number" min="1" max="100" step="0.1" value="${sp}" class="v2-num"> 倍速　<span class="v2-muted">重启游戏生效</span>`
            : '<span class="v2-muted">关闭（1.0 = 正常速度，不支持减速）</span>'}</span>
          <label class="v2-switch"><input type="checkbox" data-v2m-speed ${on ? 'checked' : ''} ${usable ? '' : 'disabled'}><span></span></label></div>
        <div class="v2-row"><span class="k">变速热键</span>
          <span class="v">${hk ? '<span class="v2-muted">Delete 在 1.0x 和上次倍率间切换 · Alt+= / Alt+- 调倍率（由「变速」模组提供）</span>'
            : sm ? `<span style="color:var(--gp-gold)">需要开启「变速」模组</span>${on ? `<span class="v2-muted">　现在游戏会一直以 ${sp}x 运行，没法切回 1.0x</span>` : ''}`
            : '<span class="v2-muted">没装「变速」模组，游戏里不能用热键变速</span>'}</span>
          ${sm && !hk ? `<button class="v2-btn line" data-v2m="speedModOn">开启模组</button>` : ''}</div>
      </div>

      <div class="v2-card">
        <div class="v2-card-title"><span>已安装 <span class="v2-muted" style="font-weight:400">${enabled} / ${mods().length} 已启用</span></span>
          ${mods().length > 6 ? `<label class="v2-search" style="max-width:240px;height:30px">${I().search}<input id="v2mq" placeholder="搜索模组" value="${esc(M.q)}"></label>` : ''}</div>
        ${list.map(m => {
          const deps = (m.dependencies || []).map(d => `${d.id}${d.minVersion ? ' ≥ ' + d.minVersion : ''}`).join('、');
          return `<div class="v2-mod ${m.enabled === false ? 'off' : ''}">
            <div class="body">
              <div class="name">${esc(m.displayName || m.name || m.fileName)}${m.version ? `<span class="v2-tag">v${esc(m.version)}</span>` : ''}
                ${isBuiltIn(m) ? '<span class="v2-tag" title="随加载器一起安装的内置模组">内置</span>' : '<span class="v2-tag" title="不是随加载器内置的：来自市场或手动导入">第三方</span>'}
                ${(Number(m.permissions) & 2) ? '<span class="v2-badge warn" title="此模组声明可以操作游戏（模拟出牌 / 掷骰 / 移动等），请确认来源可信">可操作游戏</span>' : ''}</div>
              <div class="desc" title="${esc(m.description || '')}">${esc(m.description || '')}${deps ? `　<span class="v2-muted">依赖：${esc(deps)}</span>` : ''}</div>
            </div>
            <button class="v2-icon-btn" data-v2m-cfg="${esc(m.fileName)}" title="配置">${I().sliders}</button>
            <button class="v2-icon-btn" data-v2m-more="${esc(m.fileName)}" data-v2menu title="更多">${I().more}</button>
            <label class="v2-switch" title="启用 / 停用（重启游戏后生效）"><input type="checkbox" data-v2m-toggle="${esc(m.fileName)}" ${m.enabled !== false ? 'checked' : ''}><span></span></label>
          </div>`;
        }).join('') || `<div class="v2-empty">${mods().length ? '没有匹配的模组' : '还没有模组。点「导入模组」添加 DLL 或 ZIP，或者安装加载器时一起装上内置模组。'}</div>`}
      </div>

      <details class="v2-card" id="v2ModsAdv" ${M.advOpen ? 'open' : ''}>
        <summary><div class="t"><b>高级操作</b><small>版本、目录、发布包、停用与卸载</small></div>${I().down}</summary>
        <div class="v2-adv">
          ${s.installed && !s.loaderManaged ? `<div class="v2-notice" style="margin:0 18px 12px">${I().info}<span>游戏目录里的 version.dll 不是本工具装的。要换成 CesiumLoader，用下面的「覆盖安装」；只想清掉就用「强制卸载」。</span></div>` : ''}
          <div class="v2-row"><span class="n">版本</span><span class="d">已装 ${esc(s.installedVersion || '—')} · 程序内置 ${esc(s.embeddedVersion || '—')} · GitHub ${esc(latest() || '未检查')}</span>
            <button class="v2-btn line" data-v2m="changelog">更新日志</button></div>
          <div class="v2-row"><span class="n">游戏目录</span><span class="d" style="font-family:var(--v2-mono);font-size:12px">${esc(s.gameDirectory || '未找到')}</span>
            <button class="v2-btn line" data-v2m="dirMenu" data-v2menu>更改…</button></div>
          <div class="v2-row"><span class="n">打开文件夹</span><span class="d">mods / SDK / 日志</span><button class="v2-btn line" data-v2m="openMenu" data-v2menu>选择…</button></div>
          ${(status().sdk || []).length ? `<div class="v2-row"><span class="n">SDK</span><span class="d">${(status().sdk || []).map(x => esc(`${x.name || x.fileName}${x.version ? ' v' + x.version : ''}`)).join('<br>')}</span></div>` : ''}
          <div class="v2-row"><span class="n">${s.installed && !s.loaderManaged ? '覆盖安装' : '重新安装内置版本'}</span>
            <span class="d">装回程序附带的 v${esc(s.embeddedVersion || '?')}${s.installedVersion && s.embeddedVersion && cmpVer(s.installedVersion, s.embeddedVersion) > 0 ? '，<span style="color:var(--gp-gold)">比你现在的旧，属于降级</span>' : ''}</span>
            <button class="v2-btn line" data-v2m="reinstall" ${b ? 'disabled' : ''}>安装…</button></div>
          <div class="v2-row"><span class="n">导入发布包</span><span class="d">用本地的加载器发布 ZIP 安装或降级</span><button class="v2-btn line" data-v2m="importPkg" ${b ? 'disabled' : ''}>导入…</button></div>
          ${s.installed && s.loaderManaged ? `<div class="v2-row"><span class="n">停用加载器</span><span class="d">只备份并删除 version.dll，模组和配置都保留，之后能恢复</span>
            <button class="v2-btn line" data-v2m="disable" ${b ? 'disabled' : ''}>停用…</button></div>` : ''}
          <div class="v2-row"><span class="n">${s.installed && !s.loaderManaged ? '强制卸载' : '完整卸载'}</span><span class="d">删除加载器和整个 AstralParty_ModLoader 文件夹，<b style="color:#f43f5e">模组、配置、日志都会删掉</b></span>
            <button class="v2-btn danger" data-v2m="uninstall" ${b || !(s.installed || s.loaderRootExists) ? 'disabled' : ''}>卸载…</button></div>
        </div>
      </details>
    </div>`;
  }

  function render() {
    const root = $('v2Mods'); if (!root || U().isComposing()) return;
    const adv = $('v2ModsAdv'); if (adv) M.advOpen = adv.open;
    const focusSearch = document.activeElement?.id === 'v2mq';
    root.innerHTML = html();
    U().bindSearch('v2mq', v => { M.q = v; render(); });
    const box = $('v2mq');
    if (box && focusSearch) { box.focus(); box.setSelectionRange(box.value.length, box.value.length); }
  }

  /* ---------- 操作 ---------- */
  const findMod = f => mods().find(m => m.fileName === f);
  const confirm = (o) => U().confirmDelete(o);

  function toggleMod(m, enabled) {
    // 实测（2026-11-03）：关掉「变速」模组后热键失效，加载器仍按启动倍速加速，游戏会卡在加速状态
    if (!enabled && m === speedMod() && baseSpeed() > 1.0001) return speedModOffDialog(m);
    post({ type: 'modToggle', fileName: m.fileName, enabled });
  }

  function speedModOffDialog(m) {
    const x = baseSpeed();
    U().modalActions.v2mSpeedOffBoth = () => { U().closeModal(); post({ type: 'modSaveSpeedhack', speedhackBaseSpeed: 1.0 }); post({ type: 'modToggle', fileName: m.fileName, enabled: false }); };
    U().modalActions.v2mSpeedOffOnly = () => { U().closeModal(); post({ type: 'modToggle', fileName: m.fileName, enabled: false }); };
    U().openModal(`
      <div class="v2-confirm"><div class="v2-confirm-icon" style="background:rgba(250,204,21,.12);color:var(--gp-gold)">${I().info}</div>
        <div><b class="v2-confirm-title">关掉「${esc(m.displayName || m.name)}」模组？</b>
          <ul class="v2-confirm-lines"><li>现在的启动倍速是 <b>${x}x</b></li>
            <li>Delete、Alt+= / Alt+- 这些热键都由这个模组提供，关掉后<b>游戏会一直以 ${x}x 运行，没法切回 1.0x</b></li></ul></div></div>
      <div class="v2-modal-foot"><button class="v2-btn ghost" data-v2modal="close">取消</button><span class="v2-top-spacer"></span>
        <button class="v2-btn line" data-v2modal="v2mSpeedOffOnly">只关模组</button>
        <button class="v2-btn primary" data-v2modal="v2mSpeedOffBoth">关模组，并把倍速改回 1.0x</button></div>`, 'narrow');
    render();   // 开关先弹回「开」
  }

  const actions = {
    import: () => post({ type: 'modPickImport' }),
    install: () => post({ type: 'modInstall', overwriteDll: false, allowDowngrade: false, includeSample: true }),
    restore: () => MM()?.startRecovery(true),
    update: () => post({ type: 'modDownloadUpdate', overwriteDll: false, allowDowngrade: false }),
    check: () => {
      const mm = MM(); if (!mm) return;
      mm.updateInfo = { state: 'checking', latest: mm.updateInfo.latest, error: '' };
      post({ type: 'modCheckUpdate' });
      render();
    },
    adv: () => { const d = $('v2ModsAdv'); if (d) { d.open = true; d.scrollIntoView({ behavior: 'smooth' }); } },
    loaderSettings: () => MM()?.openLoaderSettings(),
    changelog: () => window.VersionModule?.requestChangelog('loader'),
    speedModOn: () => { const m = speedMod(); if (m) post({ type: 'modToggle', fileName: m.fileName, enabled: true }); },
    importPkg: () => MM()?.openPackageImport(),
    dirMenu: el => U().openMenu(el, [
      { label: '自动检测', icon: I().search, fn: () => post({ type: 'modDetect' }) },
      { label: '手动选择游戏文件夹…', icon: I().folder, fn: () => post({ type: 'modBrowse' }) },
      { label: '管理多个游戏目录…', icon: I().sliders, fn: () => showPage('settings') },
    ]),
    openMenu: el => U().openMenu(el, [
      { label: 'mods 目录', icon: I().folder, fn: () => post({ type: 'modOpenModsFolder' }) },
      { label: 'SDK 目录', icon: I().folder, fn: () => post({ type: 'modOpenSdkFolder' }) },
      { label: '日志目录', icon: I().folder, fn: () => post({ type: 'modOpenLogsFolder' }) },
    ]),
    reinstall: () => {
      const s = status(), foreign = s.installed && !s.loaderManaged;
      const downgrade = s.installedVersion && s.embeddedVersion && cmpVer(s.installedVersion, s.embeddedVersion) > 0;
      U().modalActions.confirmYes = () => { U().closeModal(); post({ type: 'modInstall', overwriteDll: foreign, allowDowngrade: !!downgrade, includeSample: true }); };
      const m = U().openModal(`
        <div class="v2-confirm"><div class="v2-confirm-icon" style="background:rgba(250,204,21,.12);color:var(--gp-gold)">${I().download}</div>
          <div><b class="v2-confirm-title">安装程序内置的 v${esc(s.embeddedVersion || '?')}？</b>
            <ul class="v2-confirm-lines">
              ${downgrade ? `<li>你现在装的是 <b>v${esc(s.installedVersion)}</b>，这样会<b>降级</b></li>` : ''}
              ${foreign ? '<li>会<b>覆盖</b>游戏目录里那个不属于本工具的 version.dll</li>' : ''}
              <li>内置模组会一起装上</li><li>重启游戏后生效</li></ul></div></div>
        <div class="v2-modal-foot"><span class="v2-top-spacer"></span><button class="v2-btn ghost" data-v2modal="close" id="v2ConfirmNo">取消</button>
          <button class="v2-btn ${downgrade || foreign ? 'danger-fill' : 'primary'}" data-v2modal="confirmYes">${downgrade ? '降级安装' : foreign ? '覆盖安装' : '安装'}</button></div>`, 'narrow');
      U().armDanger(m); $('v2ConfirmNo')?.focus();
    },
    disable: () => confirm({
      title: '停用加载器？', ok: '停用',
      lines: [`只影响这个目录：<b>${esc(status()?.gameDirectory || '')}</b>`, '会<b>备份并删除</b>游戏目录里的 version.dll，游戏就不再加载模组', '模组、配置、日志<b>都保留</b>，之后点「恢复加载器」就回来了', status()?.gameRunning && '游戏正在运行，重启游戏后生效'],
      onOk: () => MM()?.startRecovery(false),
    }),
    uninstall: () => {
      const s = status(), foreign = s.installed && !s.loaderManaged;
      confirm({
        title: foreign ? '强制卸载游戏目录里的 version.dll？' : '完整卸载 CesiumLoader？', ok: '卸载',
        lines: [`只影响这个目录：<b>${esc(s.gameDirectory || '')}</b>`, foreign ? '这个文件<b>不是本工具装的</b>，可能属于别的工具，删掉后那个工具也会失效' : null,
          '会删除加载器和整个 <b>AstralParty_ModLoader</b> 文件夹，<b>包括所有模组、配置和日志</b>，无法撤销',
          s.installed && s.loaderManaged && '只是暂时不想用模组的话，用「停用加载器」更安全',
          s.gameRunning && '游戏正在运行，重启游戏后生效'],
        onOk: () => post({ type: 'modUninstall', force: foreign }),
      });
    },
  };

  function modMenu(anchor, m) {
    U().openMenu(anchor, [
      { label: '配置…', icon: I().sliders, fn: () => MM()?.openModConfig(m.fileName) },
      { label: '打开 mods 目录', icon: I().folder, fn: () => post({ type: 'modOpenModsFolder' }) },
      '-',
      { label: '删除…', icon: I().trash, fn: () => confirm({
        title: `删除模组「${esc(m.displayName || m.name || m.fileName)}」？`,
        lines: [`会从游戏目录删除 <b>${esc(m.directoryName ? 'mods\\' + m.directoryName + '\\' : m.fileName)}</b>`,
          isBuiltIn(m) ? '这是内置模组，以后重新安装加载器时可以装回来' : '这不是内置模组，删除后只能重新导入 DLL 或 ZIP',
          '只想暂时不用的话，关掉开关就行，不必删除',
          status()?.gameRunning && '游戏正在运行，重启游戏后生效'],
        onOk: () => post({ type: 'modDelete', fileName: m.fileName }),
      }) },
    ]);
  }

  document.addEventListener('click', ev => {
    const t = ev.target;
    if (!t.closest?.('#modsView')) return;
    const a = t.closest('[data-v2m]'), cfg = t.closest('[data-v2m-cfg]'), more = t.closest('[data-v2m-more]');
    if (a && !a.disabled) return actions[a.dataset.v2m]?.(a);
    if (cfg) return MM()?.openModConfig(cfg.dataset.v2mCfg);
    if (more) { const m = findMod(more.dataset.v2mMore); if (m) modMenu(more, m); }
  });
  document.addEventListener('change', ev => {
    const t = ev.target;
    if (!t.closest?.('#modsView')) return;
    if (t.dataset.v2mToggle) { const m = findMod(t.dataset.v2mToggle); if (m) toggleMod(m, t.checked); return; }
    if (t.dataset.v2mSpeed !== undefined) { saveSpeed(t.checked ? 2.0 : 1.0); return; }
    if (t.id === 'v2mSpeed') {
      const v = Number(t.value);
      if (!(v >= 1 && v <= 100)) { toast('倍速必须在 1.0 ~ 100 之间（加载器不支持减速）'); t.value = baseSpeed(); return; }
      saveSpeed(v);
    }
  });
  // 先在界面上改过来，宿主随后推 modSpeedhackSync 时以它为准
  function saveSpeed(v) {
    post({ type: 'modSaveSpeedhack', speedhackBaseSpeed: v });
    M.speedCfg = { ...(M.speedCfg || {}), speedhackBaseSpeed: v };
    render();
  }

  // 市场上的名字和模组自己的名字可能不一样（实测：市场叫「保持神秘」，模组页叫「手牌查看」），装完要说清楚，不然像没装上
  document.addEventListener('DOMContentLoaded', () => {
    const wm = window.WorkshopModule;
    if (typeof wm?.renderResult !== 'function') return;
    const orig = wm.renderResult;
    wm.renderResult = function (p) {
      const sel = p?.id ? `#workshopGrid [data-mod-id="${CSS.escape(p.id)}"] :is(.workshop-row-title, .workshop-card-title)` : '';
      const title = ((sel && document.querySelector(sel)) || document.querySelector('#workshopDetailView .workshop-detail-title'))?.textContent.trim();
      const r = orig.call(this, p);
      if (p?.success && p.name && title && p.name !== title) toast(`${p.message} 市场上的「${title}」在模组页叫「${p.name}」，排在列表最前面。`);
      return r;
    };
  });

  // 原版这些方法跑完后重画（数据仍由原版保存：modStatus / builtInMods / updateInfo / 变速配置）
  document.addEventListener('DOMContentLoaded', () => {
    const mm = MM(); if (!mm) return;
    ['renderModStatus', 'renderUpdateCheck', 'clearBusy', 'setBusy', 'renderPackageImportResult', 'syncSpeedhackBar', 'clearRecoveryBusy'].forEach(name => {
      const orig = mm[name]; if (typeof orig !== 'function') return;
      mm[name] = function (...args) {
        if (name === 'syncSpeedhackBar' && args[0]) M.speedCfg = args[0];
        const r = orig.apply(this, args);
        render();
        return r;
      };
    });
    render();
  });
})();
