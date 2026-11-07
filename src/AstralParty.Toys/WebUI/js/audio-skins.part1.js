// audio-skins.js - Audio Skin Management and Replacement
(() => {
  const AudioSkinsModule = {
    profiles: [],
    activeSchemeId: 'default',
    selectedSchemeId: 'default',
    items: [],
    category: 'all',
    statusFilter: 'all',
    voiceChar: 'all',
    voiceSkin: 'all',
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

      // New Scheme Dialog
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

      // Duplicate Scheme Dialog (Replaces prompt)
      $('audioDuplicateBtn')?.addEventListener('click', () => {
        const dialog = $('audioDuplicateDialog');
        if (dialog) {
          $('audioDuplicateSourceId').value = this.selectedSchemeId;
          $('audioDuplicateSourceDisplay').value = this.selectedSchemeId;
          $('audioDuplicateNewId').value = this.selectedSchemeId + '_copy';
          $('audioDuplicateNewName').value = this.selectedSchemeId + ' (副本)';
          dialog.showModal();
        }
      });

      $('audioDuplicateCancel')?.addEventListener('click', () => {
        $('audioDuplicateDialog')?.close();
      });

      $('audioDuplicateForm')?.addEventListener('submit', (e) => {
        e.preventDefault();
        const f = new FormData(e.target);
        const newId = (f.get('newId') || '').toString().trim();
        const newName = (f.get('newName') || '').toString().trim();
        if (newId) {
          post({
            type: 'audioSkinsDuplicate',
            sourceId: this.selectedSchemeId,
            newId,
            name: newName || newId
          });
          $('audioDuplicateDialog')?.close();
        }
      });

      // Edit Scheme Metadata Dialog
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

      // Delete Scheme Dialog (Replaces confirm/alert)
      $('audioDeleteSchemeBtn')?.addEventListener('click', () => {
        if (this.selectedSchemeId === 'default') {
          post({ type: 'toast', message: '默认方案不可删除！' });
          return;
        }
        const dialog = $('audioDeleteConfirmDialog');
        if (dialog) {
          $('audioDeleteSchemeTargetName').textContent = this.selectedSchemeId;
          dialog.showModal();
        }
      });

      $('audioDeleteConfirmCancel')?.addEventListener('click', () => {
        $('audioDeleteConfirmDialog')?.close();
      });

      $('audioDeleteConfirmForm')?.addEventListener('submit', (e) => {
        e.preventDefault();
        post({ type: 'audioSkinsDelete', id: this.selectedSchemeId });
        $('audioDeleteConfirmDialog')?.close();
      });

      // Scheme Folder & ZIP Import / Export
      $('audioOpenFolderBtn')?.addEventListener('click', () => {
        post({ type: 'audioSkinsOpenFolder' });
      });

      $('audioExportZipBtn')?.addEventListener('click', () => {
        post({ type: 'audioSkinsExportZip', id: this.selectedSchemeId });
      });

      $('audioImportZipBtn')?.addEventListener('click', () => {
        post({ type: 'audioSkinsImportZip' });
      });

      // Extract Game Audio Dialog (Custom Dialog replaces browser confirm)
      $('audioExtractGameBtn')?.addEventListener('click', () => {
        $('audioExtractConfirmDialog')?.showModal();
      });

      $('audioExtractCancelBtn')?.addEventListener('click', () => {
        $('audioExtractConfirmDialog')?.close();
      });

      $('audioExtractConfirmForm')?.addEventListener('submit', (e) => {
        e.preventDefault();
        post({ type: 'audioSkinsExtractGameAudio' });
        $('audioExtractConfirmDialog')?.close();
      });

      // Import Original Audio ZIP package
      const triggerImportOriginal = () => {
        $('audioExtractConfirmDialog')?.close();
        post({ type: 'audioSkinsImportOriginalZip' });
      };
      $('audioImportOriginalZipBtn')?.addEventListener('click', triggerImportOriginal);
      $('audioImportOriginalZipBtnBar')?.addEventListener('click', triggerImportOriginal);

      // Direct Export Original Audio to ZIP
      $('audioExportZipDirectBtn')?.addEventListener('click', () => {
        post({ type: 'audioSkinsExportOriginalZip' });
        $('audioExtractConfirmDialog')?.close();
      });

      $('audioExportOriginalBtn')?.addEventListener('click', () => {
        post({ type: 'audioSkinsExportOriginalZip' });
      });

      // Category filter tabs
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

      // Voice Character & Skin sub-filters
      const charSelect = $('audioVoiceCharSelect');
      if (charSelect) {
        charSelect.addEventListener('change', () => {
          this.voiceChar = charSelect.value;
          this.voiceSkin = 'all';
          this.updateVoiceSkinDropdown();
          this.currentPage = 1;
          this.renderList();
        });
      }

      const skinSelect = $('audioVoiceSkinSelect');
      if (skinSelect) {
        skinSelect.addEventListener('change', () => {
          this.voiceSkin = skinSelect.value;
          this.currentPage = 1;
          this.renderList();
        });
      }

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

      this.updateVoiceDropdowns();
      this.renderList();
    },

    updateVoiceDropdowns() {
      const charSelect = $('audioVoiceCharSelect');
      if (!charSelect) return;

      const voiceItems = this.items.filter(x => x.category === 'Voice');
      const charMap = new Map();
      voiceItems.forEach(item => {
        const cname = item.characterName || '其它';
        if (!charMap.has(cname)) {
          charMap.set(cname, { id: item.characterId, count: 0 });
        }
        charMap.get(cname).count++;
      });

      let charHtml = '<option value="all">全部角色 (全部)</option>';
      const sortedChars = Array.from(charMap.entries()).sort((a, b) => {
        const idA = a[1].id || 99999;
        const idB = b[1].id || 99999;
        return idA - idB;
      });

      sortedChars.forEach(([cname, info]) => {
        const sel = (cname === this.voiceChar) ? 'selected' : '';
        charHtml += `<option value="${escapeHtml(cname)}" ${sel}>${escapeHtml(cname)} (${info.count})</option>`;
      });
      charSelect.innerHTML = charHtml;

      this.updateVoiceSkinDropdown();
    },

    updateVoiceSkinDropdown() {
      const skinSelect = $('audioVoiceSkinSelect');
      if (!skinSelect) return;

      const voiceItems = this.items.filter(x => {
        if (x.category !== 'Voice') return false;
        if (this.voiceChar !== 'all' && x.characterName !== this.voiceChar) return false;
        return true;
      });

      const skinMap = new Map();
      voiceItems.forEach(item => {
        const sname = item.skinName || '常规默认';
        skinMap.set(sname, (skinMap.get(sname) || 0) + 1);
      });

      let skinHtml = '<option value="all">全部皮肤</option>';
      Array.from(skinMap.entries()).forEach(([sname, count]) => {
        const sel = (sname === this.voiceSkin) ? 'selected' : '';
        skinHtml += `<option value="${escapeHtml(sname)}" ${sel}>${escapeHtml(sname)} (${count})</option>`;
      });
      skinSelect.innerHTML = skinHtml;
    },

    getFilteredItems() {
      const isVoice = (this.category === 'Voice');
      const voiceRow = $('audioVoiceFilterRow');
      if (voiceRow) {
        voiceRow.style.display = isVoice ? 'flex' : 'none';
