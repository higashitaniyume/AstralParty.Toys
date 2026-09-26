// version.js - 本程序自己的版本信息（详情页顶部栏的版本胶囊 / 大厅底部状态栏 / 版本详情弹窗）
//
// 版本号全部来自宿主（C# 的 AppInfo）：程序集版本 + .NET SDK 写进去的提交号，
// 所以"自己 dotnet build 的"也一样说得清是哪一版、哪个提交、什么配置，
// 而不是给一个脱离上下文的数字。这里只负责显示，不猜任何版本。
(function () {
  // 纯浏览器里打开 index.html 预览时没有宿主：如实说"没有宿主"，而不是一直显示"读取中"
  const HOST_AVAILABLE = Boolean(window.chrome?.webview);
  const FALLBACK_DELAY = 1500;

  const VersionModule = {
    info: null,

    init() {
      $('appVersionChip')?.addEventListener('click', () => this.showDetails());
      $('dockVersionBtn')?.addEventListener('click', () => this.showDetails());
      $('aboutVersionDetailBtn')?.addEventListener('click', () => this.showDetails());
      $('aboutChangelogBtn')?.addEventListener('click', () => this.requestChangelog('app'));
      // 大厅「公告位」更新卡片：关闭（记住这一版不再提示）/ 查看完整日志
      $('lobbyWhatsNewClose')?.addEventListener('click', () => this.dismissWhatsNew());
      $('lobbyWhatsNewMore')?.addEventListener('click', () => this.requestChangelog('app'));
      if (!HOST_AVAILABLE) this.render(null);
      else setTimeout(() => { if (!this.info) this.render(null); }, FALLBACK_DELAY);
    },

    render(payload) {
      this.info = payload || null;
      const chipValue = $('appVersionChipValue');
      const chipCommit = $('appVersionChipCommit');
      const dockValue = $('dockVersionValue');

      if (!this.info) {
        if (chipValue) chipValue.textContent = '版本未知';
        if (chipCommit) chipCommit.textContent = '';
        if (dockValue) dockValue.textContent = '版本未知';
        this.renderAbout();
        return;
      }

      const info = this.info;
      const loader = this.loaderLabel(info.embeddedLoaderVersion);
      // 顶部栏窄，只放最少的：版本 + 提交（自建构建的关键识别信息，窄窗口会被 CSS 收起来）
      if (chipValue) chipValue.textContent = info.version ? `v${info.version}` : '版本未知';
      if (chipCommit) chipCommit.textContent = info.commit ? `· ${info.commit}` : '· 本地构建';
      if (dockValue) {
        const parts = [info.shortLabel || '版本未知', loader ? `加载器 ${loader}` : ''];
        dockValue.textContent = parts.filter(Boolean).join(' · ');
      }
      this.renderAbout();
      this.maybeShowWhatsNew();
    },

    /// 设置页「关于」里的几行。版本消息与设置页谁先到都有可能，所以两边都会调它。
    renderAbout() {
      const info = this.info;
      const set = (id, text) => { const el = $(id); if (el) el.textContent = text; };
      if (!info) {
        set('aboutVersion', '未知');
        set('aboutCommit', '未知');
        set('aboutRuntime', '未知');
        set('aboutLoader', '未知');
        return;
      }
      set('aboutVersion', (info.version ? `v${info.version}` : '未知') + (info.isLocalBuild ? '（本地构建）' : ''));
      set('aboutCommit', info.commitFull || '无 —— 不是从 git 仓库构建的');
      set('aboutRuntime', [info.runtime, info.architecture].filter(Boolean).join(' · ') || '未知');
      set('aboutLoader', this.loaderLabel(info.embeddedLoaderVersion) || '没有内嵌发布包');
    },

    /// 内置加载器版本：本地开发构建没内嵌发布包时返回空串（界面上就不显示这一段）
    loaderLabel(value) {
      return /^[0-9]/.test(String(value || '')) ? String(value) : '';
    },

    showDetails() {
      const info = this.info;
      if (!info) {
        openModal('版本信息', `
          <div class="version-info">
            <div class="version-info-hero">
              <div class="version-info-number">版本未知</div>
              <div class="version-info-sub">没有检测到桌面宿主，读不到程序集版本</div>
            </div>
            <p class="version-info-note">在 AstralParty.Toys 程序窗口里打开这一页就能看到版本信息。</p>
          </div>`);
        return;
      }

      const loader = this.loaderLabel(info.embeddedLoaderVersion);
      const rows = [
        ['程序集版本', info.fileVersion],
        ['源码提交', info.commitFull || '无 —— 不是从 git 仓库构建的'],
        ['构建配置', info.configuration + (info.isLocalBuild ? '（本地构建）' : '')],
        ['内置加载器', loader ? loader : '没有内嵌发布包（本地开发构建）'],
        ['运行时', [info.runtime, info.architecture].filter(Boolean).join(' · ')],
        ['操作系统', info.operatingSystem],
        ['程序集文件时间', info.assemblyTime],
        ['程序目录', info.appDirectory]
      ].filter(row => String(row[1] || '').trim() !== '');

      openModal('版本信息', `
        <div class="version-info">
          <div class="version-info-hero">
            <div class="version-info-number">${esc(info.version ? 'v' + info.version : '版本未知')}</div>
            <div class="version-info-sub">${esc(
              info.isLocalBuild
                ? '本地构建' + (info.commit ? ' · 提交 ' + info.commit : '')
                : (info.commit ? '提交 ' + info.commit : '')
            )}</div>
          </div>
          <dl class="version-info-rows">
            ${rows.map(([name, value]) => `
              <div class="version-info-row">
                <dt>${esc(name)}</dt>
                <dd>${esc(value)}</dd>
              </div>`).join('')}
          </dl>
          <p class="version-info-note">${esc(info.isLocalBuild
            ? '这是本地构建：版本号来自项目文件，提交号与文件时间说明它对应哪一次源码。'
            : '反馈问题时把这段信息一起贴上，能省很多来回。')}</p>
          <div class="version-info-actions">
            <button class="primary-btn" id="versionCopyBtn" type="button">复制版本信息</button>
          </div>
        </div>`);

      $('versionCopyBtn')?.addEventListener('click', () => post({ type: 'copyVersionInfo' }));
    },

    // ============================ 更新日志（弹窗 · 双 Tab） ============================
    // 工具箱（本程序内嵌 CHANGELOG.md）/ 加载器（CesiumLoader Release 说明：CI 内嵌 + 运行时兜底）。
    changelogMarkdown: null,       // 工具箱日志（宿主 changelog 消息）
    loaderChangelogMarkdown: null, // 加载器日志（宿主 loaderChangelog 消息）
    loaderSource: '',
    activeChangelogTab: 'app',
    pendingWhatsNew: false,

    /// 打开更新日志弹窗并切到指定 Tab（'app' | 'loader'）。
    requestChangelog(tab) {
      tab = tab === 'loader' ? 'loader' : 'app';
      if (!HOST_AVAILABLE) {
        openModal('更新日志', '<p class="changelog-empty">在 AstralParty.Toys 程序窗口里打开这一页才能查看更新日志。</p>');
        return;
      }
      this.activeChangelogTab = tab;
      openModal('更新日志', `
        <div class="changelog-tabs" role="tablist">
          <button class="changelog-tab${tab === 'app' ? ' is-active' : ''}" data-tab="app" role="tab">🧰 工具箱</button>
          <button class="changelog-tab${tab === 'loader' ? ' is-active' : ''}" data-tab="loader" role="tab">🧩 加载器</button>
        </div>
        <div class="changelog-body" id="changelogBody"><p class="changelog-empty">正在读取…</p></div>`);
      document.querySelectorAll('#globalModal .changelog-tab').forEach(btn =>
        btn.addEventListener('click', () => this.switchChangelogTab(btn.dataset.tab)));
      this.loadChangelogTab(tab);
    },

    switchChangelogTab(tab) {
      document.querySelectorAll('#globalModal .changelog-tab').forEach(b => b.classList.toggle('is-active', b.dataset.tab === tab));
      this.loadChangelogTab(tab);
    },

    loadChangelogTab(tab) {
      this.activeChangelogTab = tab;
      const body = document.querySelector('#globalModal .changelog-body');
      if (!body) return;
      if (tab === 'loader') {
        if (this.loaderChangelogMarkdown !== null) body.innerHTML = this.loaderChangelogHtml();
        else { body.innerHTML = '<p class="changelog-empty">正在读取加载器更新日志…</p>'; post({ type: 'loaderChangelogGet' }); }
      } else {
        if (this.changelogMarkdown !== null) body.innerHTML = this.appChangelogHtml();
        else { body.innerHTML = '<p class="changelog-empty">正在读取更新日志…</p>'; post({ type: 'changelogGet' }); }
      }
    },

    appChangelogHtml() {
      const md = this.changelogMarkdown || '';
      return md.trim() ? this.markdownToHtml(md) : '<p class="changelog-empty">读不到更新日志内容。</p>';
    },

    loaderChangelogHtml() {
      const md = this.loaderChangelogMarkdown || '';
      const sourceNote = this.loaderSource === 'github'
        ? '来源：GitHub 最新 Release'
        : (this.loaderSource === 'bundled' ? '来源：随本程序内置（打包时的版本）' : '');
      const foot = `<div class="changelog-foot">
          <span class="changelog-source">${md.trim() ? esc(sourceNote) : ''}</span>
          <button class="secondary-btn changelog-refresh" id="loaderChangelogRefresh" type="button">↻ 拉取最新</button>
        </div>`;
      const bodyHtml = md.trim()
        ? this.markdownToHtml(md)
        : '<p class="changelog-empty">读不到加载器更新日志（内置为空，联网获取也没成功）。点「↻ 拉取最新」重试。</p>';
      // 先渲染，再绑定按钮
      setTimeout(() => {
        $('loaderChangelogRefresh')?.addEventListener('click', () => {
          this.loaderChangelogMarkdown = null;
          const b = document.querySelector('#globalModal .changelog-body');
          if (b) b.innerHTML = '<p class="changelog-empty">正在从 GitHub 拉取最新加载器更新日志…</p>';
          post({ type: 'loaderChangelogGet', fresh: true });
        });
      }, 0);
      return bodyHtml + foot;
    },

    /// 宿主推来工具箱 CHANGELOG（Markdown）。
    renderChangelog(payload) {
      this.changelogMarkdown = (payload && payload.markdown) || '';
      const body = document.querySelector('#globalModal .changelog-body');
      if (body && this.activeChangelogTab === 'app') body.innerHTML = this.appChangelogHtml();
      if (this.pendingWhatsNew) { this.pendingWhatsNew = false; this.buildWhatsNew(); }
    },

    /// 宿主推来加载器 CHANGELOG（Markdown + source）。
    renderLoaderChangelog(payload) {
      this.loaderChangelogMarkdown = (payload && payload.markdown) || '';
      this.loaderSource = (payload && payload.source) || '';
      const body = document.querySelector('#globalModal .changelog-body');
      if (body && this.activeChangelogTab === 'loader') body.innerHTML = this.loaderChangelogHtml();
    },

    // ======================== 大厅「公告位」：更新卡片（按版本只弹一次） ========================
    // 存 changelogSeenVersion：当前版本 != 存的值就显示；点「知道了」写入当前版本；更新后自然再现一次。
    maybeShowWhatsNew() {
      if (!HOST_AVAILABLE) return;
      const v = this.info?.version;
      if (!v) return;
      if (window.localStorage.getItem('changelogSeenVersion') === v) return;
      if (this.changelogMarkdown !== null) { this.buildWhatsNew(); return; }
      this.pendingWhatsNew = true;
      post({ type: 'changelogGet' }); // 取来后 renderChangelog 里会 buildWhatsNew
    },

    buildWhatsNew() {
      const card = $('lobbyWhatsNew');
      const v = this.info?.version;
      if (!card || !v) return;
      if (window.localStorage.getItem('changelogSeenVersion') === v) return;
      const titleEl = $('lobbyWhatsNewTitle');
      if (titleEl) titleEl.textContent = 'v' + v + (this.info?.isLocalBuild ? '（本地构建）' : '');
      const list = $('lobbyWhatsNewList');
      if (list) {
        const highlights = this.latestHighlights(this.changelogMarkdown || '').slice(0, 4);
        list.innerHTML = (highlights.length ? highlights : ['点「查看完整更新日志」了解本次更新'])
          .map(h => `<li>${esc(h)}</li>`).join('');
      }
      card.classList.remove('hidden');
    },

    dismissWhatsNew() {
      const v = this.info?.version;
      if (v) window.localStorage.setItem('changelogSeenVersion', v);
      $('lobbyWhatsNew')?.classList.add('hidden');
    },

    /// 从 CHANGELOG 抽「最新一节」（第一个 ## 版本块）下面的各个 ### 小标题当作要点。
    latestHighlights(md) {
      const lines = md.replace(/\r\n/g, '\n').split('\n');
      const out = [];
      let inFirst = false;
      for (const line of lines) {
        if (/^##\s+/.test(line) && !/^###/.test(line)) {
          if (inFirst) break; // 到了第二个版本块就停
          inFirst = true;
          continue;
        }
        if (inFirst && /^###\s+/.test(line)) out.push(line.replace(/^###\s+/, '').trim());
      }
      return out;
    },

    /// 极简 Markdown → HTML：只覆盖 CHANGELOG 用到的语法（# / ## / ###、- 列表、**加粗**、`代码`）。
    /// 先整体转义 HTML，再做内联替换，杜绝注入。
    markdownToHtml(md) {
      const inline = (s) => esc(s)
        .replace(/`([^`]+)`/g, '<code>$1</code>')
        .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
      const lines = md.replace(/\r\n/g, '\n').split('\n');
      const out = [];
      let inList = false;
      const closeList = () => { if (inList) { out.push('</ul>'); inList = false; } };
      for (const raw of lines) {
        const line = raw.replace(/\s+$/, '');
        if (/^###\s+/.test(line)) { closeList(); out.push(`<h4 class="changelog-h3">${inline(line.replace(/^###\s+/, ''))}</h4>`); }
        else if (/^##\s+/.test(line)) { closeList(); out.push(`<h3 class="changelog-h2">${inline(line.replace(/^##\s+/, ''))}</h3>`); }
        else if (/^#\s+/.test(line)) { closeList(); out.push(`<h2 class="changelog-h1">${inline(line.replace(/^#\s+/, ''))}</h2>`); }
        else if (/^\s*[-*]\s+/.test(line)) {
          if (!inList) { out.push('<ul class="changelog-list">'); inList = true; }
          out.push(`<li>${inline(line.replace(/^\s*[-*]\s+/, ''))}</li>`);
        }
        else if (line.trim() === '') { closeList(); }
        else {
          // 列表项的续行（缩进后的补充说明）并进上一个 <li>，否则作为普通段落
          if (inList) { const li = out.pop(); out.push(li.replace(/<\/li>$/, ' ' + inline(line.trim()) + '</li>')); }
          else out.push(`<p class="changelog-p">${inline(line.trim())}</p>`);
        }
      }
      closeList();
      return `<div class="changelog">${out.join('')}</div>`;
    }
  };

  document.addEventListener('DOMContentLoaded', () => VersionModule.init());
  window.VersionModule = VersionModule;
})();
