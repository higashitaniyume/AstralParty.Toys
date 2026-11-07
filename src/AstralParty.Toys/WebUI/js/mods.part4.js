      done(name, '已设为 ' + name);
    };

    // 鼠标侧键按下去还会让浏览器"后退/前进"：捕获期间一并压掉（在 WebView 里按侧键不该跳页）
    const swallow = e => { e.preventDefault(); e.stopImmediatePropagation(); };

    function done(name, message) {
      stop();
      if (name) setKeyButtonValue(btn, name);
      else btn.textContent = original;
      btn.blur();
      if (message) toast(message);
    }

    function stop() {
      document.removeEventListener('keydown', onKeyDown, true);
      document.removeEventListener('mousedown', onMouseDown, true);
      document.removeEventListener('mouseup', swallow, true);
      document.removeEventListener('auxclick', swallow, true);
      document.removeEventListener('click', swallow, true);
      document.removeEventListener('contextmenu', swallow, true);
      btn.classList.remove('capturing');
      stopKeyCapture = null;

      // 结束这一下自身的 click 还可能落到按钮上(用鼠标键绑定时必然如此), 吞掉它,
      // 否则按钮会立刻又开始捕获。没有后续 click 也没关系, 到点自动撤掉。
      const onceClick = e => { e.preventDefault(); e.stopImmediatePropagation(); removeOnce(); };
      const timer = setTimeout(removeOnce, 400);
      function removeOnce() {
        clearTimeout(timer);
        document.removeEventListener('click', onceClick, true);
      }
      document.addEventListener('click', onceClick, true);
    }

    stopKeyCapture = stop;
    document.addEventListener('keydown', onKeyDown, true);
    document.addEventListener('mousedown', onMouseDown, true);
    document.addEventListener('mouseup', swallow, true);
    document.addEventListener('auxclick', swallow, true);
    document.addEventListener('click', swallow, true);
    document.addEventListener('contextmenu', swallow, true);
  }

  function fmtBytes(bytes) {
    const value = Number(bytes ?? 0);
    if (value >= 1024 * 1024) return (value / 1024 / 1024).toFixed(2) + ' MB';
    if (value >= 1024) return (value / 1024).toFixed(1) + ' KB';
    return value + ' B';
  }

  // 页面标记存在即完成事件绑定（index.html 脚本位于 DOM 末尾）
  if (document.getElementById('modsView')) {
    ModsModule.init();
  }

  window.ModsModule = ModsModule;
})();
