// settings.js - Settings & Preferences Page Logic (Light Theme)
(function () {
  const SettingsModule = {
    init(data) {
      this.bindEvents();
      this.render();
    },

    bindEvents() {
      $('reloadPortraitsBtn')?.addEventListener('click', () => {
        post({ type: 'getHomeData' });
        toast('正在重新扫描本地立绘素材库…');
      });

      $('openClassicWpfBtn')?.addEventListener('click', () => {
        post({ type: 'openClassic' });
      });

      $('animToggle')?.addEventListener('change', e => {
        document.body.classList.toggle('reduce-motion', !e.target.checked);
        toast(`界面动态效果已${e.target.checked ? '开启' : '关闭'}`);
      });
    },

    render() {
      const portraitsCount = AppState.homeData?.portraitsCount || 0;
      if ($('loadedPortraitsCount')) {
        $('loadedPortraitsCount').textContent = `已加载 ${portraitsCount} 张角色立绘`;
      }

      if ($('settingsReplayDir') && AppState.replayLibrary?.directory) {
        $('settingsReplayDir').textContent = AppState.replayLibrary.directory;
      }
    }
  };

  window.SettingsModule = SettingsModule;
})();
