// replay/download.js - download by ID and CDN
(function () {
  'use strict';
  const { fmtBytes, QUALITY_TONE, qualityTone } = window.ReplayHelpers;
  Object.assign(window.ReplayModule, {
    cdnOptions() {
      return [
        { value: 'CnProd', label: '国服（正式服）' },
        { value: 'CnDev', label: '国服（测试服）' },
        { value: 'JpProd', label: '国际服（正式服）' },
        { value: 'JpDev', label: '国际服（测试服）' }
      ];
    },

    /** 默认区服跟随「当前游戏」的版本：国际服 → 国际服正式服，其余（国服 / TapTap / 自定义）→ 国服正式服。 */
    defaultCdn() {
      const profiles = AppState.gameProfiles?.profiles || [];
      const active = profiles.find(profile => profile.active);
      return active?.edition === 'global' ? 'JpProd' : 'CnProd';
    },
    openDownloadDialog() {
      const options = this.cdnOptions()
        .map(item => `<option value="${item.value}">${esc(item.label)}</option>`)
        .join('');
      openModal('按回放 ID 下载到回放库', `
        <p class="download-intro">
          回放由服务器录制后放在官方回放服务器上，游戏里点「保存回放」也是从那里拉的。
          填上回放 ID 就能直接下载进<strong>回放库</strong>：不用打开游戏，也不占游戏内的 10 个席位。
        </p>
        <div class="download-form">
          <label class="download-field">
            <span>回放 ID</span>
            <input class="library-input wide" id="replayDownloadIdInput" inputmode="numeric" autocomplete="off"
                   placeholder="战绩里那一长串数字" aria-label="回放 ID">
          </label>
          <label class="download-field">
            <span>回放服务器（不确定就按当前游戏版本选）</span>
            <select class="library-select" id="replayDownloadCdnSelect" aria-label="回放服务器">${options}</select>
          </label>
        </div>
        <div class="download-progress hidden" id="replayDownloadProgress">
          <div class="download-bar" id="replayDownloadBarWrap"><i id="replayDownloadBar"></i></div>
          <span id="replayDownloadText"></span>
        </div>
        <p class="download-status hidden" id="replayDownloadStatus"></p>
        <div class="download-actions">
          <button class="primary-btn" id="replayDownloadStartBtn">开始下载</button>
          <button class="secondary-btn hidden" id="replayDownloadCancelBtn">取消下载</button>
        </div>
      `);

      const select = $('replayDownloadCdnSelect');
      if (select) select.value = this.defaultCdn();
      this.downloadId = null;

      const start = () => this.startDownload();
      $('replayDownloadStartBtn')?.addEventListener('click', start);
      $('replayDownloadIdInput')?.addEventListener('keydown', event => {
        if (event.key === 'Enter') { event.preventDefault(); start(); }
      });
      $('replayDownloadCancelBtn')?.addEventListener('click', () => {
        const button = $('replayDownloadCancelBtn');
        if (button) button.disabled = true;
        this.setDownloadStatus('正在取消下载…', false);
        post({ type: 'replayDownloadCancel' });
      });
      $('replayDownloadIdInput')?.focus();
    },
    startDownload() {
      const replayId = ($('replayDownloadIdInput')?.value || '').trim();
      if (!/^\d{1,32}$/.test(replayId)) {
        this.setDownloadStatus('回放 ID 只能是数字——就是游戏战绩里那一长串数字。', true);
        return;
      }
      this.downloadId = replayId;
      this.setDownloadStatus('', false);
      this.setDownloadRunning(true);
      this.updateDownloadProgress({ receivedBytes: 0, totalBytes: null, percent: null });
      post({
        type: 'replayDownload',
        replayId,
        cdn: $('replayDownloadCdnSelect')?.value || 'CnProd'
      });
    },
    setDownloadRunning(running) {
      const start = $('replayDownloadStartBtn');
      const cancel = $('replayDownloadCancelBtn');
      if (start) {
        start.disabled = running;
        start.textContent = running ? '下载中…' : '开始下载';
      }
      if (cancel) {
        cancel.disabled = false;
        cancel.classList.toggle('hidden', !running);
      }
      $('replayDownloadProgress')?.classList.toggle('hidden', !running);
    },
    setDownloadStatus(text, isError) {
      const box = $('replayDownloadStatus');
      if (!box) return;
      box.textContent = text || '';
      box.classList.toggle('hidden', !text);
      box.classList.toggle('ok', !!text && !isError);
    },
    updateDownloadProgress(payload) {
      const percent = payload?.percent;
      const bar = $('replayDownloadBar');
      $('replayDownloadBarWrap')?.classList.toggle('indeterminate', percent == null);
      if (bar && percent != null) bar.style.width = `${percent}%`;

      const text = $('replayDownloadText');
      if (!text) return;
      const received = fmtBytes(payload?.receivedBytes || 0);
      text.textContent = percent != null
        ? `已下载 ${received} / ${fmtBytes(payload.totalBytes)}（${percent}%）`
        : `已下载 ${received}`;
    },

    /** 宿主回推的下载状态：running = 进度，其余三种是收尾。 */
    renderDownloadState(payload) {
      if (!payload) return;
      // 弹窗已经关掉（或被别的弹窗顶掉）时忽略进度：列表刷新另有通道
      if (!$('globalModal')?.classList.contains('open') || !$('replayDownloadIdInput')) return;
      // 只认领本弹窗正在下载的那个 ID
      if (this.downloadId && payload.replayId && payload.replayId !== this.downloadId) return;

      if (payload.state === 'running') {
        this.updateDownloadProgress(payload);
        return;
      }

      this.downloadId = null;
      this.setDownloadRunning(false);
      const messages = (payload.messages || []).filter(Boolean);

      if (payload.state === 'done') {
        closeModal();
        toast(payload.summary || '下载完成。');
        // 下载成功但解析不出结算帧这类提醒别丢掉（它决定"能不能放回游戏"）
        if (messages.length > 1) {
          openModal('下载完成', `
            <div class="download-messages">${messages.map(message => `<div>${esc(message)}</div>`).join('')}</div>
          `);
        }
        return;
      }

      // 失败 / 取消：弹窗留着，ID 可以直接改
      this.setDownloadStatus(messages.join('\n') || payload.summary || '下载失败。', payload.state !== 'canceled');
    },
    renderLibraryResult(payload) {
      if (!payload) return;
      toast(payload.summary || (payload.ok ? '操作完成。' : '操作失败。'));
      const messages = payload.messages || [];

      if (payload.needsConfirmation && (payload.pendingIds || []).length) {
        const ids = payload.pendingIds;
        openModal('这些回放解析不出结算帧', `
          <p style="font-size:13px;color:var(--gp-text-sub);line-height:1.75;margin:0 0 12px">
            本工具读不出它们的结算帧（工具自带的协议版本可能比游戏旧）。如果游戏也读不出来，
            <strong style="color:#fca5a5">游戏在「本地回放」列表里遇到这种目录会把它整个删掉</strong>。
            确认要继续放回，就点下面的按钮；库里的副本无论如何都会保留。
          </p>
          <p style="font-size:12px;color:var(--gp-text-muted);margin:0 0 14px;word-break:break-all">${ids.map(id => esc(id)).join('<br>')}</p>
          <div style="display:flex;gap:10px">
            <button class="primary-btn" id="libraryForceRestoreBtn">仍要放回（${ids.length} 份）</button>
            <button class="secondary-btn" id="libraryCancelRestoreBtn">取消</button>
          </div>
        `);
        $('libraryForceRestoreBtn')?.addEventListener('click', () => {
          post({ type: 'libraryRestore', ids, allowUnparseable: true });
          closeModal();
        });
        $('libraryCancelRestoreBtn')?.addEventListener('click', closeModal);
        return;
      }

      if (messages.length > 1) {
        openModal('回放库操作结果', `
          <div style="display:flex;flex-direction:column;gap:7px;font-size:12.5px;line-height:1.65">
            ${messages.map(message => `<div>${esc(message)}</div>`).join('')}
          </div>
        `);
      }
    },
  });
})();
