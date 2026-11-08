// audio-skins/helpers.js - Shared HTML escaping helper
(() => {
  'use strict';
  function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }
  window.AudioSkinsHelpers = { escapeHtml };
})();
