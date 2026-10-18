// skins.js - 卡面皮肤方案管理模块（含单卡可视化替换）
(function () {
  // 仅详情预览播放序列帧，列表和选择界面保留静态首帧。
  const cardPlayers = new Map();
  let playbackTimer = null;
  const visiblePlayers = new Set();
  const visibilityObserver = new IntersectionObserver(entries => {
    for (const entry of entries) {
      if (entry.isIntersecting) visiblePlayers.add(entry.target);
      else visiblePlayers.delete(entry.target);
    }
    refreshPlayback();
  });
  function refreshPlayback() {
    clearTimeout(playbackTimer);
    playbackTimer = null;
    for (const image of cardPlayers.keys()) {
      if (!image.isConnected) {
        visibilityObserver.unobserve(image);
        visiblePlayers.delete(image);
        cardPlayers.delete(image);
      }
    }
    if (document.hidden) return;
    const now = performance.now();
    let nextDelay = Infinity;
    for (const image of visiblePlayers) {
      const player = cardPlayers.get(image);
      if (!player || image.closest('.hidden')) continue;
      const modal = image.closest('#globalModal');
      if (modal && !modal.classList.contains('open')) continue;
      const elapsed = now - player.start;
      const frame = Math.floor(elapsed / player.interval) % player.card.animationFrameCount;
      if (frame !== player.frame) {
        image.src = player.card.animationFrameUrlPrefix + String(frame).padStart(6, '0') + '.png?v=' + encodeURIComponent(player.card.lastModified || '');
        player.frame = frame;
      }
      nextDelay = Math.min(nextDelay, player.interval - elapsed % player.interval);
    }
    if (Number.isFinite(nextDelay)) playbackTimer = setTimeout(refreshPlayback, Math.max(16, nextDelay));
  }
  function playCard(image, card) {
    if (!image || !card.isAnimated || !card.animationFrameUrlPrefix || !(card.animationFrameCount > 0) || !(card.animationFps > 0)) return;
    cardPlayers.set(image, { card, start: performance.now(), interval: 1000 / card.animationFps, frame: -1 });
    visibilityObserver.observe(image);
  }
  document.addEventListener('visibilitychange', refreshPlayback);
  document.addEventListener('DOMContentLoaded', () => {
    // 页面切换、关闭弹窗和重绘网格时立即暂停或清理播放器。
    new MutationObserver(refreshPlayback).observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class'] });
  });

  const SkinsModule = {
    profiles: [],
    activeSkin: 'default',
    selectedSkin: 'default',
    cards: [],
    activeCategory: 'all',
    searchQuery: '',

    init() {
      this.bindEvents();
    },

    bindEvents() {
      $('skinsBackHomeBtn')?.addEventListener('click', () => showPage('home'));

      $('skinDownloadDefaultBtn')?.addEventListener('click', () => this.confirmDownloadDefault());
      $('skinRestoreDefaultBtn')?.addEventListener('click', () => this.confirmRestoreDefault());
      $('skinExportZipBtn')?.addEventListener('click', () => { if (this.selectedSkin && this.selectedSkin !== '__game_default__') post({ type: 'skinsExportZip', name: this.selectedSkin }); });
      $('skinAnimationBindingBtn')?.addEventListener('click', () => this.openAnimationBinding());
      // 顶部操作按钮
      $('skinOpenFolderTopBtn')?.addEventListener('click', () => post({ type: 'skinsOpenFolder' }));
      $('skinNewProfileTopBtn')?.addEventListener('click', () => this.openDuplicateModal(this.selectedSkin || 'default'));
      $('skinImportZipTopBtn')?.addEventListener('click', () => post({ type: 'skinsImportZip' }));
      $('skinLegacyImportTopBtn')?.addEventListener('click', () => post({ type: 'skinsImportLegacyZip' }));

      // 方案下拉切换
      $('skinProfileSelect')?.addEventListener('change', e => {
        this.selectedSkin = e.target.value;
        this.updateProfileMetaDisplay();
        post({ type: 'skinsGetCards', name: this.selectedSkin });
      });

      // 设为当前
      $('skinApplyActiveBtn')?.addEventListener('click', () => {
        if (!this.selectedSkin) return;
        post({ type: 'skinsSetActive', name: this.selectedSkin });
      });

      // 基于方案复制
      $('skinDuplicateProfileBtn')?.addEventListener('click', () => this.openDuplicateModal(this.selectedSkin));
      // 编辑方案元信息（显示名称 / 作者 / 描述）
      $('skinEditMetaBtn')?.addEventListener('click', () => this.openEditMeta());
      // 打开方案目录
      $('skinOpenProfileFolderBtn')?.addEventListener('click', () => post({ type: 'skinsOpenFolder', name: this.selectedSkin }));
      // 删除方案
      $('skinDeleteProfileBtn')?.addEventListener('click', () => this.confirmDeleteProfile());

      // 分类筛选 Tabs
      document.querySelectorAll('.gallery-tab').forEach(tab => {
        tab.addEventListener('click', () => {
          document.querySelectorAll('.gallery-tab').forEach(t => t.classList.remove('active'));
          tab.classList.add('active');
          this.activeCategory = tab.dataset.category || 'all';
          this.renderGrid();
        });
      });

      // 搜索框
      $('skinCardSearchInput')?.addEventListener('input', e => {
        this.searchQuery = (e.target.value || '').trim().toLowerCase();
        const clearBtn = $('skinCardSearchClear');
        if (clearBtn) clearBtn.classList.toggle('hidden', !this.searchQuery);
        this.renderGrid();
      });
      $('skinCardSearchClear')?.addEventListener('click', () => {
        const input = $('skinCardSearchInput');
        if (input) input.value = '';
        this.searchQuery = '';
        $('skinCardSearchClear')?.classList.add('hidden');
        this.renderGrid();
      });

      // 空状态重置
      $('skinEmptyResetBtn')?.addEventListener('click', () => {
        this.activeCategory = 'all';
        this.searchQuery = '';
        const input = $('skinCardSearchInput');
        if (input) input.value = '';
        $('skinCardSearchClear')?.classList.add('hidden');
        document.querySelectorAll('.gallery-tab').forEach(t => t.classList.remove('active'));
        document.querySelector('.gallery-tab[data-category="all"]')?.classList.add('active');
        this.renderGrid();
      });

      // 游戏目录管理
      $('skinGamePickerManageBtn')?.addEventListener('click', () => showPage('settings'));
    },

    onShowSkins() {
      window.GameLibModule?.init();
      post({ type: 'skinsGetProfiles' });
    },

    legacyImported(payload) {
      if (!payload?.name) return;
      this.selectedSkin = payload.name;
      this.pendingImportedSkin = payload.name;
      this.activeCategory = 'all';
      this.searchQuery = '';
      if ($('skinCardSearchInput')) $('skinCardSearchInput').value = '';
      $('skinCardSearchClear')?.classList.add('hidden');
      document.querySelectorAll('.gallery-tab').forEach(tab => tab.classList.toggle('active', tab.dataset.category === 'all'));
    },

    renderProfiles(payload) {
      if (!payload) return;
      const firstLoad = this.profiles.length === 0;
      this.profiles = payload.profiles || [];
      this.activeSkin = payload.activeSkin || 'default';
      if (this.pendingImportedSkin && this.profiles.some(p => p.name === this.pendingImportedSkin)) {
        this.selectedSkin = this.pendingImportedSkin;
        this.pendingImportedSkin = null;
      } else if (firstLoad) this.selectedSkin = this.activeSkin;

      if (!this.profiles.some(p => p.name.toLowerCase() === (this.selectedSkin || '').toLowerCase())) {
        this.selectedSkin = this.activeSkin;
      }

      const selectEl = $('skinProfileSelect');
      if (selectEl) {
        selectEl.innerHTML = this.profiles.map(p => {
          const isAct = p.name.toLowerCase() === this.activeSkin.toLowerCase();
          const label = `${p.displayName}${p.name !== '__game_default__' && p.name !== p.displayName ? ' (' + p.name + ')' : ''}${isAct ? ' [当前生效]' : ''}`;
          return `<option value="${esc(p.name)}"${p.name === this.selectedSkin ? ' selected' : ''}>${esc(label)}</option>`;
        }).join('');
      }

      this.updateProfileMetaDisplay();
      if (this.selectedSkin) {
        post({ type: 'skinsGetCards', name: this.selectedSkin });
      }
    },

    updateProfileMetaDisplay() {
      const p = this.profiles.find(x => x.name.toLowerCase() === (this.selectedSkin || '').toLowerCase());
      const isActive = p && p.name.toLowerCase() === this.activeSkin.toLowerCase();

      const panel = document.querySelector('.skin-active-panel');
      if (panel) panel.classList.toggle('is-active-highlight', isActive);

      const badgeEl = $('skinActiveBadge');
      if (badgeEl) {
        if (isActive) {
          badgeEl.textContent = '当前选择 · 重启游戏生效';
          badgeEl.className = 'badge badge-success';
        } else {
          badgeEl.textContent = '未启用';
          badgeEl.className = 'badge badge-notice';
        }
      }

      const applyBtn = $('skinApplyActiveBtn');
      if (applyBtn) {
        applyBtn.disabled = isActive;
        applyBtn.textContent = isActive ? '已设为当前方案' : '设为当前皮肤';
      }

      const gameDefault = this.selectedSkin === '__game_default__';
      if ($('skinDuplicateProfileBtn')) $('skinDuplicateProfileBtn').disabled = false;
      const metaBtn = $('skinEditMetaBtn');
      if (metaBtn) {
        const canEditMeta = !!p && !gameDefault && p.name.toLowerCase() !== 'default';
        metaBtn.disabled = !canEditMeta;
        metaBtn.style.opacity = canEditMeta ? '1' : '0.4';
        metaBtn.title = canEditMeta ? '修改显示名称、作者和方案描述' : '游戏默认方案没有可编辑的方案信息';
      }
      if ($('skinOpenProfileFolderBtn')) $('skinOpenProfileFolderBtn').disabled = gameDefault;
      if ($('skinExportZipBtn')) $('skinExportZipBtn').disabled = gameDefault || !p || !p.cardCount;
      const deleteBtn = $('skinDeleteProfileBtn');
      if (deleteBtn) {
        const isDefault = p && (p.name.toLowerCase() === 'default' || gameDefault);
        deleteBtn.disabled = isDefault;
        deleteBtn.style.opacity = isDefault ? '0.4' : '1';
        deleteBtn.title = isDefault ? '默认方案不可删除' : '删除该方案';
      }

      if (p) {
        if ($('skinDisplayName')) $('skinDisplayName').textContent = p.displayName || p.name;
        if ($('skinDescription')) $('skinDescription').textContent = p.description || '暂无描述说明';
        if ($('skinCardCountTag')) $('skinCardCountTag').textContent = `${p.cardCount} 张卡面`;
        if ($('skinAuthorTag')) $('skinAuthorTag').textContent = `作者: ${p.author || '未知'}`;
        if ($('skinDateTag')) {
          const dt = p.lastModified ? new Date(p.lastModified).toLocaleDateString('zh-CN') : '—';
          $('skinDateTag').textContent = `修改时间: ${dt}`;
        }
      }
    },

    renderCards(payload) {
      if (!payload || payload.skinName !== this.selectedSkin) return;
      this.cards = payload.cards || [];
      this.updateCardCounts();
      this.renderGrid();
    },

    updateCardCounts() {
      const total = this.cards.length;
      if ($('skinGalleryCount')) $('skinGalleryCount').textContent = `共 ${total} 张卡面`;

      // 分类计数
      const countHand = this.cards.filter(c => c.category === '手牌卡').length;
      const countDestiny = this.cards.filter(c => c.category === '命运卡').length;
      const countEvent = this.cards.filter(c => c.category === '事件卡').length;
      const countMapEvent = this.cards.filter(c => c.category === '地图事件').length;
      const countAltArt = this.cards.filter(c => c.category === '异画道具').length;

      if ($('countHand')) $('countHand').textContent = countHand;
      if ($('countDestiny')) $('countDestiny').textContent = countDestiny;
      if ($('countEvent')) $('countEvent').textContent = countEvent;
      if ($('countMapEvent')) $('countMapEvent').textContent = countMapEvent;
      if ($('countAltArt')) $('countAltArt').textContent = countAltArt;

    },

    renderGrid() {
      const grid = $('skinCardGrid');
      const emptyState = $('skinEmptyState');
      if (!grid) return;

      const filtered = this.cards.filter(c => {
        // 分类筛选
        if (this.activeCategory !== 'all' && c.category !== this.activeCategory) return false;
        // 搜索
        if (this.searchQuery) {
          const q = this.searchQuery;
          const idStr = String(c.cardId || '');
          const fn = (c.fileName || '').toLowerCase();
          const ak = (c.assetKey || '').toLowerCase();
          const cn = (c.cardName || '').toLowerCase();
          const hashId = q.startsWith('#') ? q.slice(1) : null;
          if (hashId) {
            if (!idStr.includes(hashId)) return false;
          } else if (!idStr.includes(q) && !fn.includes(q) && !ak.includes(q) && !cn.includes(q)) {
            return false;
          }
        }
        return true;
      });

      if ($('skinEmptyMsg')) $('skinEmptyMsg').textContent = this.selectedSkin === '__game_default__' ? '游戏默认方案仅使用游戏内部卡面，无需外部图片。' : '未找到匹配的卡牌';
      if ($('skinEmptyResetBtn')) $('skinEmptyResetBtn').classList.toggle('hidden', this.selectedSkin === '__game_default__');
      if (filtered.length === 0) {
        grid.innerHTML = '';
        if (emptyState) emptyState.classList.remove('hidden');
        return;
      }

      if (emptyState) emptyState.classList.add('hidden');

      const isDefaultSkin = this.selectedSkin.toLowerCase() === 'default';

      grid.innerHTML = filtered.map(c => {
        const idBadge = c.cardId > 0 ? `<span class="skin-badge-id">#${c.cardId}</span>` : '';
        const sfwBadge = c.isSfw ? `<span class="skin-badge-sfw">SFW</span>` : '';
        const cardClass = c.isCustomized ? 'is-customized' : 'is-fallback';
        const displayName = c.cardName || c.assetKey || c.fileName;

        // 卡片下方的常驻操作
        let overlayBtns = `<button class="skin-card-overlay-btn" data-action="replace" data-filename="${esc(c.fileName)}">更换图片</button>`;
        overlayBtns += `<button class="skin-card-overlay-btn" data-action="animation" data-filename="${esc(c.fileName)}">导入视频 / GIF</button>`;
        if (c.isAnimated) overlayBtns += `<button class="skin-card-overlay-btn" data-action="remove-animation" data-filename="${esc(c.fileName)}">移除动态覆盖</button>`;
        overlayBtns += `<button class="skin-card-overlay-btn btn-preview" data-action="preview" data-filename="${esc(c.fileName)}">查看详情</button>`;
        if (c.canRestore) {
          overlayBtns += `<button class="skin-card-overlay-btn btn-revert" data-action="revert" data-filename="${esc(c.fileName)}">恢复内置卡面</button>`;
        }

        return `
          <div class="skin-card-item ${cardClass}" data-filename="${esc(c.fileName)}">
            <div class="skin-card-thumb-wrap">
              <img class="skin-card-thumb" src="${esc(c.previewUrl)}" loading="lazy" alt="${esc(c.fileName)}"
                   onerror="this.style.opacity='0.15'">
              <div class="skin-card-badge-row">${idBadge}${sfwBadge}${c.isAnimated ? '<span class="skin-badge-sfw">动态</span>' : ''}</div>
            </div>
            <div class="skin-card-footer">
              <div class="skin-card-title" title="${esc(c.fileName)}">${esc(displayName)}</div>
              <div class="skin-card-category">${esc(c.category)} · ${fmtBytes(c.fileSizeBytes)}</div>
            </div>
             <div class="skin-card-overlay">${overlayBtns}</div>
          </div>
        `;
      }).join('');

      // 绑定 overlay 按钮事件
      grid.querySelectorAll('.skin-card-overlay-btn').forEach(btn => {
        btn.addEventListener('click', (e) => {
          e.stopPropagation();
          const action = btn.dataset.action;
          const fileName = btn.dataset.filename;
          if (action === 'replace') {
            post({ type: 'skinsPickAndReplaceCard', skinName: this.selectedSkin, fileName });
          } else if (action === 'animation') {
            const card = this.cards.find(c => c.fileName === fileName);
            post({ type: 'skinsPickCardAnimation', skinName: this.selectedSkin, fileName: card?.animationBinding ? card.animationBinding + '.png' : fileName });
          } else if (action === 'remove-animation') {
            const card = this.cards.find(c => c.fileName === fileName);
            post({ type: 'skinsRemoveCardAnimation', skinName: this.selectedSkin, fileName: card?.animationBinding ? card.animationBinding + '.png' : fileName });
          } else if (action === 'revert') {
            this.confirmRevertCard(fileName);
          } else if (action === 'preview') {
            const card = this.cards.find(c => c.fileName === fileName);
            if (card) this.showCardPreview(card);
          }
        });
      });
    },

    openAnimationBinding() {
      if (!this.selectedSkin || this.selectedSkin === '__game_default__') { toast('请先选择外部皮肤方案'); return; }
      if (this.animationRunning) { toast('请先等待或取消当前转换'); return; }
      openModal('按编号导入动态卡面', `<div class="skin-confirm"><p>填写普通卡牌编号，或异画自己的编号。普通卡牌绑定只替换普通版；异画需要单独指定。</p><label for="skinAnimationBinding">卡牌 / 异画编号</label><input id="skinAnimationBinding" class="skin-meta-input" inputmode="numeric" placeholder="例如 10001" aria-describedby="skinAnimationHelp"><p id="skinAnimationHelp">支持 MP4、WebM、MOV、AVI、MKV 和 GIF，最多 20 秒。先裁剪为 404 × 400，再在普通卡框中循环播放。</p><div class="skin-modal-actions"><button class="secondary-btn" id="skinAnimationBindingCancel">取消</button><button class="primary-btn" id="skinAnimationBindingChoose">选择视频 / GIF</button></div></div>`, 'info');
      $('skinAnimationBindingCancel')?.addEventListener('click', closeModal);
      $('skinAnimationBindingChoose')?.addEventListener('click', () => {
        const binding = $('skinAnimationBinding').value.trim();
        if (!/^[1-9][0-9]{0,8}$/.test(binding)) { toast('请输入有效的数字编号'); return; }
        post({ type: 'skinsPickCardAnimation', skinName: this.selectedSkin, fileName: binding + '.png' });
        closeModal();
      });
      $('skinAnimationBinding')?.focus();
    },

    onAnimationState(payload) {
      const running = !!payload?.running;
      this.animationRunning = running;
      if (running) {
        if (window.SkinCropModule?.state?.isAnimation) window.SkinCropModule.dispose();
        if (!$('skinAnimationProgress')) {
          openModal('转换动态卡面', `<div class="skin-confirm"><p id="skinAnimationProgress" role="status" aria-live="polite"></p><p>视频 / GIF 按选定区域裁剪为普通卡面的 404 × 400，所有帧使用同一选区。首次使用可能下载 FFmpeg。</p><div class="skin-modal-actions"><button class="secondary-btn" id="skinAnimationCancel">取消转换</button></div></div>`, 'info');
          $('skinAnimationCancel')?.addEventListener('click', () => { post({ type: 'skinsAnimationCancel' }); $('skinAnimationCancel').disabled = true; });
        }
        $('skinAnimationProgress').textContent = payload.message || '正在转换…';
      } else if ($('skinAnimationProgress')) {
        closeModal();
      }
    },

    // 后端推送的单卡更新
    onCardUpdated(payload) {
      if (!payload || payload.skinName !== this.selectedSkin) return;
      const updated = payload.card;
      if (!updated) return;

      const idx = this.cards.findIndex(c => c.fileName === updated.fileName);
      if (idx >= 0) {
        this.cards[idx] = updated;
      } else {
        this.cards.push(updated);
      }

      this.updateCardCounts();
      this.renderGrid();
    },

    confirmAction(title, message, action) {
      openModal(title, `<div class="skin-confirm"><p>${esc(message)}</p><div class="skin-modal-actions"><button class="secondary-btn" id="skinConfirmCancel">取消</button><button class="primary-btn" id="skinConfirmOk">确认继续</button></div></div>`, 'warning');
      $('skinConfirmCancel').addEventListener('click', closeModal);
      $('skinConfirmOk').addEventListener('click', () => { closeModal(); post(action); });
    },
    confirmDownloadDefault() {
      this.confirmAction('下载默认皮肤包', '将从最新 Release 下载默认包并安装到当前游戏，覆盖 default 中的同名图片。下载不会自动切换当前方案。', { type: 'skinsDownloadDefault', overwrite: true });
    },
    confirmRestoreDefault() {
      this.confirmAction('恢复内置默认包', '将从应用内置资源恢复当前游戏的 default 方案，覆盖同名图片并移除 default 中的动态覆盖。不会改动其他方案，也不会自动切换当前选择。', { type: 'skinsRestoreDefault' });
    },
    confirmRevertCard(fileName) {
      const card = this.cards.find(c => c.fileName === fileName);
      const name = card ? (card.cardName || card.assetKey) : fileName;

      openModal('恢复内置卡面', `
        <div style="display:flex; flex-direction:column; gap:14px;">
          <p style="color:#f8fafc; font-size:0.92rem; margin:0;">
            确定要将 <strong>${esc(name)}</strong> (<code>${esc(fileName)}</code>) 恢复为应用内置的原始卡面吗？
          </p>
          <p style="color:#94a3b8; font-size:0.82rem; margin:0;">
            将从应用内置默认包恢复此卡面，覆盖当前方案的同名图片；磁盘上修改过的 default 不会被用作原始模板。
          </p>
          <div class="skin-modal-actions">
            <button class="secondary-btn" id="revertCancelBtn">取消</button>
            <button class="danger-btn" id="revertConfirmBtn">确认还原</button>
          </div>
        </div>
      `, 'warning');

      $('revertCancelBtn')?.addEventListener('click', closeModal);
      $('revertConfirmBtn')?.addEventListener('click', () => {
        post({ type: 'skinsRevertCard', skinName: this.selectedSkin, fileName });
        closeModal();
      });
    },

    showCardPreview(card) {
      const isDefaultSkin = this.selectedSkin.toLowerCase() === 'default';
      const statusLabel = card.isAnimated ? `动态卡面 · ${card.animationFps} 帧/秒 · ${(card.animationFrameCount / card.animationFps).toFixed(1)} 秒循环` : (card.isCustomized ? '方案内图片' : '默认方案预览');
      const statusColor = card.isCustomized ? '#10b981' : '#94a3b8';

      openModal('卡面详情预览', `
        <div class="card-preview-modal">
          <img class="card-preview-img" src="${esc(card.previewUrl)}" alt="${esc(card.fileName)}">
          <div class="card-preview-details">
            <p><strong>卡牌名称:</strong> ${esc(card.cardName || '—')}</p>
            <p><strong>文件名称:</strong> ${esc(card.fileName)}</p>
            <p><strong>卡牌编号:</strong> ${card.cardId > 0 ? '#' + card.cardId : '未识别'}</p>
            <p><strong>类别:</strong> ${esc(card.category)} ${card.isSfw ? '(SFW 审查版本)' : ''}</p>
            <p><strong>大小:</strong> ${fmtBytes(card.fileSizeBytes)}</p>
            <p style="color:${statusColor}; font-weight:600;">${statusLabel}</p>
          </div>
          <div class="skin-modal-actions">
            <button class="secondary-btn" id="previewCloseBtn">关闭</button>
            <button class="primary-btn" id="previewReplaceBtn">更换图片</button>
            <button class="secondary-btn" id="previewAnimationBtn">导入视频 / GIF</button>
            ${card.canRestore ? '<button class="danger-btn" id="previewRevertBtn">恢复内置卡面</button>' : ''}
            <button class="secondary-btn" id="previewOpenFolderBtn">打开目录</button>
          </div>
        </div>
      `, 'image');

      playCard(document.querySelector('.card-preview-img'), card);
      $('previewAnimationBtn')?.addEventListener('click', () => {
        post({ type: 'skinsPickCardAnimation', skinName: this.selectedSkin, fileName: card.animationBinding ? card.animationBinding + '.png' : card.fileName });
        closeModal();
      });
      $('previewCloseBtn')?.addEventListener('click', closeModal);
      $('previewReplaceBtn')?.addEventListener('click', () => {
        post({ type: 'skinsPickAndReplaceCard', skinName: this.selectedSkin, fileName: card.fileName });
        closeModal();
      });
      $('previewRevertBtn')?.addEventListener('click', () => {
        this.confirmRevertCard(card.fileName);
      });
      $('previewOpenFolderBtn')?.addEventListener('click', () => {
        post({ type: 'skinsOpenFolder', name: this.selectedSkin });
        closeModal();
      });
    },

    openDuplicateModal(sourceSkinName) {
      sourceSkinName = 'default';
      const suggestName = `${sourceSkinName}_copy`;
      openModal('新建皮肤方案', `
        <div class="skin-duplicate-modal">
          <p style="font-size:0.88rem; color:#94a3b8; margin:0;">
            从应用内置默认包创建全新方案，不使用磁盘上已经修改过的图片。无需联网，新建后可更换单张卡面。
          </p>
          <div class="skin-form-group">
            <label for="newSkinFolderName">方案英文目录名（唯一标识）：</label>
            <input type="text" id="newSkinFolderName" value="${esc(suggestName)}" spellcheck="false" placeholder="如 my_custom_skin">
            <span class="skin-form-hint">仅支持英文、数字、下划线及减号</span>
          </div>
          <div class="skin-form-group">
            <label for="newSkinDisplayName">方案显示名称：</label>
            <input type="text" id="newSkinDisplayName" value="我的定制卡面" spellcheck="false" placeholder="如 二次元萌化包">
          </div>
          <div class="skin-form-group">
            <label for="newSkinAuthor">作者签名（可选）：</label>
            <input type="text" id="newSkinAuthor" value="" spellcheck="false" placeholder="您的昵称">
          </div>
          <div class="skin-form-group">
            <label for="newSkinDesc">方案描述（可选）：</label>
            <textarea id="newSkinDesc" rows="2" placeholder="简要说明此方案的特色…"></textarea>
          </div>
          <div class="skin-modal-actions">
            <button class="secondary-btn" id="dupCancelBtn">取消</button>
            <button class="primary-btn" id="dupConfirmBtn">立即复制创建</button>
          </div>
        </div>
      `, 'add');

      $('dupCancelBtn')?.addEventListener('click', closeModal);
      $('dupConfirmBtn')?.addEventListener('click', () => {
        const folderName = ($('newSkinFolderName')?.value || '').trim();
        const dispName = ($('newSkinDisplayName')?.value || '').trim() || folderName;
        const author = ($('newSkinAuthor')?.value || '').trim();
        const desc = ($('newSkinDesc')?.value || '').trim();

        if (!folderName) {
          toast('请输入方案目录名');
          return;
        }

        post({
          type: 'skinsDuplicate',
          source: sourceSkinName,
          newName: folderName,
          displayName: dispName,
          author: author,
          description: desc
        });
        closeModal();
      });
    },

    openEditMeta() {
      const p = this.profiles.find(x => x.name.toLowerCase() === (this.selectedSkin || '').toLowerCase());
      if (!p || p.name === '__game_default__' || p.name.toLowerCase() === 'default') {
        toast('游戏默认方案没有可编辑的方案信息');
        return;
      }

      openModal('编辑方案信息', `
        <div class="skin-duplicate-modal">
          <p style="font-size:0.88rem; color:#94a3b8; margin:0;">
            只修改方案对外展示的信息，不会改变方案目录名 <code>${esc(p.name)}</code> 或其中的卡面图片。
          </p>
          <div class="skin-form-group">
            <label for="editSkinDisplayName">方案显示名称：</label>
            <input type="text" id="editSkinDisplayName" value="${esc(p.displayName || p.name)}" maxlength="100" spellcheck="false" placeholder="如 二次元萌化包">
          </div>
          <div class="skin-form-group">
            <label for="editSkinAuthor">作者签名（可选）：</label>
            <input type="text" id="editSkinAuthor" value="${esc(p.author || '')}" maxlength="100" spellcheck="false" placeholder="您的昵称">
          </div>
          <div class="skin-form-group">
            <label for="editSkinDesc">方案描述（可选）：</label>
            <textarea id="editSkinDesc" rows="3" maxlength="2000" placeholder="简要说明此方案的特色…">${esc(p.description || '')}</textarea>
          </div>
          <div class="skin-modal-actions">
            <button class="secondary-btn" id="editSkinMetaCancel">取消</button>
            <button class="primary-btn" id="editSkinMetaSave">保存信息</button>
          </div>
        </div>
      `, 'add');

      $('editSkinMetaCancel')?.addEventListener('click', closeModal);
      $('editSkinMetaSave')?.addEventListener('click', () => {
        const displayName = ($('editSkinDisplayName')?.value || '').trim();
        const author = ($('editSkinAuthor')?.value || '').trim();
        const description = ($('editSkinDesc')?.value || '').trim();
        if (!displayName) {
          toast('请输入方案显示名称');
          return;
        }
        post({
          type: 'skinsUpdateMeta',
          name: p.name,
          displayName,
          author,
          description
        });
        closeModal();
      });
    },

    confirmDeleteProfile() {
      if (!this.selectedSkin || this.selectedSkin.toLowerCase() === 'default') {
        toast('默认皮肤方案 default 禁止删除');
        return;
      }

      openModal('确认删除皮肤方案', `
        <div style="display:flex; flex-direction:column; gap:16px;">
          <p style="color:#f8fafc; font-size:0.95rem; margin:0;">
            确定要彻底删除方案 <strong>${esc(this.selectedSkin)}</strong> 及其目录下的所有卡面图片吗？此操作不可撤销。
          </p>
          <div class="skin-modal-actions">
            <button class="secondary-btn" id="delCancelBtn">取消</button>
            <button class="danger-btn" id="delConfirmBtn">确认删除</button>
          </div>
        </div>
      `, 'warning');

      $('delCancelBtn')?.addEventListener('click', closeModal);
      $('delConfirmBtn')?.addEventListener('click', () => {
        post({ type: 'skinsDelete', name: this.selectedSkin });
        closeModal();
      });
    }
  };

  function fmtBytes(bytes) {
    if (!bytes || bytes <= 0) return '0 B';
    if (bytes >= 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    if (bytes >= 1024) return (bytes / 1024).toFixed(0) + ' KB';
    return bytes + ' B';
  }

  document.addEventListener('DOMContentLoaded', () => SkinsModule.init());
  window.SkinsModule = SkinsModule;
})();
