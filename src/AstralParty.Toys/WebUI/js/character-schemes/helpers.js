// character-schemes/helpers.js - Local helpers (esc, field)
(() => {
  'use strict';
  const esc = (value) =>
    String(value ?? "").replace(
      /[&<>"']/g,
      (c) =>
        ({
          "&": "&amp;",
          "<": "&lt;",
          ">": "&gt;",
          '"': "&quot;",
          "'": "&#39;",
        })[c],
    );
  const field = (o, key) =>
    o?.[key] ?? o?.[key[0].toUpperCase() + key.slice(1)];
  window.CharacterSchemesHelpers = { esc, field };
})();
