// wiki.js - Direct BWiki Webpage Integration (https://wiki.biligame.com/starengine/%E9%A6%96%E9%A1%B5)
(function () {
  const WIKI_URL = 'https://wiki.biligame.com/starengine/%E9%A6%96%E9%A1%B5';

  const WikiModule = {
    init(data) {
      this.bindEvents();
    },

    bindEvents() {
      // Return to home page
      $('wikiBackHomeBtn')?.addEventListener('click', () => {
        showPage('home');
      });

      // Refresh wiki iframe
      $('wikiRefreshBtn')?.addEventListener('click', () => {
        const iframe = $('wikiIframe');
        if (iframe) {
          const current = iframe.src;
          iframe.src = 'about:blank';
          setTimeout(() => {
            iframe.src = current || WIKI_URL;
          }, 50);
        }
      });

      // Open in system browser
      $('wikiExternalBtn')?.addEventListener('click', () => {
        post({
          type: 'openBrowser',
          url: WIKI_URL
        });
      });
    },

    onShowWiki() {
      // Ensure iframe is pointing to wiki url
      const iframe = $('wikiIframe');
      if (iframe && (!iframe.src || iframe.src === 'about:blank')) {
        iframe.src = WIKI_URL;
      }
    }
  };

  window.WikiModule = WikiModule;
})();
