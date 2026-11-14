// v2-mods.js — 新界面：模组页
// 页面整体重做；模组配置表单、加载器设置、导入发布包这三个弹窗暂时沿用原版 ModsModule（按键捕获等逻辑较多）。
// 字段与消息见 astralparty-redesign/模组与皮肤接口.md。
(function () {
  // 排序 / 分组记在 localStorage：换页面回来还是上次的样子
  const PREF = { sort: 'v2ModsSort', group: 'v2ModsGroup' };
  const readPref = (key, fallback) => { try { return localStorage.getItem(key) || fallback; } catch { return fallback; } };
  const writePref = (key, value) => { try { localStorage.setItem(key, value); } catch { /* 存不了就只在本次会话生效 */ } };

  const M = {
    q: '', speedCfg: null, sel: new Set(), pendingBatch: null,
    sort: readPref(PREF.sort, 'installed'),   // installed | name | status
    group: readPref(PREF.group, 'kind'),      // kind | time | none
    progress: null,
  };
  const U = () => window.V2UI;
  const I = () => window.V2UI.ICON;
  const MM = () => window.ModsModule;
  const GL = () => window.GameLibModule;

  const status = () => MM()?.modStatus || null;
  const mods = () => status()?.mods || [];
  const busy = () => !!(MM()?.busy || MM()?.recoveryBusy || MM()?.packageImportPending);
  const latest = () => MM()?.updateInfo?.latest || '';
  const checking = () => MM()?.updateInfo?.state === 'checking';
  const updateInfo = () => MM()?.updateInfo || {};
  const ghError = () => updateInfo().error || '';
  const ghCheckedAt = () => updateInfo().checkedAt || 0;
  const ghHtmlUrl = () => updateInfo().htmlUrl || 'https://github.com/higashitaniyume/CesiumLoader/releases/latest';
  const ghPublishedAt = () => updateInfo().publishedAt || '';
  const githubPillText = () => {
    if (checking()) return '检查中…';
    if (latest()) return 'v' + latest();
    if (ghError()) return '检查失败';
    return '未检查';
  };
  const checkedAtLabel = () => {
    const ts = ghCheckedAt();
    if (!ts) return '';
    const d = new Date(ts);
    if (Number.isNaN(d.getTime())) return '';
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  };

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

  // ---------- 排序 / 分组 / 搜索 ----------
  const SORTS = [['installed', '最近安装'], ['name', '名称'], ['status', '启用状态']];
  const GROUPS = [['kind', '来源'], ['time', '安装时间'], ['none', '不分组']];
  const nameOf = m => String(m.displayName || m.name || m.fileName || '');
  const modTime = m => { const t = Date.parse(m.modifiedUtc || ''); return Number.isFinite(t) ? t : 0; };
  const haystack = m => [m.displayName, m.name, m.fileName, m.description, m.author, m.directoryName, m.id]
    .map(v => String(v ?? '').toLowerCase()).join('\n');
  const matches = m => { const q = M.q.trim().toLowerCase(); return !q || haystack(m).includes(q); };

  const collator = (() => { try { return new Intl.Collator('zh-Hans-CN', { sensitivity: 'base' }); } catch { return { compare: (a, b) => a.localeCompare(b) }; } })();
  function sortMods(list) {
    const arr = list.slice();
    if (M.sort === 'name') arr.sort((a, b) => collator.compare(nameOf(a), nameOf(b)));
    else if (M.sort === 'status') arr.sort((a, b) => (a.enabled === false) - (b.enabled === false) || collator.compare(nameOf(a), nameOf(b)));
    else arr.sort((a, b) => (modTime(b) - modTime(a)) || collator.compare(nameOf(a), nameOf(b)));
    return arr;
  }

  const DAY = 86400000;
  function timeBucket(m) {
    const t = modTime(m);
    if (!t) return { key: 'unknown', title: '时间未知', order: 9 };
    const startOfToday = new Date(); startOfToday.setHours(0, 0, 0, 0);
    const dayStart = startOfToday.getTime();
    if (Date.now() - t < 3600000) return { key: 'now', title: '刚刚', order: 0 };
    if (t >= dayStart) return { key: 'today', title: '今天内', order: 1 };
    if (t >= dayStart - DAY) return { key: 'yesterday', title: '昨天', order: 2 };
    if (t >= dayStart - 6 * DAY) return { key: 'week', title: '一周内', order: 3 };
    return { key: 'older', title: '更早', order: 4 };
  }

  /** 按当前分组方式把已排序列表切开；每组带 key / 标题 / 数量。 */
  function groupMods(list) {
    if (M.group === 'none') return [{ key: 'all', title: '', mods: list }];
    if (M.group === 'time') {
      const buckets = new Map();
      list.forEach(m => {
        const b = timeBucket(m);
        if (!buckets.has(b.key)) buckets.set(b.key, { ...b, mods: [] });
        buckets.get(b.key).mods.push(m);
      });
      return [...buckets.values()].sort((a, b) => a.order - b.order);
    }
    const third = list.filter(m => !isBuiltIn(m)), built = list.filter(m => isBuiltIn(m));
    const out = [];
    if (third.length) out.push({ key: 'third', title: '第三方模组', mods: third });
    if (built.length) out.push({ key: 'builtin', title: '内置模组', mods: built });
    return out;
  }

  const filteredList = () => sortMods(mods().filter(matches));

  /** 一行模组。data-mod-file / data-mod-search 供原地搜索筛选，不重建 DOM。 */
  function modRow(m, disableUi) {
    const deps = (m.dependencies || []).map(d => `${d.id}${d.minVersion ? ' ≥ ' + d.minVersion : ''}`).join('、');
    const off = m.enabled === false;
    const selected = M.sel.has(m.fileName);
    const title = off
      ? '已停用：模组文件夹在 disabled_mods 里，加载器不会加载。打开开关会搬回 mods'
      : '启用 / 停用（停用会把模组文件夹搬到 disabled_mods，重启游戏后生效）';
    return `<div class="v2-mod ${off ? 'off' : ''} ${selected ? 'sel' : ''}" data-mod-file="${esc(m.fileName)}" data-mod-search="${esc(haystack(m))}">
      <label class="v2-check" title="选中此模组"><input type="checkbox" data-v2m-sel="${esc(m.fileName)}" ${selected ? 'checked' : ''} ${disableUi ? 'disabled' : ''}><span></span></label>
      <div class="body">
        <div class="name">${esc(m.displayName || m.name || m.fileName)}${m.version ? `<span class="v2-tag">v${esc(m.version)}</span>` : ''}
          ${isBuiltIn(m) ? '<span class="v2-tag blue" title="随加载器一起安装的内置模组">内置</span>' : '<span class="v2-tag" title="不是随加载器内置的：来自市场或手动导入">第三方</span>'}
          ${m.disabledFolder ? '<span class="v2-tag" title="模组文件夹在 disabled_mods 里，加载器不会扫描这个目录">disabled_mods</span>' : ''}
          ${(Number(m.permissions) & 2) ? '<span class="v2-badge warn" title="此模组声明可以操作游戏（模拟出牌 / 掷骰 / 移动等），请确认来源可信">可操作游戏</span>' : ''}</div>
        <div class="desc" title="${esc(m.description || '')}">${esc(m.description || '')}${deps ? `　<span class="v2-muted">依赖：${esc(deps)}</span>` : ''}</div>
      </div>
      <button class="v2-icon-btn" data-v2m-cfg="${esc(m.fileName)}" title="配置">${I().sliders}</button>
      <button class="v2-icon-btn" data-v2m-more="${esc(m.fileName)}" data-v2menu title="更多">${I().more}</button>
      <label class="v2-switch" title="${esc(title)}"><input type="checkbox" data-v2m-toggle="${esc(m.fileName)}" ${off ? '' : 'checked'}><span></span></label>
    </div>`;
  }

  // ---------- 加载器进度条 ----------
  function progressHtml() {
    const p = M.progress || {};
    const show = !!M.progress && (p.active !== false || p.phase === 'done' || p.phase === 'error');
    const pct = Number.isFinite(p.percent) && p.percent >= 0 ? p.percent : -1;
    const cls = ['v2-card', 'v2-modp', p.phase === 'error' ? 'is-error' : p.phase === 'done' ? 'is-done' : '',
      pct < 0 && p.active !== false && p.phase !== 'error' ? 'is-indeterminate' : ''].filter(Boolean).join(' ');
    return `<div class="${cls}" id="v2mProgress" ${show ? '' : 'hidden'}>
      <div class="v2-modp-head"><span class="msg" id="v2mProgressMsg">${esc(p.message || '')}</span><span class="pct" id="v2mProgressPct">${pct >= 0 ? pct + '%' : ''}</span></div>
      <div class="v2-modp-bar"><i id="v2mProgressFill" style="width:${pct >= 0 ? pct : 0}%"></i></div>
    </div>`;
  }

  let progressTimer = 0;
  function applyProgress() {
    const el = $('v2mProgress');
    if (!el) { render(); return; }
    const p = M.progress || {};
    el.hidden = !(M.progress && (p.active !== false || p.phase === 'done' || p.phase === 'error'));
    el.classList.toggle('is-error', p.phase === 'error');
    el.classList.toggle('is-done', p.phase === 'done');
    const pct = Number.isFinite(p.percent) && p.percent >= 0 ? p.percent : -1;
    el.classList.toggle('is-indeterminate', pct < 0 && p.active !== false && p.phase !== 'error');
    const msg = $('v2mProgressMsg'); if (msg) msg.textContent = p.message || '';
    const pctEl = $('v2mProgressPct'); if (pctEl) pctEl.textContent = pct >= 0 ? pct + '%' : '';
    const fill = $('v2mProgressFill'); if (fill) fill.style.width = pct >= 0 ? pct + '%' : '';
  }

  /** 宿主推来的下载 / 校验 / 安装进度。完成后停留几秒再收起，避免一闪而过。 */
  function progress(payload) {
    if (!payload) return;
    progressTimer && clearTimeout(progressTimer);
    M.progress = { ...payload };
    applyProgress();
    if (payload.phase === 'done') {
      progressTimer = setTimeout(() => {
        if (M.progress?.phase === 'done') { M.progress = null; applyProgress(); }
      }, 4000);
    }
  }

  // ---------- 原地筛选（不重建输入框，输入法拼字不会被打断） ----------
  function applyFilter() {
    const listEl = $('v2mList');
    if (!listEl) return;
    const q = M.q.trim().toLowerCase();
    let visible = 0;
    listEl.querySelectorAll('[data-mod-file]').forEach(row => {
      const show = !q || (row.dataset.modSearch || '').includes(q);
      row.hidden = !show;
      if (show) visible++;
    });
    listEl.querySelectorAll('[data-mod-group]').forEach(group => {
      let n = 0;
      group.querySelectorAll('[data-mod-file]').forEach(row => { if (!row.hidden) n++; });
      group.hidden = n === 0;
      const count = group.querySelector('[data-group-count]');
      if (count) count.textContent = String(n);
    });
    const scope = $('v2mScope');
    if (scope) scope.textContent = q ? `当前筛选 ${visible} 个` : `全部 ${visible} 个`;
    const empty = $('v2mEmpty');
    if (empty) {
      empty.hidden = visible > 0;
      empty.textContent = mods().length
        ? '没有匹配的模组'
        : '还没有模组。点「导入模组」添加 DLL 或 ZIP，或者安装加载器时一起装上内置模组。';
    }
    // 全选：只按当前可见的行算
    const all = document.querySelector('#v2Mods [data-v2m-select-all]');
    if (all) {
      const boxes = [...listEl.querySelectorAll('[data-v2m-sel]')].filter(box => !box.closest('[data-mod-file]').hidden);
      const checked = boxes.length > 0 && boxes.every(box => box.checked);
      all.checked = checked;
      all.indeterminate = !checked && boxes.some(box => box.checked);
    }
  }

  // 加载器状态（与原版 renderStatusHeader / renderVerdict 的分支一致）
  function loaderView(s) {
    const foreign = s.installed && !s.loaderManaged;
    // 外来文件版本未知时：查到 GitHub 最新就视为有新版（点更新即覆盖），已知版本才比大小
    const newer = foreign && latest()
      ? (s.installedVersion ? (cmpVer(latest(), s.installedVersion) > 0 ? latest() : '') : latest())
      : (latest() && s.installedVersion && cmpVer(latest(), s.installedVersion) > 0 ? latest() : '');
    if (s.bundleLoaderPresent === false) return { badge: '缺少内置文件', cls: 'warn', text: '本程序缺少内置加载器文件，无法安装，请使用完整发布包', act: null };
    if (!s.gameDirectory) return { badge: '未找到游戏', cls: 'warn', text: '还没找到游戏目录，在下面「高级操作」里自动检测或手动选择', act: 'adv' };
    if (s.loaderMissing) return { badge: s.loaderDisabled ? '已停用' : 'DLL 缺失', cls: 'off',
      text: s.canRestoreLoader ? `模组与配置都还在，可以恢复加载器（保留版本 ${s.installedVersion ? 'v' + s.installedVersion : '未知'}）` : '保留的组件读不出加载器版本，请重新安装或更新', act: 'restore' };
    if (!s.installed) return { badge: '未安装', cls: 'off', text: '安装后加载器和内置模组会一起装进游戏目录', act: 'install' };
    if (foreign && newer) return { badge: '文件不属于本工具 · 可更新', cls: 'warn', text: `GitHub 最新 v${newer}，更新会覆盖游戏目录里那个不属于本工具的 version.dll`, act: 'updateForeign', newer };
    if (!s.loaderManaged) return { badge: '文件不属于本工具', cls: 'warn', text: '游戏目录里的 version.dll 不是本工具装的（可能是旧版变速器或第三方文件），仍可检查更新或覆盖安装内置版本', act: 'foreign' };
    if (newer) return { badge: '可更新', cls: 'warn', text: `已装 v${s.installedVersion}，最新 v${newer}`, act: 'update', newer };
    const note = latest() ? (cmpVer(latest(), s.installedVersion) === 0 ? '已是最新' : '比 GitHub 上的更新')
      : s.embeddedVersion && cmpVer(s.installedVersion, s.embeddedVersion) === 0 ? '与内置版本一致'
      : s.embeddedVersion && cmpVer(s.installedVersion, s.embeddedVersion) > 0 ? `比程序内置的 v${s.embeddedVersion} 新` : '';
    return { badge: '已安装', cls: '', text: `v${s.installedVersion || '?'}${note ? ' · ' + note : ''}`, act: 'check' };
  }

  function html() {
    const s = status();
    const head = (primary) => {
      const open = s?.gameDirectory;
      const bNow = busy();
      const canReinstall = open && !bNow && s?.bundleLoaderPresent !== false;
      const embeddedSuffix = s?.embeddedVersion ? ` v${esc(s.embeddedVersion)}` : '';
      const chk = checking();
      const checkLabel = chk ? '检查中…' : (latest() ? `GitHub 最新 v${esc(latest())}` : '检查 GitHub 更新');
      const checkTitle = latest() ? `GitHub 最新 v${latest()}${checkedAtLabel() ? `（${checkedAtLabel()} 检查）` : ''}，点按重新检查` : '向 GitHub 查询 CesiumLoader 最新版本';
      return `<div class="v2-head"><h1>模组</h1><span class="v2-top-spacer"></span>
      <button class="v2-btn primary" data-v2m="reinstallTop" ${canReinstall ? '' : 'disabled'} title="安装本程序内置的 CesiumLoader${embeddedSuffix}（含内置模组）">${I().download}重新安装内置版本</button>
      <button class="v2-btn line" data-v2m="checkTop" ${bNow || chk ? 'disabled' : ''} title="${esc(checkTitle)}">${I().refresh}${checkLabel}</button>
      <button class="v2-btn line" data-v2m="openMods" ${open ? '' : 'disabled'}>${I().folder}打开 Mod 文件夹</button>
      <button class="v2-btn line" data-v2m="openLogs" ${open ? '' : 'disabled'}>${I().folder}打开日志文件夹</button>
      <button class="v2-btn line" data-v2m="openGame" ${open ? '' : 'disabled'}>${I().folder}打开游戏文件夹</button>
      <button class="v2-btn line" data-v2m="importPkg" ${open && !bNow ? '' : 'disabled'} title="导入本地 cesium-loader 发布 ZIP，安装加载器、SDK 与包内模组">${I().upload}导入发布包</button>
      <button class="v2-btn line" data-v2m="update" ${open && !bNow ? '' : 'disabled'} title="直接从 GitHub 下载最新 CesiumLoader 并安装（与下面卡片的「更新」同流程）">${I().download}从 GitHub 更新</button>
      <button class="v2-btn ${primary ? 'primary' : 'line'}" data-v2m="import" ${s && (s.installed || s.loaderRootExists) ? '' : 'disabled'}>${I().upload}导入模组</button></div>`;
    };
    if (!s) {
      const skelRows = Array.from({ length: 6 }, () => `<div class="v2-skel-row" aria-hidden="true"><div class="v2-skel" style="width:36px;height:36px;border-radius:8px;flex:none"></div><div style="flex:1;display:grid;gap:6px"><div class="v2-skel v2-skel-block" style="width:38%"></div><div class="v2-skel v2-skel-block" style="width:72%"></div></div><div class="v2-skel" style="width:36px;height:20px;border-radius:999px"></div></div>`).join('');
      return `<div class="v2-page">${head(true)}
        <div class="v2-card" aria-hidden="true"><div class="v2-skel-row" style="border-top:0"><div class="v2-skel" style="width:40px;height:40px;border-radius:10px;flex:none"></div><div style="flex:1;display:grid;gap:6px"><div class="v2-skel v2-skel-block" style="width:30%"></div><div class="v2-skel v2-skel-block" style="width:55%"></div></div><div class="v2-skel" style="width:110px;height:34px;border-radius:8px"></div></div></div>
        <div class="v2-card"><div class="v2-card-title"><span>已安装</span></div>${skelRows}</div>
      </div>`;
    }

    const lv = loaderView(s), b = busy();
    const usable = s.installed || s.loaderRootExists;
    const loaderVersionText = s.installedVersion || '版本未知';
    const sdkVersionText = s.installedSdkVersion || (s.sdk || []).find(x => x.version)?.version || '版本未知';
    const embeddedSdkText = s.embeddedSdkVersion ? ` · 内置 v${s.embeddedSdkVersion}` : '';
    const btn = {
      install: `<button class="v2-btn primary" data-v2m="install" ${b ? 'disabled' : ''}>${I().download}安装加载器</button>`,
      restore: `<button class="v2-btn primary" data-v2m="restore" ${b || !s.canRestoreLoader ? 'disabled' : ''}>恢复加载器</button>`,
      update: `<button class="v2-btn primary" data-v2m="update" ${b ? 'disabled' : ''}>${I().download}更新到 v${esc(lv.newer || '')}</button>`,
      check: `<button class="v2-btn ghost" data-v2m="check" ${b || checking() ? 'disabled' : ''}>${I().refresh}${checking() ? '检查中…' : '检查更新'}</button>`,
      adv: '<button class="v2-btn line" data-v2m="adv">查看处理办法</button>',
      foreign: `<button class="v2-btn ghost" data-v2m="check" ${b || checking() ? 'disabled' : ''}>${I().refresh}${checking() ? '检查中…' : '检查更新'}</button><button class="v2-btn line" data-v2m="reinstall" ${b ? 'disabled' : ''}>${I().download}覆盖安装内置版本</button>`,
      updateForeign: `<button class="v2-btn primary" data-v2m="update" ${b ? 'disabled' : ''}>${I().download}更新到 v${esc(lv.newer || '')}（覆盖）</button><button class="v2-btn ghost" data-v2m="check" ${b || checking() ? 'disabled' : ''}>${I().refresh}${checking() ? '检查中…' : '检查更新'}</button>`,
    }[lv.act] || '';
    const loaderIsPrimary = ['install', 'restore', 'update'].includes(lv.act);

    const sp = baseSpeed(), on = sp > 1.0001, sm = speedMod(), hk = sm && sm.enabled !== false;
    const enabled = mods().filter(m => m.enabled !== false).length;
    // 清理已不存在的勾选（解密后列表已删 / 换游戏目录）
    if (M.sel.size) {
      const valid = new Set(mods().map(m => m.fileName));
      for (const f of [...M.sel]) if (!valid.has(f)) M.sel.delete(f);
    }
    const list = filteredList();
    const groups = groupMods(list);
    const selCount = M.sel.size;
    const inList = list.filter(m => M.sel.has(m.fileName)).length;
    const allSelected = list.length > 0 && inList === list.length;
    const someSelected = !allSelected && inList > 0;
    const needEnable = list.filter(m => m.enabled === false).length;   // 一键启用要打开的
    const needDisable = list.filter(m => m.enabled !== false).length;  // 一键禁用要关闭的
    const scopeText = M.q.trim() ? `当前筛选 ${list.length} 个` : `全部 ${list.length} 个`;
    const listHtml = groups.map(g => `<section class="v2-mod-group" data-mod-group="${g.key}">
      ${g.title ? `<div class="v2-mod-group-head"><span>${esc(g.title)}</span><span class="n" data-group-count>${g.mods.length}</span></div>` : ''}
      ${g.mods.map(m => modRow(m, b)).join('')}
    </section>`).join('');

    return `<div class="v2-page${selCount ? ' v2-has-float' : ''}">${head(!loaderIsPrimary)}
      ${U().gamePicker('v2mGame', 'data-v2m="manageGames"', b)}
      ${s.gameRunning ? `<div class="v2-notice">${I().info}<span>游戏正在运行：改动会保存，重启游戏后生效</span></div>` : ''}

      <div class="v2-card">
        <div class="v2-hero" style="padding:14px 18px">
          <div class="v2-hero-art" style="width:40px;height:40px;font-size:18px">${I().sliders}</div>
          <div class="v2-hero-text">
            <div class="v2-hero-title" style="font-size:15px">CesiumLoader <span class="v2-badge ${lv.cls}">${lv.badge}</span>${b ? ' <span class="v2-muted" style="font-size:12px;font-weight:400">处理中…</span>' : ''}</div>
            <div class="v2-hero-sub">${esc(lv.text)}</div>
            <div class="v2-version-pills" aria-label="当前安装的加载器与 SDK 版本">
              <span class="v2-version-pill" title="当前游戏目录安装的 CesiumLoader 版本"><span class="k">加载器</span><b>v${esc(loaderVersionText)}</b></span>
              <span class="v2-version-pill" title="加载器随包 SDK / doorstop_config 中的 sdkVersion"><span class="k">SDK</span><b>v${esc(sdkVersionText)}</b>${embeddedSdkText ? `<span class="v2-muted">${esc(embeddedSdkText)}</span>` : ''}</span>
              <span class="v2-version-pill" title="GitHub 最新 Release（点「检查 GitHub 更新」获取）"><span class="k">GitHub 最新</span><b>${esc(githubPillText())}</b>${latest() && checkedAtLabel() ? `<span class="v2-muted">${esc(checkedAtLabel())} 检查</span>` : ''}</span>
            </div>
          </div>
          ${btn}
          ${s.installed && s.loaderManaged ? `<button class="v2-btn line" data-v2m="disable" ${b ? 'disabled' : ''} title="备份后只删除 version.dll，保留所有模组与配置，之后能恢复">停用加载器</button>` : ''}
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

      ${progressHtml()}

      <div class="v2-card">
        <div class="v2-card-title"><span>已安装 <span class="v2-muted" style="font-weight:400">${enabled} / ${mods().length} 已启用</span>${selCount ? ` · <span class="v2-muted" style="font-weight:400">已选 ${selCount}</span>` : ''}</span>
          ${mods().length > 6 ? `<label class="v2-search" style="max-width:240px;height:30px">${I().search}<input id="v2mq" type="search" autocomplete="off" spellcheck="false" placeholder="搜索模组" value="${esc(M.q)}"></label>` : ''}</div>
        <div class="v2-mod-tools v2-mod-view">
          <label class="v2-mod-view-label" title="列表排序方式">排序
            <select class="v2-select v2-mod-select" id="v2mSort">${SORTS.map(([v, t]) => `<option value="${v}" ${M.sort === v ? 'selected' : ''}>${t}</option>`).join('')}</select></label>
          <label class="v2-mod-view-label" title="按来源或安装时间分组">分组
            <select class="v2-select v2-mod-select" id="v2mGroup">${GROUPS.map(([v, t]) => `<option value="${v}" ${M.group === v ? 'selected' : ''}>${t}</option>`).join('')}</select></label>
          <span class="v2-top-spacer"></span>
          <span class="v2-muted" style="font-size:12px">停用的模组会搬进 <span style="font-family:var(--v2-mono)">disabled_mods</span></span>
        </div>
        ${list.length ? `<div class="v2-mod-tools">
          <label class="v2-check" title="勾选当前筛选列表（${list.length} 个）"><input type="checkbox" data-v2m-select-all ${allSelected ? 'checked' : ''} ${someSelected ? 'data-indeterminate="1"' : ''} ${b ? 'disabled' : ''}><span></span>全选</label>
          <span class="v2-muted" style="font-size:12px" id="v2mScope">${esc(scopeText)}</span>
          <span class="v2-top-spacer"></span>
          <button class="v2-btn line" data-v2m="enableAll" ${b || !needEnable ? 'disabled' : ''} title="启用当前筛选列表里已禁用的模组（${needEnable} 个）">一键启用</button>
          <button class="v2-btn line" data-v2m="disableAll" ${b || !needDisable ? 'disabled' : ''} title="禁用当前筛选列表里已启用的模组（${needDisable} 个）">一键禁用</button>
        </div>` : ''}
        <div id="v2mList">${listHtml}</div>
        <div class="v2-empty" id="v2mEmpty" ${list.length ? 'hidden' : ''}>${mods().length ? '没有匹配的模组' : '还没有模组。点「导入模组」添加 DLL 或 ZIP，或者安装加载器时一起装上内置模组。'}</div>
      </div>

      ${selCount ? `<div class="v2-floatbar" role="toolbar" aria-label="已选模组操作">
        <span class="v2-floatbar-count">已选 ${selCount}</span>
        <button class="v2-btn line" data-v2m="enableSel" ${b ? 'disabled' : ''}>启用已选</button>
        <button class="v2-btn line" data-v2m="disableSel" ${b ? 'disabled' : ''}>禁用已选</button>
        <button class="v2-btn danger" data-v2m="deleteSel" ${b ? 'disabled' : ''}>删除已选</button>
        <button class="v2-btn ghost" data-v2m="clearSel" title="清空勾选"><span aria-hidden="true">✕</span></button>
      </div>` : ''}

      <details class="v2-card" id="v2ModsAdv" ${M.advOpen ? 'open' : ''}>
        <summary><div class="t"><b>高级操作</b><small>版本、目录、发布包、停用与卸载</small></div>${I().down}</summary>
        <div class="v2-adv">
          ${s.installed && !s.loaderManaged ? `<div class="v2-notice" style="margin:0 18px 12px">${I().info}<span>游戏目录里的 version.dll 不是本工具装的。要换成 CesiumLoader，用下面的「覆盖安装」；只想清掉就用「强制卸载」。</span></div>` : ''}
          <div class="v2-row"><span class="n">版本</span><span class="d">已装 ${esc(s.installedVersion || '—')} · 程序内置 ${esc(s.embeddedVersion || '—')} · GitHub <b>${esc(latest() ? 'v' + latest() : (checking() ? '检查中…' : (ghError() ? '检查失败' : '未检查')))}</b>${latest() && checkedAtLabel() ? ` <span class="v2-muted">${esc(checkedAtLabel())} 检查</span>` : ''}${latest() && ghPublishedAt() ? ` <span class="v2-muted">· 发布于 ${esc(ghPublishedAt().slice(0, 10))}</span>` : ''}${ghError() ? ` <span class="v2-muted" title="${esc(ghError())}">（点重新检查）</span>` : ''}</span>
            <button class="v2-btn line" data-v2m="checkTop" ${b || checking() ? 'disabled' : ''}>检查 GitHub 更新</button><button class="v2-btn line" data-v2m="changelog">更新日志</button><a class="v2-btn line" href="${esc(ghHtmlUrl())}" target="_blank" rel="noopener" title="${esc(ghHtmlUrl())}" style="text-decoration:none">GitHub ↗</a></div>
          <div class="v2-row"><span class="n">游戏目录</span><span class="d" style="font-family:var(--v2-mono);font-size:12px">${esc(s.gameDirectory || '未找到')}</span>
            <button class="v2-btn line" data-v2m="dirMenu" data-v2menu>更改…</button></div>
          <div class="v2-row"><span class="n">打开文件夹</span><span class="d">mods / SDK / 日志</span><button class="v2-btn line" data-v2m="openMenu" data-v2menu>选择…</button></div>
          ${(status().sdk || []).length ? `<div class="v2-row"><span class="n">SDK</span><span class="d">${(status().sdk || []).map(x => esc(`${x.name || x.fileName}${x.version ? ' v' + x.version : ''}`)).join('<br>')}</span></div>` : ''}
          <div class="v2-row"><span class="n">${s.installed && !s.loaderManaged ? '覆盖安装' : '重新安装内置版本'}</span>
            <span class="d">装回程序附带的 v${esc(s.embeddedVersion || '?')}${s.installedVersion && s.embeddedVersion && cmpVer(s.installedVersion, s.embeddedVersion) > 0 ? '，<span style="color:var(--gp-gold)">比你现在的旧，属于降级</span>' : ''}</span>
            <button class="v2-btn line" data-v2m="reinstall" ${b ? 'disabled' : ''}>安装…</button></div>
          <div class="v2-row"><span class="n">导入发布包</span><span class="d">用本地的加载器发布 ZIP 安装或降级</span><button class="v2-btn line" data-v2m="importPkg" ${b ? 'disabled' : ''}>导入…</button></div>
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
    const caret = focusSearch ? document.activeElement.selectionStart : 0;
    root.innerHTML = html();
    // 搜索只做原地筛选：输入框不重建，拼音拼字期间也不会被打断
    U().bindSearch('v2mq', v => { M.q = v; applyFilter(); });
    const box = $('v2mq');
    if (box && focusSearch) {
      box.focus();
      const pos = Math.min(caret ?? box.value.length, box.value.length);
      box.setSelectionRange(pos, pos);
    }
    const all = root.querySelector('[data-v2m-select-all]');
    if (all && all.dataset.indeterminate) all.indeterminate = true;
    applyFilter();
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

  /* ---------- 批量操作（一键 / 多选） ---------- */
  const selectedTargets = () => mods().filter(m => M.sel.has(m.fileName)).map(m => m.fileName);
  const needsSpeedGuard = fileNames => {
    const sm = speedMod();
    return !!sm && baseSpeed() > 1.0001 && fileNames.includes(sm.fileName);
  };
  function doBatchToggle(fileNames, enabled) {
    if (!fileNames.length) { toast('没有可操作的模组'); return; }
    post({ type: 'modToggleBatch', fileNames, enabled });
    M.sel.clear();
    render();
  }
  function doBatchDelete(fileNames) {
    if (!fileNames.length) { toast('没有可操作的模组'); return; }
    post({ type: 'modDeleteBatch', fileNames });
    M.sel.clear();
    render();
  }
  function speedBatchDialog(fileNames, enabled, mode) {
    const sm = speedMod(), x = baseSpeed();
    const names = fileNames.map(f => esc(findMod(f)?.displayName || findMod(f)?.name || f));
    M.pendingBatch = { fileNames, enabled, mode };
    U().modalActions.v2mBatchSkip = () => {
      U().closeModal();
      const rest = fileNames.filter(f => f !== sm.fileName);
      if (!rest.length) { toast('已跳过「变速」模组，其余没有可操作的模组'); render(); return; }
      if (mode === 'delete') doBatchDelete(rest); else doBatchToggle(rest, enabled);
    };
    U().modalActions.v2mBatchBoth = () => {
      U().closeModal();
      post({ type: 'modSaveSpeedhack', speedhackBaseSpeed: 1.0 });
      if (mode === 'delete') doBatchDelete(fileNames); else doBatchToggle(fileNames, enabled);
    };
    const verb = mode === 'delete' ? '删除' : '禁用';
    const listHtml = names.slice(0, 8).join('、') + (names.length > 8 ? ` 等 ${names.length} 个` : '');
    U().openModal(`
      <div class="v2-confirm"><div class="v2-confirm-icon" style="background:rgba(250,204,21,.12);color:var(--gp-gold)">${I().info}</div>
        <div><b class="v2-confirm-title">${mode === 'delete' ? '删除' : '禁用'} ${fileNames.length} 个模组？</b>
          <ul class="v2-confirm-lines">
            <li>${listHtml}</li>
            <li>现在的启动倍速是 <b>${x}x</b>，${esc(sm.displayName || sm.name)} 是切回 1.0x 的热键，${verb === '删除' ? '删掉后' : '关掉后'}<b>游戏会一直以 ${x}x 运行，没法切回 1.0x</b></li>
          </ul></div></div>
      <div class="v2-modal-foot"><button class="v2-btn ghost" data-v2modal="close">取消</button><span class="v2-top-spacer"></span>
        <button class="v2-btn line" data-v2modal="v2mBatchSkip">跳过变速模组，其他照常</button>
        <button class="v2-btn primary" data-v2modal="v2mBatchBoth">同时把倍速改回 1.0x 并全部${verb}</button></div>`, 'narrow');
    render();
  }
  function batchToggle(fileNames, enabled) {
    fileNames = [...new Set(fileNames.filter(Boolean))];
    if (!fileNames.length) { toast('没有可操作的模组'); return; }
    if (!enabled && needsSpeedGuard(fileNames)) return speedBatchDialog(fileNames, enabled, 'toggle');
    doBatchToggle(fileNames, enabled);
  }
  function batchDelete(fileNames) {
    fileNames = [...new Set(fileNames.filter(Boolean))];
    if (!fileNames.length) { toast('没有可操作的模组'); return; }
    if (needsSpeedGuard(fileNames)) return speedBatchDialog(fileNames, false, 'delete');
    const names = fileNames.map(f => findMod(f));
    const shown = names.slice(0, 10).map(m => esc(m?.displayName || m?.name || m?.fileName || '')).join('、');
    const suffix = names.length > 10 ? ` 等 ${names.length} 个` : '';
    const hasBuiltIn = names.some(m => m && isBuiltIn(m));
    confirm({
      title: `删除 ${fileNames.length} 个模组？`,
      lines: [`将从游戏目录删除这些模组：<b>${shown}${suffix}</b>`,
        hasBuiltIn ? '其中包含内置模组，以后重新安装加载器时可以装回来' : '只想暂时不用的话，关掉开关就行，不必删除',
        status()?.gameRunning && '游戏正在运行，重启游戏后生效'],
      onOk: () => doBatchDelete(fileNames),
    });
  }

  const actions = {
    import: () => post({ type: 'modPickImport' }),
    openMods: () => post({ type: 'modOpenModsFolder' }),
    openLogs: () => post({ type: 'modOpenLogsFolder' }),
    openGame: () => post({ type: 'speedhackOpenGameDir' }),
    install: () => post({ type: 'modInstall', overwriteDll: false, allowDowngrade: false, includeSample: true }),
    restore: () => MM()?.startRecovery(true),
    update: () => { const st = status(), foreign = st?.installed && !st?.loaderManaged; post({ type: 'modDownloadUpdate', overwriteDll: !!foreign, allowDowngrade: false }); },
    check: () => {
      const mm = MM(); if (!mm) return;
      mm.updateInfo = { ...(mm.updateInfo || {}), state: 'checking', error: '' };
      post({ type: 'modCheckUpdate' });
      render();
    },
    checkTop: () => actions.check(),
    reinstallTop: () => actions.reinstall(),
    manageGames: () => showPage('settings'),
    adv: () => { const d = $('v2ModsAdv'); if (d) { d.open = true; d.scrollIntoView({ behavior: 'smooth' }); } },
    loaderSettings: () => MM()?.openLoaderSettings(),
    changelog: () => window.VersionModule?.requestChangelog('loader'),
    enableAll: () => batchToggle(filteredList().filter(m => m.enabled === false).map(m => m.fileName), true),
    disableAll: () => batchToggle(filteredList().filter(m => m.enabled !== false).map(m => m.fileName), false),
    enableSel: () => batchToggle(selectedTargets(), true),
    disableSel: () => batchToggle(selectedTargets(), false),
    deleteSel: () => batchDelete(selectedTargets()),
    clearSel: () => { M.sel.clear(); render(); },
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
        lines: [`会从游戏目录删除 <b>${esc(m.directoryName ? (m.disabledFolder ? 'disabled_mods\\' : 'mods\\') + m.directoryName + '\\' : m.fileName)}</b>`,
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
    if (t.id === 'v2mGame') { if (t.value) post({ type: 'gameProfileSetActive', id: t.value }); return; }
    if (t.dataset.v2mToggle) { const m = findMod(t.dataset.v2mToggle); if (m) toggleMod(m, t.checked); return; }
    if (t.dataset.v2mSel) {
      if (t.checked) M.sel.add(t.dataset.v2mSel); else M.sel.delete(t.dataset.v2mSel);
      render();
      return;
    }
    if (t.dataset.v2mSelectAll !== undefined) {
      const list = filteredList();
      if (t.checked) list.forEach(m => M.sel.add(m.fileName));
      else list.forEach(m => M.sel.delete(m.fileName));
      render();
      return;
    }
    if (t.dataset.v2mSpeed !== undefined) { saveSpeed(t.checked ? 2.0 : 1.0); return; }
    if (t.id === 'v2mSort') { M.sort = t.value; writePref(PREF.sort, M.sort); render(); return; }
    if (t.id === 'v2mGroup') { M.group = t.value; writePref(PREF.group, M.group); render(); return; }
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
    const gl = GL();
    if (typeof gl?.renderProfiles === 'function') {
      const orig = gl.renderProfiles;
      gl.renderProfiles = function (...args) { const r = orig.apply(this, args); render(); return r; };
    }
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

  // 宿主消息（dispatcher）用：加载器进度条；render 供内部重画
  window.V2ModsModule = { render, progress, applyFilter, get state() { return M; } };
})();
