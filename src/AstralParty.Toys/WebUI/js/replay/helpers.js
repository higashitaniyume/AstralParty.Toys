// replay/helpers.js - Byte formatting and quality tones
(function () {
  'use strict';
  // 下载进度用（宿主回推的是字节数）
  const fmtBytes = bytes => {
    const value = Number(bytes ?? 0);
    if (value >= 1024 * 1024) return (value / 1024 / 1024).toFixed(2) + ' MB';
    if (value >= 1024) return (value / 1024).toFixed(1) + ' KB';
    return value + ' B';
  };

  // 品质显示文本会跟随界面语言变化（普通/Common/通常/傳說…）。样式类名必须用
  // 稳定的颜色键，不能拿本地化后的文本拼 class，否则切语言后筹码底色就丢了。
  const QUALITY_TONE = {
    blue: 'blue', 普通: 'blue', common: 'blue', 通常: 'blue', 一般: 'blue',
    purple: 'purple', 稀有: 'purple', rare: 'purple', レア: 'purple',
    epic: 'epic', 史诗: 'epic', 史詩: 'epic',
    gold: 'gold', orange: 'gold', 传说: 'gold', 傳說: 'gold', legendary: 'gold', レジェンド: 'gold', 传奇: 'gold', 傳奇: 'gold'
  };
  const qualityTone = quality => {
    const key = String(quality ?? '').trim().toLowerCase();
    return QUALITY_TONE[key] || QUALITY_TONE[String(quality ?? '').trim()] || 'unknown';
  };
  window.ReplayHelpers = { fmtBytes, QUALITY_TONE, qualityTone };
})();
