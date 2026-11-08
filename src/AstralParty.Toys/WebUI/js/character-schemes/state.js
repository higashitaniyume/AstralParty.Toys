// character-schemes/state.js - CharacterSchemesModule state
(() => {
  'use strict';
  const CharacterSchemesModule = window.CharacterSchemesModule = {
    data: null,
    selected: null,
    pending: null,
    mode: "schemes",
    editing: null,
    editingPackage: null,
    creatingContext: null,
    awaitingPackages: new Map(),
  };
})();
