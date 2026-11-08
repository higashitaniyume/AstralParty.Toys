// core/dom.js - DOM helpers ($, esc, fmt)
const $ = id => document.getElementById(id);
const esc = value => String(value ?? '').replace(/[&<>'"]/g, c => ({
  '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;'
}[c]));
const fmt = value => Number(value ?? 0).toLocaleString(window.i18n?.currentLocale() || 'zh-CN');
