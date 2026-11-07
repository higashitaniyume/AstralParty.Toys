    },
    // 列表/详情容器里的点击：药丸切换 / 打开选择器。返回 true 表示已处理（调用方不要再打开详情）。
    handleClick(event) {
      const pill = event.target.closest?.('[data-sticker-pill]');
      if (pill) {
        event.preventDefault();
        event.stopPropagation();
        const modId = pill.getAttribute('data-mod-id');
        const stickerId = pill.getAttribute('data-sticker-id');
        if (modId && stickerId) void toggleSticker(modId, stickerId, false);
        return true;
      }
      const trigger = event.target.closest?.('[data-sticker-trigger]');
      if (trigger) {
        event.preventDefault();
        event.stopPropagation();
        const modId = trigger.getAttribute('data-mod-id');
        if (!modId) return true;
        if (pickerEl && pickerState.modId === modId) {
          closePicker();
        } else {
          // 先拉再开：让首次渲染就拿到 loading 状态，进度条与加载文案一次到位；
          // 下载在后台继续，目录回来时 applyCatalog 会自动补齐分类与网格。
          if (!catalogReady) void loadCatalog();
          openPicker(modId, trigger);
        }
        return true;
      }
      return false;
    },
    closePicker,
    pickerOpenFor(modId) {
      return !!pickerEl && pickerState.modId === modId;
    },
    // 语言切换后条子文案要重刷（列表本身会重载，这里只管已渲染的条子）。
    onLanguageChanged() {
      if (pickerEl) closePicker(false);
      renderAllStrips();
    },
  };

  window.StickerModule = StickerModule;

  window.addEventListener('appLanguageChanged', () => StickerModule.onLanguageChanged());
})();
