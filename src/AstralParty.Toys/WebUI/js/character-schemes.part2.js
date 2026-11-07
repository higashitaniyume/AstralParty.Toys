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
