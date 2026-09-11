// home.js - Astral Party lobby, portraits, and JSON-driven version announcement
(function () {
  const ANNOUNCEMENT_URL = 'data/game-version-announcement.json';

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
      $('btnMenuLaunchGame')?.addEventListener('click', () => post({ type: 'launchGame' }));
      $('btnMenuSpeedhack')?.addEventListener('click', () => showPage('utilities'));
      $('btnMenuTools')?.addEventListener('click', () => showPage('tools'));
      $('btnMenuWiki')?.addEventListener('click', () => showPage('wiki'));
      $('dockBtnSettings')?.addEventListener('click', () => showPage('settings'));
      $('versionAnnouncementMoreBtn')?.addEventListener('click', () => this.showVersionAnnouncement());
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
