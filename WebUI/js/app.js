// app.js - Main Application Bootstrap
document.addEventListener('DOMContentLoaded', () => {
  // Initialize Replay Module
  if (window.ReplayModule) {
    window.ReplayModule.init();
  }

  // Set default page without animating the initial DOM bootstrap
  showPage('home', { immediate: true });

  // Notify WebView2 host that WebUI is ready
  post({ type: 'ready' });
});
