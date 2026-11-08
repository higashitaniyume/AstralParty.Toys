// skins/playback.js - Animated card playback (IntersectionObserver)
(function () {
  'use strict';
  // 仅详情预览播放序列帧，列表和选择界面保留静态首帧。
  const cardPlayers = new Map();
  let playbackTimer = null;
  const visiblePlayers = new Set();
  const visibilityObserver = new IntersectionObserver(entries => {
    for (const entry of entries) {
      if (entry.isIntersecting) visiblePlayers.add(entry.target);
      else visiblePlayers.delete(entry.target);
    }
    refreshPlayback();
  });
  function refreshPlayback() {
    clearTimeout(playbackTimer);
    playbackTimer = null;
    for (const image of cardPlayers.keys()) {
      if (!image.isConnected) {
        visibilityObserver.unobserve(image);
        visiblePlayers.delete(image);
        cardPlayers.delete(image);
      }
    }
    if (document.hidden) return;
    const now = performance.now();
    let nextDelay = Infinity;
    for (const image of visiblePlayers) {
      const player = cardPlayers.get(image);
      if (!player || image.closest('.hidden')) continue;
      const modal = image.closest('#globalModal');
      if (modal && !modal.classList.contains('open')) continue;
      const elapsed = now - player.start;
      const frame = Math.floor(elapsed / player.interval) % player.card.animationFrameCount;
      if (frame !== player.frame) {
        image.src = player.card.animationFrameUrlPrefix + String(frame).padStart(6, '0') + '.png?v=' + encodeURIComponent(player.card.lastModified || '');
        player.frame = frame;
      }
      nextDelay = Math.min(nextDelay, player.interval - elapsed % player.interval);
    }
    if (Number.isFinite(nextDelay)) playbackTimer = setTimeout(refreshPlayback, Math.max(16, nextDelay));
  }
  function playCard(image, card) {
    if (!image || !card.isAnimated || !card.animationFrameUrlPrefix || !(card.animationFrameCount > 0) || !(card.animationFps > 0)) return;
    cardPlayers.set(image, { card, start: performance.now(), interval: 1000 / card.animationFps, frame: -1 });
    visibilityObserver.observe(image);
  }
  document.addEventListener('visibilitychange', refreshPlayback);
  document.addEventListener('DOMContentLoaded', () => {
    // 页面切换、关闭弹窗和重绘网格时立即暂停或清理播放器。
    new MutationObserver(refreshPlayback).observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class'] });
  });
  function fmtBytes(bytes) {
    if (!bytes || bytes <= 0) return '0 B';
    if (bytes >= 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    if (bytes >= 1024) return (bytes / 1024).toFixed(0) + ' KB';
    return bytes + ' B';
  }
  window.SkinsPlayback = { cardPlayers, visiblePlayers, refreshPlayback, playCard, fmtBytes };
})();
