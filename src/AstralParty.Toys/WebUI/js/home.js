// home.js - Astral Party lobby, portraits, and JSON-driven version announcement
(function () {
  const ANNOUNCEMENT_URL = 'data/game-version-announcement.json';

  // localStorage 里记「绕过 Steam 启动」的选择（默认 false = 从 Steam 启动）
  const LAUNCH_BYPASS_KEY = 'launchBypassSteam';

  const HomeModule = {
    portraitRotationTimer: null,
    versionAnnouncement: null,
    eventsBound: false,

    init(data) {
      if (!data) return;
      this.pickInitialPortrait();
      this.startPortraitRotation();
      this.bindEvents();
      this.loadVersionAnnouncement();
    },

    onShowHome() {
      if (!AppState.currentPortrait && AppState.homeData?.portraits?.length) {
        this.pickInitialPortrait();
      }
    },

    bindEvents() {
      if (this.eventsBound) return;
      this.eventsBound = true;
      $('btnMenuLaunchGame')?.addEventListener('click', () => post({
        type: 'launchGame',
        bypassSteam: this.isBypassSteam()
      }));
      $('btnMenuSpeedhack')?.addEventListener('click', () => showPage('utilities'));
      $('btnMenuTools')?.addEventListener('click', () => showPage('tools'));
      $('btnMenuMods')?.addEventListener('click', () => showPage('mods'));
      $('btnMenuWiki')?.addEventListener('click', () => showPage('wiki'));
      $('dockBtnSettings')?.addEventListener('click', () => showPage('settings'));
      $('versionAnnouncementMoreBtn')?.addEventListener('click', () => this.showVersionAnnouncement());
      this.initLaunchMode();
    },

    // ---------- 启动方式：从 Steam 启动 / 绕过 Steam 启动（单选框，二选一） ----------
    //
    // 「绕过 Steam 启动」和「模组 → 加载器设置」里的 steamBypassEnabled 是**同一个配置**：
    //   选它 → 启动游戏时直接拉起游戏主程序（完全不经过 Steam），并把加载器的 Steam 绕过开关写开
    //          —— 否则游戏本体发现"非 Steam 客户端启动"会在启动早期自己退出；
    //   选「从 Steam 启动」→ 仍旧交给 Steam 启动，加载器那个开关也一起关掉。
    // 默认：从 Steam 启动。选择记在 localStorage 里；进界面时宿主要会推一次配置里的当前值
    // （装了加载器就以 doorstop_config.json 为准，见 applySteamBypassSync），没装加载器则用本地记住的选择。
    initLaunchMode() {
      const steamRadio = $('launchViaSteam');
      const bypassRadio = $('launchBypassSteam');
      if (!steamRadio || !bypassRadio) return;

      const bypass = window.localStorage.getItem(LAUNCH_BYPASS_KEY) === 'true';
      steamRadio.checked = !bypass;
      bypassRadio.checked = bypass;
      this.refreshLaunchModeUi();

      // 同一组 name 的单选框天生互斥、也不可能两个都不选，所以直接听 change 就够了
      [steamRadio, bypassRadio].forEach(radio => radio.addEventListener('change', () => {
        if (!radio.checked) return;
        window.localStorage.setItem(LAUNCH_BYPASS_KEY, String(bypassRadio.checked));
        this.refreshLaunchModeUi();
        // 同步进加载器配置（宿主那边保留注释地只改这一个键）
        post({ type: 'setSteamBypass', enabled: bypassRadio.checked });
      }));
    },

    isBypassSteam() {
      return !!$('launchBypassSteam')?.checked;
    },

    refreshLaunchModeUi() {
      const bypass = this.isBypassSteam();
      $('launchModeSteam')?.classList.toggle('is-active', !bypass);
      $('launchModeBypass')?.classList.toggle('is-active', bypass);
      const button = $('btnMenuLaunchGame');
      if (button) {
        button.title = bypass
          ? '启动 Astral Party（吉星派对）· 绕过 Steam：直接启动游戏主程序'
          : '启动 Astral Party（吉星派对）· 从 Steam 启动';
      }
    },

    // 反向同步：在「加载器设置」里改了 Steam 绕过 → 首页单选框跟着走（同一个配置）
    applySteamBypassSync(payload) {
      if (!payload || typeof payload.enabled !== 'boolean') return;
      const steamRadio = $('launchViaSteam');
      const bypassRadio = $('launchBypassSteam');
      if (!steamRadio || !bypassRadio) return;

      bypassRadio.checked = payload.enabled;
      steamRadio.checked = !payload.enabled;
      window.localStorage.setItem(LAUNCH_BYPASS_KEY, String(payload.enabled));
      this.refreshLaunchModeUi();
    },

    pickInitialPortrait() {
      const portraits = AppState.homeData?.portraits;
      if (!portraits?.length) {
        const fallbackPortraits = [
          { id: 'UT_Hero_Card_306', heroId: 306, heroName: '橘雪莉', url: 'https://assets.astral.local/Portraits/UT_Hero_Card_306.png' },
          { id: 'UT_Hero_Card_305', heroId: 305, heroName: '远野汉娜', url: 'https://assets.astral.local/Portraits/UT_Hero_Card_305.png' }
        ];
        const portrait = fallbackPortraits[Math.floor(Math.random() * fallbackPortraits.length)];
        AppState.currentPortrait = portrait;
        this.applyPortrait(portrait);
        return;
      }
      this.pickRandomPortrait();
    },

    startPortraitRotation() {
      clearInterval(this.portraitRotationTimer);
      this.portraitRotationTimer = setInterval(() => {
        if (AppState.currentPage === 'home') this.pickRandomPortrait();
      }, 15000);
    },

    pickRandomPortrait() {
      const portraits = AppState.homeData?.portraits;
      if (!portraits?.length) return;
      const candidates = AppState.currentPortrait
        ? portraits.filter(portrait => portrait.id !== AppState.currentPortrait.id)
        : portraits;
      const pool = candidates.length ? candidates : portraits;
      const candidate = pool[Math.floor(Math.random() * pool.length)];
      AppState.currentPortrait = candidate;
      this.applyPortrait(candidate);
    },

    applyPortrait(item) {
      const img = $('heroPortraitImg');
      if (!img) return;
      img.style.opacity = '0';
      setTimeout(() => {
        img.src = item.url;
        img.alt = item.heroName;
        img.onload = () => { img.style.opacity = '1'; };
        img.onerror = () => {
          img.src = `https://assets.astral.local/Characters/${item.heroId}.webp`;
          img.style.opacity = '1';
        };
      }, 120);
    },

    async loadVersionAnnouncement() {
      try {
        const response = await fetch(ANNOUNCEMENT_URL, { cache: 'no-store' });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const announcement = await response.json();
        if (!announcement?.title || !announcement?.summary) throw new Error('公告数据不完整');
        this.versionAnnouncement = announcement;
        this.renderVersionAnnouncement(announcement);
      } catch (error) {
        console.error('Version announcement load error:', error);
        $('versionAnnouncementCard')?.classList.add('is-error');
        if ($('versionAnnouncementDate')) $('versionAnnouncementDate').textContent = '';
        if ($('versionAnnouncementTitle')) $('versionAnnouncementTitle').textContent = '版本公告读取失败';
        if ($('versionAnnouncementSummary')) $('versionAnnouncementSummary').textContent = `请检查 ${ANNOUNCEMENT_URL}`;
        if ($('versionAnnouncementPeriod')) $('versionAnnouncementPeriod').textContent = '';
        if ($('versionAnnouncementMoreBtn')) $('versionAnnouncementMoreBtn').disabled = true;
      }
    },

    renderVersionAnnouncement(item) {
      if ($('versionAnnouncementDate')) {
        $('versionAnnouncementDate').textContent = item.versionLabel || item.displayDate || '';
      }
      if ($('versionAnnouncementTitle')) $('versionAnnouncementTitle').textContent = item.title;
      if ($('versionAnnouncementSummary')) $('versionAnnouncementSummary').textContent = item.summary;
      if ($('versionAnnouncementPeriod')) $('versionAnnouncementPeriod').textContent = item.period || '';
      $('versionAnnouncementCard')?.classList.remove('is-error');
      if ($('versionAnnouncementMoreBtn')) $('versionAnnouncementMoreBtn').disabled = false;
    },

    safeExternalUrl(value) {
      try {
        const url = new URL(value);
        return url.protocol === 'https:' || url.protocol === 'http:' ? url.href : '';
      } catch {
        return '';
      }
    },

    showVersionAnnouncement() {
      const item = this.versionAnnouncement;
      if (!item) return;
      const highlights = Array.isArray(item.highlights) ? item.highlights : [];
      const sourceUrl = this.safeExternalUrl(item.source?.url);
      const highlightHtml = highlights.map(highlight => `
        <section class="version-detail-item">
          <h4>${esc(highlight.title)}</h4>
          <p>${esc(highlight.content)}</p>
        </section>`).join('');
      openModal(`📢 ${item.category ? esc(item.category) : '游戏版本公告'}`, `
        <div class="version-detail">
          <div class="version-detail-meta"><span>${esc(item.versionLabel || '')}</span><span>${esc(item.period || '')}</span></div>
          <h3>${esc(item.title)}</h3>
          <p class="version-detail-summary">${esc(item.summary)}</p>
          <div class="version-detail-list">${highlightHtml}</div>
          ${item.source?.note ? `<p class="version-detail-note">${esc(item.source.note)}</p>` : ''}
          ${sourceUrl ? '<button class="secondary-btn" id="versionAnnouncementSourceBtn">↗ 查看 BWiki 原文</button>' : ''}
        </div>`);
      $('versionAnnouncementSourceBtn')?.addEventListener('click', () => {
        post({ type: 'openBrowser', url: sourceUrl });
      });
    }
  };

  window.HomeModule = HomeModule;
})();
