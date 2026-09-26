// home.js - Astral Party lobby & portraits
(function () {
  // localStorage 里记「绕过 Steam 启动」的选择（默认 false = 从 Steam 启动）
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
      $('btnMenuSpeedhack')?.addEventListener('click', () => showPage('utilities'));
      $('btnMenuTools')?.addEventListener('click', () => showPage('tools'));
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
      post({ type: 'launchGame', bypassSteam: this.isBypassSteam() });
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

    // 启动后给「从 Steam / 绕过 Steam」这组单选框上一小段冷却：
    // 游戏是在**启动早期**才读加载器的 doorstop 配置（Steam 绕过开关）。若这时用户手快、
    // 立刻切到另一种方式，切换会触发 setSteamBypass 改写那份配置，导致刚拉起的这一局"串味"
    // —— 明明从 Steam 启动，却被改成绕过（反之亦然）。冷却期内禁止切换即可干净地避免这个竞态。
    lockLaunchMode(ms) {
      const steamRadio = $('launchViaSteam');
      const bypassRadio = $('launchBypassSteam');
      const group = document.querySelector('.launch-mode-options');
      if (!steamRadio || !bypassRadio) return;
      steamRadio.disabled = true;
      bypassRadio.disabled = true;
      if (group) {
        group.classList.add('is-cooldown');
        group.title = '游戏启动中，稍候即可再切换启动方式';
      }
      clearTimeout(this._launchModeCooldownTimer);
      this._launchModeCooldownTimer = setTimeout(() => {
        steamRadio.disabled = false;
        bypassRadio.disabled = false;
        if (group) {
          group.classList.remove('is-cooldown');
          group.removeAttribute('title');
        }
      }, ms);
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
    }
  };

  window.HomeModule = HomeModule;
})();
