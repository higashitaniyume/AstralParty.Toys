// mods/status.js - status rendering and busy handling
(function () {
  'use strict';
  const { parseVersion, compareVersion, setVersionCell, fmtBytes } = window.ModsHelpers;
  Object.assign(window.ModsModule, {
    refreshModStatus() {
      post({ type: 'modStatus' });
    },

    /** 已知的「GitHub 最新版本」是否比已装版本更新；没有最新信息时返回 ''。 */
    newerVersion(installedVersion) {
      const latest = this.updateInfo.latest || '';
      if (!latest || !installedVersion) return '';
      return compareVersion(latest, installedVersion) > 0 ? latest : '';
    },
    renderModStatus(payload) {
      if (!payload) return;
      // 兼容两种载荷：{ status, builtInMods }（当前）与直接给 status（老格式）
      const status = payload.status ?? payload;
      this.modStatus = status;
      if (Array.isArray(payload.builtInMods)) this.builtInMods = payload.builtInMods;

      // 恢复只由 modRecoveryDone 收尾，切换游戏时的状态消息不能提前解锁。
      if (!this.recoveryBusy) this.clearBusy();


      if (payload.mirrorType) {
        this.applyLibrarySettings({
          githubMirrorType: payload.mirrorType,
          customGitHubMirrorUrl: payload.customMirrorUrl
        });
      }
      this.renderStatusHeader(status);
      this.renderUpdateInfo();
      this.renderGameDirectory(status);
      this.renderAdvancedWarnings(status);

      this.searchQuery = ($('modSearchInput')?.value || '').trim().toLowerCase();
      this.renderFilteredMods();
      this.renderModList($('modSdkContainer'), status.sdk || [], 'sdk');
      if (this.recoveryBusy) this.lockRecoveryButtons();
    },

    // ---------- 加载器状态（徽标 + 一句话 + 按钮） ----------
    renderStatusHeader(status) {
      const running = status.gameRunning === true;
      const installed = status.installed === true;
      const managed = status.loaderManaged === true;
      const newer = this.newerVersion(status.installedVersion);

      const badge = $('modStatusBadge');
      const icon = $('modStatusIcon');
      const title = $('modStatusTitle');
      const text = $('modStatusText');

      let badgeText = installed ? '已安装' : '未安装';
      let badgeClass = installed ? 'badge badge-update' : 'badge badge-muted';
      let iconText = installed ? 'extension' : 'search';
      let titleText = installed ? '加载器已就位' : '尚未安装加载器';

      if (status.bundleLoaderPresent === false) {
        badgeText = '缺少内置文件';
        badgeClass = 'badge badge-danger';
        iconText = 'cancel';
        titleText = '程序缺少内置加载器文件';
      } else if (status.loaderMissing) {
        badgeText = status.loaderDisabled ? '已停用' : 'DLL 缺失';
        badgeClass = 'badge badge-event';
        iconText = 'extension';
        titleText = status.canRestoreLoader ? '模组与配置已保留，可恢复加载器' : '加载器缺失且版本未知';
      } else if (installed && !managed) {
        badgeText = '文件不属于本工具';
        badgeClass = 'badge badge-danger';
        iconText = 'warning';
        titleText = '游戏目录里的 version.dll 不是本工具装的';
      } else if (installed && newer) {
        badgeText = '可更新';
        badgeClass = 'badge badge-event';
        iconText = 'upgrade';
        titleText = `可以更新到 v${newer}`;
      }
      if (running) badgeText += ' · 游戏运行中';

      if (badge) {
        badge.textContent = badgeText;
        badge.className = badgeClass;
      }
      if (icon) icon.innerHTML = Icons.svg(iconText);
      if (title) title.textContent = titleText;
      if (text) text.textContent = status.message || '';

      // 主按钮随状态换角色：没装 → 「安装加载器」是主操作；装了 → 主操作变成「更新」
      const installBtn = $('modInstallBtn');
      if (installBtn) {
        installBtn.innerHTML = installed ? Icons.svg('refresh') + ' 重新安装内置版本' : Icons.svg('download') + ' 安装加载器';
        installBtn.className = installed ? 'secondary-btn' : 'primary-btn';
      }
      const updateBtn = $('modDownloadUpdateBtn');
      if (updateBtn) {
        updateBtn.innerHTML = newer ? Icons.svg('upgrade') + ` 更新到 v${newer}` : Icons.svg('upgrade') + ' 从 GitHub 更新';
        updateBtn.className = newer ? 'primary-btn' : 'secondary-btn';
        updateBtn.classList.toggle('hidden', !installed && !status.loaderMissing);
      }
      const settingsBtn = $('modLoaderSettingsBtn');
      if (settingsBtn) settingsBtn.disabled = !installed && !status.loaderRootExists;
      $('modDisableLoaderBtn')?.classList.toggle('hidden', !installed || !managed);
      $('modRestoreLoaderBtn')?.classList.toggle('hidden', !status.loaderMissing);
      if ($('modRestoreLoaderBtn')) $('modRestoreLoaderBtn').disabled = !status.canRestoreLoader;
      if (this.recoveryBusy) this.lockRecoveryButtons();

      // 没装加载器时 doorstop_config.json 还不存在，变速无处可写 → 整块禁用（而不是让保存报错）
      this.setSpeedhackAvailable(installed || status.loaderRootExists);
    },

    // ---------- 版本三栏 + 结论 ----------
    renderUpdateInfo() {
      const status = this.modStatus || {};
      const installed = status.installed === true;
      const managed = status.loaderManaged === true;
      const running = status.gameRunning === true;
      const installedVersion = status.installedVersion || '';
      const embeddedVersion = status.embeddedVersion || '';
      const info = this.updateInfo;
      const latest = info.latest || '';
      const newer = this.newerVersion(installedVersion);

      // ① 已安装
      let installedState = 'is-current';
      let installedNote;
      if (status.loaderMissing) {
        installedState = 'is-unknown';
        installedNote = status.canRestoreLoader ? '已停用或缺失，恢复时使用此版本' : 'DLL 缺失，无法确定恢复版本';
      } else if (!installed) {
        installedState = 'is-unknown';
        installedNote = '点「安装加载器」把加载器写进游戏目录';
      } else if (!managed) {
        installedState = 'is-conflict';
        installedNote = '不是本工具装的文件 —— 见下方「高级与排障」';
      } else if (!installedVersion) {
        installedState = 'is-unknown';
        installedNote = '这份安装没有版本清单，重新安装一次即可记录';
      } else if (newer) {
        installedState = 'is-update';
        installedNote = `有新版本 v${latest} 可更新`;
      } else if (embeddedVersion && compareVersion(installedVersion, embeddedVersion) === 0) {
        installedNote = '与随程序内置的版本一致';
      } else if (embeddedVersion) {
        // 方向要说清楚：更新过的加载器比内置的新是正常的，比内置的旧则可以直接装回内置版
        installedNote = compareVersion(installedVersion, embeddedVersion) > 0
          ? `比内置的 v${embeddedVersion} 更新（从 GitHub 更新过）`
          : `比内置的 v${embeddedVersion} 旧 —— 点「重新安装内置版本」可装回内置版`;
      } else {
        installedNote = '已安装（内置版本号读取失败）';
      }
      setVersionCell('modInstalledCell', 'modInstalledVersion', 'modInstalledNote', {
        value: installed || status.loaderMissing ? (installedVersion ? `v${installedVersion}` : '未知') : '未安装',
        note: installedNote,
        state: installedState
      });

      // ② 随程序内置
      setVersionCell('modEmbeddedCell', 'modEmbeddedVersion', null, {
        value: embeddedVersion ? `v${embeddedVersion}` : '缺失',
        state: embeddedVersion ? '' : 'is-conflict'
      });

      // ③ GitHub 最新
      const checking = info.state === 'checking';
      let latestState = '';
      if (latest && installedVersion) {
        const cmp = compareVersion(latest, installedVersion);
        latestState = cmp > 0 ? 'is-update' : (cmp === 0 ? 'is-current' : '');
      }
      setVersionCell('modLatestCell', 'modLatestVersion', 'modLatestNote', {
        value: checking ? '检查中…' : (latest ? `v${latest}` : (info.error ? '检查失败' : '未检查')),
        note: info.error ? info.error : (latest ? 'GitHub 最新 Release' : '点「检查更新」向 GitHub 查询'),
        state: latestState
      });

      this.renderVerdict(status, { installed, managed, running, installedVersion, embeddedVersion, latest, newer });
    },

    /** 三栏下面那句「所以现在该怎么办」——把状态收成一个动作，避免用户自己拼三个版本号。 */
    renderVerdict(status, ctx) {
      const verdict = $('modVersionVerdict');
      if (!verdict) return;
      const textEl = $('modVersionVerdictText');
      const { installed, managed, installedVersion, embeddedVersion, latest, newer } = ctx;

      let stateClass = '';
      let text;
      if (status.bundleLoaderPresent === false) {
        stateClass = 'is-warn';
        text = '本程序缺少内置加载器文件，无法安装 —— 请使用完整发布包（推荐 portable.zip）。';
      } else if (!status.gameDirectory) {
        stateClass = 'is-idle';
        text = '还没找到游戏目录 —— 展开「高级与排障」点「自动检测」，或手动选择游戏 exe 所在的文件夹。';
      } else if (status.loaderMissing) {
        stateClass = 'is-idle';
        text = status.canRestoreLoader
          ? `version.dll 已停用或缺失，保留版本 ${installedVersion ? 'v' + installedVersion : '未知'}。点「恢复 version.dll」；本地文件缺失时下载对应版本发布包。`
          : '保留的组件无法确定加载器版本，请重新安装或更新加载器。';
      } else if (!installed) {
        stateClass = 'is-idle';
        text = '尚未安装加载器 —— 点「安装加载器」，加载器与内置模组会一起装进游戏目录。';
      } else if (!managed) {
        stateClass = 'is-warn';
        text = '游戏目录里的 version.dll 不是本工具安装的（可能是旧版独立变速器或第三方文件）——安装或更新前请先确认来源。';
      } else if (!installedVersion) {
        stateClass = 'is-update';
        text = '已安装，但这一份没有版本清单，读不出准确版本 —— 重新安装一次即可纳入版本管理。';
      } else if (newer) {
        stateClass = 'is-update';
        text = `发现新版本：已装 v${installedVersion} → 最新 v${latest}，点「更新到 v${latest}」。`;
      } else if (latest) {
        stateClass = 'is-ok';
        text = compareVersion(latest, installedVersion) === 0
          ? `已是最新版本 v${latest}。`
          : `已装的 v${installedVersion} 比 GitHub 上的 v${latest} 更新，无需更新。`;
      } else if (embeddedVersion && compareVersion(installedVersion, embeddedVersion) === 0) {
        stateClass = 'is-ok';
        text = `已安装 v${installedVersion}，与随程序内置的版本一致 —— 点「检查更新」可确认 GitHub 上有没有新版。`;
      } else if (embeddedVersion && compareVersion(installedVersion, embeddedVersion) > 0) {
        text = `已安装 v${installedVersion}，比随程序内置的 v${embeddedVersion} 更新（这份是从 GitHub 更新来的）—— 点「检查更新」看看还有没有更新的版本。`;
      } else if (embeddedVersion && compareVersion(installedVersion, embeddedVersion) < 0) {
        text = `已安装 v${installedVersion}，比随程序内置的 v${embeddedVersion} 旧 —— 想回到内置版本就点「重新安装内置版本」，想知道有没有新版就点「检查更新」。`;
      } else {
        text = `已安装 v${installedVersion} —— 点「检查更新」看看 GitHub 上有没有新版。`;
      }
      if (ctx.running) text += ' 游戏正在运行：安装 / 更新 / 卸载都要重启游戏才生效。';

      verdict.classList.remove('is-ok', 'is-update', 'is-warn', 'is-idle');
      if (stateClass) verdict.classList.add(stateClass);
      if (textEl) textEl.textContent = text;
    },

    /** 「检查更新」的结果（Host 在最后一步回推）。 */
    renderUpdateCheck(payload) {
      this.clearBusy();
      if (!payload) return;
      if (payload.error) {
        this.updateInfo = { state: 'done', latest: '', error: `检查失败：${payload.error}` };
        toast(`检查更新失败：${payload.error}`);
      } else {
        this.updateInfo = { state: 'done', latest: payload.latest || '', error: '' };
      }
      this.renderUpdateInfo();
    },

    recoveryBusy: false,
    recoveryDisabledButtons: [],
    startRecovery(restore) {
      if (this.recoveryBusy || this.busy || this.packageImportPending) { toast('请等待当前操作完成。'); return; }
      this.recoveryBusy = true;
      this.setBusy(restore ? 'modRestoreLoaderBtn' : 'modDisableLoaderBtn', restore ? '恢复中…' : '备份并删除中…');
      this.lockRecoveryButtons();
      post({
        type: restore ? 'modRestoreLoader' : 'modDisableLoader',
        mirrorType: $('modLoaderMirrorSelect')?.value,
        customMirrorUrl: ($('modLoaderCustomMirrorInput')?.value || '').trim()
      });
    },
    lockRecoveryButtons() {
      document.querySelectorAll('#modsView button, #modsView input, #modsView select').forEach(button => {
        if (!button.disabled) { this.recoveryDisabledButtons.push(button); button.disabled = true; }
      });
    },
    clearRecoveryBusy() {
      this.recoveryBusy = false;
      this.clearBusy();
      this.recoveryDisabledButtons.forEach(button => { if (button.isConnected) button.disabled = false; });
      this.recoveryDisabledButtons = [];
      if (this.modStatus) this.renderStatusHeader(this.modStatus);
    },

    // ---------- 按钮忙碌态（安装/卸载/检查/更新） ----------

    /**
     * 把按钮切成"进行中"：禁用 + 转圈 + 换文案。
     * 解除由宿主消息触发（modStatus / modUpdateCheck），失败路径走 error 消息；
     * 另外挂一个兜底定时器，万一宿主异常没回音，两分钟后自己恢复，不会把按钮永久锁死。
     */
    setBusy(buttonId, label) {
      this.clearBusy();
      const button = $(buttonId);
      if (!button) return;
      this.busy = { id: buttonId, html: button.innerHTML, wasDisabled: button.disabled };
      button.disabled = true;
      button.classList.add('is-busy');
      button.innerHTML = `<span class="btn-spinner" aria-hidden="true"></span><span>${esc(label)}</span>`;
      this.busyTimer = setTimeout(() => this.clearBusy(), 120000);
    },
    clearBusy() {
      clearTimeout(this.busyTimer);
      this.busyTimer = null;
      const busy = this.busy;
      this.busy = null;
      if (!busy) return;
      const button = $(busy.id);
      if (!button) return;
      button.innerHTML = busy.html;
      button.disabled = busy.wasDisabled;
      button.classList.remove('is-busy');
    },

    // ---------- 游戏目录 / 高级区提示 ----------
    renderGameDirectory(status) {
      const dirText = $('modGameDirText');
      if (!dirText) return;
      if (status.gameDirectory) {
        dirText.textContent = status.gameDirectory;
        dirText.classList.remove('placeholder');
        dirText.title = '双击在资源管理器中打开';
      } else {
        dirText.textContent = '尚未选择游戏目录';
        dirText.classList.add('placeholder');
        dirText.title = '';
      }
    },

    /** 只有真出问题（文件不属于本工具 / 缺内置资源）时才在高级区给出处理办法。 */
    renderAdvancedWarnings(status) {
      const box = $('modAdvancedWarn');
      if (!box) return;
      const messages = [];
      if (status.installed && status.loaderManaged !== true) {
        messages.push('游戏目录里的 version.dll 既不是本程序内置的那一份，也没有记录在安装清单里：'
          + '多半是别的工具（例如「游戏工具」页的旧版独立变速器）或第三方放进来的文件。'
          + '要换成 CesiumLoader，勾选下面的「允许覆盖游戏目录里不属于本工具的 version.dll」，再点「安装加载器」；'
          + '只想清掉就勾选「强制卸载」。');
      }
      if (status.bundleLoaderPresent === false) {
        messages.push('本程序缺少内置加载器资源，安装按钮不会生效 —— 请重新下载完整发布包（推荐 portable.zip，single-file 版偶发解包不完整）。');
      }
      box.innerHTML = messages.map(m => `<p>${esc(m)}</p>`).join('');
      box.classList.toggle('hidden', messages.length === 0);
    },

    // ---------- 模组列表 ----------
  });
})();
