// replay/index.js - Language-change observer
(function () {
  'use strict';
  window.addEventListener('appLanguageChanged', () => {
    if (window.ReplayModule.libraryData) {
      window.ReplayModule.renderLibraryRows();
    }
    if (window.AppState.replayReport) {
      window.ReplayModule.renderReport(window.AppState.replayReport);
    }
  });
})();
