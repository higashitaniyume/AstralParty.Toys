// stickers.js — 贴纸回应：选择器（与网页版一致的行为与外观）+ 模组上的回应药丸
//
// 资源从市场下载后本地缓存（宿主 WorkshopService 调 /api/stickers 并缓存 64×64 无损 WebP，
// 图片经 workshop.aspatoys.hiynet.localhost 虚拟域名提供），不再内嵌在程序集里。
// 计数与点选切换同样走宿主（WorkshopService 调 /api/mod-stickers），
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

  // ---------- 目录（由宿主从市场拉取并缓存） ----------
  let catalogPromise = null;
  let catalogResolvers = [];
  let catalogReady = false;
  let catalogItems = [];
  let catalogGroups = [];
  let catalogById = new Map();
  // 贴纸图片缓存进度（首次从市场下载时可视化，避免长时间黑盒等待）。
  let catalogProgress = { active: false, downloaded: 0, total: 0, percent: 0 };

  function isCatalogLoading() {
    return !catalogReady && catalogPromise !== null;
  }

  function catalogProgressText() {
    if (catalogProgress.total > 0) {
      return t('workshop.sticker_downloading', {
        downloaded: fmt(catalogProgress.downloaded),
        total: fmt(catalogProgress.total),
        percent: fmt(catalogProgress.percent),
      });
    }
    return t('workshop.sticker_downloading', {
      downloaded: fmt(catalogProgress.downloaded),
      total: fmt(catalogProgress.total),
      percent: fmt(catalogProgress.percent),
    });
  }

  function stickerFileSrc(sticker) {
    return String(sticker?.imageUrl || '');
  }

  // 与网页版 lib/stickers.ts 的 matchesStickerQuery 一致：每个空格分词都命中。
  function searchText(sticker) {
    const kindWord = sticker.kind === 'animated' ? '动图 动画 animated' : '静态 static';
    return [
      sticker.stickerId,
      String(sticker.stickerId).replace(/[_-]+/g, ' '),
      sticker.groupId,
      sticker.groupName,
      sticker.imageUrl || '',
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
    if (catalogReady) return Promise.resolve({ items: catalogItems, groups: catalogGroups });
    if (catalogPromise) return catalogPromise;
    catalogProgress = { active: true, downloaded: 0, total: 0, percent: 0 };
    catalogPromise = new Promise(resolve => {
      catalogResolvers.push(resolve);
      post({ type: 'workshopStickerCatalog' });
    });
    return catalogPromise;
  }

  function resolveCatalogWaiters() {
    const waiters = catalogResolvers;
    catalogResolvers = [];
    const value = { items: catalogItems, groups: catalogGroups };
    waiters.forEach(resolve => resolve(value));
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

  function pickerParts() {
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

    const sectionHtml = `<span>${esc(sectionTitle)}</span>`
      + (isSearching ? `<button type="button" class="forge-sticker-clearlink" data-sticker-clear>${esc(t('workshop.sticker_clear_search'))}</button>` : '');
    let gridHtml = emptyHtml || `<div class="forge-sticker-grid-inner" role="listbox" aria-label="${esc(sectionTitle)}">${cells}</div>`;
    if (isCatalogLoading()) {
      gridHtml = `<div class="forge-sticker-loading" role="status"><span>${esc(catalogProgressText())}</span></div>`;
    }

    return { tabs, sectionTitle, sectionHtml, gridHtml, preview };
  }

  function pickerHtml() {
    const { tabs, sectionHtml, gridHtml, preview } = pickerParts();
    const loading = isCatalogLoading();
    const indeterminate = loading && catalogProgress.total === 0;
    return `<div class="forge-sticker-picker" role="dialog" aria-label="${esc(t('workshop.sticker_picker_label'))}" aria-modal="false">`
      + `<div class="forge-sticker-searchrow">`
      + `<span class="forge-sticker-search-ico" aria-hidden="true"><svg viewBox="0 0 20 20" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="9" cy="9" r="6"/><path d="m13.5 13.5 4 4"/></svg></span>`
      + `<input type="search" class="forge-sticker-search" data-sticker-search placeholder="${esc(t('workshop.sticker_search_placeholder'))}" aria-label="${esc(t('workshop.sticker_search_label'))}" value="${esc(pickerState.query)}" autocomplete="off" spellcheck="false">`
      + (pickerState.query ? `<button type="button" class="forge-sticker-x" data-sticker-clear aria-label="${esc(t('workshop.sticker_clear_search'))}"><span aria-hidden="true">✕</span></button>` : '')
      + `<button type="button" class="forge-sticker-x" data-sticker-close aria-label="${esc(t('workshop.sticker_close'))}"><span aria-hidden="true">✕</span></button>`
      + `</div>`
      + `<div class="forge-sticker-progress" data-sticker-progress${loading ? '' : ' hidden'}>`
      + `<div class="forge-sticker-progress-text" data-sticker-progress-text>${esc(loading ? catalogProgressText() : '')}</div>`
      + `<div class="forge-sticker-progress-bar${indeterminate ? ' is-indeterminate' : ''}" role="progressbar" aria-valuemin="0" aria-valuemax="100" aria-valuenow="${esc(String(catalogProgress.percent))}"><span class="forge-sticker-progress-fill" data-sticker-progress-fill style="width:${indeterminate ? '40' : clamp(catalogProgress.percent, 0, 100)}%"></span></div>`
      + `</div>`
      + `<div class="forge-sticker-nav" role="tablist" aria-label="${esc(t('workshop.sticker_categories'))}">${tabs}</div>`
      + `<div class="forge-sticker-section">${sectionHtml}</div>`
      + `<div class="forge-sticker-grid" data-sticker-grid>${gridHtml}</div>`
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

  function syncCatalogProgressUi() {
    if (!pickerEl) return;
    const panel = pickerPanel();
    if (!panel) return;
    const progress = panel.querySelector('[data-sticker-progress]');
    if (!progress) return;
    const loading = isCatalogLoading();
    progress.hidden = !loading;
    if (!loading) return;
    const text = progress.querySelector('[data-sticker-progress-text]');
    if (text) text.textContent = catalogProgressText();
    const bar = progress.querySelector('.forge-sticker-progress-bar');
    const fill = progress.querySelector('[data-sticker-progress-fill]');
    if (bar) {
      bar.classList.toggle('is-indeterminate', catalogProgress.total === 0);
      bar.setAttribute('aria-valuenow', String(catalogProgress.percent));
    }
    if (fill) fill.style.width = (catalogProgress.total === 0 ? 40 : clamp(catalogProgress.percent, 0, 100)) + '%';
    const grid = panel.querySelector('[data-sticker-grid]');
    if (grid) {
      const loadingNode = grid.querySelector('.forge-sticker-loading');
      if (loadingNode) loadingNode.querySelector('span')?.replaceChildren(document.createTextNode(catalogProgressText()));
    }
  }

  function renderPicker() {
    if (!pickerEl) return;
    const panel = pickerPanel();
    const { tabs, sectionHtml, gridHtml, preview } = pickerParts();
    if (!panel) {
      pickerEl.innerHTML = pickerHtml();
      positionPicker();
      syncPickerHover();
