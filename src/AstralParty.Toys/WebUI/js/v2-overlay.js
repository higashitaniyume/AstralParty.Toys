// v2-overlay.js — 新界面第二阶段：覆盖层显示（WPF Overlay 配置 + 对应 Mod）
// 配置由 HybridWindow 后端读写 overlay_config.json（CESIUM_OVERLAY_CFG）
(function () {
  const V2O = { config: null, mods: [], loading: false };

  function esc(s) { return String(s ?? '').replace(/[&<>"']/g, m => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[m])); }

  function render() {
    const root = $('v2Overlay');
    if (!root) return;
    const c = V2O.config || { enabled: true, scale: 1.0, fontSize: 14, opacity: 0.85, toggleKey: 'Backslash', toggleVk: 220, offsetX: 20, offsetY: 70, panelWidth: 340 };
    const mods = V2O.mods || [];
    root.innerHTML = `
    <div class="v2-page">
      <div class="v2-head"><h1>覆盖层显示</h1>${V2O.loading ? '<span class="v2-muted" style="margin-left:8px">读取中…</span>' : ''}</div>
      <div class="v2-card">
        <div class="v2-row"><span class="k">对应使用的 Mod</span><span class="v">${mods.map(m => esc(m.name) + (m.enabled ? '（已启用）' : '（已停用）')).join('、') || 'OverlayFeedMod / ShopRelicPeekMod'}</span></div>
        <div class="v2-row"><span class="k">覆盖层配置</span><span class="v mono" style="font-size:11px">${esc(V2O.configPath || 'overlay/overlay_config.json')}</span></div>
        <div class="v2-row" style="align-items:center">
          <span class="k">启用覆盖层</span>
          <label class="util-switch"><input type="checkbox" id="ovEnabled" ${c.enabled ? 'checked' : ''}/><span class="switch-track"></span></label>
          <span class="v2-muted" style="font-size:12px">关掉时游戏内渲染降级，Overlay 进程不启动</span>
        </div>
        <div class="v2-row" style="align-items:center">
          <span class="k">缩放</span>
          <input type="range" id="ovScale" min="0.5" max="2" step="0.05" value="${c.scale || 1}" style="flex:1"/>
          <span class="v mono" id="ovScaleVal">${(c.scale || 1).toFixed(2)}x</span>
        </div>
        <div class="v2-row" style="align-items:center">
          <span class="k">字号</span>
          <input type="range" id="ovFont" min="10" max="28" step="1" value="${c.fontSize || 14}" style="flex:1"/>
          <span class="v mono" id="ovFontVal">${c.fontSize || 14}</span>
        </div>
        <div class="v2-row" style="align-items:center">
          <span class="k">不透明度</span>
          <input type="range" id="ovOpacity" min="0.2" max="1" step="0.05" value="${c.opacity || 0.85}" style="flex:1"/>
          <span class="v mono" id="ovOpacityVal">${Math.round((c.opacity || 0.85) * 100)}%</span>
        </div>
        <div class="v2-row" style="align-items:center">
          <span class="k">穿透键</span>
          <button class="v2-btn line" id="ovToggleKeyBtn" style="min-width:140px">${esc(c.toggleKey || 'Backslash')}</button>
          <button class="v2-btn line" id="ovToggleDebug">调试捕获</button>
          <input type="text" id="ovToggleInput" value="${esc(c.toggleKey || 'Backslash')}" style="width:120px" class="v2-select"/>
          <span class="v2-muted" style="font-size:12px">默认反斜杠（\），在 Toys 里可改</span>
        </div>
        <div class="v2-row" style="align-items:center">
          <span class="k">位置 X/Y</span>
          <label>X <input type="number" id="ovOffsetX" value="${c.offsetX || 20}" min="0" max="1200" style="width:70px" class="v2-select"/></label>
          <label>Y <input type="number" id="ovOffsetY" value="${c.offsetY || 70}" min="0" max="1200" style="width:70px" class="v2-select"/></label>
          <span class="v2-muted" style="font-size:12px">也可在游戏中按反斜杠关掉穿透后直接拖动工具条</span>
        </div>
        <div class="v2-row">
          <span class="k">面板宽度</span>
          <input type="number" id="ovPanelWidth" value="${c.panelWidth || 340}" min="200" max="800" step="10" style="width:80px" class="v2-select"/>
        </div>
        <div class="v2-row" style="justify-content:flex-end">
          <span class="v2-top-spacer"></span>
          <button class="v2-btn primary" id="ovSave">保存</button>
        </div>
        <div class="v2-muted" style="font-size:12px;margin-top:8px">保存后游戏内按反斜杠切换穿透，拖动后自动回写同一份配置。对应 Mod 列表来自当前游戏的 mods 目录。</div>
      </div>
    </div>`;

    // 绑定
    const onSave = () => {
      const cfg = {
        enabled: !!$('ovEnabled')?.checked,
        scale: parseFloat($('ovScale')?.value || '1'),
        fontSize: parseInt($('ovFont')?.value || '14', 10),
        opacity: parseFloat($('ovOpacity')?.value || '0.85'),
        toggleKey: ($('ovToggleInput')?.value || 'Backslash').trim() || 'Backslash',
        offsetX: parseFloat($('ovOffsetX')?.value || '20'),
        offsetY: parseFloat($('ovOffsetY')?.value || '70'),
        panelWidth: parseFloat($('ovPanelWidth')?.value || '340')
      };
      post({ type: 'overlaySave', config: cfg });
    };
    $('ovSave')?.addEventListener('click', onSave);
    $('ovScale')?.addEventListener('input', e => { $('ovScaleVal').textContent = parseFloat(e.target.value).toFixed(2) + 'x'; });
    $('ovFont')?.addEventListener('input', e => { $('ovFontVal').textContent = e.target.value; });
    $('ovOpacity')?.addEventListener('input', e => { $('ovOpacityVal').textContent = Math.round(parseFloat(e.target.value) * 100) + '%'; });
    $('ovToggleKeyBtn')?.addEventListener('click', () => {
      $('ovToggleInput').focus();
      toast('在输入框里按你想要的键，保存时后端会转成 VK');
    });
    // 调试捕获：监听一次 keydown，把键盘事件转成 Key 名字
    $('ovToggleDebug')?.addEventListener('click', () => {
      toast('正在监听，按下任意键（再按一次取消）…');
      const once = (e) => {
        e.preventDefault();
        e.stopPropagation();
        const name = e.code === 'Backslash' ? 'Backslash' : e.code === 'Insert' ? 'Insert' : e.code === 'Delete' ? 'Delete' : e.code || 'Backslash';
        $('ovToggleInput').value = name;
        $('ovToggleKeyBtn').textContent = name;
        window.removeEventListener('keydown', once, true);
      };
      window.addEventListener('keydown', once, true);
    });
  }

  V2O.configPath = '';
  function load() { V2O.loading = true; post({ type: 'overlayStatus' }); }
  window.addEventListener('message', e => {
    const m = e.data;
    if (m?.type === 'overlayStatus') {
      V2O.loading = false;
      V2O.config = m.payload?.config || V2O.config;
      V2O.configPath = m.payload?.configPath || '';
      V2O.mods = m.payload?.mods || [];
      if (AppState.currentPage === 'overlay') render();
    }
  });

  // 进入页面时刷新
  const origShowPage = window.showPage;
  window.showPage = function (page, opts) {
    origShowPage(page, opts);
    if (page === 'overlay') { load(); setTimeout(render, 50); }
  };
  document.addEventListener('DOMContentLoaded', () => {
    // nav 点击 overlay 都走 showPage('overlay')
  });
})();
