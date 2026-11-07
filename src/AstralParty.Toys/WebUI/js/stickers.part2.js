      syncCatalogProgressUi();
      return;
    }
    // 增量更新：绝不重建搜索框与面板本体。整块 innerHTML 会丢焦点/选区，
    // 使每刻一键光标跳动且面板入场动画重放，就是之前打字跳的原因。
    const nav = panel.querySelector('.forge-sticker-nav');
    if (nav) nav.innerHTML = tabs;
    const section = panel.querySelector('.forge-sticker-section');
    if (section) section.innerHTML = sectionHtml;
    const grid = panel.querySelector('[data-sticker-grid]');
    if (grid) {
      grid.innerHTML = gridHtml;
      grid.scrollTop = 0;
    }
    const previewEl = panel.querySelector('.forge-sticker-preview');
    if (previewEl) previewEl.innerHTML = preview;
    // 进度条不走整块重画：这里只同步显隐与数字，避免每张图都抖动网格。
    // 清除按钮随查询有无增减，不碰搜索框本体。
    const input = panel.querySelector('[data-sticker-search]');
    let clearBtn = panel.querySelector('.forge-sticker-searchrow [data-sticker-clear]');
    if (pickerState.query) {
      if (!clearBtn) {
        clearBtn = document.createElement('button');
        clearBtn.type = 'button';
        clearBtn.className = 'forge-sticker-x';
        clearBtn.setAttribute('data-sticker-clear', '');
        clearBtn.setAttribute('aria-label', t('workshop.sticker_clear_search'));
        clearBtn.innerHTML = '<span aria-hidden="true">✕</span>';
        input?.after(clearBtn);
      }
    } else {
      clearBtn?.remove();
    }
    // 错误条增减。
    let errorEl = panel.querySelector('.forge-sticker-error');
    if (pickerState.error) {
      if (!errorEl) {
        errorEl = document.createElement('p');
        errorEl.className = 'forge-sticker-error';
        errorEl.setAttribute('role', 'alert');
        panel.appendChild(errorEl);
      }
      errorEl.textContent = pickerState.error;
    } else {
      errorEl?.remove();
    }
    syncCatalogProgressUi();
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

    // 聚焦预览（键盘导航时的大预览）。鼠标悬停唤起大预览已按用户要求关掉。
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
    // 宿主回推的贴纸目录：写入本地缓存并唤醒等待打开选择器的调用。
    applyCatalog(payload) {
      if (!payload) return;
      catalogProgress = { active: false, downloaded: 0, total: 0, percent: 0 };
      if (payload.error) {
        catalogItems = [];
        catalogGroups = [];
        catalogById = new Map();
        catalogPromise = null;
        catalogReady = false;
        toast(payload.error);
        resolveCatalogWaiters();
        if (pickerEl) renderPicker();
        return;
      }
      catalogItems = Array.isArray(payload.items) ? payload.items : [];
      catalogGroups = Array.isArray(payload.groups) ? payload.groups : [];
      catalogById = new Map(catalogItems.map(s => [s.stickerId, s]));
      catalogReady = true;
      resolveCatalogWaiters();
      renderAllStrips();
      if (pickerEl) renderPicker();
    },
    // 宿主逐张回推的缓存进度：只刷新进度条与数字，不重画整个选择器。
    applyCatalogProgress(payload) {
      if (!payload) return;
      catalogProgress = {
        active: true,
        downloaded: Math.max(0, Number(payload.downloaded) || 0),
        total: Math.max(0, Number(payload.total) || 0),
        percent: Math.min(100, Math.max(0, Number(payload.percent) || 0)),
      };
      syncCatalogProgressUi();
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
