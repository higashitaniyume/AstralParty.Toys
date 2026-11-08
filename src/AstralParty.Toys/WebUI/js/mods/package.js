// mods/package.js - package import flows
(function () {
  'use strict';
  const { parseVersion, compareVersion, setVersionCell, fmtBytes } = window.ModsHelpers;
  Object.assign(window.ModsModule, {
    openUninstallConfirm() {
      openModal('卸载加载器', `
        <div class="cfg-form">
          <p class="cfg-hint">卸载将删除加载器及 AstralParty_ModLoader 目录，包括模组、配置和日志。此操作无法通过工具撤销。</p>
          <div class="cfg-actions">
            <button class="secondary-btn" id="modUninstallCancel">取消</button>
            <button class="danger-btn" id="modUninstallConfirm">确认卸载</button>
          </div>
        </div>`, 'settings');
      $('modUninstallCancel')?.addEventListener('click', closeModal);
      $('modUninstallConfirm')?.addEventListener('click', () => {
        closeModal();
        this.setBusy('modUninstallBtn', '卸载中…');
        post({ type: 'modUninstall', force: $('modForceUninstallCheck')?.checked === true });
      });
    },
    openPackageImport() {
      if (this.packageImportPending) { toast('发布包正在导入，请稍候。'); return; }
      const html = `
        <div class="cfg-form">
          <p class="cfg-hint">加载器发布 ZIP 将一起安装加载器、SDK、bootstrap 和包内模组，保留已有加载器配置。仅使用可信的官方发布文件，重启游戏后生效。</p>
          <label class="cfg-row">
            <span class="cfg-label">发布包完整路径</span>
            <input type="text" id="modPackagePath" placeholder="选择发布包或粘贴完整路径" autocomplete="off">
          </label>
          <div class="cfg-actions"><button class="secondary-btn" id="modPackageBrowse">选择文件…</button></div>
          <p class="cfg-hint">选择文件后，点击「确认并导入」才会安装。SDK 工具包或单独 SDK DLL 仅更新 SDK。</p>
          <label class="util-check-row"><input type="checkbox" id="modPackageOverwrite">允许覆盖不属于本工具的 version.dll</label>
          <label class="util-check-row"><input type="checkbox" id="modPackageDowngrade">允许安装较旧版本（降级）</label>
          <div class="cfg-hint" id="modPackageResult" role="status" aria-live="polite"></div>
          <div class="cfg-actions">
            <button class="secondary-btn" id="modPackageCancel">取消</button>
            <button class="primary-btn" id="modPackageSubmit">确认并导入</button>
          </div>
        </div>`;
      openModal('导入发布包', html, 'settings');
      $('modPackageOverwrite').checked = $('modUpdateOverwriteCheck')?.checked === true;
      $('modPackageDowngrade').checked = $('modUpdateDowngradeCheck')?.checked === true;
      $('modPackageCancel')?.addEventListener('click', closeModal);
      $('modPackageBrowse')?.addEventListener('click', () => post({ type: 'modPickPackageFile' }));
      $('modPackageSubmit')?.addEventListener('click', () => {
        const path = $('modPackagePath').value.trim().replace(/^"|"$/g, '');
        if (!path) {
          $('modPackageResult').textContent = '请选择发布包或粘贴完整路径。';
          $('modPackagePath').focus();
          return;
        }
        this.packageImportPending = true;
        $('modPackageBrowse').disabled = true;
        $('modPackagePath').readOnly = true;
        $('modPackageSubmit').disabled = true;
        $('modPackageSubmit').textContent = '导入中…';
        $('modPackageResult').textContent = '正在校验并安装发布包…';
        post({ type: 'modImportPackage', path,
          overwriteDll: $('modPackageOverwrite').checked,
          allowDowngrade: $('modPackageDowngrade').checked });
      });
      $('modPackagePath')?.focus();
    },
    applyPackageFile(payload) {
      const input = $('modPackagePath');
      if (input && payload?.path && !this.packageImportPending) {
        input.value = payload.path;
        $('modPackageResult').textContent = '文件已选择，确认上方安装说明后点击「确认并导入」。';
      }
    },
    renderPackageImportResult(payload) {
      this.packageImportPending = false;
      if ($('modPackageBrowse')) $('modPackageBrowse').disabled = false;
      if ($('modPackagePath')) $('modPackagePath').readOnly = false;
      const result = $('modPackageResult');
      if (result) result.textContent = payload?.message || '导入结束。';
      const button = $('modPackageSubmit');
      if (button) {
        button.disabled = false;
        button.textContent = payload?.success ? '再次导入' : '重试导入';
      }
      const cancel = $('modPackageCancel');
      if (cancel) cancel.textContent = '关闭';
    },

    /** 导入前的降级确认：包内版本比已装的旧，不能无声覆盖。（Host 只在预检发现降级时发这条消息） */
    openImportConfirm(payload) {
      const packages = payload?.packages || [];
      const rows = packages.map(pkg => {
        const mods = (pkg.mods || []).map(mod =>
          `<li><b>${esc(mod.name)}</b>：已装 <code>v${esc(mod.from)}</code> → 包内 <code>v${esc(mod.to)}</code></li>`
        ).join('');
        return `<li><b>${esc(pkg.name)}</b><ul class="cfg-list">${mods}</ul></li>`;
      }).join('');
      const html = `
        <div class="cfg-form">
          <p class="cfg-hint">下面这些模组，包里的版本比游戏目录里已装的更旧：</p>
          <ul class="cfg-list">${rows}</ul>
          <p class="cfg-hint">继续导入会用包里的旧版本覆盖现有文件。玩家改过的 <code>config.json</code> 与「已禁用」状态仍会保留，
          被替换掉的旧文件会移入回收站（回收站不可用时保留原文件）。如果只是想升级，请改用更新的模组包。</p>
          <div class="cfg-actions">
            <button class="secondary-btn" id="modImportDowngradeCancel">取消</button>
            <button class="danger-btn" id="modImportDowngradeConfirm">仍然导入</button>
          </div>
        </div>`;
      openModal('检测到版本降级', html, 'warning');
      const modal = $('globalModal');
      // 无论点按钮还是右上角 X 关闭，都要给宿主一个答复，否则它会一直等在这个确认上
      const finish = allow => {
        if (modal) modal._onClose = null;
        post({ type: 'modImportConfirmAnswer', allow });
        closeModal();
      };
      $('modImportDowngradeCancel')?.addEventListener('click', () => finish(false));
      $('modImportDowngradeConfirm')?.addEventListener('click', () => finish(true));
      if (modal) modal._onClose = () => post({ type: 'modImportConfirmAnswer', allow: false });
    },
  });
})();
