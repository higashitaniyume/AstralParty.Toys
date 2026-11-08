// utilities/editor.js - Speedhack config editor, key picker and save handling
(function () {
  'use strict';
  const { VK_GROUPS, VK_LABELS } = window.UtilitiesKeys;
  function numberOr(value, fallback) {
    const number = Number(value);
    return Number.isFinite(number) && number > 0 ? number : fallback;
  }

  function fmtBytes(bytes) {
    const value = Number(bytes ?? 0);
    if (value >= 1024 * 1024) return (value / 1024 / 1024).toFixed(2) + ' MB';
    if (value >= 1024) return (value / 1024).toFixed(1) + ' KB';
    return value + ' B';
  }
  Object.assign(window.UtilitiesModule, {
    loadConfigIntoEditor(config) {
      if (!config) {
        this.cfg = this.defaultEditorConfig();
      } else {
        this.cfg = {
          console: !!config.console,
          autoSpeedEnabled: numberOr(config.baseSpeed, 1) !== 1,
          baseSpeed: numberOr(config.baseSpeed, 1),
          waitMs: numberOr(config.waitMs, 250),
          startup: {
            enabled: !!config.startup?.enabled,
            speed: numberOr(config.startup?.speed, 10),
            durationSecs: numberOr(config.startup?.durationSecs, 5)
          },
          reloadKeys: Array.isArray(config.reloadKeys) ? [...config.reloadKeys] : [],
          states: Array.isArray(config.speedStates) && config.speedStates.length
            ? config.speedStates.map(s => ({
                keys: Array.isArray(s.keys) ? [...s.keys] : [],
                speed: numberOr(s.speed, 2),
                isToggle: !!s.isToggle
              }))
            : [{ keys: ['VK_CONTROL'], speed: 2, isToggle: true }]
        };
      }
      const badge = $('utilsConfigStateBadge');
      if (this.configBroken) {
        badge.textContent = '解析失败';
        badge.className = 'badge badge-notice';
      } else {
        badge.textContent = '已同步';
        badge.className = 'badge badge-update';
      }
      this.renderAll();
    },
    defaultEditorConfig() {
      return {
        console: false,
        autoSpeedEnabled: true,
        baseSpeed: 2,
        waitMs: 250,
        startup: { enabled: false, speed: 10, durationSecs: 5 },
        reloadKeys: ['VK_CONTROL', 'VK_SHIFT', 'VK_R'],
        states: [{ keys: ['VK_CONTROL'], speed: 2, isToggle: true }]
      };
    },
    renderAll() {
      const cfg = this.cfg;
      if (!cfg) return;
      $('utilsAutoSpeedToggle').checked = cfg.autoSpeedEnabled;
      $('utilsBaseSpeed').value = String(cfg.baseSpeed);
      $('utilsBaseSpeed').disabled = !cfg.autoSpeedEnabled;
      document.querySelectorAll('.auto-speed-preset').forEach(button => button.disabled = !cfg.autoSpeedEnabled);
      $('utilsConsoleToggle').checked = cfg.console;
      $('utilsWaitMs').value = String(Math.round(cfg.waitMs));
      const startupSpeed = $('utilsStartupSpeed');
      startupSpeed.value = String(cfg.startup.speed);
      startupSpeed.disabled = !cfg.startup.enabled;
      $('utilsStartupDuration').value = String(cfg.startup.durationSecs);
      $('utilsStartupDuration').disabled = !cfg.startup.enabled;
      $('utilsStartupToggle').checked = cfg.startup.enabled;
      this.renderRows();
      this.renderReloadKeys();
    },
    renderRows() {
      const container = $('utilsStateRows');
      if (!container || !this.cfg) return;

      const html = this.cfg.states.map((state, idx) => `
        <div class="util-state-row" data-idx="${idx}">
          <div class="util-state-row-head">
            <span class="util-state-title">档位 ${idx + 1}${state.isToggle ? ' · 点按切换' : ' · 按住生效'}</span>
            <button class="state-del-btn" data-idx="${idx}" title="删除该档位">${Icons.svg('close')}</button>
          </div>
          <div class="util-keys-area">
            ${this.chipsHtml(state.keys, `state:${idx}`)}
            <button class="util-add-chip-btn" data-target="state:${idx}">＋ 按键</button>
          </div>
          <div class="util-state-speed-row">
            <div class="speed-presets">
              <button class="speed-preset-btn" data-idx="${idx}" data-speed="1">×1</button>
              <button class="speed-preset-btn" data-idx="${idx}" data-speed="2">×2</button>
              <button class="speed-preset-btn" data-idx="${idx}" data-speed="5">×5</button>
              <button class="speed-preset-btn" data-idx="${idx}" data-speed="10">×10</button>
            </div>
            <label style="display:flex;align-items:center;gap:6px;font-size:12.5px;color:var(--gp-text-sub)">
              倍速
              <input type="number" class="util-input state-speed-input" data-idx="${idx}"
                     value="${state.speed}" min="0.1" step="0.5" style="width:84px">
            </label>
            <label class="util-state-toggle">
              <input type="checkbox" class="state-toggle-input" data-idx="${idx}" ${state.isToggle ? 'checked' : ''}>
              点按切换（不勾选 = 按住才生效）
            </label>
          </div>
        </div>`).join('');
      container.innerHTML = html || '<div class="util-empty-hint">还没有倍速档位——点下面「＋ 添加倍速档位」创建一个，例如 Ctrl = 2 倍速。</div>';
    },
    renderReloadKeys() {
      const area = $('utilsReloadKeysArea');
      if (!area || !this.cfg) return;
      const chips = this.chipsHtml(this.cfg.reloadKeys, 'reload');
      area.innerHTML = chips +
        `<button class="util-add-chip-btn" id="utilsReloadKeysAddBtn">＋ 按键</button>`;
      $('utilsReloadKeysAddBtn')?.addEventListener('click', () => this.openKeyPicker('reload'));
    },
    chipsHtml(keys, target) {
      if (!keys.length) return '<span class="key-chip empty-chip">未设置按键</span>';
      return keys.map(vk => `
        <span class="key-chip">${esc(VK_LABELS[vk] || vk)}
          <span class="key-remove" data-vk="${esc(vk)}" data-target="${target}" title="移除">×</span>
        </span>`).join('');
    },

    // ---------- 按键选择 ----------
    openKeyPicker(target) {
      if (!this.cfg) return;
      const existing = target === 'reload'
        ? [...this.cfg.reloadKeys]
        : [...(this.cfg.states[Number(target.split(':')[1])]?.keys || [])];
      this.picker = { target, keys: existing };
      const grid = VK_GROUPS.map(([group, list]) =>
        `<div class="key-picker-group-name">${esc(group)}</div>` +
        list.map(([vk]) =>
          `<button class="key-pick-btn ${existing.includes(vk) ? 'on' : ''}" data-vk="${vk}">${esc(VK_LABELS[vk] || vk)}</button>`
        ).join('')).join('');
      openModal(`选择按键组合 · ${target === 'reload' ? '重载配置热键' : `档位 ${Number(target.split(':')[1]) + 1}`}`, `
        <p style="margin:0 0 10px;font-size:12.5px;color:var(--gp-text-sub)">点击选择 / 取消选择，确认后应用到该档位。档位按键可多选（如 Ctrl + Shift）。</p>
        <div class="key-picker-grid">${grid}</div>
        <div style="display:flex;justify-content:flex-end;gap:10px;margin-top:16px">
          <button class="secondary-btn" id="keyPickerCancel" type="button">取消</button>
          <button class="primary-btn" id="keyPickerConfirm" type="button">确定（已选 ${existing.length} 个）</button>
        </div>`);
      // 同步已选数量文案
      const counter = $('keyPickerConfirm');
      $('globalModal')?.querySelectorAll('.key-pick-btn').forEach(btn => {
        btn.addEventListener('click', () => {
          if (!this.picker) return;
          counter.textContent = `确定（已选 ${this.picker.keys.length} 个）`;
        });
      });
    },
    applyPickedKeys(target, keys) {
      if (target === 'reload') {
        this.cfg.reloadKeys = [...keys];
        this.renderReloadKeys();
      } else {
        const idx = Number(target.split(':')[1]);
        if (this.cfg.states[idx]) {
          this.cfg.states[idx].keys = [...keys];
          this.renderRows();
        }
      }
      this.markDirty();
    },
    markDirty() {
      this.dirty = true;
      const badge = $('utilsConfigStateBadge');
      if (badge) {
        badge.textContent = '未保存修改';
        badge.className = 'badge badge-event';
      }
    },
    saveConfig(afterSave) {
      if (!this.cfg) return;
      const model = {
        console: this.cfg.console,
        baseSpeed: this.cfg.autoSpeedEnabled ? this.cfg.baseSpeed : 1,
        waitMs: Math.round(this.cfg.waitMs),
        startup: this.cfg.startup.enabled ? {
          enabled: true,
          speed: this.cfg.startup.speed,
          durationSecs: Math.max(1, Math.round(this.cfg.startup.durationSecs))
        } : { enabled: false, speed: this.cfg.startup.speed, durationSecs: this.cfg.startup.durationSecs },
        reloadKeys: this.cfg.reloadKeys,
        speedStates: this.cfg.states.map(s => ({
          keys: s.keys,
          speed: s.speed,
          isToggle: s.isToggle
        }))
      };
      this.dirty = false;
      post({ type: 'speedhackSaveConfig', config: model, afterSave: afterSave || null,
        overwriteDll: $('utilsOverwriteCheck')?.checked === true });
    }
  });
})();
