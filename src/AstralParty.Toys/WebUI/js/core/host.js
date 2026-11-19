// core/host.js - WebView2 and native Linux transport
const host = (() => {
  const webview = window.chrome?.webview;
  const invoke = window.__TAURI__?.core?.invoke;
  const isLinux = typeof invoke === 'function';
  const messages = isLinux ? new EventTarget() : webview;
  let pending = Promise.resolve();

  return {
    isLinux,
    available: isLinux || Boolean(webview),
    onMessage(listener) {
      messages?.addEventListener('message', event => listener(event.data));
    },
    post(message) {
      if (!isLinux) return webview?.postMessage(message);
      // Deliver each reply before starting the next request, including after failures.
      const response = pending.then(async () => {
        const events = await invoke('host_message', { message });
        for (const event of events) messages.dispatchEvent(new MessageEvent('message', { data: event }));
      });
      pending = response.catch(() => {});
      return response;
    }
  };
})();

if (host.isLinux) document.documentElement.dataset.host = 'linux';

const post = message => host.post(message);

const assetUrl = (root, ...segments) => {
  if (host.isLinux && root !== 'assets') return null;
  const base = host.isLinux ? '/game-assets/' : `https://${root}.aspatoys.hiynet.localhost/`;
  return base + segments.map(encodeURIComponent).join('/');
};
