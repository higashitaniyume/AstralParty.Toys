// mods/list.js - Mod list filtering and card rendering
(function () {
  'use strict';
  const { compareVersion, setVersionCell, fmtBytes } = window.ModsHelpers;
  Object.assign(window.ModsModule, {
    renderFilteredMods() {
      const allMods = this.modStatus?.mods || [];
      const query = this.searchQuery;
      let filtered = allMods;
      if (query) {
        const terms = query.split(/\s+/).filter(Boolean);
        filtered = allMods.filter(entry => {
          const haystacks = [
            entry.displayName,
            entry.name,
            entry.fileName,
            entry.id,
            entry.author,
            entry.description,
            entry.directoryName
          ];
          if (this.isBuiltInMod(entry)) {
            haystacks.push('内置', 'builtin');
          }
          if (entry.enabled === false) {
            haystacks.push('禁用', '已禁用', 'disabled');
          } else {
            haystacks.push('启用', '已启用', 'enabled');
          }
          const lower = haystacks.map(s => String(s ?? '').toLowerCase());
          return terms.every(term => lower.some(h => h.includes(term)));
        });
      }

      this.renderModList($('modListContainer'), filtered, 'mod', Boolean(query && allMods.length > 0));
      const count = $('modModsCount');
      if (count) {
        count.textContent = query && allMods.length > 0
          ? `${filtered.length} / ${allMods.length}`
          : String(allMods.length);
      }
    },
    renderModList(container, entries, kind, isSearchEmpty) {
      if (!container) return;
      const list = entries || [];
      if (list.length === 0) {
        if (kind === 'mod') {
          const emptyText = isSearchEmpty
            ? (window.i18n ? window.i18n.t('mods.empty_search') : '没有找到匹配的模组')
            : '还没有模组 —— 点「导入模组」添加 DLL 或 ZIP 模组包，或重新安装加载器时勾选内置模组';
          container.innerHTML = `<p class="mods-empty">${esc(emptyText)}</p>`;
        } else {
          container.innerHTML = '<p class="mods-empty">sdk 目录为空（重新安装加载器会写入 CesiumLoader.SDK.dll）</p>';
        }
        return;
      }
      container.innerHTML = list
        .map(entry => (kind === 'mod' ? this.modCard(entry) : this.sdkRow(entry)))
        .join('');
    },
    isBuiltInMod(entry) {
      const name = String(entry.name || '');
      const file = String(entry.fileName || '').replace(/\.dll$/i, '');
      return this.builtInMods.some(id => id === name || id === file);
    },

    /** 一个模组 = 一张卡片：名字 / 版本 / 能力标签 → 描述 → 事实行 → 开关 + 配置 + 删除。 */
    modCard(entry) {
      const enabled = entry.enabled !== false;
      const title = entry.displayName || entry.name || entry.fileName;

      const chips = [];
      if (this.isBuiltInMod(entry)) {
        chips.push('<span class="mod-chip mod-chip-builtin" title="随加载器一起安装的内置模组">内置</span>');
      }
      if ((Number(entry.permissions) & 2) === 2) {
        chips.push('<span class="mod-chip mod-chip-warn" title="此模组声明可操作游戏（模拟出牌 / 掷骰 / 移动等）。权限机制已取消，这里只提示：请确认来源可信">' + Icons.svg('warning') + ' 可操作游戏</span>');
      }
      if (!enabled) chips.push('<span class="mod-chip mod-chip-off">已禁用</span>');

      const facts = [];
      if (entry.version) facts.push(`v${entry.version}`);
      if (entry.author) facts.push(entry.author);
      if (entry.sdkVersion) facts.push(`SDK ${entry.sdkVersion}`);
      facts.push(fmtBytes(entry.sizeBytes));

      if (entry.directoryName) facts.push(`mods\\${entry.directoryName}\\`);
      if (entry.modifiedUtc) facts.push(new Date(entry.modifiedUtc).toLocaleString('zh-CN'));

      const deps = (entry.dependencies || []).length
        ? '依赖：' + entry.dependencies
            .map(d => `${d.id}${d.minVersion ? ' ≥ ' + d.minVersion : ''}`)
            .join('、')
        : '';
      const description = [entry.description, deps].filter(Boolean).join('　');

      return `<article class="mod-card${enabled ? '' : ' mod-disabled'}" title="${esc(entry.fileName)}">
        <div class="mod-card-main">
          <div class="mod-card-name-row">
            <span class="mod-card-name">${esc(title)}</span>
            ${chips.join('')}
          </div>
          ${description ? `<p class="mod-card-desc">${esc(description)}</p>` : ''}
          <span class="mod-card-meta">${esc(facts.join(' · '))}</span>
        </div>
        <div class="mod-card-side">
          <label class="mod-toggle" title="启用 / 禁用（重启游戏后生效）">
            <input type="checkbox" data-toggle-file="${esc(entry.fileName)}" ${enabled ? 'checked' : ''}>
            <span class="switch-track"></span>
            <span class="mod-toggle-text">${enabled ? '已启用' : '已禁用'}</span>
          </label>
          <button class="mod-config-btn" data-config-file="${esc(entry.fileName)}" title="修改这个模组的配置" aria-label="修改配置">${Icons.svg('settings')}</button>
          <button class="mod-del-btn" data-file="${esc(entry.fileName)}" title="删除这个模组" aria-label="删除模组">${Icons.svg('delete')}</button>
        </div>
      </article>`;
    },
    sdkRow(entry) {
      const facts = [fmtBytes(entry.sizeBytes)];
      if (entry.modifiedUtc) facts.push(new Date(entry.modifiedUtc).toLocaleString('zh-CN'));
      return `<article class="mod-card">
        <div class="mod-card-main">
          <div class="mod-card-name-row">
            <span class="mod-card-name">${esc(entry.name || entry.fileName)}</span>
            ${entry.version ? `<span class="mod-card-version">v${esc(entry.version)}</span>` : ''}
          </div>
          <span class="mod-card-meta">${esc(facts.join(' · '))}</span>
        </div>
      </article>`;
    },

    // ---------- 游戏变速栏（加载器内置功能，非 mod） ----------

    /** 加载器没装时 doorstop_config.json 无处可写：整栏禁用，别让用户点了才报错。 */
  });
})();
