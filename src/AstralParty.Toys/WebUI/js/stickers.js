// stickers.js — 贴纸回应：选择器（与网页版一致的行为与外观）+ 模组上的回应药丸
//
// 资源全部内嵌在程序集里（WebUI/stickers/，随 WebUI\** 一起嵌入）：
// 498 张 64×64 无损 WebP（424 静 + 74 动图）+ sticker-catalog.json 目录。
// 计数与点选切换走宿主（WorkshopService 调 /api/mod-stickers），
// 匿名身份与网页版共用 localStorage 键名（ forge.voter / forge.sticker.* ），
// 两边各自独立（WebView 与浏览器 localStorage 不互通，符合匿名计数）。
(function () {
  'use strict';

  const RECENT_KEY = 'forge.sticker.recent.v1';
  const FAVORITES_KEY = 'forge.sticker.favorites.v1';
  const VOTER_KEY = 'forge.voter';
  const RECENT_LIMIT = 24;
  const FAVORITES_LIMIT = 200;

  const t = (key, params) => (window.i18n ? window.i18n.t(key, params) : key);

  // ---------- 目录（内嵌） ----------
  let catalogPromise = null;
  let catalogItems = [];
  let catalogGroups = [];
  let catalogById = new Map();

  function stickerFileSrc(sticker) {
    // sticker-catalog.json 里的 imageUrl 是 /stickers/...（网页服务路径），
    // Toys 内嵌路径是相对文档的 stickers/...，按 file 字段拼。
    if (sticker && sticker.file) return 'stickers/' + sticker.file;
    const url = String(sticker?.imageUrl || '');
    if (!url) return '';
    return url.replace(/^\/stickers\//, 'stickers/');
  }

  // 与网页版 lib/stickers.ts 的 matchesStickerQuery 一致：每个空格分词都命中。
  function searchText(sticker) {
    const kindWord = sticker.kind === 'animated' ? '动图 动画 animated' : '静态 static';
    return [
      sticker.stickerId,
      String(sticker.stickerId).replace(/[_-]+/g, ' '),
      sticker.groupId,
      sticker.groupName,
      sticker.imageUrl || sticker.file || '',
      sticker.kind,
      kindWord,
    ].join(' ').toLowerCase();
  }

  function matchesQuery(sticker, query) {
    const q = String(query || '').trim().toLowerCase();
    if (!q) return true;
    const haystack = searchText(sticker);
    return q.split(/\s+/).filter(Boolean).every(token => haystack.includes(token));
  }

  function loadCatalog() {
    if (catalogPromise) return catalogPromise;
    catalogPromise = fetch('stickers/sticker-catalog.json', { cache: 'no-store' })
      .then(response => {
        if (!response.ok) throw new Error('catalog HTTP ' + response.status);
        return response.json();
      })
      .then(data => {
        catalogItems = Array.isArray(data?.items) ? data.items : [];
        catalogGroups = Array.isArray(data?.groups) ? data.groups : [];
        catalogById = new Map(catalogItems.map(s => [s.stickerId, s]));
        return { items: catalogItems, groups: catalogGroups };
      })
      .catch(() => {
        catalogItems = [];
        catalogGroups = [];
        catalogById = new Map();
        return { items: [], groups: [] };
      });
    return catalogPromise;
  }

  // ---------- 匿名身份 / 最近 / 收藏 ----------
  function createVoterId() {
    if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) return crypto.randomUUID();
    return Date.now().toString(36) + '-' + Math.random().toString(36).slice(2, 10);
  }

  function ensureVoterId() {
    let voterId = '';
    try { voterId = localStorage.getItem(VOTER_KEY) || ''; } catch { /* ignore */ }
    if (!voterId) {
      voterId = createVoterId();
      try { localStorage.setItem(VOTER_KEY, voterId); } catch { /* best effort */ }
    }
    return voterId;
  }

  function loadList(key) {
    try {
      const raw = localStorage.getItem(key);
      if (!raw) return [];
      const parsed = JSON.parse(raw);
      if (!Array.isArray(parsed)) return [];
      return parsed.filter(v => typeof v === 'string' && v.length > 0);
    } catch {
      return [];
    }
  }

  function saveList(key, list) {
    try { localStorage.setItem(key, JSON.stringify(list)); } catch { /* best effort */ }
  }

  // ---------- 回应状态（由 WorkshopModule 在拿到宿主回推后写入） ----------
  const countsByMod = new Map(); // modId -> { stickers, total }
  const busyMods = new Set();
  const pending = new Map(); // modId -> resolve(ok)

  function hydrateStickers(list) {
    const arr = Array.isArray(list) ? list : [];
    return arr.map(s => {
      const imageUrl = s.imageUrl || catalogById.get(s.stickerId)?.imageUrl || s.imageUrl || '';
      return { ...s, imageUrl };
    });
  }

  // ---------- 回应药丸条（与网页版 ModReactionStrip 一致） ----------
  function stripHtml(modId) {
    const entry = countsByMod.get(modId);
    const stickers = entry ? hydrateStickers(entry.stickers).filter(s => s.count > 0) : [];
    const total = entry ? (entry.total || 0) : 0;
    const busy = busyMods.has(modId);
    const pills = stickers.map(s => {
      const src = stickerFileSrc(s);
      const img = src
        ? `<img src="${esc(src)}" alt="" loading="lazy" width="24" height="24" class="forge-sticker-pill-img">`
        : `<span class="forge-sticker-pill-fallback">${esc(String(s.stickerId).slice(-2))}</span>`;
      return `<button type="button" class="forge-sticker-pill${s.reactedByMe ? ' is-mine' : ''}" data-sticker-pill data-mod-id="${esc(modId)}" data-sticker-id="${esc(s.stickerId)}" aria-pressed="${s.reactedByMe ? 'true' : 'false'}" aria-label="${esc(t('workshop.sticker_reactions'))} ${esc(s.stickerId)}" title="${esc(s.stickerId)}"${busy ? ' disabled' : ''}>`
        + `<span class="forge-sticker-pill-ico" aria-hidden="true">${img}`
        + `<span class="forge-telegram-particle p1">${esc(String(s.stickerId).slice(-2))}</span><span class="forge-telegram-particle p2">${esc(String(s.stickerId).slice(-2))}</span><span class="forge-telegram-particle p3">${esc(String(s.stickerId).slice(-2))}</span></span>`
        + `<span class="forge-sticker-pill-count">${esc(fmt(s.count))}</span></button>`;
    }).join('');
    const totalText = total > 0
      ? `<span class="forge-sticker-total">${esc(t('workshop.sticker_total', { count: fmt(total) }))}</span>`
      : '';
    return `<div class="forge-sticker-strip" role="group" aria-label="${esc(t('workshop.sticker_reactions'))}" data-sticker-strip="${esc(modId)}">${pills}`
      + `<button type="button" class="forge-sticker-trigger" data-sticker-trigger data-mod-id="${esc(modId)}" aria-haspopup="dialog" aria-expanded="false" aria-label="${esc(t('workshop.sticker_picker_label'))}"><span aria-hidden="true">＋</span> ${esc(t('workshop.sticker_add'))}</button>`
      + totalText
      + `</div>`;
  }

  function renderStrip(modId) {
    document.querySelectorAll('[data-sticker-strip]').forEach(node => {
      if (node.getAttribute('data-sticker-strip') !== modId) return;
      node.outerHTML = stripHtml(modId);
    });
    if (pickerEl && pickerState.modId === modId) syncPickerTriggerExpanded();
  }

  function renderAllStrips() {
    document.querySelectorAll('[data-sticker-strip]').forEach(node => {
      const modId = node.getAttribute('data-sticker-strip');
      if (modId) node.outerHTML = stripHtml(modId);
    });
  }

  // ---------- 选择器（与网页版 StickerPicker 一致） ----------
  const pickerState = {
    open: false,
    anchor: null,
    modId: '',
    query: '',
    activeId: 'recent',
    pickedRecent: false,
    hoveredId: null,
    favorites: [],
    recent: [],
    busy: false,
    error: '',
  };
  let pickerEl = null; // host: fixed inset-0 pointer-events:none
  let pickerCleanup = null; // { document: [], window: [] } 待移除的监听

  function clamp(value, min, max) {
    return Math.min(Math.max(value, min), Math.max(min, max));
  }

  function packsFromCatalog() {
    const sorted = [...catalogItems].sort((a, b) => (a.sortIndex || 0) - (b.sortIndex || 0));
    const firstByGroup = new Map();
    for (const s of sorted) {
      if (!firstByGroup.has(s.groupId)) firstByGroup.set(s.groupId, s);
    }
    return catalogGroups.map(g => ({
      id: g.groupId,
      name: g.groupName,
      count: g.count,
      icon: firstByGroup.get(g.groupId),
    }));
  }

  function effectiveActiveId(packs) {
    // 目录后到时若还停在空的「最近使用」，自动切到第一个贴纸包（网页版修复逻辑一致）。
    if (pickerState.activeId === 'recent' && !pickerState.pickedRecent
        && pickerState.recent.length === 0 && packs.length > 0) {
      return packs[0].id;
    }
    return pickerState.activeId;
  }

  function visibleStickers(effectiveId) {
    const sorted = [...catalogItems].sort((a, b) => (a.sortIndex || 0) - (b.sortIndex || 0));
    const q = pickerState.query.trim();
    if (q) return sorted.filter(s => matchesQuery(s, q));
    if (effectiveId === 'recent') {
      return pickerState.recent.map(id => catalogById.get(id)).filter(Boolean);
    }
    if (effectiveId === 'favorites') {
      return pickerState.favorites.map(id => catalogById.get(id)).filter(Boolean);
    }
    return sorted.filter(s => s.groupId === effectiveId);
  }

  function pickerHtml() {
    const packs = packsFromCatalog();
    const effectiveId = effectiveActiveId(packs);
    const isSearching = pickerState.query.trim().length > 0;
    const visible = visibleStickers(effectiveId);
    const hovered = pickerState.hoveredId ? (catalogById.get(pickerState.hoveredId) || null) : null;
    const favoriteSet = new Set(pickerState.favorites);
    const entry = pickerState.modId ? countsByMod.get(pickerState.modId) : null;
    const mine = new Set((entry?.stickers || []).filter(s => s.reactedByMe).map(s => s.stickerId));

    const sectionTitle = isSearching
      ? t('workshop.sticker_search_results', { count: fmt(visible.length) })
      : effectiveId === 'recent'
        ? t('workshop.sticker_recent')
        : effectiveId === 'favorites'
          ? t('workshop.sticker_favorites')
          : (packs.find(p => p.id === effectiveId)?.name || t('workshop.sticker_add'));

    function tabButton(id, label, inner, active) {
      return `<button type="button" role="tab" class="forge-sticker-tab${active ? ' is-active' : ''}" data-sticker-tab="${esc(id)}" aria-selected="${active ? 'true' : 'false'}" aria-label="${esc(label)}" title="${esc(label)}">${inner}</button>`;
    }
    const recentIcon = `<svg viewBox="0 0 20 20" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><circle cx="10" cy="10" r="8"/><path d="M10 5v5l3 2"/></svg>`;
    const favIcon = `<svg viewBox="0 0 20 20" width="16" height="16" fill="currentColor" stroke="currentColor" stroke-width="1.5" stroke-linejoin="round" aria-hidden="true"><path d="M10 2.5 12.6 7.8l5.9.9-4.2 4.1 1 5.8L10 15.9 4.7 18.6l1-5.8L1.5 8.7l5.9-.9Z"/></svg>`;
    const tabs = tabButton('recent', t('workshop.sticker_recent'), recentIcon, effectiveId === 'recent' && !isSearching)
      + tabButton('favorites', t('workshop.sticker_favorites'), favIcon, effectiveId === 'favorites' && !isSearching)
      + `<span class="forge-sticker-divider" aria-hidden="true"></span>`
      + packs.map(pack => {
        const active = effectiveId === pack.id && !isSearching;
        const inner = pack.icon
          ? `<img src="${esc(stickerFileSrc(pack.icon))}" alt="" loading="lazy" width="32" height="32" class="forge-sticker-tab-img">`
          : `<span class="forge-sticker-tab-char">${esc(String(pack.name).slice(0, 1))}</span>`;
        return `<button type="button" role="tab" class="forge-sticker-tab forge-sticker-pack${active ? ' is-active' : ''}" data-sticker-tab="${esc(pack.id)}" aria-selected="${active ? 'true' : 'false'}" aria-label="${esc(pack.name)}" title="${esc(pack.name)}">${inner}</button>`;
      }).join('');

    const cells = visible.map((sticker, index) => {
      const isMine = mine.has(sticker.stickerId);
      const isFav = favoriteSet.has(sticker.stickerId);
      return `<div class="forge-sticker-cell">`
        + `<button type="button" class="forge-sticker-cell-btn${isMine ? ' is-mine' : ''}" role="option" aria-selected="${isMine ? 'true' : 'false'}" aria-label="${esc(sticker.groupName)} ${esc(sticker.stickerId)}" title="${esc(sticker.stickerId)}" data-sticker-index="${index}" data-sticker-cell="${esc(sticker.stickerId)}"${pickerState.busy ? ' disabled' : ''}>`
        + `<img src="${esc(stickerFileSrc(sticker))}" alt="" loading="lazy" width="64" height="64" draggable="false" class="forge-sticker-cell-img"></button>`
        + `<button type="button" class="forge-sticker-fav${isFav ? ' is-fav' : ''}" data-sticker-fav="${esc(sticker.stickerId)}" aria-label="${esc(isFav ? t('workshop.sticker_fav_cancel') : t('workshop.sticker_fav_add'))}" aria-pressed="${isFav ? 'true' : 'false'}"><span aria-hidden="true">${isFav ? '★' : '☆'}</span></button>`
        + `</div>`;
    }).join('');

    const emptyHtml = visible.length === 0 ? emptyStateHtml(isSearching, effectiveId) : '';

    const preview = hovered
      ? `<img src="${esc(stickerFileSrc(hovered))}" alt="" width="80" height="80" draggable="false" class="forge-sticker-preview-img">`
        + `<div class="forge-sticker-preview-text"><p class="forge-sticker-preview-id" title="${esc(hovered.stickerId)}">${esc(hovered.stickerId)}</p>`
        + `<p class="forge-sticker-preview-meta">${esc(hovered.groupName)} · ${esc(hovered.kind === 'animated' ? t('workshop.sticker_animated') : t('workshop.sticker_static'))}</p></div>`
        + `<button type="button" class="forge-sticker-preview-fav${favoriteSet.has(hovered.stickerId) ? ' is-fav' : ''}" data-sticker-preview-fav="${esc(hovered.stickerId)}" aria-label="${esc(favoriteSet.has(hovered.stickerId) ? t('workshop.sticker_fav_cancel') : t('workshop.sticker_fav_add'))}" aria-pressed="${favoriteSet.has(hovered.stickerId) ? 'true' : 'false'}"><span aria-hidden="true" class="forge-sticker-preview-star">${favoriteSet.has(hovered.stickerId) ? '★' : '☆'}</span></button>`
      : `<p class="forge-sticker-hint">${esc(t('workshop.sticker_preview_hint'))} <span class="forge-sticker-hint-dim">· ${esc(t('workshop.sticker_kbd_hint'))}</span></p>`;

    return `<div class="forge-sticker-picker" role="dialog" aria-label="${esc(t('workshop.sticker_picker_label'))}" aria-modal="false">`
      + `<div class="forge-sticker-searchrow">`
      + `<span class="forge-sticker-search-ico" aria-hidden="true"><svg viewBox="0 0 20 20" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="9" cy="9" r="6"/><path d="m13.5 13.5 4 4"/></svg></span>`
      + `<input type="search" class="forge-sticker-search" data-sticker-search placeholder="${esc(t('workshop.sticker_search_placeholder'))}" aria-label="${esc(t('workshop.sticker_search_label'))}" value="${esc(pickerState.query)}" autocomplete="off" spellcheck="false">`
      + (pickerState.query ? `<button type="button" class="forge-sticker-x" data-sticker-clear aria-label="${esc(t('workshop.sticker_clear_search'))}"><span aria-hidden="true">✕</span></button>` : '')
      + `<button type="button" class="forge-sticker-x" data-sticker-close aria-label="${esc(t('workshop.sticker_close'))}"><span aria-hidden="true">✕</span></button>`
      + `</div>`
      + `<div class="forge-sticker-nav" role="tablist" aria-label="${esc(t('workshop.sticker_categories'))}">${tabs}</div>`
      + `<div class="forge-sticker-section"><span>${esc(sectionTitle)}</span>`
      + (isSearching ? `<button type="button" class="forge-sticker-clearlink" data-sticker-clear>${esc(t('workshop.sticker_clear_search'))}</button>` : '')
      + `</div>`
      + `<div class="forge-sticker-grid" data-sticker-grid>${emptyHtml || `<div class="forge-sticker-grid-inner" role="listbox" aria-label="${esc(sectionTitle)}">${cells}</div>`}</div>`
      + `<div class="forge-sticker-preview">${preview}</div>`
      + (pickerState.error ? `<p class="forge-sticker-error" role="alert">${esc(pickerState.error)}</p>` : '')
      + `</div>`;
  }

  function emptyStateHtml(searching, effectiveId) {
    if (searching) {
      return `<div class="forge-sticker-empty"><span class="forge-sticker-empty-ico" aria-hidden="true"><svg viewBox="0 0 20 20" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="9" cy="9" r="6"/><path d="m13.5 13.5 4 4"/></svg></span>`
        + `<p class="forge-sticker-empty-title">${esc(t('workshop.sticker_empty_search_title'))}</p>`
        + `<p class="forge-sticker-empty-hint">${esc(t('workshop.sticker_empty_search_hint'))}</p>`
        + `<button type="button" class="forge-sticker-empty-clear" data-sticker-clear>${esc(t('workshop.sticker_clear_search'))}</button></div>`;
    }
    if (effectiveId === 'recent') {
      return `<div class="forge-sticker-empty"><p class="forge-sticker-empty-title">${esc(t('workshop.sticker_empty_recent_title'))}</p><p class="forge-sticker-empty-hint">${esc(t('workshop.sticker_empty_recent_hint'))}</p></div>`;
    }
    if (effectiveId === 'favorites') {
      return `<div class="forge-sticker-empty"><p class="forge-sticker-empty-title">${esc(t('workshop.sticker_empty_fav_title'))}</p><p class="forge-sticker-empty-hint">${esc(t('workshop.sticker_empty_fav_hint'))}</p></div>`;
    }
    return `<div class="forge-sticker-empty"><p class="forge-sticker-empty-title">${esc(t('workshop.sticker_empty_pack'))}</p></div>`;
  }

  function pickerPanel() {
    return pickerEl?.querySelector('.forge-sticker-picker') || null;
  }

  function syncPickerTriggerExpanded() {
    if (!pickerState.modId) return;
    document.querySelectorAll('[data-sticker-trigger]').forEach(btn => {
      btn.setAttribute('aria-expanded', pickerEl && btn.getAttribute('data-mod-id') === pickerState.modId ? 'true' : 'false');
    });
  }

  function positionPicker() {
    const panel = pickerPanel();
    if (!panel) return;
    const vw = window.innerWidth;
    const vh = window.innerHeight;
    const w = panel.offsetWidth;
    const h = panel.offsetHeight;
    let left = (vw - w) / 2;
    let top = 8;
    const anchor = pickerState.anchor;
    if (anchor && anchor.isConnected) {
      const rect = anchor.getBoundingClientRect();
      left = clamp(rect.left, 8, Math.max(8, vw - w - 8));
      top = rect.bottom + 8;
      if (top + h > vh - 8) top = rect.top - h - 8;
      top = clamp(top, 8, Math.max(8, vh - h - 8));
    }
    panel.style.left = left + 'px';
    panel.style.top = top + 'px';
    syncPickerTriggerExpanded();
  }

  function renderPicker() {
    if (!pickerEl) return;
    // 保留选择器内非受控状态（搜索框焦点/选区、滚动位置）：整块重画会丢焦点。
    const grid = pickerEl.querySelector('[data-sticker-grid]');
    const gridScroll = grid ? grid.scrollTop : 0;
    const active = document.activeElement;
    const focusIsSearch = !!(pickerEl.contains(active) && active?.hasAttribute?.('data-sticker-search'));
    const searchStart = focusIsSearch ? active.selectionStart : null;
    const searchEnd = focusIsSearch ? active.selectionEnd : null;

    pickerEl.innerHTML = pickerHtml();
    positionPicker();
    const newGrid = pickerEl.querySelector('[data-sticker-grid]');
    if (newGrid) newGrid.scrollTop = gridScroll;
    if (focusIsSearch) {
      const input = pickerEl.querySelector('[data-sticker-search]');
      input?.focus({ preventScroll: true });
      if (searchStart !== null) {
        try { input?.setSelectionRange(searchStart, searchEnd); } catch { /* ignore */ }
      }
    }
    syncPickerHover();
  }

  function syncPickerHover() {
    if (!pickerEl) return;
    pickerEl.querySelectorAll('.forge-sticker-cell-btn').forEach(btn => btn.classList.toggle('is-hover', btn.getAttribute('data-sticker-cell') === pickerState.hoveredId));
  }

  function renderPreviewOnly() {
    if (!pickerEl) return;
    const preview = pickerEl.querySelector('.forge-sticker-preview');
    if (!preview) return;
    const hovered = pickerState.hoveredId ? (catalogById.get(pickerState.hoveredId) || null) : null;
    const favoriteSet = new Set(pickerState.favorites);
    preview.innerHTML = hovered
      ? `<img src="${esc(stickerFileSrc(hovered))}" alt="" width="80" height="80" draggable="false" class="forge-sticker-preview-img">`
        + `<div class="forge-sticker-preview-text"><p class="forge-sticker-preview-id" title="${esc(hovered.stickerId)}">${esc(hovered.stickerId)}</p>`
        + `<p class="forge-sticker-preview-meta">${esc(hovered.groupName)} · ${esc(hovered.kind === 'animated' ? t('workshop.sticker_animated') : t('workshop.sticker_static'))}</p></div>`
        + `<button type="button" class="forge-sticker-preview-fav${favoriteSet.has(hovered.stickerId) ? ' is-fav' : ''}" data-sticker-preview-fav="${esc(hovered.stickerId)}" aria-label="${esc(favoriteSet.has(hovered.stickerId) ? t('workshop.sticker_fav_cancel') : t('workshop.sticker_fav_add'))}" aria-pressed="${favoriteSet.has(hovered.stickerId) ? 'true' : 'false'}"><span aria-hidden="true" class="forge-sticker-preview-star">${favoriteSet.has(hovered.stickerId) ? '★' : '☆'}</span></button>`
      : `<p class="forge-sticker-hint">${esc(t('workshop.sticker_preview_hint'))} <span class="forge-sticker-hint-dim">· ${esc(t('workshop.sticker_kbd_hint'))}</span></p>`;
  }

  function openPicker(modId, anchor) {
    if (pickerEl) closePicker(false);
    pickerState.open = true;
    pickerState.modId = modId;
    pickerState.anchor = anchor;
    pickerState.query = '';
    pickerState.hoveredId = null;
    pickerState.error = '';
    pickerState.favorites = loadList(FAVORITES_KEY);
    pickerState.recent = loadList(RECENT_KEY);
    const packs = packsFromCatalog();
    const firstPack = packs[0]?.id;
    pickerState.activeId = loadList(RECENT_KEY).length > 0 ? 'recent' : (firstPack || 'recent');
    pickerState.pickedRecent = false;
    pickerState.busy = false;

    pickerEl = document.createElement('div');
    pickerEl.className = 'forge-sticker-picker-host';
    document.body.appendChild(pickerEl);
    renderPicker();
    pickerPanel()?.addEventListener('pointerdown', e => e.stopPropagation());
    requestAnimationFrame(positionPicker);

    const input = pickerEl.querySelector('[data-sticker-search]');
    input?.focus();
    input?.select();

    bindPickerEvents();
  }

  function closePicker(restoreFocus = true) {
    if (!pickerEl) {
      pickerState.open = false;
      return;
    }
    const anchor = pickerState.anchor;
    if (pickerCleanup) {
      pickerCleanup.forEach(({ target, type, listener, options }) => target.removeEventListener(type, listener, options));
      pickerCleanup = null;
    }
    pickerEl.remove();
    pickerEl = null;
    pickerState.open = false;
    syncPickerTriggerExpanded();
    if (restoreFocus && anchor?.isConnected) anchor.focus({ preventScroll: true });
  }

  function bindPickerEvents() {
    if (!pickerEl) return;
    // 委托监听挂在 host（pickerEl）上，renderPicker 重画 innerHTML 不会丢（网页版 React 也是声明式）。
    const on = (target, type, listener, options) => {
      target.addEventListener(type, listener, options);
      pickerCleanup.push({ target, type, listener, options });
    };
    pickerCleanup = [];

    // 搜索：实时过滤（与网页版一致）。renderPicker 会在之前用 input 事件驱动。
    on(pickerEl, 'input', e => {
      if (!e.target.closest?.('[data-sticker-search]')) return;
      pickerState.query = e.target.value;
      renderPicker();
    });

    // 搜索框 ArrowDown → 第一个贴纸（与网页版一致）。
    on(pickerEl, 'keydown', e => {
      if (e.target.closest?.('[data-sticker-search]') && e.key === 'ArrowDown') {
        e.preventDefault();
        pickerEl.querySelector('[data-sticker-cell]')?.focus();
      }
    });

    // 点击：分类 / 关闭 / 清除 / 收藏 / 选贴纸（与网页版一致）。
    on(pickerEl, 'click', e => {
      const tab = e.target.closest?.('[data-sticker-tab]');
      if (tab) {
        const id = tab.getAttribute('data-sticker-tab');
        if (id === 'recent') pickerState.pickedRecent = true;
        pickerState.activeId = id;
        pickerState.query = '';
        pickerState.hoveredId = null;
        renderPicker();
        return;
      }
      if (e.target.closest?.('[data-sticker-close]')) { closePicker(); return; }
      if (e.target.closest?.('[data-sticker-clear]')) {
        pickerState.query = '';
        renderPicker();
        pickerEl.querySelector('[data-sticker-search]')?.focus();
        return;
      }
      const favBtn = e.target.closest?.('[data-sticker-fav], [data-sticker-preview-fav]');
      if (favBtn) {
        const id = favBtn.getAttribute('data-sticker-fav') || favBtn.getAttribute('data-sticker-preview-fav');
        if (id) toggleFavorite(id);
        return;
      }
      const cell = e.target.closest?.('[data-sticker-cell]');
      if (cell) {
        const id = cell.getAttribute('data-sticker-cell');
        if (id) void handlePick(id);
      }
    });

    // 悬停 / 聚焦预览（与网页版一致）。
    on(pickerEl, 'mouseover', e => {
      const cell = e.target.closest?.('[data-sticker-cell]');
      const id = cell?.getAttribute('data-sticker-cell') || null;
      if (id !== pickerState.hoveredId) {
        pickerState.hoveredId = id;
        syncPickerHover();
        renderPreviewOnly();
      }
    });
    on(pickerEl, 'focusin', e => {
      const cell = e.target.closest?.('[data-sticker-cell]');
      const id = cell?.getAttribute('data-sticker-cell') || null;
      if (id && id !== pickerState.hoveredId) {
        pickerState.hoveredId = id;
        syncPickerHover();
        renderPreviewOnly();
      }
    });
    on(pickerEl, 'focusout', e => {
      const cell = e.target.closest?.('[data-sticker-cell]');
      if (cell && cell.getAttribute('data-sticker-cell') === pickerState.hoveredId) {
        pickerState.hoveredId = null;
        syncPickerHover();
        renderPreviewOnly();
      }
    });

    // 网格键盘导航：方向键 + Home/End（与网页版一致）。
    on(pickerEl, 'keydown', e => {
      const search = e.target.closest?.('[data-sticker-search]');
      if (search && e.key === 'ArrowDown') return; // 上面已处理
      const cell = e.target.closest?.('[data-sticker-cell]');
      if (!cell) return;
      const grid = pickerEl.querySelector('[data-sticker-grid]');
      const width = grid?.clientWidth ?? 344;
      const cols = Math.max(1, Math.floor((width + 4) / 68));
      const count = pickerEl.querySelectorAll('[data-sticker-cell]').length;
      const current = Number(cell.getAttribute('data-sticker-index') ?? '-1');
      let next = null;
      if (e.key === 'ArrowRight') next = Math.min(count - 1, current + 1);
      else if (e.key === 'ArrowLeft') next = Math.max(0, current - 1);
      else if (e.key === 'ArrowDown') next = Math.min(count - 1, current + cols);
      else if (e.key === 'ArrowUp') next = Math.max(0, current - cols);
      else if (e.key === 'Home') next = 0;
      else if (e.key === 'End') next = count - 1;
      else return;
      if (next === null || next < 0 || next >= count) return;
      e.preventDefault();
      const target = pickerEl.querySelector(`[data-sticker-index="${next}"]`);
      target?.focus();
      target?.scrollIntoView({ block: 'nearest' });
    });

    // 分类栏左右键（与网页版一致）。
    on(pickerEl, 'keydown', e => {
      if (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight') return;
      const nav = e.target.closest?.('.forge-sticker-nav');
      if (!nav) return;
      const buttons = [...nav.querySelectorAll('[data-sticker-tab]')];
      const current = e.target.closest?.('[data-sticker-tab]');
      const idx = buttons.indexOf(current);
      if (idx < 0) return;
      e.preventDefault();
      const next = e.key === 'ArrowRight' ? buttons[idx + 1] : buttons[idx - 1];
      next?.focus();
    });

    // 外部点击 + Esc + resize/scroll 关闭（与网页版一致；grid 内部滚动不关）。
    const onPointerDown = e => {
      const target = e.target;
      if (!target) return;
      if (pickerEl?.contains(target)) return;
      if (pickerState.anchor?.contains?.(target)) return;
      closePicker();
    };
    const onKeyDown = e => {
      if (e.key !== 'Escape') return;
      const search = pickerEl.querySelector('[data-sticker-search]');
      if (e.target === search && pickerState.query.trim()) {
        pickerState.query = '';
        renderPicker();
        pickerEl.querySelector('[data-sticker-search]')?.focus();
        return;
      }
      closePicker();
    };
    const onResize = () => closePicker();
    const onScroll = e => {
      if (pickerEl?.contains(e.target)) return;
      closePicker();
    };
    on(document, 'pointerdown', onPointerDown);
    on(window, 'keydown', onKeyDown);
    on(window, 'resize', onResize);
    on(window, 'scroll', onScroll, true);
  }

  function toggleFavorite(stickerId) {
    const prev = pickerState.favorites;
    const next = prev.includes(stickerId)
      ? prev.filter(id => id !== stickerId)
      : [stickerId, ...prev].slice(0, FAVORITES_LIMIT);
    pickerState.favorites = next;
    saveList(FAVORITES_KEY, next);
    renderPicker();
  }

  async function handlePick(stickerId) {
    if (pickerState.busy || busyMods.has(pickerState.modId)) return;
    const modId = pickerState.modId;
    pickerState.recent = [stickerId, ...pickerState.recent.filter(id => id !== stickerId)].slice(0, RECENT_LIMIT);
    saveList(RECENT_KEY, pickerState.recent);
    pickerState.error = '';
    const ok = await toggleSticker(modId, stickerId, true);
    if (ok) closePicker();
  }

  // ---------- 切换回应（药丸与选择器共用；失败不关选择器，网页版一致） ----------
  function toggleSticker(modId, stickerId, fromPicker) {
    return new Promise(resolve => {
      if (busyMods.has(modId)) { resolve(false); return; }
      busyMods.add(modId);
      if (fromPicker) { pickerState.busy = true; renderPicker(); }
      // Telegram 弹跳（与网页版一致）：重画条子会丢动画，所以先弹现有的、再重画。
      document.querySelectorAll('[data-sticker-strip]').forEach(strip => {
        if (strip.getAttribute('data-sticker-strip') !== modId) return;
        const pill = strip.querySelector(`[data-sticker-pill][data-sticker-id="${CSS.escape(stickerId)}"]`);
        pill?.classList.add('forge-telegram-pop');
        window.setTimeout(() => pill?.classList.remove('forge-telegram-pop'), 520);
      });
      pending.set(modId, resolve);
      post({ type: 'workshopStickerToggle', modId, stickerId, voterId: ensureVoterId() });
    });
  }

  // ---------- 供 WorkshopModule 调用的公开入口 ----------
  const StickerModule = {
    // 模板里只放回应条本身，数据由 syncAfterRender 补齐（避免重画整卡闪图）。
    placeholder(modId) {
      return stripHtml(modId);
    },
    // 列表/详情重画后：补齐当前可见条子的内容（不发请求）。
    syncAfterRender() {
      // 先关掉浮在已重建锚点上的选择器，避免锚点已不在文档里还开着。
      if (pickerEl && pickerState.anchor && !pickerState.anchor.isConnected) {
        closePicker(false);
      }
      renderAllStrips();
      if (pickerEl) renderPicker();
    },
    // 向宿主请求计数；modIds 去重上限 100（与服务器一致）。
    requestCounts(modIds) {
      const ids = [...new Set((modIds || []).filter(Boolean))].slice(0, 100);
      if (!ids.length) return;
      post({ type: 'workshopStickers', modIds: ids, voterId: ensureVoterId() });
    },
    applyBulk(payload) {
      if (!payload) return;
      if (payload.error) {
        // 列表本身不受影响（网页版 SSR 计数失败时只留空药丸），只提示错误。
        toast(payload.error);
        return;
      }
      const items = Array.isArray(payload.items) ? payload.items : [];
      for (const item of items) {
        if (!item?.modId) continue;
        countsByMod.set(item.modId, {
          stickers: hydrateStickers(item.stickers),
          total: item.reactionTotal || (item.stickers || []).reduce((sum, s) => sum + (s.count || 0), 0),
        });
        renderStrip(item.modId);
      }
      if (pickerEl) renderPicker();
    },
    applyToggle(payload) {
      if (!payload) return;
      const modId = payload.modId || '';
      const resolve = pending.get(modId);
      pending.delete(modId);
      busyMods.delete(modId);
      pickerState.busy = false;
      if (payload.error) {
        // 与网页版一致：选择器内联错误、不关选择器；药丸条场景走 toast。
        if (pickerEl && pickerState.modId === modId) {
          pickerState.error = payload.error || t('workshop.sticker_error');
          renderPicker();
        } else {
          toast(payload.error);
        }
        renderStrip(modId);
        resolve?.(false);
        return;
      }
      countsByMod.set(modId, {
        stickers: hydrateStickers(payload.stickers),
        total: payload.reactionTotal || (payload.stickers || []).reduce((sum, s) => sum + (s.count || 0), 0),
      });
      renderStrip(modId);
      if (pickerEl && pickerState.modId === modId) renderPicker();
      resolve?.(true);
    },
    // 列表/详情容器里的点击：药丸切换 / 打开选择器。返回 true 表示已处理（调用方不要再打开详情）。
    handleClick(event) {
      const pill = event.target.closest?.('[data-sticker-pill]');
      if (pill) {
        event.preventDefault();
        event.stopPropagation();
        const modId = pill.getAttribute('data-mod-id');
        const stickerId = pill.getAttribute('data-sticker-id');
        if (modId && stickerId) void toggleSticker(modId, stickerId, false);
        return true;
      }
      const trigger = event.target.closest?.('[data-sticker-trigger]');
      if (trigger) {
        event.preventDefault();
        event.stopPropagation();
        const modId = trigger.getAttribute('data-mod-id');
        if (!modId) return true;
        if (pickerEl && pickerState.modId === modId) {
          closePicker();
        } else {
          // 目录已就绪才打开；没就绪先拉，回来再开（与网页版 SSR 失败补拉一致）。
          loadCatalog().then(() => {
            const fresh = document.querySelector(`[data-sticker-trigger][data-mod-id="${CSS.escape(modId)}"]`);
            openPicker(modId, fresh || trigger);
          });
        }
        return true;
      }
      return false;
    },
    closePicker,
    pickerOpenFor(modId) {
      return !!pickerEl && pickerState.modId === modId;
    },
    // 语言切换后条子文案要重刷（列表本身会重载，这里只管已渲染的条子）。
    onLanguageChanged() {
      if (pickerEl) closePicker(false);
      renderAllStrips();
    },
  };

  window.StickerModule = StickerModule;

  window.addEventListener('appLanguageChanged', () => StickerModule.onLanguageChanged());
})();
