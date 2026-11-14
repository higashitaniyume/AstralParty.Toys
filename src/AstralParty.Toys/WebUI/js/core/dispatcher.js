// core/dispatcher.js - Host message dispatcher
// Host Message Handler
host?.addEventListener('message', event => {
  const msg = event.data;
  if (!msg?.type) return;
  if (msg.type.startsWith('myAccount')) { window.MyAccountModule?.receive(msg); return; }

  switch (msg.type) {
    case 'modRecoveryDone':
      window.ModsModule?.clearRecoveryBusy();
      break;
    case 'appVersion':
      window.VersionModule?.render(msg.payload);
      break;

    case 'changelog':
      window.VersionModule?.renderChangelog(msg.payload);
      break;

    case 'loaderChangelog':
      window.VersionModule?.renderLoaderChangelog(msg.payload);
      break;

    case 'homeData':
      AppState.homeData = msg.payload;
      if (window.HomeModule) window.HomeModule.init(msg.payload);
      if (window.WikiModule) window.WikiModule.init(msg.payload);
      if (window.SettingsModule) window.SettingsModule.init();
      break;

    case 'replayLibrary':
      AppState.replayLibrary = msg.payload;
      if (window.ReplayModule) window.ReplayModule.renderLibrary(msg.payload);
      break;

    case 'libraryResult':
      if (window.ReplayModule) window.ReplayModule.renderLibraryResult(msg.payload);
      break;

    case 'replayDownloadState':
      // 「按 ID 下载」的进度与收尾（弹窗自己认领，见 ReplayModule.renderDownloadState）
      window.ReplayModule?.renderDownloadState(msg.payload);
      break;

    case 'appUpdateState':
      AppState.appUpdateState = msg.payload;
      window.SettingsModule?.renderUpdateState(msg.payload);
      break;

    case 'librarySettings':
      AppState.librarySettings = msg.payload;
      if (msg.payload?.language && msg.payload.language !== window.i18n?.lang) {
        window.i18n?.setLanguage(msg.payload.language, false);
      }
      if (window.SettingsModule) {
        window.SettingsModule.init();
        window.SettingsModule.applyLibrarySettings(msg.payload);
      }
      if (window.ModsModule) {
        window.ModsModule.applyLibrarySettings?.(msg.payload);
      }
      break;

    case 'characterExportTasks':
      window.CharacterSkinsModule?.exportTasks(msg.payload);
      break;
    case 'characterTemplateState':
      window.CharacterSkinsModule?.templateState(msg.payload);
      break;
    case 'characterCropOpen':
      window.CharacterCropModule?.open(msg.payload);
      break;
    case 'characterCropSaved':
      window.CharacterCropModule?.saved(msg.payload);
      break;
    case 'characterSkins':
      window.CharacterSkinsModule?.render(msg.payload);
      window.CharacterSchemesModule?.render(msg.payload);
      break;
    case 'characterSchemePackageAttached':
      window.CharacterSchemesModule?.packageAttached(msg.payload);
      break;
    case 'characterSchemesSaved':
      window.CharacterSchemesModule?.saved(msg.payload);
      break;
    case 'skinsLegacyImported':
      window.SkinsModule?.legacyImported?.(msg.payload);
      break;
    case 'skinsProfiles':
      window.SkinsModule?.renderProfiles?.(msg.payload);
      break;

    case 'skinsCards':
      window.SkinsModule?.renderCards?.(msg.payload);
      break;

    case 'loading':
      const loading = $('loadingOverlay');
      if (loading) {
        loading.classList.toggle('hidden', !msg.active);
        if (msg.message && $('loadingText')) $('loadingText').textContent = msg.message;
      }
      break;

    case 'report':
      try {
        AppState.replayReport = msg.payload;
        showPage('tools');
        if (window.ReplayModule) window.ReplayModule.renderReport(msg.payload);
        toast('回放文件解析成功！');
      } catch (err) {
        console.error('Failed to display report:', err);
        toast(`解析展示失败: ${err.message}`);
      }
      break;

    case 'frameDetail':
      if (window.ReplayModule) window.ReplayModule.renderFrameDetail(msg.payload);
      break;

    case 'toast':
      toast(msg.message);
      break;

    case 'speedhackStatus':
      if (window.UtilitiesModule) window.UtilitiesModule.applyStatus(msg.payload);
      break;

    case 'modStatus':
      // payload = { status, builtInMods }：整份交给渲染，页面自己区分「哪个模组是内置的」
      if (window.ModsModule) window.ModsModule.renderModStatus(msg.payload);
      break;

    case 'modLoaderProgress':
      // 加载器下载 / 校验 / 安装进度（阶段 + 百分比），新界面自己画进度条
      window.V2ModsModule?.progress?.(msg.payload);
      break;

    case 'toysLog':
      // 日志落盘的同时转发到界面上的「运行日志」
      window.V2ModsModule?.log?.(msg.payload);
      break;

    case 'toysLogHistory':
      // 打开日志面板时补的历史（窗口订阅 Written 之前的启动日志在这里）
      window.V2ModsModule?.history?.(msg.payload?.lines || []);
      break;

    case 'resourceModsStatus':
      window.ResourceModsModule?.render(msg.payload);
      break;

    case 'resourceModsProgress':
      window.ResourceModsModule?.renderProgress(msg.payload);
      break;

    case 'resourceModsPaused':
      window.ResourceModsModule?.renderPaused(msg.payload?.paused);
      break;

    case 'resourceModsOperation':
      window.ResourceModsModule?.renderOperation(msg.payload);
      break;

    case 'modPackageFileSelected':
      window.ModsModule?.applyPackageFile(msg.payload);
      break;

    case 'modPackageImportResult':
      window.ModsModule?.renderPackageImportResult(msg.payload);
      break;

    case 'modImportConfirm':
      // 模组包里是比已装更旧的版本：弹窗确认后才真正写入（不能无声降级）
      window.ModsModule?.openImportConfirm(msg.payload);
      break;

    case 'modUpdateCheck':
      // 「检查更新」的联网结果（最新版本号 / 错误）
      if (window.ModsModule) window.ModsModule.renderUpdateCheck(msg.payload);
      break;

    case 'workshopList':
      window.WorkshopModule?.renderList(msg.payload);
      break;

    case 'workshopConsentRequired':
      window.WorkshopModule?.requireConsent(msg.payload);
      break;

    case 'workshopConsent':
      window.WorkshopModule?.renderConsent(msg.payload);
      break;

    case 'workshopDetail':
      window.WorkshopModule?.renderDetail(msg.payload);
      break;

    case 'workshopOpenMod':
      // 网页点 aspatoys://install-mod 唤起：打开模组详情，由用户确认安装
      window.WorkshopModule?.openExternalMod?.(msg.payload);
      break;

    case 'workshopProgress':
      window.WorkshopModule?.renderProgress(msg.payload);
      break;

    case 'workshopResult':
      window.WorkshopModule?.renderResult(msg.payload);
      break;

    case 'workshopInstallConfirm':
      // 包内版本比已装旧：确认后才真正写入
      window.WorkshopModule?.openInstallConfirm(msg.payload);
      break;

    case 'workshopStickers':
      window.WorkshopModule?.renderStickers(msg.payload);
      break;

    case 'workshopStickerCatalog':
      window.WorkshopModule?.renderStickerCatalog(msg.payload);
      break;

    case 'workshopStickerCatalogProgress':
      window.WorkshopModule?.renderStickerCatalogProgress(msg.payload);
      break;

    case 'workshopStickerToggle':
      window.WorkshopModule?.renderStickerToggle(msg.payload);
      break;

    case 'modLoaderConfig':
      if (window.ModsModule) window.ModsModule.renderLoaderSettings(msg.payload?.config);
      break;

    case 'modSpeedhackSync':
      if (window.ModsModule) window.ModsModule.syncSpeedhackBar(msg.payload?.config);
      break;

    case 'launchModeSync':
      if (window.HomeModule) window.HomeModule.applyLaunchModeSync(msg.payload);
      break;

    case 'steamBypassSync':
      // 加载器设置里改了「Steam 绕过」→ 首页的「绕过 Steam 启动」单选框跟着走（同一个配置）
      if (window.HomeModule) window.HomeModule.applySteamBypassSync(msg.payload);
      break;

    case 'gameProfiles':
      AppState.gameProfiles = msg.payload;
      if (window.GameLibModule) window.GameLibModule.renderProfiles(msg.payload);
      break;

    case 'accountProfiles':
      AppState.accountProfiles = msg.payload;
      window.AccountProfilesModule?.render(msg.payload);
      break;

    case 'gameScanResult':
      if (window.GameLibModule) window.GameLibModule.renderScanResult(msg.payload);
      break;

    case 'gameScanProgress':
      if (window.GameLibModule) window.GameLibModule.renderScanProgress(msg.payload);
      break;

    case 'modConfigFields':
      if (window.ModsModule) window.ModsModule.renderConfigFields(msg.payload);
      break;

    case 'characterExportTasks':
      window.CharacterSkinsModule?.exportTasks(msg.payload);
      break;
    case 'characterTemplateState':
      window.CharacterSkinsModule?.templateState(msg.payload);
      break;
    case 'characterCropOpen':
      window.CharacterCropModule?.open(msg.payload);
      break;
    case 'characterCropSaved':
      window.CharacterCropModule?.saved(msg.payload);
      break;
    case 'characterSkins':
      window.CharacterSkinsModule?.render(msg.payload);
      window.CharacterSchemesModule?.render(msg.payload);
      break;
    case 'characterSchemePackageAttached':
      window.CharacterSchemesModule?.packageAttached(msg.payload);
      break;
    case 'characterSchemesSaved':
      window.CharacterSchemesModule?.saved(msg.payload);
      break;
    case 'skinsLegacyImported':
      window.SkinsModule?.legacyImported?.(msg.payload);
      break;
    case 'skinsProfiles':
      if (window.SkinsModule) window.SkinsModule.renderProfiles(msg.payload);
      break;

    case 'skinsCards':
      if (window.SkinsModule) window.SkinsModule.renderCards(msg.payload);
      break;

    case 'audioSkinProfiles':
      window.AudioSkinsModule?.renderProfiles(msg.payload);
      break;

    case 'audioSkinItems':
      window.AudioSkinsModule?.renderItems(msg.payload);
      break;

    case 'audioSkinExtractProgress':
      window.AudioSkinsModule?.renderExtractProgress(msg.payload);
      break;

    case 'audioSkinExtractDone':
      window.AudioSkinsModule?.renderExtractDone();
      break;

    case 'skinsExportConfirm':
      window.SkinsModule?.confirmAction('覆盖已有 ZIP？', `文件 ${msg.payload.fileName} 已存在，继续将覆盖它。`, { type: 'skinsExportConfirmed', token: msg.payload.token });
      break;
    case 'skinsDownloadConfirm':
      window.SkinsModule?.confirmDownloadDefault();
      break;
    case 'skinsAnimationCropSource':
      window.SkinCropModule?.open(msg.payload);
      break;
    case 'skinsCropSource':
      window.SkinCropModule?.open(msg.payload);
      break;
    case 'skinsCropSaved':
      window.SkinCropModule?.saved(msg.payload);
      break;
    case 'skinsDownloadState': {
      const button = $('skinDownloadDefaultBtn');
      if (button) { button.disabled = !!msg.payload?.running; button.textContent = msg.payload?.running ? '正在下载…' : '下载默认包'; }
      break;
    }
    case 'skinsAnimationState':
      window.SkinsModule?.onAnimationState(msg.payload);
      break;
    case 'skinsCardUpdated':
      if (window.SkinsModule) window.SkinsModule.onCardUpdated(msg.payload);
      break;

    case 'error':
      const loadEl = $('loadingOverlay');
      if (loadEl) loadEl.classList.add('hidden');
      // 宿主抛异常时不会有别的收尾消息，这里兜住按钮的忙碌态，免得一直转圈
      window.ModsModule?.clearBusy?.();
      toast(`错误：${msg.message}`);
      break;
  }
});

// Expose to window
window.AppState = AppState;
window.post = post;
window.toast = toast;
window.openModal = openModal;
window.closeModal = closeModal;
window.showPage = showPage;
window.$ = $;
window.esc = esc;
window.fmt = fmt;
