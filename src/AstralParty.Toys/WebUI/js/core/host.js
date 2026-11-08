// core/host.js - WebView host bridge
const host = window.chrome?.webview;

const post = message => host?.postMessage(message);
