    },
    // 音效
    audioAddMenu: el => U().openMenu(el, [
      { label: '新建空白方案…', icon: I().plus, fn: () => metaForm({
        title: '新建音效方案', ok: '创建',
        fields: [{ key: 'id', label: '方案 ID', required: true, value: 'my_audio', ...ID_RULE }, { key: 'name', label: '显示名称', value: '我的音效' },
          { key: 'author', label: '作者' }, { key: 'description', label: '简介', multiline: true }],
        onOk: v => post({ type: 'audioSkinsCreate', id: v.id, name: v.name || v.id, author: v.author, description: v.description }),
      }) },
      { label: '导入方案 ZIP…', icon: I().upload, fn: () => post({ type: 'audioSkinsImportZip' }) },
    ]),
    audioPrep: audioPrepDialog,
    audioExportOriginal: () => post({ type: 'audioSkinsExportOriginalZip' }),
    audioFolder: () => post({ type: 'audioSkinsOpenFolder' }),
    audioBack: () => { K.audioScheme = null; render(); },
    audioApply: () => post({ type: 'audioSkinsSetActive', id: K.audioScheme }),
    audioMore: () => { K.aLimit += 200; render(); },
    audioDuplicate: () => {
      const p = curAudio(); if (!p) return;
      metaForm({ title: `复制「${esc(p.name || p.id)}」`, ok: '复制',
        fields: [{ key: 'newId', label: '新方案 ID', required: true, value: `${p.id}_copy`, ...ID_RULE }, { key: 'name', label: '显示名称', value: `${p.name || p.id} 副本` }],
        onOk: v => post({ type: 'audioSkinsDuplicate', sourceId: p.id, newId: v.newId, name: v.name || v.newId }) });
    },
    audioSchemeMenu: el => {
      const p = curAudio(); if (!p) return;
      const isDefault = p.id === 'default';
      U().openMenu(el, [
        { label: '编辑方案信息…', icon: I().sliders, disabled: isDefault, fn: () => metaForm({
          title: '编辑方案信息', ok: '保存',
          fields: [{ key: 'name', label: '显示名称', required: true, value: p.name }, { key: 'author', label: '作者', value: p.author }, { key: 'description', label: '简介', multiline: true, value: p.description }],
          onOk: v => post({ type: 'audioSkinsUpdateMeta', id: p.id, name: v.name, author: v.author, description: v.description }),
        }) },
        { label: '复制为新方案…', icon: I().copy, fn: actions.audioDuplicate },
        { label: '导出方案 ZIP', icon: I().download, disabled: isDefault, fn: () => post({ type: 'audioSkinsExportZip', id: p.id }) },
        '-',
        { label: '删除方案…', icon: I().trash, disabled: isDefault, fn: () => confirm({
          title: `删除音效方案「${esc(p.name || p.id)}」？`,
          lines: [p.ruleCount ? `方案里 <b>${p.ruleCount} 项替换 / 静音</b>和导入的音频文件会一起删除，无法恢复` : '这个方案里还没有改动',
            p.ruleCount && '想留个备份，先在 ⋯ 里「导出方案 ZIP」', p.id === aActive() && '这是<b>当前正在用</b>的方案'],
          onOk: () => { post({ type: 'audioSkinsDelete', id: p.id }); K.audioScheme = null; render(); },
        }) },
      ]);
    },
    // 资源包
    resImport: () => post({ type: 'resourceModsImport' }),
    resApply: () => post({ type: 'resourceModsApply' }),
    resRestore: () => confirm({ title: '还原官方资源？', ok: '还原', lines: ['撤销所有资源包对游戏文件的修改', '资源包本身不会删除，之后可以重新应用'], onOk: () => post({ type: 'resourceModsRestore' }) }),
    resFolder: () => post({ type: 'resourceModsOpenFolder' }),
    resRefresh: () => RS()?.refresh(),
    resPause: () => post({ type: 'resourceModsPause', paused: !RS()?.progressPaused }),
    resCancel: () => confirm({ title: '取消并还原？', ok: '取消并还原',
      lines: ['停止当前操作，并把游戏资源恢复到操作前的状态', '已经解压的临时文件会被删除'], onOk: () => post({ type: 'resourceModsCancel' }) }),
    resDismissLog: () => { K.resLog = []; K.resP = null; K.resOp = null; render(); },
  };

  function audioItemMenu(anchor, i) {
    const base = { schemeId: K.audioScheme, configId: i.id, eventName: i.name };
    U().openMenu(anchor, [
      { label: '替换音频（.wav）…', icon: I().upload, fn: () => post({ type: 'audioSkinsPickAndReplace', ...base, volume: 1.0 }) },
      { label: i.action === 'mute' ? '取消静音' : '静音', icon: I().dot, fn: () => post({ type: 'audioSkinsSetMute', ...base, mute: i.action !== 'mute' }) },
      { label: '导出这条原版音频', icon: I().download, disabled: !i.hasOriginal, fn: () => post({ type: 'audioSkinsExportSingleOriginal', id: i.id, name: i.name }) },
      '-',
      { label: '恢复原版', icon: I().refresh, disabled: !i.action, fn: () => post({ type: 'audioSkinsRevertAudio', ...base }) },
    ]);
  }

  document.addEventListener('click', ev => {
    const t = ev.target;
    if (!t.closest?.('#skinsView') || !t.closest('#v2Skins')) return;
    const q = sel => t.closest(sel);
    let el;
    if ((el = q('[data-v2s]'))) { if (!el.disabled) actions[el.dataset.v2s]?.(el); return; }
    if ((el = q('[data-v2s-tab]'))) { CS()?.switchTab(el.dataset.v2sTab); return; }
    if ((el = q('[data-v2s-card]'))) {
      K.cardScheme = el.dataset.v2sCard; K.cardCat = 'all'; K.cardQ = '';
      if (SK()) { SK().selectedSkin = K.cardScheme; post({ type: 'skinsGetCards', name: K.cardScheme }); }
      return render();
    }
    if ((el = q('[data-v2s-cat]'))) { K.cardCat = el.dataset.v2sCat; return render(); }
    if ((el = q('[data-v2s-tcard]'))) { const c = (SK()?.cards || []).find(x => x.fileName === el.dataset.v2sTcard); if (c) cardModal(c); return; }
    if ((el = q('[data-v2s-audio]'))) return selectAudio(el.dataset.v2sAudio);
    if ((el = q('[data-v2s-acat]'))) { K.aCat = el.dataset.v2sAcat; K.aChar = 'all'; K.aLimit = 200; return render(); }
    if ((el = q('[data-v2s-astatus]'))) { K.aStatus = el.dataset.v2sAstatus; K.aLimit = 200; return render(); }
    if ((el = q('[data-v2s-play]'))) { if (!el.disabled && el.dataset.v2sPlay) AU()?.playAudio(el.dataset.v2sPlay, el); return; }
    if ((el = q('[data-v2s-aitem]'))) { const i = aItems().find(x => String(x.id) === el.dataset.v2sAitem); if (i) audioItemMenu(el, i); return; }
    if ((el = q('[data-v2s-rmenu]'))) {
      const m = (rStatus()?.mods || []).find(x => x.id === el.dataset.v2sRmenu); if (!m) return;
      U().openMenu(el, [{ label: '删除资源包…', icon: I().trash, disabled: rRunning(), fn: () => confirm({
        title: `删除资源包「${esc(m.name)}」？`,
        lines: [`会删除导入的资源包文件（${fmtBytes(m.size)}），之后只能重新导入`, m.enabled && '它现在是开启状态。删除后点一次「应用」，游戏才会撤掉它的修改'],
        onOk: () => post({ type: 'resourceModsDelete', id: m.id, name: m.name }),
      }) }]);
    }
  });
  document.addEventListener('input', ev => {
    const t = ev.target;
    if (t.dataset?.v2sAvol) t.nextElementSibling.textContent = Math.round(t.value * 100) + '%';
  });
  document.addEventListener('change', ev => {
    const t = ev.target;
    if (!t.closest?.('#v2Skins')) return;
    if (t.id === 'v2sGame') {
      if (!t.value) return;
      post({ type: 'gameProfileSetActive', id: t.value });
      // 卡牌由 GameLibModule 自动刷新；音效、资源包要重新向宿主要一次新游戏的数据
      if (tab() === 'audio' || tab() === 'resource') CS()?.switchTab(tab());
      return;
    }
    if (t.dataset.v2sAvol) { const i = aItems().find(x => String(x.id) === t.dataset.v2sAvol); if (i) post({ type: 'audioSkinsSetVolume', schemeId: K.audioScheme, configId: i.id, eventName: i.name, volume: Number(t.value) }); return; }
    if (t.id === 'v2sAChar') { K.aChar = t.value; K.aLimit = 200; return render(); }
    if (t.dataset.v2sRtoggle) {
      const m = (rStatus()?.mods || []).find(x => x.id === t.dataset.v2sRtoggle);
      if (m) post({ type: 'resourceModsToggle', id: m.id, enabled: t.checked, name: m.name });
    }
  });

  // 包一层原版方法：跑完后重画
  function wrap(obj, names, before) {
    if (!obj) return;
    names.forEach(name => {
      const orig = obj[name]; if (typeof orig !== 'function') return;
      obj[name] = function (...args) { before?.(name, args); const r = orig.apply(this, args); render(); return r; };
    });
  }
  // 宿主提取完不管拿到几个都提示「已同步并就绪 N 个」；N = 0 时这句话会让人以为成功了，换成实话
  const origToast = window.toast;
  if (typeof origToast === 'function') {
    window.toast = function (msg, ...rest) {
      if (/^已同步并就绪 0 个原版音频/.test(String(msg))) msg = '没有提取到原版音频：电脑上没有事先导出的 audio-export 文件夹（替换、静音不受影响）';
      return origToast.call(this, msg, ...rest);
    };
  }

  document.addEventListener('DOMContentLoaded', () => {
    wrap(window.GameLibModule, ['renderProfiles']);
    wrap(SK(), ['renderProfiles', 'renderCards', 'onCardUpdated', 'legacyImported', 'onAnimationState']);
    wrap(CS(), ['switchTab']);
    wrap(AU(), ['renderProfiles', 'renderItems', 'renderExtractProgress', 'renderExtractDone'], (name, args) => {
      if (name === 'renderExtractProgress') K.extract = args[0] || K.extract;
      if (name === 'renderExtractDone') { K.extract = null; K.extractChecking = true; if (AU()?.selectedSchemeId) post({ type: 'audioSkinsGetItems', schemeId: AU().selectedSchemeId }); }
      // 提取完重新拿到条目后：一个原版都没有，就说明是「0 个」那种情况
      if (name === 'renderItems' && K.extractChecking) { K.extractChecking = false; K.extractEmpty = !(args[0]?.items || []).some(i => i.hasOriginal); }
    });
    wrap(RS(), ['render', 'renderProgress', 'renderPaused', 'setBusy'], (name, args) => {
      if (name === 'renderProgress') {
        const p = args[0];
        K.resP = p || null;
        if (p && p.detail) { K.resLog.push(String(p.detail)); if (K.resLog.length > 200) K.resLog.shift(); }
      }
    });
    // 操作消息只在开始/结束时重画，逐行进度期间不重复重画（Progress 已会重画，避免双倍开销）
    const rs = RS();
    if (rs && typeof rs.renderOperation === 'function') {
      const origOp = rs.renderOperation.bind(rs);
      rs.renderOperation = (p) => {
        const wasOpRunning = !!(K.resOp && K.resOp.active && !rRunning());
        K.resOp = p || null;
        // 新操作开始时清零日志，并留第一行说明这次是什么操作（还原等无逐行进度也有用）
        if (p && p.active && p.percent == null) {
          K.resLog = [];
          const line = `${p.title || ''}${p.detail ? '：' + p.detail : ''}`.trim();
          if (line) K.resLog.push(line);
          origOp(p);
          render();
        } else if (p && !p.active && wasOpRunning) {
          origOp(p);
          render();
        } else {
          origOp(p);
        }
      };
    }
    render();
  });
})();
