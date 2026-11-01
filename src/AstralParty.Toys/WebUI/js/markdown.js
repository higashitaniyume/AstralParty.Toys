// markdown.js - GitHub 风格的 README / 更新说明渲染（marked + DOMPurify + highlight.js）。
//
// 市场里的 README 和版本说明由作者提供，属于不可信内容：这里先按 GFM 解析，再用
// DOMPurify 消毒，只放行 http(s) 链接与图片，最后交给 highlight.js 上色。
// 返回的 HTML 片段要放进带 .markdown-body 的容器里（样式见 css/markdown.css）。
(function () {
  'use strict';

  const LINK_PROTOCOLS = ['http:', 'https:', 'mailto:'];
  const IMAGE_PROTOCOLS = ['http:', 'https:'];
  const SANITIZE_TAGS = [
    'script', 'style', 'iframe', 'object', 'embed', 'form', 'button',
    'select', 'textarea', 'base', 'link', 'meta'
  ];

  let prepared = false;
  let headingSeed = 0;

  function isSafeUrl(value, protocols) {
    const text = String(value || '').trim();
    if (!text) return false;
    // 协议相对地址（//host）与相对路径都不解析：README 里的图片大多指向仓库本身。
    if (text.startsWith('//')) return false;
    if (text.startsWith('#')) return protocols === LINK_PROTOCOLS;

    try {
      const url = new URL(text);
      return protocols.includes(url.protocol);
    } catch {
      return false;
    }
  }

  function escapeHtml(value) {
    return String(value ?? '').replace(/[&<>'"]/g, ch => ({
      '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;'
    }[ch]));
  }

  function slugify(text) {
    const base = String(text || '')
      .trim()
      .toLowerCase()
      .replace(/[^\p{L}\p{N}]+/gu, '-')
      .replace(/^-+|-+$/g, '');
    const slug = base || `section-${++headingSeed}`;
    return `${slug}`;
  }

  function prepare() {
    if (prepared) return;
    prepared = true;

    if (window.marked?.use) {
      // GitHub 的 README 不把单个换行当成 <br>；表格/任务列表/删除线走 GFM。
      window.marked.use({
        gfm: true,
        breaks: false,
        renderer: {
          heading(token) {
            const level = Math.min(6, Math.max(1, token.depth || 1));
            const inner = this.parser.parseInline(token.tokens || []);
            const id = slugify(token.text || '');
            return `<h${level} id="${id}">${inner}</h${level}>\n`;
          }
        }
      });
    }

    if (window.DOMPurify) {
      window.DOMPurify.addHook('afterSanitizeAttributes', node => {
        const tag = node.tagName ? node.tagName.toUpperCase() : '';

        if (tag === 'A') {
          const href = node.getAttribute('href') || '';
          if (isSafeUrl(href, LINK_PROTOCOLS)) {
            // 站内锚点就留在当前页，其余链接交给系统浏览器。
            if (!href.startsWith('#')) {
              node.setAttribute('target', '_blank');
              node.setAttribute('rel', 'noopener noreferrer');
            }
          } else {
            node.removeAttribute('href');
          }
        } else if (tag === 'INPUT') {
          // 任务列表只保留不可点击的复选框。
          node.setAttribute('type', 'checkbox');
          node.setAttribute('disabled', '');
        } else if (tag === 'IMG') {
          const src = node.getAttribute('src') || '';
          if (!isSafeUrl(src, IMAGE_PROTOCOLS)) {
            // 相对路径的图片多半指向作者仓库：保留代替文字，去掉坏图。
            const alt = node.getAttribute('alt') || '';
            const text = document.createTextNode(alt);
            node.parentNode?.replaceChild(text, node);
          } else {
            node.setAttribute('loading', 'lazy');
            node.setAttribute('referrerpolicy', 'no-referrer');
          }
        }
      });
    }
  }

  /**
   * Markdown → 安全的 HTML 片段。
   * @param {string} markdown
   * @returns {string}
   */
  function render(markdown) {
    const source = String(markdown ?? '');
    if (!source.trim()) return '';

    prepare();

    if (!window.marked?.parse) {
      return `<pre class="workshop-md-fallback">${escapeHtml(source)}</pre>`;
    }

    let html;
    try {
      html = window.marked.parse(source, { async: false });
    } catch {
      return `<pre class="workshop-md-fallback">${escapeHtml(source)}</pre>`;
    }

    if (!window.DOMPurify?.sanitize) {
      return `<pre class="workshop-md-fallback">${escapeHtml(source)}</pre>`;
    }

    const fragment = window.DOMPurify.sanitize(html, {
      RETURN_DOM_FRAGMENT: true,
      FORBID_TAGS: SANITIZE_TAGS,
      ALLOW_DATA_ATTR: false
    });

    if (window.hljs?.highlightElement) {
      fragment.querySelectorAll('pre code').forEach(block => {
        try {
          window.hljs.highlightElement(block);
        } catch {
          // 未知语言就保持纯文本。
        }
      });
    }

    const container = document.createElement('div');
    container.appendChild(fragment);
    return container.innerHTML;
  }

  window.Markdown = { render, escapeHtml };
})();
