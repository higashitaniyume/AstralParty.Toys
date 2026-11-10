// Shared, interruptible motion. Rendering/state changes never wait for an animation.
(function () {
  'use strict';
  const preference = window.matchMedia('(prefers-reduced-motion: reduce)');
  const active = new Set();
  const running = new WeakMap();
  const contentKeys = new WeakMap();
  const reduced = () => preference.matches || document.body.classList.contains('reduce-motion');

  function duration(token, fallback) {
    if (reduced()) return 0;
    const value = getComputedStyle(document.documentElement).getPropertyValue(token).trim();
    const number = parseFloat(value);
    return Number.isFinite(number) ? Math.max(0, value.endsWith('ms') ? number : number * 1000) : fallback;
  }

  function cancelAll() {
    for (const animation of active) animation.cancel();
    active.clear();
  }

  function reveal(element, { distance = 6, duration = 180 } = {}) {
    if (!element) return;
    running.get(element)?.cancel();
    if (reduced() || !element.isConnected || !element.getClientRects().length || !element.animate) return;
    const animation = element.animate([
      { opacity: 0.4, transform: `translateY(${distance}px)` },
      { opacity: 1, transform: 'translateY(0)' },
    ], { duration, easing: 'cubic-bezier(0.2, 0.7, 0.2, 1)' });
    running.set(element, animation);
    active.add(animation);
    const cleanup = () => {
      active.delete(animation);
      if (running.get(element) === animation) running.delete(element);
    };
    animation.finished.then(cleanup, cleanup);
    return animation;
  }

  // Stable semantic keys stop polling, progress messages and rerenders from replaying entry motion.
  // Animate a single content surface, never hundreds of individual rows/images.
  function content(root, key, selector) {
    if (!root) return;
    const previous = contentKeys.get(root);
    contentKeys.set(root, key);
    if (previous === key) return;
    reveal(selector ? root.querySelector(selector) : root);
  }

  preference.addEventListener('change', () => { if (reduced()) cancelAll(); });
  const observePreference = () => new MutationObserver(() => {
    if (reduced()) cancelAll();
  }).observe(document.body, { attributes: true, attributeFilter: ['class'] });
  if (document.body) observePreference();
  else document.addEventListener('DOMContentLoaded', observePreference, { once: true });
  window.Motion = Object.freeze({ reduced, duration, reveal, content, cancelAll });
})();
