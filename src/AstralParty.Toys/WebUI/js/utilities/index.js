// utilities/index.js - Utilities bootstrap (runs init when the page shell is present)
(function () {
  'use strict';
  if (document.getElementById('utilitiesView')) {
    window.UtilitiesModule.init();
  }
})();
