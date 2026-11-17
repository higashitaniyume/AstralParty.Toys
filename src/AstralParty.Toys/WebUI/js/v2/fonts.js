// v2/fonts.js — 自定义 → 字体：系统字体卡片选择
// 卡片预览直接用当前系统字体渲染中文示例；选中后写 FontMod 的 config.json，
// 游戏内由模组热重载生效，不需要重启。
(function () {
  'use strict';

  const K = {
    loaded: false,
    installed: false,
    loaderEnabled: false,
    enabled: false,
    disabled: false,
    family: '',
    applyToAll: true,
    fonts: [],
    q: '',
    onlyCjk: true,
    error: '',
    busy: false,
  };

  const SAMPLE_MAIN = '星引擎Party';
  const SAMPLE_SUB = '星币 ×3 · Lv.12';
  const MAX_RENDER = 200;

  const I = () => window.V2UI.ICON;
  const frame = action => window.V2Skins?.frameHead?.(action) ?? '';

  const hue = s => [...String(s)].reduce((a, c) => a + c.charCodeAt(0), 0) % 360;

  function skeleton() {
    return `<div class="v2-skel-grid" aria-hidden="true">${Array.from({ length: 8 }, () =>
      '<div class="v2-skel-card"><div class="v2-skel v2-skel-thumb"></div><div class="v2-skel-body"><div class="v2-skel v2-skel-block" style="width:65%"></div><div class="v2-skel v2-skel-block" style="width:40%"></div></div></div>').join('')}</div>`;
  }

  function visibleFonts() {
    const q = K.q.trim().toLowerCase();
    const out = [];
    for (const f of K.fonts) {
      if (K.onlyCjk && !f.cjk) continue;
      if (q) {
        const hay = (String(f.family || '') + ' ' + String(f.display || '')).toLowerCase();
        if (!hay.includes(q)) continue;
      }
      out.push(f);
      if (out.length >= MAX_RENDER) break;
    }
    return out;
  }

  function banner() {
    if (K.error) return `<div class="v2-card v2-empty">${esc(K.error)}</div>`;
    if (!K.installed) {
      return `<div class="v2-card" style="padding:16px 18px">
        <b>还没有安装 FontMod</b>
        <p class="v2-muted" style="margin:6px 0 12px">字体替换由内置模组 FontMod 完成。请先到「模组」页安装或更新加载器，内置模组会一起装上。</p>
        <button class="v2-btn primary" data-v2f="mods">去模组页</button></div>`;
    }
    if (!K.loaderEnabled) {
      return `<div class="v2-card" style="padding:16px 18px">
        <b>FontMod 已被停用</b>
        <p class="v2-muted" style="margin:6px 0 12px">${K.disabled ? '模组被停在 <span style="font-family:var(--v2-mono)">disabled_mods</span> 里' : '模组的 sidecar 标记为停用'}，加载器不会加载它。到「模组」页启用后再回来。</p>
        <button class="v2-btn primary" data-v2f="mods">去模组页</button></div>`;
    }
    return '';
  }

  function variantCss(family, weight, style) {
    return `font-family:${esc(JSON.stringify(String(family)))},'Microsoft YaHei',sans-serif;font-weight:${weight || 400};font-style:${style === 'italic' ? 'italic' : 'normal'}`;
  }

  function card(f) {
    const variants = Array.isArray(f.variants) && f.variants.length
      ? f.variants
      : [{ label: '', family: f.family, weight: 400, style: 'normal', available: true }];
    const active = variants.find(v => v.family === K.family);
    const on = !!active;
    const preview = active || variants[0];
    const pickFamily = preview.family || f.family;
    const h = hue(f.family);
    const bg = `linear-gradient(135deg,hsl(${h},42%,26%),hsl(${(h + 42) % 360},38%,18%))`;
    const previewStyle = variantCss(preview.family || f.family, preview.weight, preview.style);
    const chips = variants.length > 1
      ? `<div style="display:flex;flex-wrap:wrap;gap:4px;padding:6px 8px 8px;border-top:1px solid var(--v2-border)">${variants.map(v => {
          const onV = !!K.family && v.family === K.family;
          const css = variantCss(v.family || f.family, v.weight, v.style);
          const tip = v.available ? (v.family || f.family) : (v.family || f.family) + '（系统没有注册成独立字体名，游戏内选不了）';
          return `<button data-v2f-variant="${esc(v.family || f.family)}" data-v2f-cjk="${f.cjk ? '1' : '0'}" title="${esc(tip)}"
            style="${css};font-size:11px;line-height:1;padding:4px 8px;border-radius:999px;border:1px solid ${onV ? 'rgba(255,23,118,.55)' : 'var(--v2-border)'};background:transparent;color:${onV ? 'var(--v2-text)' : 'var(--v2-text-soft)'};cursor:pointer;white-space:nowrap">${esc(v.label || '常规')}</button>`;
        }).join('')}</div>`
      : '';

    return `<div class="v2-scheme${on ? ' active' : ''}" data-v2f-cjk="${f.cjk ? '1' : '0'}">
      <button data-v2f-pick="${esc(pickFamily)}" data-v2f-cjk="${f.cjk ? '1' : '0'}" title="${esc(pickFamily)}"
        style="display:block;width:100%;padding:0;border:0;background:none;color:inherit;font:inherit;text-align:left;cursor:pointer">
        <div class="thumb" style="display:flex;flex-direction:column;justify-content:center;align-items:center;gap:2px;background:${bg};${previewStyle};font-size:22px;line-height:1.3;white-space:normal;padding:10px;text-align:center">
          ${on ? '<span class="v2-badge pink">使用中</span>' : ''}
          <span>${esc(SAMPLE_MAIN)}</span>
          <span style="font-size:14px;opacity:.85">${esc(SAMPLE_SUB)}</span>
        </div>
        <div class="info"><b>${esc(f.display || f.family)}</b>
          <small>${esc(f.family)}${f.cjk ? ' · 支持中文' : ' · <span style="color:#ffb02e">不含中文</span>'}</small></div>
      </button>
      ${chips}
    </div>`;
  }

  // .v2-switch 是 36×20 的固定开关（input/span 都是绝对定位），文字必须放在它外面，
  // 否则会压到开关上（实测截图就是这么坏掉的）。
  const toggle = (id, checked, label, title) =>
    `<label title="${esc(title)}" style="display:inline-flex;align-items:center;gap:8px;font-size:13px;color:var(--v2-text-soft);cursor:pointer;white-space:nowrap">
      <span class="v2-switch" style="display:inline-block"><input type="checkbox" id="${id}" ${checked ? 'checked' : ''}><span></span></span>${label}
    </label>`;

  function html() {
    if (!K.loaded) return frame('') + skeleton();

    const list = visibleFonts();
    const current = K.family ? `当前字体：<b>${esc(K.family)}</b>` : '当前：游戏默认字体';
    return frame('') + banner() + `
      <div class="v2-toolbar" style="flex-wrap:wrap;gap:10px">
        <label class="v2-search">${I().search}<input id="v2fQ" placeholder="搜索字体（中文名 / 英文名）" value="${esc(K.q)}" spellcheck="false"></label>
        <div style="display:flex;align-items:center;gap:14px;flex-wrap:wrap">
          ${toggle('v2fCjk', K.onlyCjk, '只看支持中文', '只显示包含汉字的字体')}
          ${toggle('v2fAll', K.applyToAll, '同时替换标题 / 数字', '关闭后只替换默认正文，保留游戏原本的标题与数字字体')}
        </div>
        <span class="v2-top-spacer"></span>
        <button class="v2-btn ghost" data-v2f="reset" ${K.family ? '' : 'disabled'}>恢复游戏默认</button>
      </div>
      <p class="v2-muted" style="margin:-6px 0 12px"><span class="v2-dot"></span>${current}　共 ${K.fonts.length} 个系统字体，显示 <span id="v2fShown">${list.length}</span> 个；游戏运行中选中即时生效，未运行则下次启动生效。</p>
      <div class="v2-grid">${list.map(card).join('') || '<div class="v2-empty" style="grid-column:1/-1">没有找到匹配的字体</div>'}</div>`;
  }

  // 输入/勾选不整页重画：直接改卡片显隐，避免输入框失焦。
  function applyFilter() {
    const q = K.q.trim().toLowerCase();
    let shown = 0;
    const cards = document.querySelectorAll('#skinsView [data-v2f-pick]');
    cards.forEach(el => {
      const cjk = el.getAttribute('data-v2f-cjk') === '1';
      const family = el.getAttribute('data-v2f-pick') || '';
      const display = el.querySelector('.info b')?.textContent || '';
      const ok = (!K.onlyCjk || cjk) && (!q || (family + ' ' + display).toLowerCase().includes(q));
      el.style.display = ok ? '' : 'none';
      if (ok) shown++;
    });
    const count = document.getElementById('v2fShown');
    if (count) count.textContent = String(shown);
  }

  function choose(family) {
    if (K.busy) return;
    K.busy = true;
    post({ type: 'setFontModFamily', fontFamily: family, applyToAllFonts: K.applyToAll });
  }

  function persist() {
    post({ type: 'setFontModFamily', fontFamily: K.family, applyToAllFonts: K.applyToAll });
  }

  function render(payload) {
    if (!payload) return;
    K.loaded = true;
    K.installed = !!payload.installed;
    K.loaderEnabled = payload.loaderEnabled !== false;
    K.enabled = !!payload.enabled;
    K.disabled = !!payload.disabled;
    K.family = payload.fontFamily || '';
    K.applyToAll = payload.applyToAllFonts !== false;
    K.error = payload.error || '';
    if (Array.isArray(payload.fonts)) K.fonts = payload.fonts;
    rerender();
  }

  function applied(payload) {
    K.busy = false;
    if (payload?.ok) {
      K.family = payload.fontFamily || '';
      K.applyToAll = payload.applyToAllFonts !== false;
    } else if (payload?.message) {
      window.toast?.(payload.message);
    }
    rerender();
  }

  function onShow() {
    rerender();
    post({ type: 'systemFonts' });
  }

  // 重画自定义页并恢复筛选（render 会按 K.q 重新生成输入框，所以要重新套一次显隐）
  function rerender() {
    window.V2Skins?.render?.();
    applyFilter();
  }

  document.addEventListener('click', ev => {
    const view = ev.target.closest?.('#skinsView');
    if (!view) return;

    const action = ev.target.closest?.('[data-v2f]');
    if (action && !action.disabled) {
      const name = action.dataset.v2f;
      if (name === 'mods') { window.showPage?.('mods'); return; }
      if (name === 'reset') { choose(''); return; }
    }

    const pick = ev.target.closest?.('[data-v2f-pick]');
    if (pick && !pick.disabled) choose(pick.dataset.v2fPick || '');

    const variant = ev.target.closest?.('[data-v2f-variant]');
    if (variant && !variant.disabled) choose(variant.dataset.v2fVariant || '');
  });

  document.addEventListener('input', ev => {
    if (ev.target?.id !== 'v2fQ') return;
    K.q = ev.target.value;
    applyFilter();
  });

  document.addEventListener('change', ev => {
    const el = ev.target;
    if (!el) return;
    if (el.id === 'v2fCjk') { K.onlyCjk = el.checked; applyFilter(); }
    if (el.id === 'v2fAll') { K.applyToAll = el.checked; persist(); }
  });

  window.V2Fonts = { html, render, applied, onShow };
})();

