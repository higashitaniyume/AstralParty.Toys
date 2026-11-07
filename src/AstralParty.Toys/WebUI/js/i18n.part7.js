      for (const [node, source] of this._textSources) {
        if (!node.isConnected) { this._textSources.delete(node); continue; }
        if (node.nodeValue !== source) node.nodeValue = source;
      }
      for (const [el, attrs] of this._attrSources) {
        if (!el.isConnected) { this._attrSources.delete(el); continue; }
        for (const [attr, source] of Object.entries(attrs)) {
          if (el.getAttribute(attr) !== source) el.setAttribute(attr, source);
        }
      }
    },

    observe() {
      if (this._observer || !window.MutationObserver || !document.body) return;
      this._observer = new MutationObserver(records => {
        if (this._switching) return;
        const nodes = new Set();
        for (const record of records) {
          if (record.type === 'childList') {
            record.addedNodes.forEach(node => {
              if (node.nodeType === Node.TEXT_NODE || node.nodeType === Node.ELEMENT_NODE) nodes.add(node);
            });
          } else if (record.type === 'characterData') {
            nodes.add(record.target);
          } else if (record.type === 'attributes') {
            nodes.add(record.target);
          }
        }
        nodes.forEach(node => {
          if (node.nodeType === Node.TEXT_NODE) this.localizeTextNode(node);
          else if (node.nodeType === Node.ELEMENT_NODE) {
            this.localizeAttributes(node);
            this.localizeDom(node);
          }
        });
      });
      this._observer.observe(document.body, {
        childList: true,
        subtree: true,
        characterData: true,
        attributes: true,
        attributeFilter: ['title', 'placeholder', 'aria-label', 'alt']
      });
    },

    t(key, params) {
      const dict = TRANSLATIONS[this.lang] || TRANSLATIONS['zh-Hans'];
      let text = dict[key] ?? TRANSLATIONS['zh-Hans'][key] ?? key;
      if (params && typeof params === 'object') {
        Object.keys(params).forEach(p => {
          text = text.replace(new RegExp(`\\{${p}\\}`, 'g'), params[p]);
        });
      }
      return text;
    },

    applyDOM(root = document) {
      root.querySelectorAll('[data-i18n]').forEach(el => {
        const key = el.getAttribute('data-i18n');
        const val = this.t(key);
        if (val !== undefined) el.textContent = val;
      });

      root.querySelectorAll('[data-i18n-html]').forEach(el => {
        const key = el.getAttribute('data-i18n-html');
        const val = this.t(key);
        if (val !== undefined && val !== key) el.innerHTML = val;
      });

      root.querySelectorAll('[data-i18n-title]').forEach(el => {
        const key = el.getAttribute('data-i18n-title');
        const val = this.t(key);
        if (val !== undefined) el.title = val;
      });

      root.querySelectorAll('[data-i18n-placeholder]').forEach(el => {
        const key = el.getAttribute('data-i18n-placeholder');
        const val = this.t(key);
        if (val !== undefined) el.placeholder = val;
      });
    },

    getSupportedLanguages() {
      return Object.keys(LANGUAGES).map(code => ({
        code,
        name: LANGUAGES[code].name,
        locale: LANGUAGES[code].locale
      }));
    }
  };

  I18n.init();
  window.i18n = I18n;
  window.t = (key, params) => I18n.t(key, params);
})();
