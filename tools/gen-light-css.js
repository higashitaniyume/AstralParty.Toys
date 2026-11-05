// gen-light-css.js — 从原版样式生成浅色模式的覆盖文件 WebUI/css/v2-light.css。
// 原版 CSS 里写死了几百处深色主题的颜色，手改会漏，合并作者新版后还要重来。这个脚本读原版样式，
// 把写死的颜色换成浅色对应的值，不改原文件。合并作者新版后重新运行一次：node tools/gen-light-css.js
//
// 选择器前面加的是 :where(...)：它不增加优先级，所以生成的规则和原版优先级相同、靠加载顺序盖住原版，
// 而后加载的 v2.css 仍然能盖住它。index.html 里的顺序必须是：原版样式 → v2-light.css → v2.css。
const fs = require('fs');
const path = require('path');

const WEB = path.join(__dirname, '..', 'src', 'AstralParty.Toys', 'WebUI');
const SOURCES = ['css/base.css', 'css/home.css', 'css/tools.css', 'css/wiki.css', 'css/settings.css', 'css/utilities.css', 'css/mods.css',
  'vendor/github-markdown-dark.css', 'vendor/highlight-github-dark.min.css', 'css/markdown.css', 'css/workshop.css', 'css/skins.css',
  'css/resource-mods.css', 'css/profiles.css'];
const OUTPUT = 'css/v2-light.css';
const THEME = ':where(:root[data-theme="light"])';
const COLOR = /#[0-9a-fA-F]{3,8}\b|rgba?\([^()]*\)|\bwhite\b/g;

function parseColor(token) {
  if (token === 'white') return { r: 255, g: 255, b: 255, a: 1 };
  if (token[0] === '#') {
    let h = token.slice(1);
    if (h.length <= 4) h = [...h].map(c => c + c).join('');
    return { r: parseInt(h.slice(0, 2), 16), g: parseInt(h.slice(2, 4), 16), b: parseInt(h.slice(4, 6), 16), a: h.length === 8 ? parseInt(h.slice(6, 8), 16) / 255 : 1 };
  }
  const n = token.match(/[\d.]+%?/g);
  if (!n || n.length < 3) return null;
  return { r: +n[0], g: +n[1], b: +n[2], a: n[3] === undefined ? 1 : n[3].endsWith('%') ? parseFloat(n[3]) / 100 : +n[3] };
}
const lum = c => {
  const f = v => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; };
  return 0.2126 * f(c.r) + 0.7152 * f(c.g) + 0.0722 * f(c.b);
};
const saturation = c => { const max = Math.max(c.r, c.g, c.b), min = Math.min(c.r, c.g, c.b); return max === 0 ? 0 : (max - min) / max; };
const rgba = (c, a = c.a) => (a >= 1 ? `rgb(${c.r}, ${c.g}, ${c.b})` : `rgba(${c.r}, ${c.g}, ${c.b}, ${+a.toFixed(3)})`);
const INK = { r: 31, g: 34, b: 51 };

// 底色、边框：白色半透明 → 深色半透明；深色 → 白色；纯黑（遮罩、阴影）和彩色不动
function mapSurface(token) {
  const c = parseColor(token);
  if (!c) return token;
  if (c.r === 0 && c.g === 0 && c.b === 0) return token;
  const neutral = saturation(c) < 0.45;
  if (lum(c) > 0.75 && neutral && c.a < 1) return rgba(INK, c.a);
  // 深蓝灰（比如 rgba(15, 23, 42)）按比例算饱和度很高，所以很暗的颜色不看饱和度
  if (lum(c) < 0.03 || (lum(c) < 0.09 && neutral)) return rgba({ r: 255, g: 255, b: 255 }, c.a);
  return token;
}
// 文字：浅色的中性色 → 深色文字；鲜艳的颜色压暗，白底上才看得清
function mapText(token) {
  const c = parseColor(token);
  if (!c) return token;
  if (saturation(c) < 0.3) {
    if (c.a < 1) return lum(c) > 0.3 ? rgba(INK, c.a) : token;
    const l = lum(c);
    return l > 0.65 ? 'var(--gp-text-main)' : l > 0.45 ? 'var(--gp-text-sub)' : l > 0.15 ? 'var(--gp-text-muted)' : token;
  }
  if (lum(c) <= 0.16) return token;
  const d = { ...c };
  for (let i = 0; i < 24 && lum(d) > 0.16; i++) { d.r = Math.round(d.r * 0.9); d.g = Math.round(d.g * 0.9); d.b = Math.round(d.b * 0.9); }
  return rgba(d);
}

