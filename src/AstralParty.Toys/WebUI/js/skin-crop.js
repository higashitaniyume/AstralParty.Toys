// HTML square crop editor. Coordinates are always original-image pixels.
(function () {
  const clamp = (n, min, max) => Math.min(max, Math.max(min, n));
  const editor = {
    state: null,
    open(payload) {
      this.cancel();
      const image = new Image();
      this.state = { ...payload, image, x: 0, y: 0, size: Math.min(payload.width, payload.height), busy: false };
      openModal('裁剪卡面', `
        <div class="skin-crop-intro"><div><strong>选取你的卡面</strong><p>拖动选区移动位置，拖动四角调整大小。选区始终为正方形。</p></div><span class="skin-crop-ratio">1 : 1</span></div>
        <div class="skin-crop-layout">
          <div class="skin-crop-workspace"><canvas id="skinCropCanvas" tabindex="0" aria-label="裁剪选区：拖动移动，拖动四角调整大小，方向键微调位置"></canvas><span class="skin-crop-source">${esc(payload.fileName)} · ${payload.width} × ${payload.height}</span></div>
          <aside class="skin-crop-controls"><span class="skin-crop-label">裁剪预览</span><canvas id="skinCropPreview" width="192" height="192" aria-label="裁剪结果预览"></canvas>
            <div class="skin-crop-output">保存尺寸 <strong>512 × 512</strong></div>
            <label for="skinCropRange">选区大小 <span id="skinCropPercent"></span></label>
            <input id="skinCropRange" type="range" min="1" max="100" step="1" value="100">
            <label for="skinCropSize">选区边长（原图像素）</label><input id="skinCropSize" type="number" min="1" max="${Math.min(payload.width, payload.height)}" value="${Math.min(payload.width, payload.height)}">
            <button class="secondary-btn" id="skinCropReset">居中重置</button><p class="skin-crop-help">方向键微调位置，按住 Shift 每次移动 10 像素。原图不会被修改。</p>
          </aside>
        </div><p id="skinCropError" class="skin-crop-error" role="alert"></p>
        <div class="skin-crop-actions"><button class="secondary-btn" id="skinCropCancel">取消</button><button class="primary-btn" id="skinCropSave" disabled>保存裁剪</button></div>`, 'crop');
      $('globalModal').classList.add('skin-crop-modal');
      const canvas = $('skinCropCanvas');
      canvas.addEventListener('pointerdown', e => this.pointerDown(e));
      canvas.addEventListener('pointermove', e => this.pointerMove(e));
      canvas.addEventListener('pointerup', () => { if (this.state) this.state.drag = null; });
      canvas.addEventListener('pointercancel', () => { if (this.state) this.state.drag = null; });
      canvas.addEventListener('lostpointercapture', () => { if (this.state) this.state.drag = null; });
      canvas.addEventListener('keydown', e => {
        if (!this.state || this.state.busy) return;
        const step = e.shiftKey ? 10 : 1;
        const moves = { ArrowLeft: [-step, 0], ArrowRight: [step, 0], ArrowUp: [0, -step], ArrowDown: [0, step] };
        if (moves[e.key]) { e.preventDefault(); this.state.x += moves[e.key][0]; this.state.y += moves[e.key][1]; this.bound(); this.draw(); }
      });
      $('skinCropRange').addEventListener('input', e => this.setSize(Math.round(Math.min(payload.width, payload.height) * Number(e.target.value) / 100)));
      $('skinCropSize').addEventListener('change', e => this.setSize(Number(e.target.value)));
      $('skinCropReset').addEventListener('click', () => this.reset());
      $('skinCropCancel').addEventListener('click', closeModal);
      $('skinCropSave').addEventListener('click', () => this.save());
      image.onload = () => { if (this.state?.token !== payload.token) return; this.reset(); $('skinCropSave').disabled = false; canvas.focus(); };
      image.onerror = () => { if (this.state?.token === payload.token) $('skinCropError').textContent = '无法加载图片，请取消后重新选择。'; };
      this.observer = new ResizeObserver(() => this.draw()); this.observer.observe(canvas);
      image.src = payload.previewUrl;
    },
    bound() {
      const s = this.state;
      s.size = clamp(Math.round(s.size), 1, Math.min(s.width, s.height));
      s.x = clamp(Math.round(s.x), 0, s.width - s.size); s.y = clamp(Math.round(s.y), 0, s.height - s.size);
    },
    setSize(value) {
      const s = this.state; if (!s || s.busy || !Number.isFinite(value)) return;
      const old = s.size; s.size = clamp(Math.round(value), 1, Math.min(s.width, s.height));
      s.x += (old - s.size) / 2; s.y += (old - s.size) / 2; this.bound(); this.draw();
    },
    reset() {
      const s = this.state; if (!s || s.busy) return;
      s.size = Math.min(s.width, s.height); s.x = (s.width - s.size) / 2; s.y = (s.height - s.size) / 2; this.bound(); this.draw();
    },
    draw() {
      const s = this.state, canvas = $('skinCropCanvas'); if (!s || !canvas || !s.image.complete || !s.image.naturalWidth) return;
      const w = Math.max(1, canvas.clientWidth), h = Math.max(1, canvas.clientHeight), dpr = window.devicePixelRatio || 1;
      canvas.width = Math.round(w * dpr); canvas.height = Math.round(h * dpr);
      const ctx = canvas.getContext('2d'); ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      const scale = Math.min((w - 32) / s.width, (h - 32) / s.height), ox = (w - s.width * scale) / 2, oy = (h - s.height * scale) / 2;
      s.view = { scale, ox, oy }; ctx.drawImage(s.image, ox, oy, s.width * scale, s.height * scale);
      const x = ox + s.x * scale, y = oy + s.y * scale, size = s.size * scale;
      ctx.fillStyle = 'rgba(0,0,0,.62)'; ctx.beginPath(); ctx.rect(0, 0, w, h); ctx.rect(x, y, size, size); ctx.fill('evenodd');
      ctx.strokeStyle = '#ffffff'; ctx.lineWidth = 2; ctx.strokeRect(x, y, size, size);
      ctx.strokeStyle = 'rgba(255,255,255,.4)'; ctx.lineWidth = 1;
      for (let i = 1; i < 3; i++) { ctx.beginPath(); ctx.moveTo(x + size * i / 3, y); ctx.lineTo(x + size * i / 3, y + size); ctx.moveTo(x, y + size * i / 3); ctx.lineTo(x + size, y + size * i / 3); ctx.stroke(); }
      ctx.fillStyle = '#38bdf8'; for (const [hx, hy] of [[x,y],[x+size,y],[x,y+size],[x+size,y+size]]) ctx.fillRect(hx - 5, hy - 5, 10, 10);
      const preview = $('skinCropPreview'), pc = preview.getContext('2d'); pc.clearRect(0, 0, 192, 192);
      const ratioX = s.image.naturalWidth / s.width, ratioY = s.image.naturalHeight / s.height;
      pc.drawImage(s.image, s.x * ratioX, s.y * ratioY, s.size * ratioX, s.size * ratioY, 0, 0, 192, 192);
      const percent = Math.round(s.size / Math.min(s.width, s.height) * 100);
      $('skinCropRange').value = Math.max(1, percent); $('skinCropPercent').textContent = percent + '%'; $('skinCropSize').value = s.size;
    },
    point(e) {
      const r = $('skinCropCanvas').getBoundingClientRect(), v = this.state.view;
      return { x: (e.clientX - r.left - v.ox) / v.scale, y: (e.clientY - r.top - v.oy) / v.scale };
    },
    pointerDown(e) {
      const s = this.state; if (!s?.view || s.busy || e.button !== 0) return;
      const p = this.point(e), tolerance = 14 / s.view.scale;
      const corners = [[-1,-1,s.x,s.y],[1,-1,s.x+s.size,s.y],[-1,1,s.x,s.y+s.size],[1,1,s.x+s.size,s.y+s.size]];
      const corner = corners.find(c => Math.abs(p.x-c[2]) <= tolerance && Math.abs(p.y-c[3]) <= tolerance);
      if (corner) s.drag = { mode: 'resize', sx: corner[0], sy: corner[1], ax: corner[0] < 0 ? s.x+s.size : s.x, ay: corner[1] < 0 ? s.y+s.size : s.y };
      else if (p.x >= s.x && p.x <= s.x+s.size && p.y >= s.y && p.y <= s.y+s.size) s.drag = { mode: 'move', dx: p.x-s.x, dy: p.y-s.y };
      else return;
      e.preventDefault(); $('skinCropCanvas').focus(); $('skinCropCanvas').setPointerCapture(e.pointerId);
    },
    pointerMove(e) {
      const s = this.state; if (!s?.drag || s.busy) return; const p = this.point(e), d = s.drag;
      if (d.mode === 'move') { s.x = p.x-d.dx; s.y = p.y-d.dy; }
      else { const max = Math.min(d.sx > 0 ? s.width-d.ax : d.ax, d.sy > 0 ? s.height-d.ay : d.ay); s.size = clamp(Math.max((p.x-d.ax)*d.sx,(p.y-d.ay)*d.sy),1,max); s.x = d.sx > 0 ? d.ax : d.ax-s.size; s.y = d.sy > 0 ? d.ay : d.ay-s.size; }
      this.bound(); this.draw();
    },
    save() {
      const s = this.state; if (!s || s.busy || !s.image.naturalWidth) return; this.bound(); s.busy = true;
      $('skinCropSave').disabled = true; $('skinCropSave').textContent = '正在保存…'; $('skinCropError').textContent = '';
      post({ type: 'skinsCropSave', token: s.token, x: s.x, y: s.y, size: s.size });
    },
    saved(payload) {
      if (!this.state || payload.token !== this.state.token) return;
      if (payload.success) { this.dispose(); closeModal(); }
      else { this.state.busy = false; $('skinCropSave').disabled = false; $('skinCropSave').textContent = '保存裁剪'; $('skinCropError').textContent = payload.error || '保存失败，请重试。'; }
    },
    dispose() { this.observer?.disconnect(); this.observer = null; this.state = null; $('globalModal')?.classList.remove('skin-crop-modal'); },
    cancel() { if (this.state) post({ type: 'skinsCropCancel', token: this.state.token }); this.dispose(); }
  };
  window.SkinCropModule = editor;
})();
