// app.js - Main Application Bootstrap
document.addEventListener('DOMContentLoaded', () => {
  // Initialize Replay Module
  if (window.ReplayModule) {
    window.ReplayModule.init();
  }

  // Set default page
  showPage('home');

  // Notify WebView2 host that WebUI is ready
  post({ type: 'ready' });
});