// 彩色底上的白字要保留
const COLORED_BG = /var\(--(gp-pink|primary|accent|gp-green|gp-blue|gp-gold|gp-purple)|gradient\(/;
const COLORED_SELECTOR = /primary|danger|\.active-pill|badge-fill/;
function hasColoredBackground(decls) {
  return decls.some(([prop, value]) => {
    if (!/^background(-color|-image)?$/.test(prop)) return false;
    if (COLORED_BG.test(value)) return true;
    return (value.match(COLOR) || []).some(t => { const c = parseColor(t); return c && saturation(c) > 0.45 && c.a > 0.5; });
  });
}

function convert(selector, decls) {
  const colored = hasColoredBackground(decls) || COLORED_SELECTOR.test(selector);
  const out = [];
  for (const [prop, value] of decls) {
    let next = value;
    if (prop === 'color' || prop === '-webkit-text-fill-color' || prop === 'caret-color') {
      if (!colored) next = value.replace(COLOR, mapText);
    } else if (/^(background(-color|-image)?|border(-[a-z]+)*|outline(-color)?|fill|stroke)$/.test(prop)) {
      next = value.replace(COLOR, mapSurface);
    } else if (prop === 'text-shadow' && value !== 'none') {
      next = 'none';
    } else if (prop === 'box-shadow') {
      // 白色高光在浅底上是脏边；为深色背景设计的黑色大阴影在浅底上是一圈灰，都换成很淡的深色
      next = value.replace(COLOR, t => {
        const c = parseColor(t);
        if (!c) return t;
        if (lum(c) > 0.75 && saturation(c) < 0.3) return rgba(INK, Math.min(c.a, 0.12));
        if (c.r === 0 && c.g === 0 && c.b === 0) return rgba(INK, Math.min(c.a * 0.25, 0.1));
        return t;
      });
    }
    if (next !== value) out.push(`${prop}: ${next}`);
  }
  return out;
}

// 不增加优先级地加上主题条件
function scope(selector) {
  const parts = [];
  let depth = 0, cur = '';
  for (const ch of selector) {
    if (ch === '(' || ch === '[') depth++;
    if (ch === ')' || ch === ']') depth--;
    if (ch === ',' && depth === 0) { parts.push(cur); cur = ''; } else cur += ch;
  }
  parts.push(cur);
  return parts.map(p => p.trim()).filter(Boolean).map(p => {
    if (/^(:root|\*)/.test(p)) return null;
    if (/^html\b/.test(p)) return p.replace(/^html\b/, 'html:where([data-theme="light"])');
    return `${THEME} ${p}`;
  }).filter(Boolean).join(',\n');
}

function parseDecls(body) {
  const decls = [];
  let depth = 0, cur = '';
  for (const ch of body + ';') {
    if (ch === '(') depth++;
    if (ch === ')') depth--;
    if (ch === ';' && depth === 0) {
      const i = cur.indexOf(':');
      if (i > 0) decls.push([cur.slice(0, i).trim().toLowerCase(), cur.slice(i + 1).trim()]);
      cur = '';
    } else cur += ch;
  }
  return decls;
}

function walk(css) {
  const rules = [];
  let i = 0;
  while (i < css.length) {
    const open = css.indexOf('{', i);
    if (open < 0) break;
    const semi = css.indexOf(';', i);
    if (semi >= 0 && semi < open && css.slice(i, semi).trim().startsWith('@')) { i = semi + 1; continue; }
    const prelude = css.slice(i, open).trim();
    let depth = 1, j = open + 1;
    while (j < css.length && depth > 0) { if (css[j] === '{') depth++; if (css[j] === '}') depth--; j++; }
    const body = css.slice(open + 1, j - 1);
    i = j;
    if (/^@(media|supports)/.test(prelude)) {
      const inner = walk(body);
      if (inner.length) rules.push(`${prelude} {\n${inner.join('\n')}\n}`);
    } else if (!prelude.startsWith('@')) {
      const decls = convert(prelude, parseDecls(body));
      const selector = decls.length && scope(prelude);
      if (selector) rules.push(`${selector} { ${decls.join('; ')}; }`);
    }
  }
  return rules;
}

let output = `/* 自动生成，不要手改。改生成规则在 tools/gen-light-css.js，运行 node tools/gen-light-css.js 重新生成。 */\n`;
let count = 0;
for (const file of SOURCES) {
  const css = fs.readFileSync(path.join(WEB, file), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
  const rules = walk(css);
  count += rules.length;
  output += `\n/* ---------- ${file} ---------- */\n${rules.join('\n')}\n`;
}
fs.writeFileSync(path.join(WEB, OUTPUT), output);
console.log(`已生成 ${OUTPUT}：${count} 条规则，${Math.round(output.length / 1024)} KB`);
