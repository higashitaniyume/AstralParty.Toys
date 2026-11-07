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

    openLoaderSettings() {
      post({ type: 'modReadLoaderConfig' });
    },

    renderLoaderSettings(config) {
      if (!config) return;
      // 同步变速栏（加载器设置里的基础倍速与页面变速开关一致）
      this.syncSpeedhackBar(config);

      const basicRows = [
        ['enabled', '启用加载器', 'bool', config.enabled],
        ['speedhackBaseSpeed', '启动时基础倍速（1.0 = 正常；2.0 = 全程 2 倍速；也可留 1.0 由游戏内热键变速。不支持减速，最小 1.0）', 'number', config.speedhackBaseSpeed, { min: 1, max: 100, step: 0.1 }],
        ['speedControlEnabled', '允许游戏内热键变速（Delete 在 1.0x 与刚才的倍率之间切换，Alt+= / Alt+- 调倍率）', 'bool', config.speedControlEnabled],
        ['consoleEnabled', '显示控制台窗口（模组日志）', 'bool', config.consoleEnabled],
        ['consoleTopmost', '控制台窗口置顶', 'bool', config.consoleTopmost],
        ['debugShell', '控制台命令输入（CesiumLoader Debug Shell，输入 help 查看命令）', 'bool', config.debugShell],
        ['forwardActivityLog', '把模组日志转发到控制台', 'bool', config.forwardActivityLog],
        ['steamBypassEnabled', '不装 Steam 也能启动游戏（首页「启动方式」把这一套三个开关一起开 / 关）', 'bool', config.steamBypassEnabled],
        ['tapTapBypassEnabled', 'TapTap 自杀门绕过（首页「启动方式」绕过 TapTap 启动，TapTap 版专用）', 'bool', config.tapTapBypassEnabled]
      ];
      const advancedRows = [
        ['__steam', 'Steam 绕过细节 —— 要从 Steam 启动并使用真大厅，这三个开关必须全部关闭', 'header', ''],
        ['steamBypassRestartCheck', '附加保险：忽略 Steam 的“请从 Steam 启动”重启请求', 'bool', config.steamBypassRestartCheck],
        ['steamBypassMatchmaking', '修复点“创建 / 加入房间”没反应（大厅匹配改为直接返回空结果）', 'bool', config.steamBypassMatchmaking],
        ['steamBypassLobbyQuery', '修复退房 / 被踢时报错（房间列表查询返回空列表）', 'bool', config.steamBypassLobbyQuery],
        ['steamBypassLobbyHasValue', '阶段3 兜底（实测已作废、hook 从未命中；保持开启即可）', 'bool', config.steamBypassLobbyHasValue],
        ['steamBypassTaskCtorMode', 'Task 构造方式（auto / direct / invoke，一般保持 auto）', 'text', config.steamBypassTaskCtorMode],
        ['steamBypassLobbyMethods', '⚠ 方案B 备用安全网 —— 实测开启会导致游戏启动崩溃（0xC0000005），请保持关闭', 'bool', config.steamBypassLobbyMethods],
        ['__taptap', 'TapTap 绕过细节', 'header', ''],
        ['tapTapBypassRestartCheck', '附加保险：忽略 TapTap 重启请求（TapSDK_RestartAppIfNecessary 恒返回 0）', 'bool', config.tapTapBypassRestartCheck],
        ['tapTapBypassLogin', 'TapTap 登录 stub（预留开关）', 'bool', config.tapTapBypassLogin],
        ['__boot', '引导与超时（一般不用改）', 'header', ''],
        ['useManagedBootstrap', '使用托管引导（实验性，一般保持关闭）', 'bool', config.useManagedBootstrap],
        ['gameAssemblyTimeoutSec', '等待 GameAssembly.dll 秒数', 'number', config.gameAssemblyTimeoutSec, { min: 1, max: 600 }],
        ['domainTimeoutSec', '等待托管域初始化秒数', 'number', config.domainTimeoutSec, { min: 1, max: 600 }],
        ['hybridclrTimeoutSec', '等待 HybridCLR 热更秒数', 'number', config.hybridclrTimeoutSec, { min: 1, max: 600 }],
        ['bootstrapAssembly', '托管引导程序集', 'text', config.bootstrapAssembly],
        ['bootstrapType', '引导类型', 'text', config.bootstrapType],
        ['bootstrapMethod', '引导方法', 'text', config.bootstrapMethod],
        ['sdkVersion', 'SDK 版本（只读，随包更新）', 'readonly', config.sdkVersion]
      ];

      const html = `
        <div class="cfg-form">
          ${basicRows.map(r => this.cfgRow(...r)).join('')}
          <details class="mods-help cfg-advanced">
            <summary>高级选项（Steam 绕过细节 · 引导 · 超时）</summary>
            ${advancedRows.map(r => this.cfgRow(...r)).join('')}
          </details>
          <div class="cfg-actions">
            <button class="secondary-btn" data-cfg-cancel>取消</button>
            <button class="primary-btn" data-cfg-save>保存设置</button>
          </div>
          <div class="cfg-hint">保存后重启游戏生效；日常只需要改「启用加载器」或「启动时基础倍速」。</div>
        </div>`;
      openModal('加载器设置', html, 'settings', 'modal-wide');
      $('globalModal')?.querySelector('[data-cfg-cancel]')?.addEventListener('click', closeModal);
      $('globalModal')?.querySelector('[data-cfg-save]')?.addEventListener('click', () => {
        const cfg = this.collectLoaderConfig();
        if (cfg) post({ type: 'modSaveLoaderConfig', config: cfg });
      });
    },

    cfgRow(id, label, kind, value, attrs) {
      const a = attrs || {};
      const attrStr = Object.entries(a).map(([k, v]) => `${k}="${esc(String(v))}"`).join(' ');
      // 标签里以「⚠ 」开头的，渲染成 warning 图标 + 文本（其余仍做 HTML 转义）。
      const lbl = s => String(s).startsWith('⚠ ')
        ? Icons.svg('warning') + ' ' + esc(String(s).slice(2))
        : esc(String(s));
      switch (kind) {
        case 'bool':
          return `<label class="cfg-row cfg-check"><input type="checkbox" data-cfg="${id}" ${value ? 'checked' : ''}> <span>${lbl(label)}</span></label>`;
        case 'header':
          // 分组标题：只是视觉分隔，不参与 collectLoaderConfig 取值
          return `<div class="cfg-hint cfg-section" data-cfg-header="${id}">${lbl(label)}</div>`;
        case 'number':
          return `<label class="cfg-row"><span class="cfg-label">${lbl(label)}</span><input type="number" data-cfg="${id}" value="${esc(String(value))}" ${attrStr}></label>`;
        case 'readonly':
          return `<label class="cfg-row"><span class="cfg-label">${lbl(label)}</span><input type="text" data-cfg="${id}" value="${esc(String(value))}" readonly></label>`;
        default:
          return `<label class="cfg-row"><span class="cfg-label">${lbl(label)}</span><input type="text" data-cfg="${id}" value="${esc(String(value))}"></label>`;
      }
    },

    collectLoaderConfig() {
      const modal = $('globalModal');
      if (!modal) return null;
      const get = id => modal.querySelector(`[data-cfg="${id}"]`);
      const bool = id => get(id)?.checked === true;
      const num = id => {
        const el = get(id);
        return el ? Number(el.value) : 0;
      };
      const text = id => get(id)?.value ?? '';
      const speed = num('speedhackBaseSpeed');
      // 加载器硬性下限 1.0（加载器不接受减速）；这里必须拦住，否则会出现
      // 「界面写 0.5、实际按 1.0 跑」这种最难排查的不一致。
      if (!(speed >= 1 && speed <= 100)) {
        toast('启动时基础倍速必须在 1.0 ~ 100 之间（1.0 = 正常速度，不支持减速）');
        return null;
      }
      return {
        enabled: bool('enabled'),
        useManagedBootstrap: bool('useManagedBootstrap'),
        bootstrapAssembly: text('bootstrapAssembly'),
        bootstrapType: text('bootstrapType'),
        bootstrapMethod: text('bootstrapMethod'),
        gameAssemblyTimeoutSec: num('gameAssemblyTimeoutSec'),
        domainTimeoutSec: num('domainTimeoutSec'),
        hybridclrTimeoutSec: num('hybridclrTimeoutSec'),
        consoleEnabled: bool('consoleEnabled'),
        consoleTopmost: bool('consoleTopmost'),
        debugShell: bool('debugShell'),
        forwardActivityLog: bool('forwardActivityLog'),
        speedhackBaseSpeed: speed,
        speedControlEnabled: bool('speedControlEnabled'),
        steamBypassEnabled: bool('steamBypassEnabled'),
        steamBypassRestartCheck: bool('steamBypassRestartCheck'),
        steamBypassMatchmaking: bool('steamBypassMatchmaking'),
        steamBypassLobbyQuery: bool('steamBypassLobbyQuery'),
        steamBypassLobbyHasValue: bool('steamBypassLobbyHasValue'),
        steamBypassLobbyMethods: bool('steamBypassLobbyMethods'),
        steamBypassTaskCtorMode: text('steamBypassTaskCtorMode'),
        tapTapBypassEnabled: bool('tapTapBypassEnabled'),
        tapTapBypassRestartCheck: bool('tapTapBypassRestartCheck'),
        tapTapBypassLogin: bool('tapTapBypassLogin'),
        sdkVersion: text('sdkVersion')
      };
    },

    // ---------- mod 配置弹窗（mods\{mod}\config.json 键值表单编辑） ----------

    openModConfig(fileName) {
      post({ type: 'modReadConfigFields', fileName });
    },

    renderConfigFields(data) {
      if (!data || !data.fields) return;
      const fileName = data.fileName;
      const fields = data.fields;
      const rows = fields.length === 0
        ? '<div class="cfg-hint">还没有配置文件（mods\\' + esc(fileName.replace(/\.dll$/i, '')) + '\\config.json）。保存后将创建。</div>'
        : fields.map((f, idx) => {
            const id = `cfgfield-${idx}`;
            const described = f.comment ? ' cfg-described' : '';
            // 有说明时显示「当前语言的说明（小灰色配置名）」；没有说明的旧配置保持原样。
            const label = (suffix = '') => f.comment
              ? `<span class="cfg-label" data-i18n-skip><span class="cfg-comment">${esc(f.comment)}</span><span class="cfg-field-key">（${esc(f.name)}${esc(suffix)}）</span></span>`
              : `<span class="cfg-label">${esc(f.name)}${esc(suffix)}</span>`;
            if (f.kind === 'bool') {
              return `<label class="cfg-row cfg-check${described}"><input type="checkbox" data-field="${id}" data-kind="bool" ${f.boolValue ? 'checked' : ''}> ${label()}</label>`;
            }
            if (f.kind === 'number') {
              return `<label class="cfg-row${described}">${label()}<input type="number" step="any" data-field="${id}" data-kind="number" value="${esc(String(f.numberValue))}"></label>`;
            }
            if (f.kind === 'other') {
              return `<label class="cfg-row${described}">${label(' · JSON')}<input type="text" data-field="${id}" data-kind="other" value="${esc(f.stringValue)}"></label>`;
            }
            if (f.kind === 'key') {
              const current = f.stringValue || '';
              return `<div class="cfg-row cfg-key${described}">
                ${label()}
                <button type="button" class="key-btn" data-field="${id}" data-kind="key" data-key="${esc(current)}" title="点一下，再按下要绑的键（支持鼠标左/右/中键与侧键；Esc 取消）">${esc(current || '未设置（用 mod 默认）')}</button>
                <button type="button" class="key-clear" data-clear="${id}" title="清空（让 mod 用它自己的默认键）">×</button>
              </div>`;
            }
            return `<label class="cfg-row${described}">${label()}<input type="text" data-field="${id}" data-kind="string" value="${esc(f.stringValue)}"></label>`;
          }).join('');
      const html = `
        <div class="cfg-form">
          <div class="cfg-hint">配置保存在 <b>mods\\${esc(fileName.replace(/\.dll$/i, ''))}\\config.json</b>（与 mod 的 dll 同目录，由 mod 的 SdkConfig 读写；重启游戏后生效）。勾选框 = 开关，数字框 = 数值，文本框 = 文字；<b>按键按钮</b> = 点一下再按要绑的键（键盘任意键或鼠标左/右/中键、侧键，Esc 取消）。</div>
          ${rows}
          <div class="cfg-actions">
            <button class="secondary-btn" data-cfg-field-cancel>取消</button>
            <button class="secondary-btn" data-cfg-field-raw title="高级：用系统默认编辑器直接改 JSON">高级编辑…</button>
            <button class="primary-btn" data-cfg-field-save>保存配置</button>
          </div>
        </div>`;
      openModal(`配置 · ${fileName}`, html, 'settings', 'modal-wide');
      const modal = $('globalModal');
      bindKeyCapture(modal);
      modal?.querySelector('[data-cfg-field-cancel]')?.addEventListener('click', closeModal);
      modal?.querySelector('[data-cfg-field-raw]')?.addEventListener('click', () => {
        closeModal();
        post({ type: 'modOpenConfigRaw', fileName });
      });
      modal?.querySelector('[data-cfg-field-save]')?.addEventListener('click', () => {
        const out = fields.map((f, idx) => {
          const el = modal.querySelector(`[data-field="cfgfield-${idx}"]`);
          const kind = el?.dataset.kind || f.kind;
          if (kind === 'bool') return { name: f.name, kind: 'bool', boolValue: el?.checked === true };
          if (kind === 'number') return { name: f.name, kind: 'number', numberValue: Number(el?.value ?? f.numberValue) };
          // 键位在后台就是普通字符串, 只是 UI 用"捕获"来采集
          if (kind === 'key') return { name: f.name, kind: 'string', stringValue: el?.dataset.key ?? f.stringValue ?? '' };
          // 嵌套对象/数组按原 JSON 回写，不能当字符串存（否则 _comments / 列表会被写坏）
          if (kind === 'other') return { name: f.name, kind: 'other', stringValue: el?.value ?? f.stringValue };
          return { name: f.name, kind: 'string', stringValue: el?.value ?? f.stringValue };
        });
        post({ type: 'modSaveConfigFields', fileName, fields: out });
      });
    }
  };

  // =====================================================================
  // 按键捕获（mod 配置里的 toggleKey / speedUpKey / resetKey …）
  //
  // mod 侧(Unity)读的是 KeyCode 名字，所以这里要把浏览器的事件翻译成那套名字：
  //   键盘：event.code（"KeyA" / "Digit1" / "NumpadAdd" / "AltLeft" …）
  //   鼠标：event.button（0 左 / 1 中 / 2 右 / 3 侧键"后退" / 4 侧键"前进"）
  // 认不出来的键（媒体键等）不写进配置，只提示 —— 绝不猜一个可能不对的名字存下去。
  // =====================================================================

  // 与 Unity KeyCode 名字相同的部分（不用换算）
  const UNITY_KEY_BY_CODE = {
    Backquote: 'BackQuote', Minus: 'Minus', Equal: 'Equals', Backspace: 'Backspace', Tab: 'Tab',
    CapsLock: 'CapsLock', Space: 'Space', Enter: 'Return', NumpadEnter: 'KeypadEnter',
    Escape: 'Escape', Delete: 'Delete', Insert: 'Insert', Home: 'Home', End: 'End',
    PageUp: 'PageUp', PageDown: 'PageDown', PrintScreen: 'Print', ScrollLock: 'ScrollLock', Pause: 'Pause',
    ArrowUp: 'UpArrow', ArrowDown: 'DownArrow', ArrowLeft: 'LeftArrow', ArrowRight: 'RightArrow',
    ShiftLeft: 'LeftShift', ShiftRight: 'RightShift',
    ControlLeft: 'LeftControl', ControlRight: 'RightControl',
    AltLeft: 'LeftAlt', AltRight: 'RightAlt',
    MetaLeft: 'LeftWindows', MetaRight: 'RightWindows',
    BracketLeft: 'LeftBracket', BracketRight: 'RightBracket', Backslash: 'Backslash',
    Semicolon: 'Semicolon', Quote: 'Quote', Comma: 'Comma', Period: 'Period', Slash: 'Slash',
    NumpadAdd: 'KeypadPlus', NumpadSubtract: 'KeypadMinus', NumpadMultiply: 'KeypadMultiply',
    NumpadDivide: 'KeypadDivide', NumpadDecimal: 'KeypadPeriod', NumLock: 'Numlock', ContextMenu: 'Menu'
  };

  // 浏览器鼠标键号 → Unity Mouse0-4（注意中间/右键的编号不是直觉顺序：Unity 里 1 = 右键、2 = 中键）
  const UNITY_KEY_BY_MOUSE_BUTTON = { 0: 'Mouse0', 1: 'Mouse2', 2: 'Mouse1', 3: 'Mouse3', 4: 'Mouse4' };

  function unityKeyNameFromCode(code) {
    if (!code) return '';
    if (UNITY_KEY_BY_CODE[code]) return UNITY_KEY_BY_CODE[code];
    let m = /^Key([A-Z])$/.exec(code);        // KeyA…KeyZ → A…Z
    if (m) return m[1];
    m = /^Digit([0-9])$/.exec(code);          // Digit0…Digit9 → Alpha0…Alpha9
    if (m) return 'Alpha' + m[1];
    m = /^F([1-9]|1[0-2])$/.exec(code);       // F1…F12
    if (m) return code;
    m = /^Numpad([0-9])$/.exec(code);         // Numpad0…Numpad9 → Keypad0…Keypad9
    if (m) return 'Keypad' + m[1];
    return '';
  }

  let stopKeyCapture = null;   // 当前正在捕获的那一个（同一时刻只允许一个）

  function bindKeyCapture(modal) {
    if (!modal) return;

    modal.querySelectorAll('.key-btn').forEach(btn => {
      btn.addEventListener('click', () => startKeyCapture(btn));
    });

    // 清空 = 让 mod 用它的默认键（mod 读到空值会退回内置默认，例如变速的 Delete）
    modal.querySelectorAll('.key-clear').forEach(btn => {
      btn.addEventListener('click', () => {
        const target = modal.querySelector(`[data-field="${btn.dataset.clear}"]`);
        if (target) setKeyButtonValue(target, '');
      });
    });
  }

  function setKeyButtonValue(btn, name) {
    btn.dataset.key = name;
    btn.textContent = name || '未设置（用 mod 默认）';
  }

  function startKeyCapture(btn) {
    if (stopKeyCapture) stopKeyCapture();   // 前一个还开着：直接取消

    const original = btn.textContent;
    btn.classList.add('capturing');
    btn.textContent = '按下按键…（Esc 取消）';

    const onKeyDown = e => {
      e.preventDefault();
      e.stopImmediatePropagation();
      if (e.repeat) return;
      if (e.key === 'Escape' || e.code === 'Escape') { done('', '取消'); return; }
      const name = unityKeyNameFromCode(e.code);
      if (!name) { toast('这个键不支持，请换一个'); return; }
      done(name, '已设为 ' + name);
    };

    const onMouseDown = e => {
      e.preventDefault();
      e.stopImmediatePropagation();
      const name = UNITY_KEY_BY_MOUSE_BUTTON[e.button];
      if (!name) { toast('这个鼠标键不支持，请换一个'); return; }
