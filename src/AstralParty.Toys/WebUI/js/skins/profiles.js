// skins/profiles.js - profile list and animation binding
(function () {
  'use strict';
  const { cardPlayers, visiblePlayers, refreshPlayback, playCard, fmtBytes } = window.SkinsPlayback;
  Object.assign(window.SkinsModule, {
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
      if (window.CharacterSkinsModule?.tab === 'resource') window.ResourceModsModule?.onShow();
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
  });
})();
