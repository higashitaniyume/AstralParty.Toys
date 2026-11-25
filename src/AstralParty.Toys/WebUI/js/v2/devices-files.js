// v2/devices-files.js — Device container file explorer with virtual scrolling & inline Explorer rename
(function () {
  const S = () => window._V2Devices;
  const ROW_H = 38;
  const VIEW_H = 400;
  const OVERSCAN = 8;

  let sortCache = { raw: null, key: 'name', dir: 1, sorted: [] };
  let scrollRaf = 0;
  let lastSliceKey = '';

  function fmtBytes(bytes) {
    const n = Number(bytes) || 0;
    if (n < 1024) return `${n} B`;
    if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KB`;
    if (n < 1024 * 1024 * 1024) return `${(n / 1048576).toFixed(2)} MB`;
    return `${(n / 1073741824).toFixed(2)} GB`;
  }

  function getSortedEntries() {
    const { state } = S();
    const raw = Array.isArray(state.filesEntries) ? state.filesEntries : null;
    if (!raw) return null;
    const key = state.filesSortKey;
    const dir = state.filesSortDir;
    if (sortCache.raw === raw && sortCache.key === key && sortCache.dir === dir) {
      return sortCache.sorted;
    }
    const sorted = [...raw].sort((a, b) => {
      if (!!a.isDir !== !!b.isDir) return a.isDir ? -1 : 1;
      if (key === 'size') {
        const diff = (a.size || 0) - (b.size || 0);
        if (diff !== 0) return diff * dir;
      } else if (key === 'mtime') {
        const cmp = String(a.modified || '').localeCompare(String(b.modified || ''));
        if (cmp !== 0) return cmp * dir;
      }
      return String(a.name || '').localeCompare(String(b.name || ''), undefined, { sensitivity: 'base' }) * dir;
    });
    sortCache = { raw, key, dir, sorted };
    return sorted;
  }

  function navigateTo(dev, path, pushHistory = true) {
    const { state, post, tr } = S();
    if (!dev || !dev.online || state.busy) return;
    const clean = String(path || '').replace(/^\/+|\/+$/g, '');
    if (pushHistory && clean !== state.filesPath) {
      const nextHist = state.filesHistory.slice(0, state.filesHistoryIndex + 1);
      nextHist.push(clean);
      state.filesHistory = nextHist;
      state.filesHistoryIndex = nextHist.length - 1;
    }
    state.filesPath = clean;
    state.filesSelectedPath = '';
    state.filesRenamingPath = '';
    state.filesScrollTop = 0;
    S().startBusy(tr('devices.busyDetect'));
    post({
      type: 'deviceFilesList',
      targetId: dev.id,
      path: clean,
      showHidden: !!state.filesShowHidden,
    });
  }

  function renderRowHtml(item) {
    const { state, icon } = S();
    const isSelected = state.filesSelectedPath === item.path;
    const isRenaming = state.filesRenamingPath === item.path;
    const nameNode = isRenaming
      ? `<input type="text" class="dv-file-rename-input" data-dev-file-rename-input
           data-orig-name="${esc(item.name)}" data-orig-path="${esc(item.path)}" data-is-dir="${item.isDir ? '1' : '0'}"
           value="${esc(item.name)}" spellcheck="false" autocomplete="off">`
      : `<span>${esc(item.name)}</span>`;

    return `
      <div class="dv-file-row${isSelected ? ' is-selected' : ''}" role="row" tabindex="-1"
        data-dev-file-row="${esc(item.path)}" data-dev-file-dir="${item.isDir ? '1' : '0'}">
        <span class="dv-file-name">
          ${icon(item.isDir ? 'folder' : 'description')}
          ${nameNode}
        </span>
        <span class="dv-file-col-size">${item.isDir ? '—' : esc(fmtBytes(item.size))}</span>
        <span class="dv-file-col-time">${esc(item.modified || '—')}</span>
      </div>`;
  }

  function computeWindow(total, scrollTop, clientHeight) {
    const h = clientHeight > 0 ? clientHeight : VIEW_H;
    const start = Math.max(0, Math.floor(scrollTop / ROW_H) - OVERSCAN);
    const visibleCount = Math.ceil(h / ROW_H) + OVERSCAN * 2;
    const end = Math.min(total, start + visibleCount);
    return { start, end, topPx: start * ROW_H, bottomPx: Math.max(0, (total - end) * ROW_H) };
  }

  function renderWindowedRowsHtml(entries, scrollTop, clientHeight) {
    const { start, end, topPx, bottomPx } = computeWindow(entries.length, scrollTop, clientHeight);
    lastSliceKey = `${start}:${end}:${S().state.filesRenamingPath}:${S().state.filesSelectedPath}`;
    const rows = entries.slice(start, end).map(renderRowHtml).join('');
    return `
      <div class="dv-file-spacer" style="height:${topPx}px" aria-hidden="true"></div>
      ${rows}
      <div class="dv-file-spacer" style="height:${bottomPx}px" aria-hidden="true"></div>`;
  }

  function openPreviewModal(payload) {
    const { U, tr, icon, post } = S();
    const ui = U();
    if (!ui?.openModal || !payload) return;

    let body = '';
    if (payload.error) {
      body = `<div class="dv-alert bad">${icon('error')}<div class="dv-alert-content"><span>${esc(payload.error)}</span></div></div>`;
    } else if (payload.binary) {
      body = `<div class="dv-alert">${icon('info')}<div class="dv-alert-content"><span>${esc(tr('devices.filesBinaryHint'))}</span></div></div>`;
    } else if (payload.truncated) {
      body = `<div class="dv-alert warn">${icon('warning')}<div class="dv-alert-content"><span>${esc(tr('devices.filesTruncatedHint'))}</span><span class="dv-alert-detail">${fmtBytes(payload.size)}</span></div></div>`;
    } else {
      body = `<pre class="dv-file-pre">${esc(payload.text ?? '')}</pre>`;
    }

    ui.modalActions.devPreviewOpenExt = () => {
      ui.closeModal();
      S().startBusy(tr('devices.busyOpen'));
      post({ type: 'deviceFileOpen', targetId: payload.targetId, path: payload.path });
    };

    ui.modalActions.devPreviewDownload = () => {
      ui.closeModal();
      S().startBusy(tr('devices.busyDetect'));
      post({ type: 'deviceFileDownload', targetId: payload.targetId, path: payload.path, isDir: false });
    };

    ui.openModal(`
      <div class="v2-modal-head">
        <h2>${esc(payload.name || payload.path)}</h2>
        <span class="dv-pill">${fmtBytes(payload.size)}</span>
        <button type="button" class="v2-icon-btn" data-v2modal="close" aria-label="${esc(tr('common.cancel'))}">${ui.ICON.x}</button>
      </div>
      ${body}
      <div class="v2-modal-foot">
        <span class="dv-payload-path" title="${esc(payload.path || '')}">${esc(payload.path || '')}</span>
        <span class="v2-top-spacer"></span>
        <button type="button" class="v2-btn line" data-v2modal="devPreviewDownload">${icon('cloud_download')}<span>${esc(tr('devices.filesDownload'))}</span></button>
        <button type="button" class="v2-btn line" data-v2modal="devPreviewOpenExt">${icon('open_in_new')}<span>${esc(tr('devices.filesOpen'))}</span></button>
        <button type="button" class="v2-btn ghost" data-v2modal="close">${esc(tr('common.cancel'))}</button>
      </div>`);
  }

  function renderPanel(dev) {
    const { state, tr, icon } = S();
    const offline = !dev.online;
    const loadedForDev = state.filesTargetId === dev.id;
    const entries = loadedForDev ? getSortedEntries() : null;
    const selected = entries?.find(e => e.path === state.filesSelectedPath) || null;
    const curPath = loadedForDev ? state.filesPath : '';
    const segments = curPath ? curPath.split('/') : [];
    const canBack = state.filesHistoryIndex > 0;
    const canForward = state.filesHistoryIndex < state.filesHistory.length - 1;
    const parentPath = segments.length > 0 ? segments.slice(0, -1).join('/') : '';

    if (entries && state.filesRenamingPath) {
      const idx = entries.findIndex(e => e.path === state.filesRenamingPath);
      if (idx >= 0) {
        const rowTop = idx * ROW_H;
        const rowBottom = rowTop + ROW_H;
        if (rowTop < state.filesScrollTop) state.filesScrollTop = rowTop;
        else if (rowBottom > state.filesScrollTop + VIEW_H) state.filesScrollTop = rowBottom - VIEW_H;
      }
    }

    const crumbs = [`
      <button type="button" class="dv-file-crumb${curPath === '' ? ' is-cur' : ''}" data-dev-file-nav="">
        ${esc(state.filesRootLabel || tr('devices.filesRoot'))}
      </button>`];
    segments.forEach((seg, idx) => {
      const targetPath = segments.slice(0, idx + 1).join('/');
      const isCur = idx === segments.length - 1;
      crumbs.push(`<span class="dv-file-sep" aria-hidden="true">/</span>
        <button type="button" class="dv-file-crumb${isCur ? ' is-cur' : ''}" data-dev-file-nav="${esc(targetPath)}">${esc(seg)}</button>`);
    });

    const quickPaths = [
      ['', tr('devices.filesRoot')],
      ['CesiumLoader', 'CesiumLoader'],
      ['CesiumLoader/mods', 'mods'],
      ['CesiumLoader/skins', 'skins'],
      ['CesiumLoader/logs', 'logs'],
    ];

    const arrow = key => state.filesSortKey === key ? (state.filesSortDir === 1 ? ' ↑' : ' ↓') : '';
    let listHtml = `<p class="dv-muted">${esc(tr('devices.filesHint'))}</p>`;
    if (loadedForDev && state.filesError) {
      listHtml = `<div class="dv-alert bad">${icon('error')}<div class="dv-alert-content"><span>${esc(state.filesError)}</span></div></div>`;
    } else if (entries) {
      if (!entries.length) {
        listHtml = `<p class="dv-muted">${esc(tr('devices.filesEmpty'))}</p>`;
      } else {
        listHtml = `
          <div class="dv-file-table">
            <div class="dv-file-thead">
              <button type="button" class="dv-file-th" data-dev-file-sort="name">${esc(tr('devices.filesColName'))}${arrow('name')}</button>
              <button type="button" class="dv-file-th dv-file-col-size" data-dev-file-sort="size">${esc(tr('devices.filesColSize'))}${arrow('size')}</button>
              <button type="button" class="dv-file-th dv-file-col-time" data-dev-file-sort="mtime">${esc(tr('devices.filesColModified'))}${arrow('mtime')}</button>
            </div>
            <div class="dv-file-tbody" id="dvFileTbody" role="grid" tabindex="0">
              ${renderWindowedRowsHtml(entries, state.filesScrollTop, VIEW_H)}
            </div>
          </div>`;
      }
    }

    return `
      <div class="v2-card dv-card">
        <div class="dv-file-nav">
          <div class="dv-file-nav-btns">
            <button type="button" class="v2-icon-btn" data-dev-file-back ${!canBack || state.busy || offline ? 'disabled' : ''} title="${esc(tr('devices.filesBack'))}">${icon('arrow_back')}</button>
            <button type="button" class="v2-icon-btn" data-dev-file-fwd ${!canForward || state.busy || offline ? 'disabled' : ''} title="${esc(tr('devices.filesForward'))}">${icon('arrow_forward')}</button>
            <button type="button" class="v2-icon-btn" data-dev-file-nav="${esc(parentPath)}" ${!curPath || state.busy || offline ? 'disabled' : ''} title="${esc(tr('devices.filesUp'))}">${icon('arrow_upward')}</button>
          </div>
          <nav class="dv-file-crumbs" aria-label="${esc(tr('devices.tabFiles'))}">${crumbs.join('')}</nav>
          ${entries ? `<span class="dv-pill dv-file-count">${entries.length} ${esc(tr('devices.filesItemsCount'))}</span>` : ''}
          <label class="dv-file-hidden-toggle">
            <input type="checkbox" class="dv-checkbox" data-dev-file-hidden ${state.filesShowHidden ? 'checked' : ''} ${state.busy || offline ? 'disabled' : ''}>
            <span>${esc(tr('devices.filesShowHidden'))}</span>
          </label>
        </div>

        <div class="dv-file-shortcuts">
          ${quickPaths.map(([p, label]) => `
            <button type="button" class="dv-file-chip${curPath === p ? ' is-cur' : ''}"
              data-dev-file-nav="${esc(p)}" ${state.busy || offline ? 'disabled' : ''}>${esc(label)}</button>`).join('')}
        </div>

        <div class="dv-file-actions">
          <button type="button" class="v2-btn line" data-dev-file-refresh ${state.busy || offline ? 'disabled' : ''}>${icon('refresh')}<span>${esc(tr('devices.refresh'))}</span></button>
          <button type="button" class="v2-btn line" data-dev-file-mkdir ${state.busy || offline ? 'disabled' : ''}>${icon('folder_plus')}<span>${esc(tr('devices.filesMkdir'))}</span></button>
          <button type="button" class="v2-btn line" data-dev-file-upload ${state.busy || offline ? 'disabled' : ''}>${icon('upload')}<span>${esc(tr('devices.filesUpload'))}</span></button>
          <span class="dv-actions-sep" aria-hidden="true"></span>
          <button type="button" class="v2-btn ghost" data-dev-file-preview ${!selected || selected.isDir || state.busy || offline ? 'disabled' : ''}>${icon('description')}<span>${esc(tr('devices.filesPreview'))}</span></button>
          <button type="button" class="v2-btn ghost" data-dev-file-open ${!selected || selected.isDir || state.busy || offline ? 'disabled' : ''}>${icon('open_in_new')}<span>${esc(tr('devices.filesOpen'))}</span></button>
          <button type="button" class="v2-btn ghost" data-dev-file-download ${!selected || state.busy || offline ? 'disabled' : ''}>${icon('cloud_download')}<span>${esc(tr('devices.filesDownload'))}</span></button>
          <button type="button" class="v2-btn ghost" data-dev-file-rename ${!selected || state.busy || offline ? 'disabled' : ''}>${icon('edit')}<span>${esc(tr('devices.filesRename'))}</span></button>
          <button type="button" class="v2-btn danger dv-danger-btn" data-dev-file-del ${!selected || state.busy || offline ? 'disabled' : ''}>${icon('delete')}<span>${esc(tr('devices.filesDelete'))}</span></button>
        </div>

        ${listHtml}
        <div class="dv-file-tip">${esc(tr('devices.filesTip'))}</div>
      </div>`;
  }

  function syncToolbarDisabled(dev) {
    const { state } = S();
    const view = S().root();
    if (!view || !dev) return;
    const offline = !dev.online;
    const entries = state.filesTargetId === dev.id ? getSortedEntries() : null;
    const sel = entries?.find(e => e.path === state.filesSelectedPath) || null;
    const setDis = (attr, dis) => {
      const btn = view.querySelector(`[${attr}]`);
      if (btn) btn.disabled = !!dis;
    };
    setDis('data-dev-file-preview', !sel || sel.isDir || state.busy || offline);
    setDis('data-dev-file-open', !sel || sel.isDir || state.busy || offline);
    setDis('data-dev-file-download', !sel || state.busy || offline);
    setDis('data-dev-file-rename', !sel || state.busy || offline);
    setDis('data-dev-file-del', !sel || state.busy || offline);
  }

  function selectRowInPlace(path, dev) {
    const { state } = S();
    state.filesSelectedPath = path;
    const tbody = document.getElementById('dvFileTbody');
    if (tbody) {
      tbody.querySelectorAll('.dv-file-row').forEach(r => {
        r.classList.toggle('is-selected', r.dataset.devFileRow === path);
      });
    }
    syncToolbarDisabled(dev);
  }

  function beginInlineRename(sel, render) {
    const { state } = S();
    if (!sel) return;
    state.filesRenamingPath = sel.path;
    render();
  }

  function commitInlineRename(input, dev, render) {
    if (!input || input.dataset.done === '1') return;
    input.dataset.done = '1';
    const { state, post, tr } = S();
    const origName = input.dataset.origName || '';
    const origPath = input.dataset.origPath || '';
    const nextName = input.value.trim();
    state.filesRenamingPath = '';
    if (!nextName || nextName === origName || !dev || !dev.online || state.busy) {
      render();
      return;
    }
    S().startBusy(tr('devices.busyWrite'));
    post({
      type: 'deviceFileRename',
      targetId: dev.id,
      path: origPath,
      newName: nextName,
      basePath: state.filesPath,
      showHidden: !!state.filesShowHidden,
    });
  }

  function cancelInlineRename(render) {
    const { state } = S();
    if (!state.filesRenamingPath) return;
    state.filesRenamingPath = '';
    render();
  }

  function afterRender(dev, render) {
    const { state } = S();
    const tbody = document.getElementById('dvFileTbody');
    if (!tbody) return;

    tbody.scrollTop = state.filesScrollTop || 0;
    tbody.addEventListener('scroll', () => {
      state.filesScrollTop = tbody.scrollTop;
      if (scrollRaf) return;
      scrollRaf = requestAnimationFrame(() => {
        scrollRaf = 0;
        const entries = getSortedEntries();
        if (!entries || !document.body.contains(tbody)) return;
        const { start, end } = computeWindow(entries.length, tbody.scrollTop, tbody.clientHeight);
        const nextKey = `${start}:${end}:${state.filesRenamingPath}:${state.filesSelectedPath}`;
        if (nextKey === lastSliceKey) return;
        tbody.innerHTML = renderWindowedRowsHtml(entries, tbody.scrollTop, tbody.clientHeight);
        if (state.filesRenamingPath) focusRenameInput(dev, render);
      });
    }, { passive: true });

    if (state.filesRenamingPath) {
      focusRenameInput(dev, render);
    }
  }

  function focusRenameInput(dev, render) {
    const input = document.querySelector('[data-dev-file-rename-input]');
    if (!input || input.dataset.wired === '1') return;
    input.dataset.wired = '1';
    const name = input.value || '';
    const isDir = input.dataset.isDir === '1';
    const dot = !isDir ? name.lastIndexOf('.') : -1;
    input.focus();
    input.setSelectionRange(0, dot > 0 ? dot : name.length);

    input.addEventListener('blur', () => commitInlineRename(input, dev, render));
    input.addEventListener('keydown', e => {
      e.stopPropagation();
      if (e.key === 'Enter') {
        e.preventDefault();
        commitInlineRename(input, dev, render);
      } else if (e.key === 'Escape') {
        e.preventDefault();
        input.dataset.done = '1';
        cancelInlineRename(render);
      } else if (e.key === 'F2') {
        e.preventDefault();
        if (input.selectionStart === 0 && input.selectionEnd < name.length) {
          input.setSelectionRange(0, name.length);
        } else {
          input.setSelectionRange(0, dot > 0 ? dot : name.length);
        }
      }
    });
  }

  function handleClick(target, dev, render) {
    const { state, post, tr, confirmAction, openPromptModal } = S();
    if (!dev) return false;
    if (target.closest?.('[data-dev-file-rename-input]')) return true;

    const row = target.closest?.('[data-dev-file-row]');
    if (row) {
      const path = row.dataset.devFileRow;
      if (state.filesRenamingPath && state.filesRenamingPath !== path) {
        state.filesRenamingPath = '';
        state.filesSelectedPath = path;
        render();
        return true;
      }
      selectRowInPlace(path, dev);
      return true;
    }

    const el = target.closest?.('button');
    if (!el || el.disabled) return false;
    const offline = !dev.online;

    if (el.hasAttribute('data-dev-file-nav')) {
      navigateTo(dev, el.dataset.devFileNav ?? '', true);
      return true;
    }
    if (el.hasAttribute('data-dev-file-back') && state.filesHistoryIndex > 0 && !offline && !state.busy) {
      state.filesHistoryIndex--;
      navigateTo(dev, state.filesHistory[state.filesHistoryIndex], false);
      return true;
    }
    if (el.hasAttribute('data-dev-file-fwd') && state.filesHistoryIndex < state.filesHistory.length - 1 && !offline && !state.busy) {
      state.filesHistoryIndex++;
      navigateTo(dev, state.filesHistory[state.filesHistoryIndex], false);
      return true;
    }
    if (el.hasAttribute('data-dev-file-refresh') && !offline && !state.busy) {
      navigateTo(dev, state.filesPath, false);
      return true;
    }
    if (el.dataset.devFileSort) {
      const key = el.dataset.devFileSort;
      if (state.filesSortKey === key) state.filesSortDir *= -1;
      else {
        state.filesSortKey = key;
        state.filesSortDir = 1;
      }
      state.filesScrollTop = 0;
      render();
      return true;
    }
    if (el.hasAttribute('data-dev-file-mkdir') && !offline && !state.busy) {
      openPromptModal({
        title: tr('devices.filesMkdir'),
        label: tr('devices.filesMkdirPrompt'),
        onSubmit: name => {
          S().startBusy(tr('devices.busyWrite'));
          post({
            type: 'deviceFileMkdir',
            targetId: dev.id,
            basePath: state.filesPath,
            name,
            showHidden: !!state.filesShowHidden,
          });
        },
      });
      return true;
    }
    if (el.hasAttribute('data-dev-file-upload') && !offline && !state.busy) {
      S().startBusy(tr('devices.busyWrite'));
      post({
        type: 'deviceFileUpload',
        targetId: dev.id,
        basePath: state.filesPath,
        showHidden: !!state.filesShowHidden,
      });
      return true;
    }

    const sel = state.filesEntries?.find(e => e.path === state.filesSelectedPath);
    if (!sel || offline || state.busy) return false;

    if (el.hasAttribute('data-dev-file-preview') && !sel.isDir) {
      S().startBusy(tr('devices.busyDetect'));
      post({ type: 'deviceFilePreview', targetId: dev.id, path: sel.path });
      return true;
    }
    if (el.hasAttribute('data-dev-file-open') && !sel.isDir) {
      S().startBusy(tr('devices.busyOpen'));
      post({ type: 'deviceFileOpen', targetId: dev.id, path: sel.path });
      return true;
    }
    if (el.hasAttribute('data-dev-file-download')) {
      S().startBusy(tr('devices.busyDetect'));
      post({ type: 'deviceFileDownload', targetId: dev.id, path: sel.path, isDir: !!sel.isDir });
      return true;
    }
    if (el.hasAttribute('data-dev-file-rename')) {
      beginInlineRename(sel, render);
      return true;
    }
    if (el.hasAttribute('data-dev-file-del')) {
      confirmAction({
        title: tr('devices.filesDelete'),
        message: tr('devices.filesDeleteConfirm', { name: sel.name }),
        okLabel: tr('devices.filesDelete'),
        danger: true,
        onConfirm: () => {
          S().startBusy(tr('devices.busyWrite'));
          post({
            type: 'deviceFileDelete',
            targetId: dev.id,
            path: sel.path,
            basePath: state.filesPath,
            showHidden: !!state.filesShowHidden,
          });
        },
      });
      return true;
    }
    return false;
  }

  function handleDblClick(target, dev) {
    const { state, post, tr } = S();
    if (!dev || !dev.online || state.busy) return false;
    if (target.closest?.('[data-dev-file-rename-input]')) return true;
    const row = target.closest?.('[data-dev-file-row]');
    if (!row) return false;
    const path = row.dataset.devFileRow;
    if (row.dataset.devFileDir === '1') {
      navigateTo(dev, path, true);
    } else {
      S().startBusy(tr('devices.busyOpen'));
      post({ type: 'deviceFileOpen', targetId: dev.id, path });
    }
    return true;
  }

  function handleKeyDown(event, dev, render) {
    const { state, post, tr, confirmAction } = S();
    if (!dev || state.tab !== 'files' || state.busy) return false;
    if (event.target.closest?.('input, textarea, select')) return false;

    const entries = state.filesTargetId === dev.id ? getSortedEntries() : null;
    if (!entries) return false;
    const idx = entries.findIndex(e => e.path === state.filesSelectedPath);
    const sel = idx >= 0 ? entries[idx] : null;

    if (event.key === 'F2' && sel && dev.online) {
      event.preventDefault();
      beginInlineRename(sel, render);
      return true;
    }
    if (event.key === 'Delete' && sel && dev.online) {
      event.preventDefault();
      confirmAction({
        title: tr('devices.filesDelete'),
        message: tr('devices.filesDeleteConfirm', { name: sel.name }),
        okLabel: tr('devices.filesDelete'),
        danger: true,
        onConfirm: () => {
          S().startBusy(tr('devices.busyWrite'));
          post({
            type: 'deviceFileDelete',
            targetId: dev.id,
            path: sel.path,
            basePath: state.filesPath,
            showHidden: !!state.filesShowHidden,
          });
        },
      });
      return true;
    }
    if (event.key === 'Enter' && sel && dev.online) {
      event.preventDefault();
      if (sel.isDir) navigateTo(dev, sel.path, true);
      else {
        S().startBusy(tr('devices.busyOpen'));
        post({ type: 'deviceFileOpen', targetId: dev.id, path: sel.path });
      }
      return true;
    }
    if (event.key === 'Backspace' && state.filesPath && dev.online) {
      event.preventDefault();
      const segs = state.filesPath.split('/');
      navigateTo(dev, segs.slice(0, -1).join('/'), true);
      return true;
    }
    if ((event.key === 'ArrowDown' || event.key === 'ArrowUp') && entries.length) {
      event.preventDefault();
      const nextIdx = event.key === 'ArrowDown'
        ? Math.min(entries.length - 1, idx < 0 ? 0 : idx + 1)
        : Math.max(0, idx < 0 ? 0 : idx - 1);
      const nextEntry = entries[nextIdx];
      if (!nextEntry) return true;
      selectRowInPlace(nextEntry.path, dev);
      const tbody = document.getElementById('dvFileTbody');
      if (tbody) {
        const top = nextIdx * ROW_H;
        const bottom = top + ROW_H;
        if (top < tbody.scrollTop) tbody.scrollTop = top;
        else if (bottom > tbody.scrollTop + (tbody.clientHeight || VIEW_H)) tbody.scrollTop = bottom - (tbody.clientHeight || VIEW_H);
        state.filesScrollTop = tbody.scrollTop;
      }
      return true;
    }
    return false;
  }

  function handleChange(el, dev) {
    if (!el.matches?.('[data-dev-file-hidden]') || !dev) return false;
    S().state.filesShowHidden = !!el.checked;
    navigateTo(dev, S().state.filesPath, false);
    return true;
  }

  function onMessage(msg, render) {
    const { state } = S();
    switch (msg?.type) {
      case 'deviceFilesListed': {
        const p = msg.payload || {};
        state.filesTargetId = p.targetId || state.selectedId;
        state.filesRootLabel = p.rootLabel || '';
        state.filesPath = p.path ?? '';
        state.filesEntries = p.entries || [];
        state.filesError = p.error || '';
        state.filesRenamingPath = '';
        sortCache = { raw: null, key: 'name', dir: 1, sorted: [] };
        render();
        return true;
      }
      case 'deviceFilePreview': {
        openPreviewModal(msg.payload);
        return true;
      }
      case 'deviceFilesOpResult': {
        const p = msg.payload || {};
        S().showCornerToast(p.ok, p.message, p.detail || '');
        if (!p.ok) {
          state.opResult = p;
          render();
        }
        return true;
      }
      default:
        return false;
    }
  }

  window._V2DevicesFiles = {
    renderPanel, afterRender, handleClick, handleDblClick, handleKeyDown, handleChange, navigateTo, onMessage,
  };
})();
