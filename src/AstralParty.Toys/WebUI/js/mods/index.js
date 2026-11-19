// mods/index.js - Bootstrap
(function () {
  'use strict';
  if (!host.isLinux && document.getElementById('modsView')) {
    window.ModsModule.init();
  }
})();
