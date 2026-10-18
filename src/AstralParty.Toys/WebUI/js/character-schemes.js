(() => {
  const esc = (value) =>
    String(value ?? "").replace(
      /[&<>"']/g,
      (c) =>
        ({
          "&": "&amp;",
          "<": "&lt;",
          ">": "&gt;",
          '"': "&quot;",
          "'": "&#39;",
        })[c],
    );
  const field = (o, key) =>
    o?.[key] ?? o?.[key[0].toUpperCase() + key.slice(1)];
  const module = {
    data: null,
    selected: null,
    pending: null,
    mode: "schemes",
    editing: null,
    editingPackage: null,
    creatingContext: null,
    awaitingPackages: new Map(),
    init() {
      $("characterSchemesMode").onclick = () => this.setMode("schemes");
      $("characterPackagesMode").onclick = () => this.setMode("packages");
      const dialogs = document.createElement("div");
      dialogs.innerHTML = `<dialog id="characterSchemeCreateDialog" aria-labelledby="characterSchemeCreateTitle"><form id="characterSchemeCreateForm"><h3 id="characterSchemeCreateTitle">新建方案</h3><input type="hidden" name="id"><label>方案名称<input name="name" required maxlength="100" placeholder="例如：我的日常搭配" autofocus></label><p>先给搭配起个名字，再添加喜欢的人物皮肤。</p><p id="characterSchemeCreateError" class="character-error" role="alert"></p><div class="character-toolbar"><button class="primary-btn" type="submit">创建方案</button><button class="secondary-btn" type="button" data-close="characterSchemeCreateDialog">取消</button></div></form></dialog>
      <dialog id="characterBindingDialog" aria-labelledby="characterBindingTitle"><form id="characterBindingForm"><h3 id="characterBindingTitle">添加人物皮肤</h3><label>人物<select id="characterBindingHero" required></select></label><label>使用方式<select id="characterBindingMode"><option value="package">已有皮肤包</option><option value="create">新建自定义皮肤</option><option value="official">官方皮肤</option></select></label><div id="characterBindingPackageArea"><label>人物皮肤包<select id="characterBindingPackage"></select></label><button type="button" class="secondary-btn" id="characterBindingImportPackage">从 ZIP 导入此人物皮肤包</button><p id="characterBindingPackageHint"></p></div><div id="characterBindingCreateArea" hidden><p>创建后会自动加入当前方案，接着编辑图片、动画和文案。</p><button type="button" class="primary-btn" id="characterBindingCreatePackage">继续创建皮肤包</button></div><div id="characterBindingOfficialArea"></div><details id="characterBindingAdvanced" class="cs-disclosure"><summary>高级设置：替换范围与混合搭配</summary><label>替换哪些原皮肤<select id="characterBindingTarget" required></select></label><div id="characterBindingOfficialAdvanced"><label id="characterBindingOfficialLabel">官方皮肤基础<select id="characterBindingOfficial"></select></label></div><p>默认替换此人物的全部原皮肤。自定义素材优先，未修改的部分沿用所选基础。</p></details><p id="characterBindingError" class="character-error" role="alert"></p><div class="character-toolbar"><button id="characterBindingSave" class="primary-btn" type="submit">添加到方案</button><button class="secondary-btn" type="button" data-close="characterBindingDialog">取消</button></div></form></dialog>
      <dialog id="characterSchemeMetaDialog" aria-labelledby="characterSchemeMetaTitle"><form id="characterSchemeMetaForm"><h3 id="characterSchemeMetaTitle">编辑方案信息</h3><p class="cs-dialog-hint">方案标识：<code id="characterSchemeMetaId"></code>（创建后不可修改）</p><label>方案名称<input name="name" required maxlength="100" placeholder="例如：我的日常搭配" autofocus></label><label>作者<input name="author" maxlength="100" placeholder="昵称，可留空"></label><label>方案简介<textarea name="description" rows="4" maxlength="2000" placeholder="介绍一下这套搭配，例如适合的玩法或角色组合。"></textarea></label><p id="characterSchemeMetaError" class="character-error" role="alert"></p><div class="character-toolbar"><button class="primary-btn" type="submit">保存信息</button><button class="secondary-btn" type="button" data-close="characterSchemeMetaDialog">取消</button></div></form></dialog>`;
      $("characterSkinPanel").appendChild(dialogs);
      // Keep the existing material editor, but host it inside the selected scheme when editing a member.
      this.detailHome = $("characterDetail").parentElement;
      $("characterSkinPanel").appendChild($("characterCreateDialog"));
      const editor = document.createElement("section");
      editor.id = "characterSchemePackageEditor";
      editor.className = "hidden";
      editor.innerHTML =
        '<div class="character-toolbar"><button id="characterSchemeEditorBack" class="secondary-btn">返回当前方案</button><strong id="characterSchemeEditorContext"></strong></div><div id="characterSchemeEditorBody"></div>';
      $("characterSkinPanel").appendChild(editor);
      $("characterSchemeEditorBack").onclick = () => this.setMode("schemes");
      $("characterBindingCreatePackage").onclick = () =>
        this.createPackageFromBinding();
      $("characterBindingImportPackage").onclick = () =>
        this.importPackageFromBinding();
      $("characterCreateDialog").addEventListener("close", () => {
        this.creatingContext = null;
        $("characterCreateHero").disabled = false;
      });
      dialogs
        .querySelectorAll("[data-close]")
        .forEach((b) => (b.onclick = () => $(b.dataset.close).close()));
      $("characterSchemeCreateForm").onsubmit = (e) => {
        e.preventDefault();
        const f = new FormData(e.target);
        const name=String(f.get("name")||"").trim();
        if(!name){$("characterSchemeCreateError").textContent="请输入方案名称。";return;}
        const id=f.get("id")||"scheme-"+crypto.randomUUID();
        e.target.elements.namedItem("id").value=id;
        this.pending = id;
        post({
          type: "characterSchemesCreate",
          id,
          name,
        });
      };
      $("characterSchemeMetaForm").onsubmit = (e) => {
        e.preventDefault();
        const f = new FormData(e.target);
        const name = String(f.get("name") || "").trim();
        if (!name) {
          $("characterSchemeMetaError").textContent = "请输入方案名称。";
          return;
        }
        $("characterSchemeMetaError").textContent = "";
        post({
          type: "characterSchemesUpdateMetadata",
          id: this.selected,
          name,
          author: String(f.get("author") || "").trim(),
          description: String(f.get("description") || "").trim(),
        });
      };
      $("characterSchemeMetaDialog").addEventListener("close", () => {
        $("characterSchemeMetaError").textContent = "";
      });
      $("characterBindingHero").onchange = () => {this.bindingOptions();if(!this.editing)this.chooseBindingDefault();this.updateBindingMode();};
      $("characterBindingMode").onchange = () => this.updateBindingMode(true);
      $("characterBindingForm").onsubmit = (e) => this.saveBinding(e);
      $("characterBindingPackage").onchange = () => this.validateBinding();
      $("characterBindingOfficial").onchange = () => this.validateBinding();
    },
    setMode(mode) {
      this.mode = mode;
      this.editingPackage = null;
      window.CharacterSkinsModule?.stop();
      if (
        this.detailHome &&
        $("characterDetail").parentElement !== this.detailHome
      )
        this.detailHome.appendChild($("characterDetail"));
      $("characterSchemePackageEditor").classList.add("hidden");
      $("characterSchemesPanel").classList.toggle("hidden", mode !== "schemes");
      $("characterPackagesPanel").classList.toggle(
        "hidden",
        mode !== "packages",
      );
      for (const [id, on] of [
        ["characterSchemesMode", mode === "schemes"],
        ["characterPackagesMode", mode === "packages"],
      ]) {
        $(id).setAttribute("aria-pressed", on);
        $(id).className = on ? "primary-btn" : "secondary-btn";
      }
      if(mode==="packages"&&window.CharacterSkinsModule?.data){window.CharacterSkinsModule.renderList();window.CharacterSkinsModule.renderDetail();}
    },
    render(payload) {
      if (this.data?.gameDirectory !== payload.gameDirectory) {
        this.selected = null;
        this.pending = null;
        this.editing = null;
        this.search = "";
        this.creatingContext = null;
        this.awaitingPackages.clear();
        this.setMode("schemes");
        $("characterBindingDialog").close();
        $("characterSchemeCreateDialog").close();
        $("characterCreateDialog").close();
      }
      this.data = payload;
      const profiles = payload.schemeProfiles || [];
      if (
        this.pending &&
        profiles.some((p) => field(p, "id") === this.pending)
      ) {
        this.selected = this.pending;
        this.search = "";
        this.pending = null;
        $("characterSchemeCreateDialog").close();
      }
      if (!profiles.some((p) => field(p, "id") === this.selected))
        this.selected =
          payload.activeSchemeId || field(profiles[0], "id") || null;
      this.renderPanel();
      if (this.editingPackage) {
        if (
          (payload.profiles || []).some(
            (p) => field(p, "id") === this.editingPackage,
          ) &&
          this.profile()
        )
          this.showPackageEditor(this.editingPackage);
        else this.setMode("schemes");
      }
      for (const [id, context] of this.awaitingPackages) {
        const p = profiles.find((p) => field(p, "id") === context.schemeId);
        if (
          (field(field(p, "scheme"), "rules") || []).some(
            (r) => field(r, "customPackageId") === id,
          )
        ) {
          this.awaitingPackages.delete(id);
          if (
            this.mode === "schemes" &&
            this.selected === context.schemeId &&
            !this.editingPackage
          )
            this.editPackage(id);
        }
      }
    },
    profile() {
      return (this.data?.schemeProfiles || []).find(
        (p) => field(p, "id") === this.selected,
      );
    },
    heroName(id) {
      return this.data?.heroes?.find((h) => h.id === id)?.name || `角色 ${id}`;
    },
    skinName(hero, id) {
      const t = (this.data?.targets || []).find(
        (t) => field(t, "heroId") === hero && field(t, "itemId") === id,
      );
      return t ? field(t, "name") : "未识别的皮肤";
    },
    renderPanel() {
      const profiles = this.data?.schemeProfiles || [],
        p = this.profile(),
        s = field(p, "scheme"),
        rules = field(s, "rules") || [];
      const active=!!field(p,"active"), invalid=!!field(p,"error"), count=new Set(rules.map(r=>field(r,"heroId"))).size;
      $("characterSchemesPanel").innerHTML = `<div class="character-workspace cs-scheme-workspace"><aside class="character-list-panel cs-scheme-sidebar"><div class="cs-section-heading"><h3>我的方案</h3><button class="primary-btn" id="characterSchemeNew">新建方案</button></div><label class="cs-search-label" for="characterSchemeSearch">搜索方案</label><input type="search" id="characterSchemeSearch" placeholder="输入方案名称" value="${esc(this.search||'')}"><div id="characterSchemeList">${profiles.map(item=>{
        const scheme=field(item,"scheme"), rs=field(scheme,"rules")||[], id=field(item,"id");
        return `<button class="character-package ${id===this.selected?'active':''}" data-scheme="${esc(id)}" data-name="${esc(field(scheme,'name')||id)}" data-search="${esc([field(scheme,'name')||id,field(scheme,'author')||'',field(scheme,'description')||''].join(' ').toLocaleLowerCase())}" aria-pressed="${id===this.selected}"><strong>${esc(field(scheme,'name')||'未命名方案')}</strong><small>${field(item,'error')?'需要修复':`${new Set(rs.map(r=>field(r,'heroId'))).size} 个人物 · ${field(item,'active')?'已启用':'未启用'}`}</small></button>`;
      }).join('')}<p id="characterSchemeSearchEmpty" hidden>没有匹配的方案。</p></div><details class="cs-disclosure"><summary>导入与管理</summary><div class="cs-menu-actions"><button class="secondary-btn" id="characterSchemeImport">导入方案 ZIP</button><button class="secondary-btn" id="characterSchemeFolder">打开方案目录</button><button class="secondary-btn" id="characterSchemeRefresh">刷新方案与状态</button>${this.data?.installed&&!this.data?.updateRequired?'<button class="secondary-btn" id="characterSchemeInstall">更新角色皮肤模组</button>':''}</div></details></aside><div class="character-detail cs-scheme-detail">${field(this.data?.runtimeStatus,"state")==="Failed"?`<div class="cs-install-notice" role="alert"><p>上次游戏未能应用方案：${esc(field(this.data.runtimeStatus,"error")||"请展开模组状态查看详情。")}</p></div>`:""}${!this.data?.installed||this.data?.updateRequired?`<div class="cs-install-notice" role="status"><p>${this.data?.installed?`游戏中的角色皮肤模组为 ${esc(this.data.installedVersion||'未知版本')}，请更新到 ${esc(this.data.bundledVersion)}，然后重启游戏以使用最新修复。`:'先安装角色皮肤模组，方案才能在游戏中生效。'}</p><button class="primary-btn" id="characterSchemeInstall">${this.data?.installed?'更新角色皮肤模组':'安装角色皮肤模组'}</button></div>`:''}${!p?`<div class="cs-empty"><h3>把喜欢的人物放进同一个方案</h3><p>新建一个方案，再添加人物皮肤。之后可以一键切换整套搭配。</p><button class="primary-btn" id="characterSchemeEmptyCreate">新建第一个方案</button></div>`:`<header class="cs-scheme-heading"><div><p class="cs-eyebrow">多人物搭配 · ${count} 个人物</p><h3>${esc(field(s,'name')||'未命名方案')}</h3>${field(s,'author')?`<p class="cs-scheme-byline">作者：${esc(field(s,'author'))}</p>`:''}<span class="cs-scheme-state ${active?'is-active':''}">${active?'已启用 · 重启游戏后生效':'尚未启用'}</span></div><button class="${active?'secondary-btn':'primary-btn'}" id="characterSchemeEnable" ${invalid?'disabled':''}>${active?'停用此方案':'应用此方案'}</button></header>${field(s,'description')?`<p class="cs-scheme-description">${esc(field(s,'description'))}</p>`:''}<p class="cs-scheme-help">添加或修改人物后，完全退出并重启游戏以应用搭配。</p>${invalid?`<p class="character-error" role="alert">${esc(field(p,'error'))}</p>`:''}<details class="cs-disclosure cs-scheme-actions"><summary>方案设置</summary>${!invalid?`<button class="secondary-btn" id="characterSchemeEditMeta">编辑方案信息</button>`:''}<div class="character-toolbar"><button class="secondary-btn" id="characterSchemeExport" ${invalid?'disabled':''}>导出方案 ZIP</button><button class="secondary-btn" id="characterSchemeOpen">打开目录</button><button class="danger-btn" id="characterSchemeDelete">删除方案</button></div></details>${invalid?'':`<div class="cs-section-heading cs-members-heading"><h4>方案中的人物</h4><div class="character-toolbar"><button class="primary-btn" id="characterBindingNew">添加人物皮肤</button><button class="secondary-btn" id="characterSchemeImportPackage">导入人物包</button></div></div><div class="character-binding-list">${rules.map(r=>{
        const hero=field(r,'heroId'),official=field(r,'officialSourceSkinItemId'),custom=field(r,'customPackageId'),rid=field(r,'id');
        const pkg=(this.data.profiles||[]).find(item=>field(item,'id')===custom), name=field(field(pkg,'manifest'),'name')||'自定义皮肤包';
        const summary=custom?`${name}${official?' + '+this.skinName(hero,official):''}`:this.skinName(hero,official);
        return `<article class="character-binding"><span class="cs-source-label">${custom?(official?'自定义 + 官方基础':'自定义皮肤'):'官方皮肤'}</span><h4>${esc(this.heroName(hero))}</h4><p class="cs-source-summary">${esc(summary)}</p><p class="cs-target-summary">${field(r,'targetSkinItemId')?'仅替换 '+esc(this.skinName(hero,field(r,'targetSkinItemId'))):'适用于此人物全部原皮肤'}</p>${custom?`<button class="primary-btn" data-edit-package="${esc(custom)}">编辑人物皮肤</button>`:`<button class="primary-btn" data-edit-binding="${esc(rid)}">更换官方皮肤</button>`}<details class="cs-disclosure"><summary>人物设置</summary><div class="cs-menu-actions">${custom?`<button class="secondary-btn" data-edit-binding="${esc(rid)}">来源与替换范围</button>`:`<button class="secondary-btn" data-create-package="${esc(rid)}">创建自定义覆盖</button>`}<button class="link-btn" data-delete-binding="${esc(rid)}">从方案移除此人物</button></div></details></article>`;
      }).join('')||`<div class="cs-empty"><h4>还没有添加人物</h4><p>选择已有皮肤包、制作自定义皮肤，或搭配官方皮肤。</p><button class="primary-btn" id="characterBindingEmptyAdd">添加第一个人物</button></div>`}</div>`}`}${this.data?.migrationError?`<p class="character-error" role="alert">旧配置已保留，但暂未转换：${esc(this.data.migrationError)}</p>`:''}<details class="cs-disclosure cs-runtime-details"><summary>模组状态与使用说明</summary><p>${this.data?.installed?'角色皮肤模组已安装':'尚未安装角色皮肤模组，请从左侧管理菜单安装。'}</p>${this.runtimeSummary()}<p>官方来源替换图片与人物动画。语音、视频、特效及特殊演出保留原版。</p></details></div></div>`;
      const filter=()=>{this.search=$("characterSchemeSearch").value;let visible=0;$("characterSchemeList").querySelectorAll('[data-scheme]').forEach(b=>{b.hidden=!b.dataset.search.includes(this.search.trim().toLocaleLowerCase());if(!b.hidden)visible++;});$("characterSchemeSearchEmpty").hidden=visible>0;};
      $("characterSchemeSearch").oninput=filter;filter();
      if($("characterSchemeEmptyCreate"))$("characterSchemeEmptyCreate").onclick=()=>$("characterSchemeNew").click();
      if($("characterBindingEmptyAdd"))$("characterBindingEmptyAdd").onclick=()=>this.openBinding();
      $("characterSchemeNew").onclick = () => {
        $("characterSchemeCreateForm").reset();
        $("characterSchemeCreateError").textContent="";
        $("characterSchemeCreateDialog").showModal();
      };
      $("characterSchemeImport").onclick = () =>
        post({ type: "characterSchemesImport" });
      $("characterSchemeFolder").onclick = () =>
        post({ type: "characterSchemesOpen", id: "" });
      $("characterSchemeRefresh").onclick = () =>
        post({ type: "characterSkinsGet" });
      if ($("characterSchemeInstall"))
        $("characterSchemeInstall").onclick = () =>
          post({ type: "characterSkinsInstall" });
      $("characterSchemesPanel")
        .querySelectorAll("[data-scheme]")
        .forEach(
          (b) =>
            (b.onclick = () => {
              this.selected = b.dataset.scheme;
              this.renderPanel();
            }),
        );
      if (!p) return;
      $("characterSchemeEnable").onclick = () =>
        post({
          type: "characterSchemesActive",
          id: field(p, "active") ? "" : this.selected,
        });
      $("characterSchemeExport").onclick = () =>
        post({ type: "characterSchemesExport", id: this.selected });
      $("characterSchemeOpen").onclick = () =>
        post({ type: "characterSchemesOpen", id: this.selected });
      $("characterSchemeDelete").onclick = () =>
        window.SkinsModule.confirmAction(
          "删除整个方案？",
          "方案会移入 .deleted 文件夹，引用的自定义皮肤包会保留。",
          { type: "characterSchemesDelete", id: this.selected },
        );
      if (field(p, "error")) return;
      $("characterSchemeEditMeta").onclick = () => this.openMeta();
      $("characterBindingNew").onclick = () => this.openBinding();
      $("characterSchemeImportPackage").onclick = () => post({type:"characterSchemesImportPackage",id:this.selected});
      $("characterSchemesPanel").querySelectorAll("[data-create-package]").forEach(b=>b.onclick=()=>{
        this.openBinding(rules.find(r=>field(r,"id")===b.dataset.createPackage));
        this.createPackageFromBinding();
      });
      $("characterSchemesPanel")
        .querySelectorAll("[data-edit-binding]")
        .forEach(
          (b) =>
            (b.onclick = () =>
              this.openBinding(
                rules.find((r) => field(r, "id") === b.dataset.editBinding),
              )),
        );
      $("characterSchemesPanel")
        .querySelectorAll("[data-delete-binding]")
        .forEach(
          (b) =>
            (b.onclick = () =>
              window.SkinsModule.confirmAction(
                "移除此人物规则？",
                "只移除当前方案中的规则，自定义皮肤包会保留。",
                {
                  type: "characterSchemesRemoveRule",
                  id: this.selected,
                  ruleId: b.dataset.deleteBinding,
                },
              )),
        );
      $("characterSchemesPanel")
        .querySelectorAll("[data-edit-package]")
        .forEach(
          (b) => (b.onclick = () => this.editPackage(b.dataset.editPackage)),
        );
    },
    runtimeSummary() {
      const status = this.data?.runtimeStatus,
        state = field(status, "state");
      if (!state) return "";
      const states = {
        Disabled: "未启用",
        Pending: "等待游戏配置",
        Loading: "正在准备资源",
        Applied: "已应用",
        Failed: "应用失败",
      };
      const error = field(status, "error");
      const schemeName=field(field((this.data.schemeProfiles||[]).find(p=>field(p,"id")===field(status,"activeSchemeId")),"scheme"),"name")||"未选择方案";
      return `<p class="${error ? "character-error" : "character-note"}" role="status">最近一次 Mod 记录：${esc(schemeName)} · ${esc(states[state] || state)} · 模组 ${esc(field(status, "version"))}${error ? `。${esc(error)}` : ""}。修改配置后，重启游戏并刷新可查看新结果。</p>`;
    },
    copyScheme() {
      const s = field(this.profile(), "scheme");
      return {
        SchemaVersion: field(s, "schemaVersion"),
        Id: field(s, "id"),
        Name: field(s, "name"),
        Author: field(s, "author") || "",
        Description: field(s, "description") || "",
        Rules: (field(s, "rules") || []).map((r) => ({
          Id: field(r, "id"),
          HeroId: field(r, "heroId"),
          TargetSkinItemId: field(r, "targetSkinItemId"),
          OfficialSourceSkinItemId: field(r, "officialSourceSkinItemId"),
          CustomPackageId: field(r, "customPackageId") || "",
        })),
      };
    },
    save(scheme) {
      post({ type: "characterSchemesSave", id: this.selected, scheme });
    },
    openMeta() {
      const p = this.profile();
      if (!p || field(p, "error")) {
        toast("请先创建或选择一个有效方案。");
        return;
      }
      const s = field(p, "scheme");
      const form = $("characterSchemeMetaForm");
      form.reset();
      $("characterSchemeMetaError").textContent = "";
      $("characterSchemeMetaId").textContent = field(s, "id") || "";
      form.elements.namedItem("name").value = field(s, "name") || "";
      form.elements.namedItem("author").value = field(s, "author") || "";
      form.elements.namedItem("description").value =
        field(s, "description") || "";
      $("characterSchemeMetaDialog").showModal();
    },
    openBinding(rule = null, packageId = null) {
      if (!this.profile() || field(this.profile(), "error")) {
        toast("请先创建或选择一个有效方案。");
        this.setMode("schemes");
        return;
      }
      this.editing = rule;
      this.bindingSchemeId = this.selected;
      $("characterBindingTitle").textContent = rule
        ? "编辑人物皮肤配置"
        : "添加人物皮肤";
      $("characterBindingHero").innerHTML = (this.data.heroes || [])
        .map((h) => `<option value="${h.id}">${esc(h.name)}</option>`)
        .join("");
      const pkg = (this.data.profiles || []).find(
        (p) => field(p, "id") === packageId,
      );
      if (rule) $("characterBindingHero").value = field(rule, "heroId");
      else if (pkg)
        $("characterBindingHero").value = field(
          field(pkg, "manifest"),
          "heroId",
        );
      this.bindingOptions();
      if (rule) {
        $("characterBindingTarget").value = field(rule, "targetSkinItemId");
        $("characterBindingOfficial").value = field(
          rule,
          "officialSourceSkinItemId",
        );
        $("characterBindingPackage").value =
          field(rule, "customPackageId") || "";
      }
      if (pkg) {
        $("characterBindingTarget").value = field(
          field(pkg, "manifest"),
          "skinItemId",
        );
        $("characterBindingPackage").value = packageId;
      }
      $("characterBindingMode").value=$('characterBindingPackage').value?'package':Number($('characterBindingOfficial').value)?'official':'package';
      if(!rule&&!pkg)this.chooseBindingDefault();
      $("characterBindingAdvanced").open=Number($('characterBindingTarget').value)!==0 || (!!$('characterBindingPackage').value && Number($('characterBindingOfficial').value)>0);
      this.updateBindingMode();
      $("characterBindingSave").textContent=rule?'保存配置':'添加到方案';
      $("characterBindingError").textContent = "";
      $("characterBindingDialog").showModal();
    },
    bindingOptions() {
      const hero = Number($("characterBindingHero").value),
        options = (this.data.targets || [])
          .filter((t) => field(t, "heroId") === hero)
          .map(
            (t) =>
              `<option value="${field(t, "itemId")}">${esc(field(t, "name"))}</option>`,
          )
          .join("");
      $("characterBindingTarget").innerHTML =
        '<option value="0">该人物全部原皮肤</option>' + options;
      $("characterBindingOfficial").innerHTML =
        '<option value="0">沿用原皮肤</option>' + options;
      $("characterBindingPackage").innerHTML =
        '<option value="">不添加自定义覆盖</option>' +
        (this.data.profiles || [])
          .filter(
            (p) =>
              !field(p, "error") &&
              field(field(p, "manifest"), "heroId") === hero,
          )
          .map(
            (p) =>
              `<option value="${esc(field(p, "id"))}">${esc(field(field(p, "manifest"), "name") || field(p, "id"))}</option>`,
          )
          .join("");
      $("characterBindingError").textContent = "";
    },
    chooseBindingDefault() {
      const packages=$("characterBindingPackage");
      $("characterBindingMode").value=packages.options.length>1?"package":"create";
      packages.selectedIndex=packages.options.length>1?1:0;
    },
    updateBindingMode(changed=false) {
      const mode=$("characterBindingMode").value;
      $("characterBindingPackageArea").hidden=mode!=="package";
      $("characterBindingCreateArea").hidden=mode!=="create";
      $("characterBindingSave").hidden=mode==="create";
      $(mode==="official"?"characterBindingOfficialArea":"characterBindingOfficialAdvanced").appendChild($("characterBindingOfficialLabel"));
      if(changed && mode!=="package")$("characterBindingPackage").value="";
      if(mode!=="official" && Number($("characterBindingOfficial").value)>0)$("characterBindingAdvanced").open=true;
      $("characterBindingPackageHint").textContent=$("characterBindingPackage").options.length>1?'也可以导入新的皮肤包。':'此人物还没有皮肤包。可导入 ZIP，或选择「新建自定义皮肤」。';
      $("characterBindingError").textContent="";
    },
    validateBinding() {
      const valid =
        Number($("characterBindingOfficial").value) > 0 ||
        !!$("characterBindingPackage").value;
      $("characterBindingError").textContent = valid
        ? ""
        : "请选择官方皮肤基础或自定义皮肤包。";
      return valid;
    },
    saveBinding(e) {
      e.preventDefault();
      if($("characterBindingMode").value==="create"){this.createPackageFromBinding();return;}
      if (!this.validateBinding()) return;
      if (this.bindingSchemeId !== this.selected) {
        $("characterBindingError").textContent =
          "当前方案已切换，请重新打开规则编辑。";
        return;
      }
      const copy = this.copyScheme(),
        id = field(this.editing, "id") || "rule-" + crypto.randomUUID();
      const rule = {
        Id: id,
        HeroId: Number($("characterBindingHero").value),
        TargetSkinItemId: Number($("characterBindingTarget").value),
        OfficialSourceSkinItemId: Number($("characterBindingOfficial").value),
        CustomPackageId: $("characterBindingPackage").value,
      };
      if (
        copy.Rules.some(
          (r) =>
            r.Id !== id &&
            r.HeroId === rule.HeroId &&
            r.TargetSkinItemId === rule.TargetSkinItemId,
        )
      ) {
        $("characterBindingError").textContent =
          "此人物的相同原皮肤已有规则，请编辑原规则。";
        return;
      }
      copy.Rules = copy.Rules.filter((r) => r.Id !== id);
      copy.Rules.push(rule);
      this.save(copy);
    },
    saved(payload) {
      if (payload.success) {
        $("characterSchemeMetaDialog").close();
        $("characterBindingDialog").close();
        $("characterSchemeCreateDialog").close();
      } else if ($("characterSchemeMetaDialog").open)
        $("characterSchemeMetaError").textContent = payload.error || "保存失败";
      else if ($("characterBindingDialog").open)
        $("characterBindingError").textContent = payload.error || "保存失败";
      else if($("characterSchemeCreateDialog").open)$("characterSchemeCreateError").textContent=payload.error||"创建失败";
    },
    readBindingDraft() {
      return {Id:field(this.editing,"id")||"rule-"+crypto.randomUUID(),HeroId:Number($("characterBindingHero").value),TargetSkinItemId:Number($("characterBindingTarget").value),OfficialSourceSkinItemId:Number($("characterBindingOfficial").value),CustomPackageId:$("characterBindingPackage").value};
    },
    createPackageFromBinding() {
      if(this.bindingSchemeId!==this.selected){$("characterBindingError").textContent="当前方案已切换，请重新添加人物。";return;}
      const binding=this.readBindingDraft();
      if(this.copyScheme().Rules.some(r=>r.Id!==binding.Id&&r.HeroId===binding.HeroId&&r.TargetSkinItemId===binding.TargetSkinItemId)){
        $("characterBindingError").textContent="此人物的相同原皮肤已有配置，请编辑已有的人物皮肤。";return;
      }
      this.creatingContext={schemeId:this.selected,binding,gameDirectory:this.data.gameDirectory};
      $("characterBindingDialog").close();
      const form=$("characterCreateForm");form.reset();
      $("characterCreateHero").value=String(binding.HeroId);
      window.CharacterSkinsModule.renderTargets();
      $("characterNew").click();
      $("characterCreateHero").disabled=true;
      $("characterCreateSkin").value=String(binding.TargetSkinItemId);
      const source=binding.OfficialSourceSkinItemId||binding.TargetSkinItemId;
      if(source&&[...$("characterTemplateSource").options].some(o=>o.value===String(source)))$("characterTemplateSource").value=String(source);
      $("characterCreateFromGame").checked=binding.OfficialSourceSkinItemId===0;
      $("characterCreateFromGame").onchange();
      form.elements.namedItem("id").value=this.selected.slice(0,35)+"-hero-"+binding.HeroId+"-"+Date.now().toString(36);
      form.elements.namedItem("name").value=this.heroName(binding.HeroId)+" · "+field(field(this.profile(),"scheme"),"name");
      $("characterTemplateProgress").textContent="创建成功后自动添加到当前方案，并进入此人物的图片、文案和动画编辑器。";
    },
    attachmentForCreate(id) {
      const context=this.creatingContext;if(!context)return {};
      this.awaitingPackages.set(id,context);this.creatingContext=null;
      return {schemeId:context.schemeId,binding:context.binding,gameDirectory:context.gameDirectory};
    },
    importPackageFromBinding() {
      if(this.bindingSchemeId!==this.selected){$("characterBindingError").textContent="当前方案已切换，请重新添加人物。";return;}
      post({type:"characterSchemesImportPackage",id:this.selected,binding:this.readBindingDraft(),gameDirectory:this.data.gameDirectory});
    },
    packageAttached(payload) {
      if(payload.gameDirectory!==this.data?.gameDirectory)return;
      this.awaitingPackages.set(payload.packageId,{schemeId:payload.schemeId,gameDirectory:payload.gameDirectory});
    },
    showPackageEditor(id) {
      $("characterSchemesPanel").classList.add("hidden");$("characterPackagesPanel").classList.add("hidden");
      $("characterSchemePackageEditor").classList.remove("hidden");
      $("characterSchemeEditorBody").appendChild($("characterDetail"));
      $("characterSchemeEditorContext").textContent="方案："+field(field(this.profile(),"scheme"),"name")+" · "+(field(field((this.data.profiles||[]).find(p=>field(p,"id")===id),"manifest"),"name")||"人物皮肤");
      if($("csEnable"))$("csEnable").hidden=true;
      if($("csDelete"))$("csDelete").hidden=true;
    },
    editPackage(id) {
      this.setMode("schemes");this.editingPackage=id;
      const m=window.CharacterSkinsModule;m.selected=id;m.renderList();m.renderDetail();
      this.showPackageEditor(id);
    },
    addPackage(id) {
      this.setMode("schemes");
      this.openBinding(null, id);
    },
  };
  window.CharacterSchemesModule = module;
  document.addEventListener("DOMContentLoaded", () => module.init());
})();
