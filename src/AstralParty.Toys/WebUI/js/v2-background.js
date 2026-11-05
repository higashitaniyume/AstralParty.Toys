// v2-background.js — 自定义 → 背景：程序窗口的背景图、模糊度、界面不透明度。
// 只改变这个工具的外观，不影响游戏。图片由宿主存在设置文件夹的 backgrounds（HybridWindow.Background.cs）；
// 选了哪张、滑块的值只影响这台电脑的外观，存在 localStorage，丢了也只是回到默认外观。
(function () {
  const HOST = 'https://backgrounds.astral.local/';
  const KEY = 'v2Background';
  const DEFAULTS = { blur: 50, ui: 80 };
  const BLUR_PX_PER_UNIT = 0.24;   // 滑块 0～100 → 0～24px，默认 50 是 12px
  const UI_MIN = 40;               // 再透明字就看不清了
  const FRAME_TIMEOUT_MS = 30000;

  const S = { file: null, blur: DEFAULTS.blur, ui: DEFAULTS.ui, items: null, queue: [], extracting: false };
  const U = () => window.V2UI;
  const I = () => window.V2UI.ICON;
  const urlOf = file => HOST + encodeURIComponent(file);

  function load() {
    try {
      const saved = JSON.parse(localStorage.getItem(KEY) || 'null');
      if (!saved) return;
      S.file = typeof saved.file === 'string' ? saved.file : null;
      if (Number.isFinite(saved.blur)) S.blur = Math.min(100, Math.max(0, saved.blur));
      if (Number.isFinite(saved.ui)) S.ui = Math.min(100, Math.max(UI_MIN, saved.ui));
    } catch { /* 当作没设置过 */ }
  }
  function save() {
    try { localStorage.setItem(KEY, JSON.stringify({ file: S.file, blur: S.blur, ui: S.ui })); } catch { /* 存不了只影响下次启动 */ }
  }

  function applyVars() {
    const style = document.documentElement.style;
    style.setProperty('--v2-bg-blur', S.blur * BLUR_PX_PER_UNIT + 'px');
    style.setProperty('--v2-ui', String(S.ui / 100));
  }
  function applyImage() {
    const layer = $('v2BgLayer'), file = S.file;
    if (!layer) return;
    if (!file) { document.body.classList.remove('v2-has-bg'); layer.style.backgroundImage = ''; return; }
    // 先加载好再显示，文件没了就保持默认外观
    const img = new Image();
    img.onload = () => {
      if (S.file !== file) return;
      layer.style.backgroundImage = `url("${urlOf(file)}")`;
      document.body.classList.add('v2-has-bg');
    };
    img.onerror = () => { if (S.file === file) document.body.classList.remove('v2-has-bg'); };
    img.src = urlOf(file);
  }

  /* ---------- WebM：取中间一帧 ---------- */
  function extractMiddleFrame(url) {
    return new Promise((resolve, reject) => {
      const video = document.createElement('video');
      let seekingMiddle = false, settled = false;
      const finish = (fn, value) => {
        if (settled) return;
        settled = true; clearTimeout(timer);
        video.removeAttribute('src'); video.load();
        fn(value);
      };
      const timer = setTimeout(() => finish(reject, new Error('解码超时')), FRAME_TIMEOUT_MS);
      const grab = () => {
        try {
          const canvas = document.createElement('canvas');
          canvas.width = video.videoWidth; canvas.height = video.videoHeight;
          canvas.getContext('2d').drawImage(video, 0, 0);
          finish(resolve, canvas.toDataURL('image/png'));
        } catch (e) { finish(reject, e); }
      };
      const seekToMiddle = () => { seekingMiddle = true; video.currentTime = video.duration / 2; };
      video.crossOrigin = 'anonymous';
      video.muted = true;
      video.preload = 'auto';
      video.onerror = () => finish(reject, new Error('无法解码这个 WebM'));
      video.onloadedmetadata = () => {
        if (!video.videoWidth) return finish(reject, new Error('这个文件里没有画面'));
        // 有些 WebM 的文件头里没写时长：先跳到很后面，浏览器才知道一共多长
        if (Number.isFinite(video.duration) && video.duration > 0) seekToMiddle();
        else video.currentTime = 1e7;
      };
      video.onseeked = () => {
        if (seekingMiddle) return grab();
        if (Number.isFinite(video.duration) && video.duration > 0) seekToMiddle();
        else grab();
      };
      video.src = url;
    });
  }
  async function runQueue() {
    if (S.extracting) return;
    S.extracting = true; render();
    while (S.queue.length) {
      const job = S.queue.shift();
      try {
        const dataUrl = await extractMiddleFrame(HOST + '.pending/' + encodeURIComponent(job.file));
        post({ type: 'backgroundSaveFrame', file: job.file, dataUrl });
      } catch (e) {
        post({ type: 'backgroundFrameFailed', file: job.file });
        toast(`「${job.name}」提取失败：${e.message || '未知原因'}`);
      }
    }
    S.extracting = false; render();
  }

  /* ---------- 页面 ---------- */
  const slider = (key, label, min, value) => `<div class="v2-row v2-bg-slider"><span class="k">${label}</span>
    <input type="range" min="${min}" max="100" step="1" value="${value}" data-v2b-range="${key}" aria-label="${label}" ${S.file ? '' : 'disabled'}>
    <span class="v2-bg-val" id="v2bVal-${key}">${value}</span></div>`;

  function html() {
    if (S.items === null) { S.items = []; post({ type: 'backgroundList' }); }
    const tile = it => `<div class="v2-scheme v2-bg-tile ${S.file === it.file ? 'active' : ''}" data-v2b-use="${esc(it.file)}" role="button" tabindex="0">
        <div class="thumb"><img src="${esc(urlOf(it.file))}" alt="" loading="lazy">${S.file === it.file ? '<span class="v2-badge pink">使用中</span>' : ''}
          <button class="v2-icon-btn v2-bg-del" data-v2b-del="${esc(it.file)}" title="删除" aria-label="删除">${I().trash}</button></div>
        <div class="info"><b title="${esc(it.name)}">${esc(it.name)}</b></div></div>`;
    return `${window.V2Skins.frameHead(`<button class="v2-btn primary" data-v2b="import">${I().upload}导入图片</button>`)}
      <div class="v2-card">
        <div class="v2-card-title"><span>效果</span><button class="v2-btn ghost" data-v2b="reset" ${S.blur === DEFAULTS.blur && S.ui === DEFAULTS.ui ? 'disabled' : ''}>重置</button></div>
        ${slider('blur', '模糊度', 0, S.blur)}
        ${slider('ui', '界面不透明度', UI_MIN, S.ui)}
        <p class="v2-muted" style="margin:0;padding:10px 18px 12px">只改变这个工具的外观，不影响游戏。支持 PNG、JPG、WebP、BMP；WebM 动图会截取中间一帧存成图片。</p>
      </div>
      ${S.extracting ? '<p class="v2-muted">正在截取 WebM 的画面…</p>' : ''}
      <div class="v2-grid">
        <div class="v2-scheme v2-bg-tile ${S.file ? '' : 'active'}" data-v2b-use="" role="button" tabindex="0">
          <div class="thumb v2-bg-none">${S.file ? '' : '<span class="v2-badge pink">使用中</span>'}无背景</div>
          <div class="info"><b>默认外观</b></div></div>
        ${(S.items || []).map(tile).join('')}
      </div>`;
  }
  function render() { if (window.CharacterSkinsModule?.tab === 'background') window.V2Skins?.render(); }

  function use(file) {
    S.file = file || null;
    save(); applyImage(); render();
  }

  function remove(file) {
    const active = S.file === file;
    U().confirmDelete({
      title: `删除背景「${esc(file)}」？`,
      lines: ['只删除程序保存的这份副本，你的<b>原文件不受影响</b>', '删除后不能从程序里找回，要用可以重新导入',
        active && '这是<b>正在使用</b>的背景，删除后恢复默认外观'],
      onOk: () => {
        if (active) { S.file = null; save(); applyImage(); }
        post({ type: 'backgroundDelete', file });
      },
    });
  }

  document.addEventListener('click', e => {
    const t = e.target, del = t.closest?.('[data-v2b-del]');
    if (del) { e.stopPropagation(); remove(del.dataset.v2bDel); return; }
    const act = t.closest?.('[data-v2b]');
    if (act && !act.disabled) {
      if (act.dataset.v2b === 'import') post({ type: 'backgroundImport' });
      if (act.dataset.v2b === 'reset') { Object.assign(S, DEFAULTS); save(); applyVars(); render(); }
      return;
    }
    const tile = t.closest?.('[data-v2b-use]');
    if (tile) use(tile.dataset.v2bUse);
  });
  document.addEventListener('keydown', e => {
    if (e.key !== 'Enter' && e.key !== ' ') return;
    if (!e.target.matches?.('[data-v2b-use]')) return;
    e.preventDefault(); use(e.target.dataset.v2bUse);
  });
  // 拖动时只改变量、不重画页面，否则滑块会被换掉、拖不动
  document.addEventListener('input', e => {
    const key = e.target.dataset?.v2bRange;
    if (!key) return;
    S[key] = Number(e.target.value);
    applyVars();
    const val = $('v2bVal-' + key); if (val) val.textContent = e.target.value;
  });
  document.addEventListener('change', e => {
    if (!e.target.dataset?.v2bRange) return;
    save();
    const reset = document.querySelector('[data-v2b="reset"]');
    if (reset) reset.disabled = S.blur === DEFAULTS.blur && S.ui === DEFAULTS.ui;
  });

  function onHostMessage(msg) {
    switch (msg?.type) {
      case 'backgroundList':
        S.items = msg.payload?.items || [];
        render();
        break;
      case 'backgroundFrameNeeded':
        S.queue.push(msg.payload);
        runQueue();
        break;
      case 'backgroundFrameSaved':
        toast('已截取中间一帧，点图片就能用作背景');
        break;
    }
  }
  window.chrome?.webview?.addEventListener('message', event => onHostMessage(event.data));

  window.V2Bg = { html, onHostMessage };

  document.addEventListener('DOMContentLoaded', () => {
    load();
    const layer = document.createElement('div');
    layer.id = 'v2BgLayer';
    document.body.prepend(layer);
    applyVars();
    applyImage();
  });
})();
