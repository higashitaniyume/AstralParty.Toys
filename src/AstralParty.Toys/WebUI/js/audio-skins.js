// audio-skins.js - Audio Skin Management and Replacement
(() => {
  const AudioSkinsModule = {
    profiles: [],
    activeSchemeId: 'default',
    selectedSchemeId: 'default',
    items: [],
    category: 'all',
    statusFilter: 'all',
    keyword: '',
    pageSize: 48,
    currentPage: 1,
    currentAudio: null,
    currentPlayingId: null,

    init() {
      // Scheme controls
      const schemeSelect = $('audioSchemeSelect');
      if (schemeSelect) {
        schemeSelect.onchange = () => {
          this.selectedSchemeId = schemeSelect.value;
          this.updateMetaDisplay();
          post({ type: 'audioSkinsGetItems', schemeId: this.selectedSchemeId });
        };
      }

      $('audioApplyActiveBtn')?.addEventListener('click', () => {
        post({ type: 'audioSkinsSetActive', id: this.selectedSchemeId });
      });

      $('audioNewSchemeBtn')?.addEventListener('click', () => {
        const dialog = $('audioCreateDialog');
        if (dialog) {
          $('audioCreateForm')?.reset();
          dialog.showModal();
        }
      });

      $('audioCreateCancel')?.addEventListener('click', () => {
        $('audioCreateDialog')?.close();
      });

      $('audioCreateForm')?.addEventListener('submit', (e) => {
        e.preventDefault();
        const f = new FormData(e.target);
        const id = (f.get('id') || '').toString().trim();
        const name = (f.get('name') || '').toString().trim();
        const author = (f.get('author') || '').toString().trim();
        const description = (f.get('description') || '').toString().trim();

        if (id) {
          post({ type: 'audioSkinsCreate', id, name, author, description });
          $('audioCreateDialog')?.close();
        }
      });

      $('audioDuplicateBtn')?.addEventListener('click', () => {
        const newId = prompt('请输入新方案目录标识 (英文/数字):', this.selectedSchemeId + '_copy');
        if (!newId) return;
        const newName = prompt('请输入新方案名称:', this.selectedSchemeId + ' (副本)');
        post({ type: 'audioSkinsDuplicate', sourceId: this.selectedSchemeId, newId: newId.trim(), name: (newName || newId).trim() });
      });

      $('audioEditMetaBtn')?.addEventListener('click', () => {
        const profile = this.profiles.find(p => p.id === this.selectedSchemeId);
        if (!profile) return;
        $('audioEditMetaId').value = profile.id;
        $('audioEditMetaName').value = profile.name || profile.id;
        $('audioEditMetaAuthor').value = profile.author || '';
        $('audioEditMetaDesc').value = profile.description || '';
        $('audioEditMetaDialog')?.showModal();
      });

      $('audioEditMetaCancel')?.addEventListener('click', () => {
        $('audioEditMetaDialog')?.close();
      });

      $('audioEditMetaForm')?.addEventListener('submit', (e) => {
        e.preventDefault();
        const f = new FormData(e.target);
        post({
          type: 'audioSkinsUpdateMeta',
          id: f.get('id'),
          name: f.get('name'),
          author: f.get('author'),
          description: f.get('description')
        });
        $('audioEditMetaDialog')?.close();
      });

      $('audioDeleteSchemeBtn')?.addEventListener('click', () => {
        if (this.selectedSchemeId === 'default') {
          alert('默认方案不可删除！');
          return;
        }
        if (confirm(`确定要删除方案 [${this.selectedSchemeId}] 及其包含的所有自定义音频吗？此操作不可撤销。`)) {
          post({ type: 'audioSkinsDelete', id: this.selectedSchemeId });
        }
      });

      $('audioOpenFolderBtn')?.addEventListener('click', () => {
        post({ type: 'audioSkinsOpenFolder' });
      });

      $('audioExportZipBtn')?.addEventListener('click', () => {
        post({ type: 'audioSkinsExportZip', id: this.selectedSchemeId });
      });

      $('audioImportZipBtn')?.addEventListener('click', () => {
        post({ type: 'audioSkinsImportZip' });
      });

      $('audioExtractGameBtn')?.addEventListener('click', () => {
        if (confirm('是否从游戏资源中整理与提取全部原版音频？\n这将允许你在界面中直接试听原版音效进行对比。')) {
          post({ type: 'audioSkinsExtractGameAudio' });
        }
      });

      // Categories
      const catTabs = document.querySelectorAll('#audioCategoryTabs .gallery-tab');
      catTabs.forEach(btn => {
        btn.addEventListener('click', () => {
          catTabs.forEach(b => b.classList.remove('active'));
          btn.classList.add('active');
          this.category = btn.getAttribute('data-category') || 'all';
          this.currentPage = 1;
          this.renderList();
        });
      });

      // Status filters
      document.querySelectorAll('input[name="audioStatusFilter"]').forEach(radio => {
        radio.addEventListener('change', () => {
          this.statusFilter = radio.value;
          this.currentPage = 1;
          this.renderList();
        });
      });

      // Search
      const searchInput = $('audioSearchInput');
      const clearBtn = $('audioSearchClear');
      if (searchInput) {
        let timer = null;
        searchInput.addEventListener('input', () => {
          clearTimeout(timer);
          timer = setTimeout(() => {
            this.keyword = searchInput.value.trim().toLowerCase();
            if (clearBtn) clearBtn.classList.toggle('hidden', !this.keyword);
            this.currentPage = 1;
            this.renderList();
          }, 150);
        });
      }

      if (clearBtn && searchInput) {
        clearBtn.addEventListener('click', () => {
          searchInput.value = '';
          this.keyword = '';
          clearBtn.classList.add('hidden');
          this.currentPage = 1;
          this.renderList();
        });
      }
    },

    onShow() {
      post({ type: 'audioSkinsGetProfiles' });
    },

    renderProfiles(payload) {
      if (!payload) return;
      this.profiles = payload.profiles || [];
      this.activeSchemeId = payload.activeScheme || 'default';

      if (!this.profiles.some(p => p.id === this.selectedSchemeId)) {
        this.selectedSchemeId = this.activeSchemeId;
      }

      const select = $('audioSchemeSelect');
      if (select) {
        select.innerHTML = '';
        this.profiles.forEach(p => {
          const opt = document.createElement('option');
          opt.value = p.id;
          opt.textContent = `${p.name || p.id} (${p.ruleCount || 0} 规则)${p.isActive ? ' [当前生效]' : ''}`;
          if (p.id === this.selectedSchemeId) opt.selected = true;
          select.appendChild(opt);
        });
      }

      this.updateMetaDisplay();

      // Request items for selected scheme
      post({ type: 'audioSkinsGetItems', schemeId: this.selectedSchemeId });
    },

    updateMetaDisplay() {
      const profile = this.profiles.find(p => p.id === this.selectedSchemeId) || {
        id: this.selectedSchemeId,
        name: this.selectedSchemeId,
        author: '—',
        description: '暂无方案描述',
        ruleCount: 0
      };

      const isCurrentActive = (profile.id === this.activeSchemeId);
      const activeBadge = $('audioActiveBadge');
      if (activeBadge) activeBadge.classList.toggle('hidden', !isCurrentActive);

      const applyBtn = $('audioApplyActiveBtn');
      if (applyBtn) {
        applyBtn.disabled = isCurrentActive;
        applyBtn.textContent = isCurrentActive ? '当前生效中' : '设为当前方案';
      }

      if ($('audioDisplayName')) $('audioDisplayName').textContent = profile.name || profile.id;
      if ($('audioDescription')) $('audioDescription').textContent = profile.description || '暂无方案描述';
      if ($('audioRuleCountTag')) $('audioRuleCountTag').textContent = `${profile.ruleCount || 0} 项规则`;
      if ($('audioAuthorTag')) $('audioAuthorTag').textContent = `作者: ${profile.author || '—'}`;
      if ($('audioDateTag')) {
        const d = profile.lastModified ? new Date(profile.lastModified).toLocaleDateString() : '—';
        $('audioDateTag').textContent = `更新: ${d}`;
      }
    },

    renderItems(payload) {
      if (!payload) return;
      this.items = payload.items || [];

      // Update category counters
      const counts = { all: this.items.length, UI: 0, Dice: 0, Combat: 0, Card: 0, Voice: 0, BGM: 0, Misc: 0 };
      this.items.forEach(item => {
        if (counts[item.category] !== undefined) counts[item.category]++;
        else counts.Misc++;
      });

      if ($('audioCountAll')) $('audioCountAll').textContent = counts.all;
      if ($('audioCountUI')) $('audioCountUI').textContent = counts.UI;
      if ($('audioCountDice')) $('audioCountDice').textContent = counts.Dice;
      if ($('audioCountCombat')) $('audioCountCombat').textContent = counts.Combat;
      if ($('audioCountCard')) $('audioCountCard').textContent = counts.Card;
      if ($('audioCountVoice')) $('audioCountVoice').textContent = counts.Voice;
      if ($('audioCountBGM')) $('audioCountBGM').textContent = counts.BGM;
      if ($('audioCountMisc')) $('audioCountMisc').textContent = counts.Misc;

      this.renderList();
    },

    getFilteredItems() {
      return this.items.filter(item => {
        // Category filter
        if (this.category !== 'all' && item.category !== this.category) return false;

        // Status filter
        if (this.statusFilter === 'replaced' && item.action !== 'replace') return false;
        if (this.statusFilter === 'muted' && item.action !== 'mute') return false;

        // Keyword filter
        if (this.keyword) {
          const matchId = ('#' + item.id).includes(this.keyword) || item.id.toString() === this.keyword;
          const matchName = (item.name || '').toLowerCase().includes(this.keyword);
          const matchLabel = (item.label || '').toLowerCase().includes(this.keyword);
          if (!matchId && !matchName && !matchLabel) return false;
        }

        return true;
      });
    },

    renderList() {
      const container = $('audioItemsContainer');
      if (!container) return;

      const filtered = this.getFilteredItems();
      const total = filtered.length;

      if ($('audioGalleryCount')) {
        $('audioGalleryCount').textContent = `共 ${total} 个音效条目`;
      }

      if (total === 0) {
        container.innerHTML = `
          <div class="skin-empty-state" style="grid-column: 1 / -1; padding: 40px 0; text-align: center;">
            <div class="empty-icon-wrap" style="font-size: 32px;">🎵</div>
            <p style="color: var(--cs-muted); margin-top: 10px;">未找到匹配的音效条目</p>
          </div>
        `;
        return;
      }

      // Pagination
      const totalPages = Math.ceil(total / this.pageSize);
      if (this.currentPage > totalPages) this.currentPage = totalPages;
      if (this.currentPage < 1) this.currentPage = 1;

      const start = (this.currentPage - 1) * this.pageSize;
      const pageItems = filtered.slice(start, start + this.pageSize);

      let html = '';
      pageItems.forEach(item => {
        html += this.renderItemCard(item);
      });

      // Pagination bar
      if (totalPages > 1) {
        html += `
          <div class="audio-pagination-bar" style="grid-column: 1 / -1; display: flex; justify-content: center; align-items: center; gap: 16px; margin: 24px 0;">
            <button class="secondary-btn" id="audioPagePrev" ${this.currentPage <= 1 ? 'disabled' : ''}>‹ 上一页</button>
            <span style="color: var(--cs-secondary); font-size: 13px;">第 ${this.currentPage} / ${totalPages} 页 (共 ${total} 条)</span>
            <button class="secondary-btn" id="audioPageNext" ${this.currentPage >= totalPages ? 'disabled' : ''}>下一页 ›</button>
          </div>
        `;
      }

      container.innerHTML = html;

      // Bind buttons in current page
      this.bindItemActions(container);

      // Bind pagination
      $('audioPagePrev')?.addEventListener('click', () => {
        if (this.currentPage > 1) {
          this.currentPage--;
          this.renderList();
          container.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
      });

      $('audioPageNext')?.addEventListener('click', () => {
        if (this.currentPage < totalPages) {
          this.currentPage++;
          this.renderList();
          container.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
      });
    },

    renderItemCard(item) {
      const isReplaced = (item.action === 'replace');
      const isMuted = (item.action === 'mute');
      const isRedirect = (item.action === 'redirect');

      let statusBadge = '<span class="badge" style="background:var(--cs-input); color:var(--cs-muted);">官方原版</span>';
      if (isReplaced) {
        statusBadge = `<span class="badge badge-success">已替换: ${escapeHtml(item.customFile || '自定义音频')}</span>`;
      } else if (isMuted) {
        statusBadge = '<span class="badge" style="background:#4a1b24; color:#ff9caa;">已静音</span>';
      } else if (isRedirect) {
        statusBadge = `<span class="badge" style="background:#1a364a; color:#8cd3ff;">重定向 #${item.redirectId}</span>`;
      }

      const categoryBadge = `<span class="badge" style="background:var(--cs-hover); color:var(--cs-secondary); font-size:11px;">${item.category}</span>`;

      return `
        <div class="audio-item-card" data-id="${item.id}" data-name="${escapeHtml(item.name)}" style="
          background: var(--cs-card);
          border: 1px solid ${isReplaced ? 'var(--cs-accent)' : 'var(--cs-border)'};
          border-radius: var(--gp-radius-md, 8px);
          padding: 14px;
          display: flex;
          flex-direction: column;
          gap: 10px;
        ">
          <!-- Card Header -->
          <div style="display: flex; justify-content: space-between; align-items: flex-start; gap: 8px;">
            <div style="min-width: 0;">
              <div style="display: flex; align-items: center; gap: 6px; margin-bottom: 4px;">
                <span style="font-weight: bold; color: var(--cs-accent); font-size: 13px;">#${item.id}</span>
                ${categoryBadge}
                ${statusBadge}
              </div>
              <h4 style="margin: 0; font-size: 14px; color: var(--cs-text); font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;" title="${escapeHtml(item.label || item.name)}">
                ${escapeHtml(item.label || item.name)}
              </h4>
              <div style="font-size: 11px; color: var(--cs-muted); margin-top: 2px; font-family: monospace;" title="${escapeHtml(item.name)}">
                ${escapeHtml(item.name)}
              </div>
            </div>
          </div>

          <!-- Audio Preview & Volume Controls -->
          <div style="display: flex; align-items: center; gap: 8px; flex-wrap: wrap; margin-top: 2px;">
            ${item.hasOriginal ? `
              <button class="secondary-btn btn-play-original" data-url="${escapeHtml(item.originalPreviewUrl)}" style="padding: 4px 10px; font-size: 12px;">
                ▶ 试听原版
              </button>
            ` : `
              <button class="secondary-btn" disabled style="padding: 4px 10px; font-size: 12px; opacity: 0.5;" title="原版音频未提取，点击上方「提取原版音频」按钮可就绪试听">
                原版未提取
              </button>
            `}

            ${isReplaced && item.previewUrl ? `
              <button class="primary-btn btn-play-custom" data-url="${escapeHtml(item.previewUrl)}" style="padding: 4px 10px; font-size: 12px;">
                ▶ 试听替换音
              </button>
            ` : ''}

            ${isReplaced ? `
              <div style="display: flex; align-items: center; gap: 6px; margin-left: auto;">
                <span style="font-size: 11px; color: var(--cs-muted);">音量:</span>
                <input type="range" class="audio-vol-slider" min="0" max="2" step="0.05" value="${item.volume || 1.0}" style="width: 70px; cursor: pointer;" title="调整单项音量倍率" />
                <span class="vol-label" style="font-size: 11px; min-width: 32px; color: var(--cs-secondary);">${Math.round((item.volume || 1.0) * 100)}%</span>
              </div>
            ` : ''}
          </div>

          <!-- Card Actions -->
          <div style="display: flex; align-items: center; gap: 8px; border-top: 1px solid var(--cs-border); padding-top: 10px; margin-top: auto;">
            <button class="primary-btn btn-replace" style="flex: 1; padding: 5px 10px; font-size: 12px;">
              ＋ 替换音频 (.wav)
            </button>
            <button class="secondary-btn btn-mute" style="padding: 5px 10px; font-size: 12px; ${isMuted ? 'color: var(--cs-accent);' : ''}">
              ${isMuted ? '取消静音' : '静音'}
            </button>
            ${isReplaced || isMuted || isRedirect ? `
              <button class="secondary-btn btn-revert" style="padding: 5px 10px; font-size: 12px;" title="清除此音效的替换规则，恢复官方默认">
                还原
              </button>
            ` : ''}
          </div>
        </div>
      `;
    },

    bindItemActions(container) {
      // Play Original
      container.querySelectorAll('.btn-play-original').forEach(btn => {
        btn.addEventListener('click', () => {
          const url = btn.getAttribute('data-url');
          this.playAudio(url, btn);
        });
      });

      // Play Custom
      container.querySelectorAll('.btn-play-custom').forEach(btn => {
        btn.addEventListener('click', () => {
          const url = btn.getAttribute('data-url');
          this.playAudio(url, btn);
        });
      });

      // Replace audio
      container.querySelectorAll('.btn-replace').forEach(btn => {
        btn.addEventListener('click', () => {
          const card = btn.closest('.audio-item-card');
          const id = Number(card.getAttribute('data-id'));
          const name = card.getAttribute('data-name');
          post({
            type: 'audioSkinsPickAndReplace',
            schemeId: this.selectedSchemeId,
            configId: id,
            eventName: name,
            volume: 1.0
          });
        });
      });

      // Mute / Unmute
      container.querySelectorAll('.btn-mute').forEach(btn => {
        btn.addEventListener('click', () => {
          const card = btn.closest('.audio-item-card');
          const id = Number(card.getAttribute('data-id'));
          const name = card.getAttribute('data-name');
          const item = this.items.find(x => x.id === id);
          const isMuted = (item?.action === 'mute');
          post({
            type: 'audioSkinsSetMute',
            schemeId: this.selectedSchemeId,
            configId: id,
            eventName: name,
            mute: !isMuted
          });
        });
      });

      // Revert to original
      container.querySelectorAll('.btn-revert').forEach(btn => {
        btn.addEventListener('click', () => {
          const card = btn.closest('.audio-item-card');
          const id = Number(card.getAttribute('data-id'));
          const name = card.getAttribute('data-name');
          post({
            type: 'audioSkinsRevertAudio',
            schemeId: this.selectedSchemeId,
            configId: id,
            eventName: name
          });
        });
      });

      // Volume slider
      container.querySelectorAll('.audio-vol-slider').forEach(slider => {
        slider.addEventListener('input', (e) => {
          const card = slider.closest('.audio-item-card');
          const id = Number(card.getAttribute('data-id'));
          const name = card.getAttribute('data-name');
          const val = parseFloat(slider.value);
          const label = slider.parentElement?.querySelector('.vol-label');
          if (label) label.textContent = `${Math.round(val * 100)}%`;

          post({
            type: 'audioSkinsSetVolume',
            schemeId: this.selectedSchemeId,
            configId: id,
            eventName: name,
            volume: val
          });
        });
      });
    },

    playAudio(url, button) {
      if (!url) return;

      // If already playing this url, pause and reset
      if (this.currentAudio && this.currentAudio.src === url && !this.currentAudio.paused) {
        this.currentAudio.pause();
        this.currentAudio = null;
        if (button) button.textContent = button.textContent.replace('⏸', '▶');
        return;
      }

      if (this.currentAudio) {
        this.currentAudio.pause();
        this.currentAudio = null;
        document.querySelectorAll('.btn-play-original, .btn-play-custom').forEach(b => {
          b.textContent = b.textContent.replace('⏸', '▶');
        });
      }

      const audio = new Audio(url);
      this.currentAudio = audio;
      if (button) button.textContent = button.textContent.replace('▶', '⏸');

      audio.onended = () => {
        if (button) button.textContent = button.textContent.replace('⏸', '▶');
        this.currentAudio = null;
      };

      audio.onerror = (e) => {
        console.error('Audio playback failed for:', url, e);
        if (button) button.textContent = button.textContent.replace('⏸', '▶');
        this.currentAudio = null;
      };

      audio.play().catch(err => {
        console.warn('Playback blocked or failed:', err);
        if (button) button.textContent = button.textContent.replace('⏸', '▶');
        this.currentAudio = null;
      });
    },

    renderExtractProgress(payload) {
      const banner = $('audioExtractBanner');
      const text = $('audioExtractText');
      const bar = $('audioExtractBar');
      const percent = $('audioExtractPercent');

      if (banner) banner.classList.remove('hidden');
      if (text && payload?.message) text.textContent = payload.message;
      if (bar && payload?.progress !== undefined) bar.value = Math.round(payload.progress * 100);
      if (percent && payload?.progress !== undefined) percent.textContent = `${Math.round(payload.progress * 100)}%`;
    },

    renderExtractDone() {
      const banner = $('audioExtractBanner');
      if (banner) {
        setTimeout(() => {
          banner.classList.add('hidden');
        }, 1500);
      }
    }
  };

  function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  window.AudioSkinsModule = AudioSkinsModule;

  document.addEventListener('DOMContentLoaded', () => {
    AudioSkinsModule.init();
  });
})();
