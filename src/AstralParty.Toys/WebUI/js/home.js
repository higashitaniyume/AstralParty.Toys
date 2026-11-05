// home.js - Astral Party lobby & portraits
(function () {
  // localStorage 里记「启动方式」的选择（默认 steam）
  const LAUNCH_MODE_KEY = 'launchMode';
  const LAUNCH_BYPASS_KEY = 'launchBypassSteam';

  const HomeModule = {
    portraitRotationTimer: null,
    eventsBound: false,

    init(data) {
      if (!data) return;
      this.pickInitialPortrait();
      this.startPortraitRotation();
      this.bindEvents();
    },

    onShowHome() {
      if (!AppState.currentPortrait && AppState.homeData?.portraits?.length) {
        this.pickInitialPortrait();
      }
    },

    bindEvents() {
      if (this.eventsBound) return;
      this.eventsBound = true;
      $('btnMenuLaunchGame')?.addEventListener('click', () => this.launchGame());
      $('btnMenuTools')?.addEventListener('click', () => showPage('tools'));
      $('btnMenuSkins')?.addEventListener('click', () => showPage('skins'));
      $('btnMenuMods')?.addEventListener('click', () => showPage('mods'));
      $('dockBtnSettings')?.addEventListener('click', () => showPage('settings'));
      // 大厅「启动版本」下拉：选一个版本即设为当前游戏（下面两种启动方式都对它生效）。
      // 选项由 GameLibModule.renderLobbySelect 填充（宿主 ready 时推 gameProfiles）。
      $('lobbyGameSelect')?.addEventListener('change', event => {
        const id = event.target.value;
        if (id) post({ type: 'gameProfileSetActive', id });
      });
      this.initLaunchMode();
    },

    // 启动游戏：游戏要好几秒才起得来，期间进程还查不到，宿主也只会回一句 toast。
    // 所以按钮先短暂锁住 + 转圈，挡掉"以为没点上又点一次"导致的重复拉起。
    launchGame() {
      const button = $('btnMenuLaunchGame');
      if (button?.dataset.busy === '1') return;
      const mode = this.getLaunchMode();
      post({ type: 'launchGame', mode, bypassSteam: mode === 'bypassSteam' });
      // 启动瞬间就把「启动方式」锁一段时间：见 lockLaunchMode 的说明（防止刚起的这局被改写配置串味）。
      this.lockLaunchMode(6000);
      if (!button) return;
      button.dataset.busy = '1';
      button.classList.add('is-launching');
      clearTimeout(this._launchTimer);
      this._launchTimer = setTimeout(() => {
        button.dataset.busy = '0';
        button.classList.remove('is-launching');
      }, 5000);
    },

    // 启动后给启动方式单选框上一小段冷却：
    // 游戏是在**启动早期**才读加载器的 doorstop 配置（绕过开关）。若这时用户手快、
    // 立刻切到另一种方式，切换会触发 setLaunchMode 改写配置，导致刚拉起的这一局"串味"。
    // 冷却期内禁止切换即可干净地避免这个竞态。
    lockLaunchMode(ms) {
      const radios = document.querySelectorAll('input[name="launchMode"]');
      const group = document.querySelector('.launch-mode-options');
      radios.forEach(r => { r.disabled = true; });
      if (group) {
        group.classList.add('is-cooldown');
        group.title = '游戏启动中，稍候即可再切换启动方式';
      }
      clearTimeout(this._launchModeCooldownTimer);
      this._launchModeCooldownTimer = setTimeout(() => {
        radios.forEach(r => { r.disabled = false; });
        if (group) {
          group.classList.remove('is-cooldown');
          group.removeAttribute('title');
        }
      }, ms);
    },

    // ---------- 启动方式：从 Steam 启动 / 绕过 Steam / 从 TapTap / 绕过 TapTap ----------
    initLaunchMode() {
      const radios = document.querySelectorAll('input[name="launchMode"]');
      if (!radios.length) return;

      let mode = window.localStorage.getItem(LAUNCH_MODE_KEY);
      if (!mode) {
        mode = window.localStorage.getItem(LAUNCH_BYPASS_KEY) === 'true' ? 'bypassSteam' : 'steam';
      }
      this.setLaunchMode(mode, false);

      radios.forEach(radio => radio.addEventListener('change', () => {
        if (!radio.checked) return;
        this.setLaunchMode(radio.value, true);
      }));
    },

    getLaunchMode() {
      const checked = document.querySelector('input[name="launchMode"]:checked');
      return checked?.value || 'steam';
    },

    isBypassSteam() {
      return this.getLaunchMode() === 'bypassSteam';
    },

    setLaunchMode(mode, postSync = true) {
      const validModes = ['steam', 'bypassSteam', 'taptap', 'bypassTapTap'];
      const targetMode = validModes.includes(mode) ? mode : 'steam';
      const radio = document.querySelector(`input[name="launchMode"][value="${targetMode}"]`);
      if (radio) radio.checked = true;

      window.localStorage.setItem(LAUNCH_MODE_KEY, targetMode);
      window.localStorage.setItem(LAUNCH_BYPASS_KEY, String(targetMode === 'bypassSteam'));

      this.refreshLaunchModeUi();

      if (postSync) {
        post({ type: 'setLaunchMode', mode: targetMode });
      }
    },

    refreshLaunchModeUi() {
      const mode = this.getLaunchMode();
      $('launchModeSteam')?.classList.toggle('is-active', mode === 'steam');
      $('launchModeBypassSteam')?.classList.toggle('is-active', mode === 'bypassSteam');
      $('launchModeBypass')?.classList.toggle('is-active', mode === 'bypassSteam');
      $('launchModeTapTap')?.classList.toggle('is-active', mode === 'taptap');
      $('launchModeBypassTapTap')?.classList.toggle('is-active', mode === 'bypassTapTap');

      const button = $('btnMenuLaunchGame');
      if (button) {
        const titles = {
          steam: '启动 Astral Party（吉星派对）· 从 Steam 启动',
          bypassSteam: '启动 Astral Party（吉星派对）· 绕过 Steam：直接启动游戏主程序',
          taptap: '启动 Astral Party（吉星派对）· 从 TapTap 启动',
          bypassTapTap: '启动 Astral Party（吉星派对）· 绕过 TapTap：直接启动游戏主程序'
        };
        button.title = titles[mode] || titles.steam;
      }
    },

    // 反向同步：加载器配置同步到首页单选框
    applySteamBypassSync(payload) {
      if (!payload || typeof payload.enabled !== 'boolean') return;
      this.setLaunchMode(payload.enabled ? 'bypassSteam' : 'steam', false);
    },

    applyLaunchModeSync(payload) {
      if (!payload || typeof payload.mode !== 'string') return;
      this.setLaunchMode(payload.mode, false);
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
    }
  };

  window.HomeModule = HomeModule;
})();
