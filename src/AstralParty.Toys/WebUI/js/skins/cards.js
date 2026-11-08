// skins/cards.js - card grid rendering
(function () {
  'use strict';
  const { cardPlayers, visiblePlayers, refreshPlayback, playCard, fmtBytes } = window.SkinsPlayback;
  Object.assign(window.SkinsModule, {
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
  });
})();
