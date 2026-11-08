// mods/speedhack.js - Built-in speedhack bar
(function () {
  'use strict';
  Object.assign(window.ModsModule, {
    setSpeedhackAvailable(available) {
      const enableEl = $('modSpeedhackEnable');
      const speedEl = $('modSpeedhackSpeed');
      const saveBtn = $('modSpeedhackSaveBtn');
      const hintEl = $('modSpeedhackHint');
      const labelEl = $('modSpeedhackEnableLabel');
      if (saveBtn) saveBtn.disabled = !available;
      if (available) return;
      if (enableEl) { enableEl.checked = false; enableEl.disabled = true; }
      if (speedEl) speedEl.disabled = true;
      if (labelEl) labelEl.textContent = '变速已禁用';
      if (hintEl) hintEl.textContent = '先安装加载器';
    },

    // 用加载器配置填充变速栏状态（开关 + 倍速）
    syncSpeedhackBar(config) {
      if (!config) return;
      const available = this.modStatus ? this.modStatus.installed === true : true;
      // 加载器硬性下限 1.0：配置里残留的 <1 值(旧版写的)按 1.0 显示，避免界面显示"0.5x"却实际跑 1x
      const raw = Number(config.speedhackBaseSpeed ?? 1.0);
      const speed = Number.isFinite(raw) && raw >= 1.0 ? raw : 1.0;
      const enabled = Math.abs(speed - 1.0) > 0.001;
      const enableEl = $('modSpeedhackEnable');
      const speedEl = $('modSpeedhackSpeed');
      const labelEl = $('modSpeedhackEnableLabel');
      const hintEl = $('modSpeedhackHint');
      const saveBtn = $('modSpeedhackSaveBtn');
      if (enableEl) {
        enableEl.checked = enabled && available;
        enableEl.disabled = !available;
      }
      if (speedEl) {
        speedEl.value = enabled ? speed : '2.0';
        speedEl.disabled = !available || !enabled;
      }
      if (saveBtn) saveBtn.disabled = !available;
      if (labelEl) labelEl.textContent = enabled ? '变速已启用' : '变速已禁用';
      if (hintEl) {
        hintEl.textContent = !available
          ? '先安装加载器'
          : (enabled ? `当前 ${speed}x（重启游戏生效）` : '1.0 = 正常速度；最低 1.0，不支持减速');
      }
    },
    saveSpeedhack() {
      const enabled = $('modSpeedhackEnable')?.checked === true;
      const speed = Number($('modSpeedhackSpeed')?.value ?? 2.0);
      if (!(speed >= 1 && speed <= 100)) {
        toast('倍速必须在 1.0 ~ 100 之间（1.0 = 正常速度；加载器不支持减速）');
        return;
      }
      post({
        type: 'modSaveSpeedhack',
        speedhackBaseSpeed: enabled ? speed : 1.0
      });
    },

    // ---------- 加载器设置弹窗（doorstop_config.json 可视化编辑） ----------
    // 分两层：日常项直接摊开，Steam 绕过细节 / 引导 / 超时收进弹窗内的「高级选项」。
  });
})();
