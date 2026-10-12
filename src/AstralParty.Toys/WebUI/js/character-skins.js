(() => {
  const labels = {Character:'完整立绘',SfwCharacter:'SFW 立绘',ProfilePhoto:'角色头像',CharacterMap:'倒计时 / 地图像素头像',InGameCharacter:'HUD 角色图',InGameCharacterSFW:'SFW HUD',RolePhoto:'角色缩略图',SfwRolePhoto:'SFW 缩略图',CharacterReady:'准备界面',SfwCharacterReady:'SFW 准备图',CharacterThin:'窄幅角色图',SfwCharacterThin:'SFW 窄幅图',LandIcon:'地块标志',Bust0:'战斗半身图 1',Bust1:'战斗半身图 2',SfwBust0:'SFW 半身图 1',SfwBust1:'SFW 半身图 2',Name:'角色名字',Nick:'称号',Biography:'角色简介',Lines:'台词',SkinLong:'皮肤故事',SkinShort:'皮肤短描述'};
  const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const field = (o, name) => o?.[name] ?? o?.[name[0].toUpperCase()+name.slice(1)];
  const normalize = value => {
    if(Array.isArray(value)) return value.map(normalize);
    if(value && typeof value === 'object') { const out={}; for(const [k,v] of Object.entries(value)) out[k[0].toLowerCase()+k.slice(1)]=normalize(v); return out; }
    return value;
  };
  const module = {
    data:null, selected:null, pendingId:null, tab:'cards', timer:null, playing:false, frame:0, action:'Idle', viewPackage:null,
    send(type, extra={}) { post({type,id:this.selected,...extra}); },
    init() {
      $('cardSkinTab').onclick=()=>this.switchTab('cards');
      $('characterSkinTab').onclick=()=>this.switchTab('characters');
      for(const [id,other] of [['cardSkinTab','characterSkinTab'],['characterSkinTab','cardSkinTab']]) $(id).addEventListener('keydown',e=>{if(['ArrowLeft','ArrowRight','Home','End'].includes(e.key)){e.preventDefault();$(other).click();$(other).focus();}});
      $('characterImport').onclick=()=>this.send('characterSkinsImport');
      $('characterFolder').onclick=()=>this.send('characterSkinsOpen',{id:''});
      $('characterInstall').onclick=()=>this.send('characterSkinsInstall');
      $('characterSearch').oninput=()=>this.renderList();
      $('characterNew').onclick=()=>{if(!this.data)return;$('characterCreateFromGame').checked=true;$('characterCreateFromGame').onchange();$('characterTemplateProgress').textContent='任务加入后台队列后可继续新建；右下角查看进度或终止。';$('characterCreateDialog').showModal();};
      $('characterCreateCancel').onclick=()=>$('characterCreateDialog').close();
      $('characterCreateHero').onchange=()=>{this.renderTargets();$('characterCreateFromGame').onchange();};
      $('characterCreateFromGame').onchange=()=>{const enabled=$('characterCreateFromGame').checked;$('characterTemplateOptions').classList.toggle('hidden',!enabled);$('characterCreateSkin').disabled=enabled;if(enabled){const hero=Number($('characterCreateHero').value);const defaults=(this.data.targets||[]).filter(t=>field(t,'heroId')===hero);$('characterCreateSkin').value=String(defaults.find(t=>field(t,'itemId')===100000000+hero*1000+1)?.itemId??field(defaults[0],'itemId')??0);}$('characterCreateSubmit').textContent=enabled?'导出并创建':'创建';};
      $('characterCreateForm').onsubmit=e=>{e.preventDefault();const f=new FormData(e.target);const fromGame=$('characterCreateFromGame').checked;this.pendingId=f.get('id');this.send('characterSkinsCreate',{id:f.get('id'),name:f.get('name'),heroId:Number(f.get('heroId')),skinItemId:Number(f.get('skinItemId')||0),fromGame,cache:$('characterTemplateCache').value});$('characterCreateDialog').close();};
      const tasks=document.createElement('aside');tasks.id='characterExportPanel';tasks.className='character-export-panel';tasks.hidden=true;tasks.setAttribute('aria-label','后台导出任务');
      tasks.innerHTML='<header><strong>后台导出</strong><span id="characterExportCount"></span></header><div id="characterExportList"></div>';
      document.body.appendChild(tasks);
      this.send('characterExportsGet');
      // Stop previews as soon as this page is hidden.
      new MutationObserver(()=>{if($('skinsView').classList.contains('hidden'))this.stop();}).observe($('skinsView'),{attributes:true,attributeFilter:['class']});
    },
    switchTab(tab) {
      this.tab=tab;this.stop();
      $('cardSkinPanel').classList.toggle('hidden',tab!=='cards');$('characterSkinPanel').classList.toggle('hidden',tab!=='characters');
      for(const [id,on] of [['cardSkinTab',tab==='cards'],['characterSkinTab',tab==='characters']]){$(id).classList.toggle('active',on);$(id).setAttribute('aria-selected',on);$(id).tabIndex=on?0:-1;}
      document.querySelector('.skins-header .utilities-header-actions').classList.toggle('hidden',tab!=='cards');
      document.querySelector('.skins-header .utilities-tagline').textContent=tab==='cards'?'卡牌皮肤 · 方案与卡面管理':'角色皮肤 · 图片 / 文案 / 精灵动画';
      if(tab==='characters')this.send('characterSkinsGet');
    },
    render(payload) {
      // Preserve manifest dictionary keys (Idle, Character, etc.). Only normalize DTO envelope fields.
      this.data={...payload,profiles:(payload.profiles||[]).map(p=>({id:field(p,'id'),manifest:field(p,'manifest'),active:field(p,'active'),error:field(p,'error')}))};
      if(!$('characterTemplateCache').value)$('characterTemplateCache').value=payload.templateCache||'';
      $('characterStatus').textContent=payload.installed?'角色皮肤模组已安装':'尚未安装角色皮肤模组';
      $('characterInstall').disabled=!!payload.installed;
      const createHero=$('characterCreateHero').value;
      $('characterCreateHero').innerHTML=(payload.heroes||[]).map(h=>`<option value="${h.id}">${esc(h.name)} (${h.id})</option>`).join('');
      if([...$('characterCreateHero').options].some(o=>o.value===createHero))$('characterCreateHero').value=createHero;
      if(this.pendingId && this.data.profiles.some(p=>p.id===this.pendingId)){this.selected=this.pendingId;this.pendingId=null;}
      if(!this.data.profiles.some(p=>p.id===this.selected))this.selected=this.data.profiles[0]?.id||null;
      this.renderTargets();$('characterCreateFromGame').onchange();this.renderList();this.renderDetail();
    },
    templateState(p){
      $('characterTemplateProgress').textContent=p.message||'';
      if(p.queued){$('characterCreateDialog').close();showToast(p.message);}
      if(p.success){this.pendingId=p.id;$('characterCreateDialog').close();showToast(p.message);}
      if(p.success===false)showToast(p.message);
    },
    exportTasks(payload){
      const tasks=(payload.tasks||[]).map(normalize),states={queued:'等待中',running:'正在导出',canceling:'正在终止',done:'已完成',failed:'导出失败',canceled:'已终止'};
      this.tasks=tasks;
      const panel=$('characterExportPanel');panel.hidden=!tasks.length;
      $('characterExportCount').textContent=`${tasks.filter(t=>['queued','running','canceling'].includes(t.status)).length} 个进行中`;
      $('characterExportList').innerHTML=tasks.map(t=>`<article class="character-export-task" data-task="${esc(t.taskId)}"><div class="character-export-title"><strong>${esc(t.name)}</strong><span>${states[t.status]||esc(t.status)}</span></div><p>${esc(t.message)}</p><div class="character-export-progress"><progress max="100" value="${Number(t.percent)||0}" aria-label="${esc(t.name)}导出进度"></progress><span>${Number(t.percent)||0}%</span></div><button class="secondary-btn" data-task-command="${['queued','running'].includes(t.status)?'cancel':'dismiss'}" ${t.status==='canceling'?'disabled':''}>${t.status==='queued'?'取消排队':t.status==='running'?'终止导出':t.status==='canceling'?'正在清理…':'关闭消息'}</button></article>`).join('');
      panel.querySelectorAll('[data-task-command]').forEach(b=>b.onclick=()=>this.send(b.dataset.taskCommand==='cancel'?'characterExportCancel':'characterExportDismiss',{taskId:b.closest('[data-task]').dataset.task}));
    },    renderTargets(){const hero=Number($('characterCreateHero').value);$('characterCreateSkin').innerHTML='<option value="0">该角色全部原皮肤</option>'+(this.data?.targets||[]).filter(t=>field(t,'heroId')===hero).map(t=>`<option value="${field(t,'itemId')}">${esc(field(t,'name'))} · ${esc(field(t,'label'))}</option>`).join('');},
    heroName(id){return this.data?.heroes?.find(h=>h.id===id)?.name||`角色 ${id}`;},
    renderList(){
      const q=$('characterSearch').value.toLowerCase();
      const profiles=this.data?.profiles||[];
      $('characterList').innerHTML=profiles.filter(p=>`${p.id} ${field(p.manifest,'name')} ${this.heroName(field(p.manifest,'heroId'))}`.toLowerCase().includes(q)).map(p=>`<button class="character-package ${p.id===this.selected?'active':''}" data-id="${esc(p.id)}"><strong>${esc(field(p.manifest,'name')||p.id)}</strong><small>${esc(this.heroName(field(p.manifest,'heroId')))} · ${field(p.manifest,'skinItemId')||'全部原皮肤'} · ${p.error?'文件有误':p.active?'已启用':'未启用'}</small></button>`).join('')||'<p>没有匹配的皮肤包。可新建方案或导入 ZIP。</p>';
      $('characterList').querySelectorAll('[data-id]').forEach(b=>b.onclick=()=>{this.selected=b.dataset.id;this.renderList();this.renderDetail();});
    },
    manifest(){return this.data?.profiles.find(p=>p.id===this.selected)?.manifest;},
    imageUrl(path){return 'https://characters.astral.local/'+encodeURIComponent(this.selected)+'/'+String(path).split(/[\\/]/).map(encodeURIComponent).join('/');},
    renderDetail(){
      const samePackage=this.viewPackage===this.selected;
      const galleryScroll=samePackage?($('csFrameGrid')?.scrollTop||0):0;
      if(samePackage && $('csAction'))this.action=$('csAction').value;
      if(!samePackage){this.action='Idle';this.frame=0;}
      this.viewPackage=this.selected;
      this.stop();const p=this.data?.profiles.find(p=>p.id===this.selected);if(!p){$('characterDetail').innerHTML='<p>新建一个角色皮肤包，或导入 ZIP 开始制作。</p>';return;}
      if(p.error){$('characterDetail').innerHTML=`<h3>${esc(p.id)}</h3><p class="character-error">${esc(p.error)}</p><button class="secondary-btn" id="csOpen">打开目录修复</button><button class="danger-btn" id="csDelete">删除方案</button>`;this.bindCommon(p);return;}
      const m=p.manifest,images=field(m,'images')||{},texts=field(m,'texts')||{},animations=field(m,'animations')||{},layout=field(m,'layout')||{};
      $('characterDetail').innerHTML=`<h3>${esc(field(m,'name')||p.id)} ${p.active?'<span class="character-badge">已启用</span>':''}</h3><p>${esc(this.heroName(field(m,'heroId')))} · 原皮肤 ${field(m,'skinItemId')||'全部'} · ${esc(field(m,'author')||'作者未填写')}</p><div class="character-toolbar"><button class="primary-btn" id="csEnable">${p.active?'恢复原版':'启用此皮肤'}</button><button class="secondary-btn" id="csExport">导出 ZIP</button><button class="secondary-btn" id="csOpen">打开目录</button><button class="danger-btn" id="csDelete">删除方案</button></div>
      <h4>图片资源</h4><div class="character-image-grid">${(this.data.imageSlots||[]).map(slot=>`<div class="character-image-slot">${images[slot]?`<img src="${esc(this.imageUrl(images[slot]))}" alt="${esc(labels[slot]||slot)}预览" loading="lazy">`:''}<strong>${esc(labels[slot]||slot)}</strong><p>${images[slot]?'已自定义':'使用原版'}</p><button class="secondary-btn" data-image="${esc(slot)}">选择图片</button>${images[slot]?`<button class="link-btn" data-clear-image="${esc(slot)}">恢复</button>`:''}</div>`).join('')}</div>
      <h4>二维精灵动画</h4><p>PNG/JPG 帧按文件名排序。待机 Idle、正向 Walk、背向 Walk-back；其余动作名以原角色为准。</p><div class="character-form-grid"><label>动作<select id="csAction">${[...new Set(['Idle','Walk','Walk-back','Show','Talent','Hit','Hit-01','Hit-02','Cheer','Cry','Die','Eat','Hospitalized','Vomit','Lose','Electricshock',...Object.keys(animations)])].map(x=>`<option>${esc(x)}</option>`).join('')}</select></label><label>FPS<input id="csFps" type="number" min="1" max="120" value="15"></label><label>循环<select id="csLoop"><option value="true">循环</option><option value="false">单次播放</option></select></label></div><div class="character-toolbar"><button class="secondary-btn" id="csFrames">导入帧序列</button><button class="secondary-btn" id="csGif">导出当前动作 GIF</button><button class="secondary-btn" id="csPlay">播放预览</button><button class="secondary-btn" id="csPrevious">上一帧</button><button class="secondary-btn" id="csStep">下一帧</button><button class="primary-btn" id="csReplaceFrame">替换选中帧</button><button class="link-btn" id="csClearAction">恢复此动作</button><span id="csFrameInfo"></span></div><div class="character-preview"><img id="csPreview" alt="动画帧预览" hidden></div><p id="csMappingInfo" class="character-animation-note"></p><label class="character-frame-seek">跳转到帧<input id="csFrameSeek" type="range" min="0" max="0" value="0" aria-label="选择动画帧"></label><div id="csFrameGrid" class="character-frame-grid" role="group" aria-label="动画逐帧图片"></div>
      <h4>地图人物布局</h4><p>锚点使用 0～1，Y 从画布底部向上计算；缩放乘在 PPU 上，不修改游戏人物碰撞或移动。</p><div class="character-form-grid">${[['PivotX','脚底锚点 X',0,1,.01,.5],['PivotY','脚底锚点 Y',0,1,.01,.1],['PixelsPerUnit','每单位像素 PPU',1,10000,1,100],['Scale','人物缩放',.01,20,.01,1]].map(([k,label,min,max,step,fallback])=>`<label>${label}<input data-layout="${k}" type="number" min="${min}" max="${max}" step="${step}" value="${esc(field(layout,k[0].toLowerCase()+k.slice(1))??fallback)}"></label>`).join('')}</div><button id="csSaveLayout" class="primary-btn">保存布局与动作参数</button>
      <details><summary>角色文案</summary>${(this.data.textSlots||[]).map(slot=>`<label>${esc(labels[slot]||slot)}<textarea data-text="${slot}" maxlength="32000" placeholder="留空使用原版">${esc(texts[slot]||'')}</textarea></label>`).join('')}<button class="primary-btn" id="csSaveTexts">保存文案</button><p>名字与角色简介使用角色共用文案，可能影响该角色的其他皮肤。</p></details>
      <details><summary>高级：编辑完整清单</summary><p>支持原精灵名映射 SpriteMappings、自定义资源地址 asset:。媒体文件可随包保存，目前不应用。</p><textarea id="csManifest" style="min-height:280px" spellcheck="false">${esc(JSON.stringify(m,null,2))}</textarea><button class="primary-btn" id="csSaveManifest">校验并保存清单</button></details>`;
      this.bindCommon(p);
      $('csEnable').onclick=()=>this.send('characterSkinsActive',{enabled:!p.active});$('csExport').onclick=()=>this.send('characterSkinsExport');
      document.querySelectorAll('[data-image]').forEach(b=>b.onclick=()=>this.send('characterSkinsImage',{slot:b.dataset.image}));
      document.querySelectorAll('[data-clear-image]').forEach(b=>b.onclick=()=>{delete images[b.dataset.clearImage];this.save(m);});
      $('csFrames').onclick=()=>this.send('characterSkinsFrames',{action:$('csAction').value,fps:Number($('csFps').value),loop:$('csLoop').value==='true'});
      $('csGif').onclick=()=>this.send('characterSkinsGif',{action:this.action,fps:Number($('csFps').value),loop:$('csLoop').value==='true'});
      $('csAction').onchange=()=>this.selectAnimation();
      $('csPlay').onclick=()=>{if(this.playing){this.stop();return;}this.playing=true;$('csPlay').textContent='暂停';this.tick();};
      $('csPrevious').onclick=()=>this.chooseFrame(this.frame-1);
      $('csStep').onclick=()=>this.chooseFrame(this.frame+1);
      $('csFrameSeek').oninput=e=>this.chooseFrame(Number(e.target.value));
      $('csReplaceFrame').onclick=()=>{this.stop();this.send('characterSkinsFrame',{action:this.action,index:this.frame});};
      $('csClearAction').onclick=()=>window.SkinsModule.confirmAction('恢复此动作？',`移除 ${this.action} 的自制帧和原精灵映射，重启游戏后恢复原动作。`,{type:'characterSkinsClearAction',id:this.selected,action:this.action});
      $('csSaveLayout').onclick=()=>{document.querySelectorAll('[data-layout]').forEach(i=>{const key=Object.hasOwn(layout,i.dataset.layout)?i.dataset.layout:i.dataset.layout[0].toLowerCase()+i.dataset.layout.slice(1);layout[key]=Number(i.value);});const a=animations[$('csAction').value];if(a){a[Object.hasOwn(a,'Fps')?'Fps':'fps']=Number($('csFps').value);a[Object.hasOwn(a,'Loop')?'Loop':'loop']=$('csLoop').value==='true';}this.save(m);};
      $('csSaveTexts').onclick=()=>{document.querySelectorAll('[data-text]').forEach(t=>{if(t.value)texts[t.dataset.text]=t.value;else delete texts[t.dataset.text];});this.save(m);};
      $('csSaveManifest').onclick=()=>{try{this.save(JSON.parse($('csManifest').value));}catch(e){showToast('清单 JSON 无效：'+e.message);}};
      if([...$('csAction').options].some(o=>o.value===this.action))$('csAction').value=this.action;
      this.selectAnimation(false);
      $('csFrameGrid').scrollTop=galleryScroll;
    },
    bindCommon(p){$('csOpen').onclick=()=>this.send('characterSkinsOpen');$('csDelete').onclick=()=>window.SkinsModule.confirmAction('删除角色皮肤？',`将 ${field(p.manifest,'name')||p.id} 停用并移入皮肤目录中的 .deleted 文件夹。`,{type:'characterSkinsDelete',id:p.id});},
    save(manifest){this.send('characterSkinsSave',{manifest});},
    animation(){return (field(this.manifest(),'animations')||{})[$('csAction')?.value];},
    selectAnimation(reset=true){
      this.stop();this.action=$('csAction').value;if(reset)this.frame=0;
      const a=this.animation(),frames=field(a,'frames')||[];
      this.frame=Math.min(Math.max(0,this.frame),Math.max(0,frames.length-1));
      $('csFps').value=field(a,'fps')??15;$('csLoop').value=String(field(a,'loop')??true);
      this.renderFrames();this.showFrame();
    },
    renderFrames(){
      const frames=field(this.animation(),'frames')||[];
      $('csFrameGrid').innerHTML=frames.length?frames.map((path,index)=>`<button type="button" class="character-frame" data-frame="${index}" aria-pressed="false" aria-label="选择第 ${index+1} 帧" title="${esc(path)}"><img src="${esc(this.imageUrl(path))}" alt="第 ${index+1} 帧" loading="lazy" decoding="async"><span>第 ${index+1} 帧</span></button>`).join(''):'<p>此动作没有自制帧。导入帧序列后可逐帧查看和替换。</p>';
      $('csFrameGrid').querySelectorAll('[data-frame]').forEach(b=>b.onclick=()=>this.chooseFrame(Number(b.dataset.frame)));
      const mapped=Object.keys(field(this.manifest(),'spriteMappings')||{}).some(name=>name.startsWith(this.action+'_'));
      $('csMappingInfo').textContent=mapped?'此动作使用原精灵映射：替换单帧会同步游戏映射；同一原帧被重复引用时会一并更新。重新导入整组帧会改为独立帧序列。':'重新导入帧序列会替换整个动作；替换选中帧保留帧数、顺序和播放参数。';
      $('csFrameSeek').max=Math.max(0,frames.length-1);
      for(const id of ['csGif','csPlay','csPrevious','csStep','csReplaceFrame','csFrameSeek'])$(id).disabled=!frames.length;
    },
    chooseFrame(index){
      this.stop();const frames=field(this.animation(),'frames')||[];if(!frames.length)return;
      this.frame=(index+frames.length)%frames.length;this.showFrame();
      $('csFrameGrid').querySelector(`[data-frame="${this.frame}"]`)?.scrollIntoView({block:'nearest',inline:'nearest'});
    },
    showFrame(){
      const a=this.animation(),frames=field(a,'frames')||[];
      if(!frames.length){this.frame=0;$('csPreview').hidden=true;$('csPreview').removeAttribute('src');$('csFrameInfo').textContent='未提供，使用原版';$('csFrameSeek').value=0;return;}
      this.frame=(this.frame+frames.length)%frames.length;
      $('csPreview').src=this.imageUrl(frames[this.frame]);$('csPreview').hidden=false;$('csPreview').alt=`${this.action} 第 ${this.frame+1} 帧预览`;
      const duration=(field(a,'durationsMs')||[])[this.frame]||1000/(field(a,'fps')||15);
      $('csFrameInfo').textContent=`第 ${this.frame+1} / ${frames.length} 帧 · ${Math.round(duration)} ms`;
      $('csFrameSeek').value=this.frame;
      const previous=$('csFrameGrid').querySelector('[aria-pressed="true"]');if(previous){previous.setAttribute('aria-pressed','false');previous.classList.remove('selected');}
      const selected=$('csFrameGrid').querySelector(`[data-frame="${this.frame}"]`);if(selected){selected.setAttribute('aria-pressed','true');selected.classList.add('selected');}
    },
    tick(){if(!this.playing)return;this.showFrame();const a=this.animation(),frames=field(a,'frames')||[];if(!frames.length){this.stop();return;}const duration=(field(a,'durationsMs')||[])[this.frame]||1000/field(a,'fps');this.timer=setTimeout(()=>{if(this.frame===frames.length-1&&!field(a,'loop')){this.stop();return;}this.frame++;this.tick();},duration);},
    stop(){clearTimeout(this.timer);this.timer=null;this.playing=false;if($('csPlay'))$('csPlay').textContent='播放预览';}
  };
  window.CharacterSkinsModule=module;
  document.addEventListener('DOMContentLoaded',()=>module.init());
})();
