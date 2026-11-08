// i18n/engine.js - Lightweight internationalization engine for AstralParty.Toys
(function () {
  'use strict';
  const LANGUAGES = {
    'zh-Hans': { name: '简体中文', locale: 'zh-CN' },
    'en': { name: 'English', locale: 'en-US' },
    'ja': { name: '日本語', locale: 'ja-JP' },
    'zh-Hant': { name: '繁體中文', locale: 'zh-TW' }
  };

  const TRANSLATIONS = window.I18N_DICTS || {};

  // Source-text catalog (gettext style): keys are the Simplified Chinese strings
  // that ship in the HTML and JS. It lets the UI translate text that is written
  // directly in markup or assembled by page modules without hand-annotating
  // hundreds of elements. `js/i18n.source.js` provides the data.
  const SOURCE_TEXT = window.I18N_SOURCE || {};
  const SOURCE_PATTERNS = window.I18N_PATTERNS || {};
  const SOURCE_TEMPLATES = window.I18N_TEMPLATES || {};
  const SKIP_TAGS = new Set(['SCRIPT', 'STYLE', 'TEXTAREA', 'INPUT', 'CODE', 'PRE']);
  const SKIP_SELECTOR = '[data-i18n],[data-i18n-html],[data-i18n-title],[data-i18n-placeholder],[data-i18n-skip]';

  function normalizeSource(text) {
    return String(text ?? '').replace(/\u00a0/g, ' ').replace(/\s+/g, ' ').trim();
  }

  const I18n = {
    lang: 'zh-Hans',
    _textSources: new Map(),
    _attrSources: new Map(),
    _observer: null,
    _switching: false,
    _titleSource: null,
    _templateLang: null,
    _templateList: [],

    init() {
      const stored = window.localStorage?.getItem('appLanguage');
      if (stored && LANGUAGES[stored]) {
        this.lang = stored;
      } else {
        this.lang = this.detectSystemLanguage();
      }
      this.updateHtmlLang();
      this.localizeDom(document.body);
      this.observe();
    },

    detectSystemLanguage() {
      const navLang = (navigator.language || navigator.userLanguage || '').toLowerCase();
      if (navLang.startsWith('ja')) return 'ja';
      if (navLang.includes('tw') || navLang.includes('hk') || navLang.includes('mo') || navLang.includes('hant')) {
        return 'zh-Hant';
      }
      if (navLang.startsWith('zh')) return 'zh-Hans';
      if (navLang.startsWith('en')) return 'en';
      return 'zh-Hans';
    },

    updateHtmlLang() {
      document.documentElement.lang = LANGUAGES[this.lang]?.locale || 'zh-CN';
    },

    currentLocale() {
      return LANGUAGES[this.lang]?.locale || 'zh-CN';
    },

    setLanguage(lang, persist = true) {
      if (!LANGUAGES[lang] || lang === this.lang) return;
      this._switching = true;
      this.restoreSources();
      this.lang = lang;
      this.updateHtmlLang();
      if (persist) {
        window.localStorage?.setItem('appLanguage', lang);
      }
      this.applyDOM();
      this._switching = false;
      this.localizeDom(document.body);
      window.dispatchEvent(new CustomEvent('appLanguageChanged', { detail: { lang } }));
    },

    /** Translate a plain source string (Chinese) into the current language. */
    translateSource(value) {
      const text = String(value ?? '');
      const source = normalizeSource(text);
      if (!source || this.lang === 'zh-Hans') return text;
      const dict = SOURCE_TEXT[this.lang];
      if (dict && Object.prototype.hasOwnProperty.call(dict, source)) {
        const translated = dict[source];
        if (translated) return translated;
      }
      const templates = this._compiledTemplates();
      if (templates.length) {
        for (const item of templates) {
          const match = item.re.exec(source);
          if (!match) continue;
          return item.t.replace(/\{(\d+)\}/g, (_, n) => this._translateExact(match[Number(n) + 1] ?? ''));
        }
      }
      const patterns = SOURCE_PATTERNS[this.lang];
      if (Array.isArray(patterns)) {
        for (const entry of patterns) {
          const re = entry?.[0];
          if (!(re instanceof RegExp) || !re.test(source)) continue;
          return source.replace(re, entry[1]);
        }
      }
      return text;
    },

    _translateExact(value) {
      const source = normalizeSource(value);
      if (!source || this.lang === 'zh-Hans') return value;
      const dict = SOURCE_TEXT[this.lang];
      const translated = dict && Object.prototype.hasOwnProperty.call(dict, source) ? dict[source] : '';
      return translated || value;
    },

    _compiledTemplates() {
      if (this._templateLang === this.lang) return this._templateList;
      this._templateLang = this.lang;
      const list = SOURCE_TEMPLATES[this.lang];
      if (!Array.isArray(list)) {
        this._templateList = [];
        return this._templateList;
      }
      const escape = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
      this._templateList = list
        .filter(item => item && typeof item.s === 'string' && typeof item.t === 'string')
        .map(item => {
          const parts = item.s.split(/\{\d+\}/);
          const re = new RegExp('^' + parts.map(escape).join('([\\s\\S]+?)') + '$');
          const literalLength = parts.join('').length;
          return { re, t: item.t, literalLength };
        })
        .sort((a, b) => b.literalLength - a.literalLength);
      return this._templateList;
    },

    _translateValue(value) {
      const match = String(value ?? '').match(/^(\s*)([\s\S]*?)(\s*)$/);
      if (!match) return value;
      const translated = this.translateSource(match[2]);
      return translated === match[2] ? value : match[1] + translated + match[3];
    },

    _hasHan(value) {
      return /[\u3400-\u9fff]/.test(String(value ?? ''));
    },

    localizeTextNode(node) {
      if (this.lang === 'zh-Hans' || !node || node.nodeType !== Node.TEXT_NODE || this._switching) return;
      const parent = node.parentElement;
      if (!parent || SKIP_TAGS.has(parent.tagName)) return;
      if (parent.closest(SKIP_SELECTOR)) return;
      const current = node.nodeValue;
      const cached = this._textSources.get(node);
      const source = cached ?? current;
      if (!this._hasHan(source)) {
        if (cached) this._textSources.delete(node);
        return;
      }
      const desired = this._translateValue(source);
      if (desired !== source) this._textSources.set(node, source);
      else if (cached) this._textSources.delete(node);
      const output = desired === source ? desired : this._padEnglishBoundary(node, desired);
      if (node.nodeValue !== output) node.nodeValue = output;
    },

    _isInlineElement(el) {
      return !!el && el.nodeType === Node.ELEMENT_NODE &&
        ['B', 'STRONG', 'CODE', 'SPAN', 'A', 'EM', 'I', 'SMALL', 'LABEL'].includes(el.tagName);
    },

    _boundaryChar(node, direction) {
      const sibling = direction < 0 ? node.previousSibling : node.nextSibling;
      if (!sibling) return '';
      if (sibling.nodeType === Node.TEXT_NODE) {
        const text = this._translateValue(sibling.nodeValue);
        return direction < 0 ? text.trimEnd().slice(-1) : text.trimStart().slice(0, 1);
      }
      if (this._isInlineElement(sibling)) {
        const text = this._translateValue(sibling.textContent || '');
        return direction < 0 ? text.trimEnd().slice(-1) : text.trimStart().slice(0, 1);
      }
      return '';
    },

    /** English needs a space where Chinese markup butts text against an inline <b>/<code>. */
    _padEnglishBoundary(node, text) {
      if (this.lang !== 'en' || !text) return text;
      let out = text;
      if (!/^\s/.test(out) && /^[A-Za-z0-9]/.test(out) && /[A-Za-z0-9]$/.test(this._boundaryChar(node, -1))) {
        out = ' ' + out;
      }
      if (!/\s$/.test(out) && /[A-Za-z0-9]$/.test(out) && /^[A-Za-z0-9]/.test(this._boundaryChar(node, 1))) {
        out = out + ' ';
      }
      return out;
    },

    localizeAttributes(el) {
      if (this.lang === 'zh-Hans' || !el || el.nodeType !== Node.ELEMENT_NODE || this._switching) return;
      if (el.closest(SKIP_SELECTOR)) return;
      const stored = this._attrSources.get(el) || {};
      let changed = false;
      for (const attr of ['title', 'placeholder', 'aria-label', 'alt']) {
        const current = el.getAttribute(attr);
        if (current == null) continue;
        const source = stored[attr] ?? current;
        if (!this._hasHan(source)) {
          if (stored[attr]) { delete stored[attr]; changed = true; }
          continue;
        }
        const desired = this._translateValue(source);
        if (desired !== source) stored[attr] = source;
        else if (stored[attr]) delete stored[attr];
        if (current !== desired) el.setAttribute(attr, desired);
      }
      if (Object.keys(stored).length) this._attrSources.set(el, stored);
      else if (changed) this._attrSources.delete(el);
    },

    /** Translate text nodes and UI attributes below `root` (document.body by default). */
    localizeDom(root = document.body) {
      if (!root || this._switching || this.lang === 'zh-Hans') return;
      if (root.nodeType === Node.TEXT_NODE) {
        this.localizeTextNode(root);
        return;
      }
      if (root.nodeType !== Node.ELEMENT_NODE && root.nodeType !== Node.DOCUMENT_NODE) return;

      if (root.nodeType === Node.ELEMENT_NODE) this.localizeAttributes(root);
      const doc = root.ownerDocument || document;
      const walker = doc.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
        acceptNode: node => {
          const parent = node.parentElement;
          if (!parent || SKIP_TAGS.has(parent.tagName) || parent.closest(SKIP_SELECTOR)) {
            return NodeFilter.FILTER_REJECT;
          }
          return this._hasHan(node.nodeValue) ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_SKIP;
        }
      });
      const nodes = [];
      while (walker.nextNode()) nodes.push(walker.currentNode);
      nodes.forEach(node => this.localizeTextNode(node));

      root.querySelectorAll?.('[title],[placeholder],[aria-label],[alt]').forEach(el => this.localizeAttributes(el));
      if (root === document.body) this.localizeDocumentTitle();
    },

    localizeDocumentTitle() {
      const current = document.title;
      const source = this._titleSource ?? current;
      const translated = this._translateValue(source);
      if (translated !== source) this._titleSource = source;
      if (translated !== current) document.title = translated;
    },

    restoreSources() {
      if (this._titleSource) document.title = this._titleSource;

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
