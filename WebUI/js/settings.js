// settings.js - Settings & Preferences Page Logic
(function () {
  const SettingsModule = {
    init() {
      if (this.initialized) return;
      this.initialized = true;

      const animToggle = $('animToggle');
      if (!animToggle) return;

      const appVersion = AppState.homeData?.appVersion;
      if ($('aboutVersion')) $('aboutVersion').textContent = appVersion ? `v${appVersion}` : '未知';

      const motionEnabled = window.localStorage.getItem('motionEnabled') !== 'false';
      animToggle.checked = motionEnabled;
      document.body.classList.toggle('reduce-motion', !motionEnabled);
      animToggle.addEventListener('change', e => {
        document.body.classList.toggle('reduce-motion', !e.target.checked);
        window.localStorage.setItem('motionEnabled', String(e.target.checked));
        toast(`界面动态效果已${e.target.checked ? '开启' : '关闭'}`);
      });

      $('aboutRepositoryBtn')?.addEventListener('click', () => {
        window.open('https://github.com/higashitaniyume/AstralParty.Toys', '_blank', 'noopener');
      });
      $('aboutSpeedhackBtn')?.addEventListener('click', () => {
        window.open('https://github.com/Hirtol/speedhack-rs', '_blank', 'noopener');
      });
    }
  };

  window.SettingsModule = SettingsModule;
})();
