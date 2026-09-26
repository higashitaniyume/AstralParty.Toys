// wiki.js - Direct BWiki Webpage Integration (https://wiki.biligame.com/starengine/%E9%A6%96%E9%A1%B5)
(function () {
  const WIKI_URL = 'https://wiki.biligame.com/starengine/%E9%A6%96%E9%A1%B5';

  const WikiModule = {
    // 框架当前指向的地址（跨域读不到 iframe.src 的真实值，只能自己记）
    currentUrl: '',

    init(data) {
      this.bindEvents();
    },

    bindEvents() {
      // Return to home page
      $('wikiBackHomeBtn')?.addEventListener('click', () => {
        showPage('home');
      });

      // 刷新：直接重设同一个地址。以前先设 about:blank 再延时设回来，
      // 连点两次时第二次读到的 src 已经是 about:blank，框架就永久空白了
      $('wikiRefreshBtn')?.addEventListener('click', () => {
        const iframe = $('wikiIframe');
        if (!iframe) return;
        iframe.src = this.currentUrl || WIKI_URL;
      });

      // Open in system browser
      $('wikiExternalBtn')?.addEventListener('click', () => {
        post({
          type: 'openBrowser',
          url: this.currentUrl || WIKI_URL
        });
      });
    },

    onShowWiki() {
      // 懒加载：HTML 里不带 src，避免启动时就白白拉一整个第三方维基
      const iframe = $('wikiIframe');
      if (!iframe) return;
      if (!this.currentUrl) {
        this.currentUrl = WIKI_URL;
        iframe.src = WIKI_URL;
      }
    }
  };

  window.WikiModule = WikiModule;
})();
