// audio-skins/state.js - AudioSkinsModule state
(() => {
  'use strict';
  const AudioSkinsModule = window.AudioSkinsModule = {
    profiles: [],
    activeSchemeId: 'default',
    selectedSchemeId: 'default',
    items: [],
    category: 'all',
    statusFilter: 'all',
    voiceChar: 'all',
    voiceSkin: 'all',
    keyword: '',
    pageSize: 48,
    currentPage: 1,
    currentAudio: null,
    currentPlayingId: null,

  };
})();
