
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
