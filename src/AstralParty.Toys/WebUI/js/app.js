// app.js - Main Application Bootstrap
document.addEventListener('DOMContentLoaded', () => {
  // Initialize Replay Module
  if (window.ReplayModule) {
    window.ReplayModule.init();
  }

  // Set default page without animating the initial DOM bootstrap
  showPage('home', { immediate: true });

  // Notify the desktop host that the interface is ready
  post({ type: 'ready' });
});
