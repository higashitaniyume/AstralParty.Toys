        // 取消或出错时宿主只给一句话（没有扫描结果），不能说成「没找到」
        body = `<div class="v2-guide-body"><b>${sc.message ? esc(sc.message) : sc.mode === 'deep' ? '这个盘上没找到吉星派对' : '常见位置没找到吉星派对'}</b>
            <p class="v2-muted">请先确认游戏已经安装。然后可以直接选择游戏文件夹，或者对某个盘做深度扫描（会慢一些）。</p>
            <div class="v2-guide-actions">
              <select id="v2GuideDrive" class="v2-select" style="min-width:90px">${drives.map(d => `<option ${d === Guide.drive ? 'selected' : ''}>${esc(d)}</option>`).join('')}</select>
              <button class="v2-btn line" data-v2="guideDeep" ${drives.length ? '' : 'disabled'}>${ICON.search}深度扫描</button>
              <button class="v2-btn ghost" data-v2="guideRescan">${ICON.refresh}重新查找</button>
              <span class="v2-top-spacer"></span>
              <button class="v2-btn primary" data-v2="browseGame">${ICON.folder}手动选择文件夹…</button></div></div>${othersHtml}`;
      }
    }
    return `<div class="v2-page"><div class="v2-head"><h1>开始使用</h1></div>
      <div class="v2-card v2-guide">${guideSteps(1)}${body}</div>${hint}</div>`;
  }

  // 第 2 步要不要显示、显示哪种。停用 / DLL 缺失不算第一次安装，交给模组页处理
  function guideLoaderState(g, s) {
    if (!s || !s.gameDirectory || g.directoryExists === false || s.loaderMissing) return null;
    if (s.installed) return s.loaderManaged ? null : 'foreign';
    return s.bundleLoaderPresent === false ? 'bundle' : 'install';
  }

  function guideInstallHtml(g, s, state) {
    if (Guide.justInstalled) {
      const on = (s?.mods || []).filter(m => m.enabled !== false).length;
      return `<div class="v2-card v2-guide">${guideSteps(3)}
        <div class="v2-guide-body"><b>模组装好了</b>
          <p class="v2-muted">${on} / ${(s?.mods || []).length} 个模组已启用，${s?.gameRunning ? '游戏正在运行，重启游戏后生效' : '启动游戏后生效'}。想开关或配置模组，去「模组」页。</p>
          <div class="v2-guide-actions"><span class="v2-top-spacer"></span>
            <button class="v2-btn ghost" data-v2page="mods">去模组页</button>
            <button class="v2-btn ghost" data-v2="guideDone">知道了</button></div></div></div>`;
    }
    if (!state || guideSkipped()) return '';
    const later = '<button class="v2-btn ghost" data-v2="guideSkip">以后再说</button>';
    let inner;
    if (state === 'foreign') {
      inner = `<b>游戏目录里已经有别的 version.dll</b>
        <p class="v2-muted">它不是本工具装的（可能是旧版变速器或第三方文件），所以不能直接安装。去模组页看处理办法。</p>
        <div class="v2-guide-actions"><span class="v2-top-spacer"></span>${later}
          <button class="v2-btn primary" data-v2page="mods">去模组页处理</button></div>`;
    } else if (state === 'bundle') {
      inner = `<b>现在没法安装模组</b>
        <p class="v2-muted">这个程序缺少内置的加载器文件，请换用完整发布包。</p>
        <div class="v2-guide-actions"><span class="v2-top-spacer"></span>${later}</div>`;
    } else {
      const count = (window.ModsModule?.builtInMods || []).length;
      inner = `<b>安装模组</b>
        <p class="v2-muted">模组要靠加载器 CesiumLoader 才能运行。点一下，就会把加载器${s.embeddedVersion ? ` v${esc(s.embeddedVersion)}` : ''}和${count ? ` ${count} 个` : ''}内置模组一起装进游戏目录。不需要联网，以后可以在「模组」页开关或卸载。</p>
        <p class="v2-muted">模组不是游戏官方提供的，在线使用可能违反游戏规则，风险自负。</p>
        ${s.gameRunning ? `<div class="v2-notice">${ICON.info}<span>游戏正在运行：可以安装，但要重启游戏才生效</span></div>` : ''}
        ${g.exeName && !isAstralExe(g.exeName) ? `<div class="v2-notice">${ICON.info}<span>这个目录的主程序是 ${esc(g.exeName)}，看起来不是吉星派对，请确认目录没选错</span></div>` : ''}
        <div class="v2-guide-target"><span class="v2-muted">装到</span><span class="mono" title="${esc(g.directory)}">${esc(g.directory)}</span>
          <button class="v2-btn ghost" data-v2="gameMenu" data-v2menu>换目录${ICON.down}</button></div>
        <div class="v2-guide-actions"><span class="v2-top-spacer"></span>${later}
          <button class="v2-btn primary big" data-v2="guideInstall" ${Guide.installing ? 'disabled' : ''}>${ICON.download}${Guide.installing ? '正在安装…' : '安装模组'}</button></div>`;
    }
    return `<div class="v2-card v2-guide">${guideSteps(2)}<div class="v2-guide-body">${inner}</div></div>`;
  }

  function guideInstallNow() {
    Guide.installing = true;
    clearTimeout(Guide.installTimer);
    // 宿主出错会发 error；万一什么都没回，两分钟后解锁按钮（和原版 setBusy 的兜底一样）
    Guide.installTimer = setTimeout(() => { Guide.installing = false; renderHome(); }, 120000);
    post({ type: 'modInstall', overwriteDll: false, allowDowngrade: false, includeSample: true });   // 和模组页的安装按钮同一条消息
    renderHome();
  }

  function renderHome() {
    const root = $('v2Home');
    if (!root) return;
    // 引导卡片里的下拉框和折叠区，重画后要保持用户刚才的选择
    const driveEl = $('v2GuideDrive'), othersEl = $('v2GuideOthers');
    if (driveEl) Guide.drive = driveEl.value;
    if (othersEl) Guide.othersOpen = othersEl.open;
    const g = activeGame();
    if (!g) {
      // 打开就自动查一次（只读）；要等游戏目录数据回来，才能确定是真的没有
      if (AppState.gameProfiles && !Guide.scan && !Guide.autoScanned) { Guide.autoScanned = true; guideStartScan('quick'); }
      root.innerHTML = guideFindHtml();
      return;
    }
    const s = V2.modStatus;
    const guideState = guideLoaderState(g, s);
    const guideHtml = guideInstallHtml(g, s, guideState);
    // 每页只有一个粉色主按钮：引导还没走完时，启动按钮让位
    const launchCls = guideHtml && !Guide.justInstalled && guideState !== 'bundle' ? 'line' : 'primary';
    const busy = launchBusy(), locked = modeLocked();
    const last = latestReplay();
    const cardSkin = V2.skins && (V2.skins.profiles || []).find(p => p.name?.toLowerCase() === (V2.skins.activeSkin || '').toLowerCase());
    const activeSkin = V2.skins?.activeSkin || '';
    const cardName = cardSkin ? (cardSkin.displayName || cardSkin.name)
      : activeSkin === '__game_default__' ? '游戏默认' : activeSkin;
    const skinText = V2.skins && cardName ? `卡牌「${cardName}」` : '卡牌、音效、资源包';

    root.innerHTML = `<div class="v2-page">
      <div class="v2-head"><h1>概览</h1></div>
      ${guideHtml}
      <div class="v2-card">
        <div class="v2-hero">
          <div class="v2-hero-art game-icon"><img src="images/astralparty-game-icon.png" alt="吉星派对"></div>
          <div class="v2-hero-text">
            <div class="v2-hero-title">吉星派对 <span class="v2-tag">${esc(g.editionLabel || '')}</span>
              ${gameRunning() ? '<span class="v2-badge pink">运行中</span>' : g.directoryExists === false ? '<span class="v2-badge warn">目录不存在</span>' : '<span class="v2-badge">就绪</span>'}</div>
            <div class="v2-hero-sub">${s ? esc(loaderText(s)) : '正在读取加载器状态…'}</div>
          </div>
          <div class="v2-split">
            <button class="v2-btn ${launchCls} big" data-v2="launch" ${busy ? 'disabled' : ''}>${ICON.play}${busy ? '正在启动…' : '启动游戏'}</button>
            <button class="v2-btn ${launchCls} big" data-v2="launchMenu" data-v2menu title="启动方式">${ICON.down}</button>
          </div>
        </div>
        <div class="v2-row"><span class="k">游戏目录</span><span class="v mono" title="${esc(g.directory)}">${esc(g.directory)}</span>
          <button class="v2-icon-btn" data-v2="copyPath" title="复制路径">${ICON.copy}</button>
          <button class="v2-btn ghost" data-v2="gameMenu" data-v2menu>${ICON.folder}${esc(g.label || g.editionLabel || '切换')}${ICON.down}</button></div>
        <div class="v2-row"><span class="k">启动方式</span>
          <span class="v">${launchLabel()}${locked ? '　<span class="v2-muted">游戏启动中，稍后可切换</span>' : ''}</span></div>
        <div class="v2-launch-mode-grid" role="radiogroup" aria-label="启动方式">
          ${LAUNCH_MODES.map(([mode, label, desc]) => `<button class="v2-launch-mode${launchMode() === mode ? ' on' : ''}" data-v2-launch="${mode}" role="radio" aria-checked="${launchMode() === mode}" ${locked ? 'disabled' : ''} title="${esc(desc)}"><span class="v2-launch-mode-name">${esc(label)}</span><span class="v2-launch-mode-desc">${esc(desc)}</span></button>`).join('')}
        </div>
      </div>

      <div class="v2-card">
        <div class="v2-row link" data-v2="openLast"><span class="k">最近对局</span>
          <span class="v">${last ? esc([last.finishText, last.mapName, last.roundCount ? last.roundCount + ' 回合' : ''].filter(v => v && v !== '—').join(' · ') || ('#' + last.replayId))
            : '<span class="v2-muted">还没有回放</span>'}</span>${ICON.right}</div>
        <div class="v2-row link" data-v2page="mods"><span class="k">模组</span>
          <span class="v">${s ? `${(s.mods || []).filter(m => m.enabled !== false).length} / ${(s.mods || []).length} 已启用` : '<span class="v2-muted">查看与配置模组</span>'}</span>${ICON.right}</div>
        <div class="v2-row link" data-v2page="skins"><span class="k">自定义</span>
          <span class="v">${esc(skinText)}</span>${ICON.right}</div>
      </div>

      <details class="v2-card">
        <summary><div class="t"><b>高级操作</b><small>扫描游戏目录、管理多个游戏版本</small></div>${ICON.down}</summary>
        <div class="v2-adv">
          <div class="v2-row"><span class="n">快速扫描</span><span class="d">在 Steam 库和常见位置查找游戏</span><button class="v2-btn line" data-v2="scanQuick">扫描</button></div>
          <div class="v2-row"><span class="n">管理游戏目录</span><span class="d">添加、改名、移除、深度扫描</span><button class="v2-btn line" data-v2page="settings">打开</button></div>
        </div>
      </details>
    </div>`;
  }

  // 用原版的单选框切换启动方式：触发它自己的 change 处理（记住选择 + 通知宿主）
  function setLaunchMode(mode) {
    const radio = document.querySelector(`input[name="launchMode"][value="${mode}"]`);
    if (!radio || radio.disabled || radio.checked) return;
    radio.checked = true;
    radio.dispatchEvent(new Event('change'));
    renderHome();
    toast(LAUNCH_MODES.find(m => m[0] === mode)[2]);
  }

  const actions = {
    launch: () => {
      if (launchBusy()) return;
      window.HomeModule?.launchGame();   // 原版：锁启动按钮 5 秒、启动方式 6 秒
      renderHome();
      setTimeout(renderHome, 5100); setTimeout(renderHome, 6100);
    },
    launchMenu: el => {
      const locked = modeLocked(), cur = launchMode();
      openMenu(el, [
        ...LAUNCH_MODES.map(([mode, label]) => ({ label, icon: cur === mode ? ICON.check : ICON.dot, disabled: locked, fn: () => setLaunchMode(mode) })),
        ...(locked ? ['-', { label: '游戏启动中，稍后再切换', disabled: true }] : []),
      ]);
    },
    gameMenu: el => openMenu(el, [
      ...profiles().map(p => ({
        label: `[${p.editionLabel}] ${p.label}${p.directoryExists === false ? '（目录不存在）' : ''}`,
        icon: p.active ? ICON.check : ICON.dot,
        fn: () => { if (!p.active) post({ type: 'gameProfileSetActive', id: p.id }); },
      })),
      '-',
      { label: '添加游戏目录…', icon: ICON.plus, fn: () => post({ type: 'gameProfileBrowse' }) },
      { label: '管理游戏目录…', icon: ICON.sliders, fn: () => showPage('settings') },
    ]),
    copyPath: () => {
      const dir = activeGame()?.directory; if (!dir) return;
      Promise.resolve(navigator.clipboard?.writeText(dir)).then(() => toast('已复制路径'), () => toast('复制失败：' + dir));
    },
    openLast: () => {
      const last = latestReplay();
      if (!last) return showPage('tools');
      if (last.healthy === false) { toast('这局无法解析：' + (last.error || '')); return showPage('tools'); }
      post({ type: 'openRecentReplay', token: last.token });   // 宿主解析完会自己切到回放页
    },
    scanQuick: () => { showPage('settings'); window.GameLibModule?.startQuickScan?.(); },
    browseGame: () => post({ type: 'gameProfileBrowse' }),
    guideRescan: () => { guideStartScan('quick'); renderHome(); },
    guideDeep: () => { const d = $('v2GuideDrive')?.value; if (d) { Guide.drive = d; guideStartScan('deep', d); renderHome(); } },
    guideCancel: () => post({ type: 'gameScanCancel' }),
    guideUse: el => {
      const c = Guide.scan?.candidates?.[Number(el.dataset.i)];
      if (c) post({ type: 'gameProfileAddPath', directory: c.directory, activate: true });
    },
    guideInstall: () => {
      const g = activeGame();
      if (Guide.installing || !g) return;
      if (!g.exeName || isAstralExe(g.exeName)) return guideInstallNow();
      // 手动选的目录可能是别的 Unity 游戏：装之前再确认一次
      modalActions.confirmYes = () => { closeModal(); guideInstallNow(); };
      const m = openModal(`
        <div class="v2-confirm"><div class="v2-confirm-icon" style="background:rgba(250,204,21,.12);color:var(--gp-gold)">${ICON.download}</div>
          <div><b class="v2-confirm-title">这个目录看起来不是吉星派对，还要安装吗？</b>
            <ul class="v2-confirm-lines"><li>主程序是 <b>${esc(g.exeName)}</b>，不是 AstralParty 开头</li>
              <li>加载器会写进：${esc(g.directory)}</li>
              <li>只有渠道版改过主程序名才需要继续，否则请取消后换目录</li></ul></div></div>
        <div class="v2-modal-foot"><span class="v2-top-spacer"></span><button class="v2-btn ghost" data-v2modal="close" id="v2ConfirmNo">取消</button>
          <button class="v2-btn danger-fill" data-v2modal="confirmYes">仍然安装</button></div>`, 'narrow');
      armDanger(m); $('v2ConfirmNo')?.focus();
    },
    guideSkip: () => {
      try { localStorage.setItem(GUIDE_SKIP_KEY, '1'); } catch { /* 存不了就隐藏不了，不影响使用 */ }
      renderHome();
      toast('以后想装模组，去「模组」页点「安装加载器」');
    },
    guideDone: () => { Guide.justInstalled = false; renderHome(); },
  };

  // 给后面各页（v2-replays.js 等）用的公共件
  /* 搜索框：结果要重画页面，但输入法还在拼字时不能重画，否则拼音会被打断、叠在一起（实测：nihao 变成 nninihniha…）。
     所以拼字期间不搜索，选完字（compositionend）再搜；各页的 render() 开头用 isComposing() 挡住其他消息触发的重画。 */
  let composingBox = null;
  const isComposing = () => !!composingBox && composingBox === document.activeElement;
  function bindSearch(id, apply) {
    const box = $(id); if (!box) return;
    const run = () => {
      apply(box.value);
      const n = $(id);
      if (n) { n.focus(); n.setSelectionRange(n.value.length, n.value.length); }
    };
    box.oninput = e => { if (!e.isComposing) run(); };
    box.oncompositionstart = () => { composingBox = box; };
    box.oncompositionend = () => { composingBox = null; run(); };
  }

  // 「当前游戏」下拉：选中后由各页发 gameProfileSetActive。manageAttr 是调用方自己的 data 属性（如 data-v2m="manageGames"）
  function gamePicker(selectId, manageAttr, disabled) {
    const list = window.GameLibModule?.data?.profiles || [], cur = list.find(p => p.active);
    const opts = list.map(p => `<option value="${esc(p.id)}" ${p.active ? 'selected' : ''}>[${esc(p.editionLabel)}] ${esc(p.label)}${p.directoryExists === false ? '（目录不存在）' : ''}</option>`).join('');
    return `<div class="v2-card v2-game-picker"><div class="v2-row"><span class="k">当前游戏</span>
      <select id="${selectId}" class="v2-select" aria-label="选择当前正在操作的游戏" ${!list.length || disabled ? 'disabled' : ''}>
        ${list.length ? (cur ? '' : '<option value="" selected disabled>请选择</option>') + opts : '<option value="">未添加游戏</option>'}
      </select>
      <span class="v mono v2-muted" title="${esc(cur?.directory || '')}">${esc(cur?.directory || '')}</span>
      <button class="v2-btn ghost" ${manageAttr}>管理游戏目录…</button></div></div>`;
  }

  window.V2UI = { ICON, gamePicker, openMenu, closeMenu, openModal, closeModal, modalActions, armDanger, confirmDelete, bindSearch, isComposing,
    setModalCancel: fn => { modalCancel = fn; } };

  /* ---------- 接线 ---------- */
  function onShowHome() {
    renderHome();
    // 概览要显示加载器和模组数：主动要一次（与模组页同一条消息，10 秒内不重复要）
    if (Date.now() - V2.lastModStatusAsk > 10000) { V2.lastModStatusAsk = Date.now(); post({ type: 'modStatus' }); }
  }

  document.addEventListener('DOMContentLoaded', () => {
    mountTopbar();
    mountThemeButton();
    renderHome();

    // 游戏多开的删除确认换成统一的弹窗；真正删除的消息和原版一样
    if (window.AccountProfilesModule) AccountProfilesModule.confirmDelete = profile => confirmDelete({
      title: `删除账号档案「${esc(profile.name)}」？`,
      lines: ['这个档案的账号缓存、存档和回放会移进<b>回收站</b>，可以从回收站找回', '默认目录（游戏自己的目录）不受影响'],
      onOk: () => post({ type: 'accountProfileDelete', name: profile.name }),
    });

    document.addEventListener('click', e => {
      const modeSet = e.target.closest('[data-v2-launch]');
      if (modeSet && !modeSet.disabled) {
        setLaunchMode(modeSet.dataset.v2Launch);
        return;
      }
      const a = e.target.closest('[data-v2]'), p = e.target.closest('[data-v2page]');
      if (a && !a.disabled) actions[a.dataset.v2]?.(a);
      if (p) showPage(p.dataset.v2page);
    });

    // 进入概览时刷新（包一层原版 showPage，不改它的行为）
    const originalShowPage = window.showPage;
    window.showPage = function (page, options) {
      originalShowPage(page, options);
      if (page === 'home') onShowHome();
    };
    onShowHome();
  });

  // 原版 core.js 已在处理这些消息；这里只是额外听一份，用来刷新概览
  window.chrome?.webview?.addEventListener('message', event => {
    const msg = event.data;
    if (!msg?.type) return;
    switch (msg.type) {
      case 'windowState': {
        // 最大化时按钮换成「还原」图标
        document.body.classList.toggle('v2-maximized', !!msg.maximized);
        const b = document.querySelector('[data-v2win="windowToggleMaximize"]');
        if (b) { b.title = msg.maximized ? '还原' : '最大化'; b.setAttribute('aria-label', b.title); }
        return;
      }
      case 'modStatus':
        V2.modStatus = msg.payload?.status ?? msg.payload;
        // 只有真的装好才算完成：安装前发出的状态请求也可能在这时才回来
        if (Guide.installing && V2.modStatus?.installed && V2.modStatus?.loaderManaged) {
          Guide.installing = false; Guide.justInstalled = true; clearTimeout(Guide.installTimer);
        }
        break;
      case 'error':
        if (!Guide.installing) return;
        Guide.installing = false; clearTimeout(Guide.installTimer);
        break;
      case 'gameScanProgress': {
        const p = msg.payload || {};
        Guide.scan = { mode: p.mode, running: !!p.running, message: p.message, current: p.current, candidates: Guide.scan?.candidates || [] };
        break;
      }
      case 'gameScanResult':
        Guide.scan = { mode: msg.payload?.mode, running: false, candidates: msg.payload?.candidates || [] };
        break;
      case 'skinsProfiles': V2.skins = msg.payload; break;
      case 'gameProfiles':
        // 程序启动时，概览可能赶在自动添加游戏之前要过状态，那份没有游戏目录，这里补要一次
        if ((msg.payload?.profiles || []).some(p => p.active) && !V2.modStatus?.gameDirectory) {
          V2.lastModStatusAsk = Date.now(); post({ type: 'modStatus' });
        }
        renderGlobalGame();
        break;
      case 'replayLibrary': case 'homeData': case 'steamBypassSync': case 'launchModeSync': break;
      default: return;
    }
    if (AppState.currentPage === 'home') setTimeout(renderHome);   // 等原版处理完（比如头像、单选框）再画
  });
})();
