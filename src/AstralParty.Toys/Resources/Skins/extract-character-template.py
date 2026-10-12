"""Local-only character template export. Called by Toys; requires UnityPy and Pillow."""
import sys,json,base64,struct,re,traceback
from pathlib import Path

def export(req):
 import UnityPy
 from PIL import Image
 out=Path(req['Output']);out.mkdir(parents=True,exist_ok=True)
 status=Path(req['Status'])
 def progress(message):status.write_text(json.dumps({'message':message},ensure_ascii=False),encoding='utf8')
 progress('定位安装目录和热更新缓存资源')
 roots=[Path(req['Game'])/ 'AstralParty_CN_Data/StreamingAssets/aa',Path(req['Cache'])]
 roots += list(Path(req['Game']).glob('*_Data/StreamingAssets/aa'))
 roots=list(dict.fromkeys(p for p in roots if p.exists()))
 index={}
 for root in roots:
  for p in root.rglob('*'):
   if not p.is_file() or not (p.suffix=='.bundle' or p.name=='__data'):continue
   for h in re.findall(r'[0-9a-fA-F]{32}',str(p)):
    index.setdefault(h.lower(),[]).append(p)
 catalogs=sorted(set(p for root in roots for p in root.rglob('catalog*.json')),key=lambda p:p.stat().st_mtime,reverse=True)
 if not catalogs:raise RuntimeError('未找到 Addressables JSON catalog，请先启动游戏完成资源更新。')
 selected=[];missing=[]
 assets=req['Assets'];label=req['Label']
 for catalog in catalogs:
  try:
   d=json.loads(catalog.read_text(encoding='utf-8-sig'))
   kd=base64.b64decode(d['m_KeyDataString']);bd=base64.b64decode(d['m_BucketDataString']);ed=base64.b64decode(d['m_EntryDataString'])
   i32=lambda b,p:struct.unpack_from('<i',b,p)[0]
   def key(p):
    t=kd[p];p+=1
    if t in (0,1):
     n=i32(kd,p);return kd[p+4:p+4+n].decode('ascii' if t==0 else 'utf-16-le')
    return None
   buckets=[];p=4
   for _ in range(i32(bd,0)):
    off=i32(bd,p);cnt=i32(bd,p+4);p+=8
    es=[i32(bd,p+4*j) for j in range(cnt)];p+=4*cnt;buckets.append((key(off),es))
   entries=[struct.unpack_from('<7i',ed,4+28*j) for j in range(i32(ed,0))]
   keys={k:es for k,es in buckets if isinstance(k,str)}
   def dependencies(ei,seen):
    if ei in seen:return
    seen.add(ei);e=entries[ei];iid=d['m_InternalIds'][e[0]]
    if '.bundle' in iid:
     hashes=re.findall(r'[0-9a-fA-F]{32}',iid)
     for h in hashes:
      candidates=index.get(h.lower(),[])
      # Prefer updated downloaded cache over the installation version when both are present.
      candidates=sorted(set(candidates),key=lambda p:(p.name=='__data',p.stat().st_mtime),reverse=True)
      if candidates:
       if candidates[0] not in selected:selected.append(candidates[0])
       break
    if 0<=e[2]<len(buckets):
     for child in buckets[e[2]][1]:dependencies(child,seen)
   wanted=set(assets.values())|{'GameData_CN'}|{k for k in keys if k.startswith(label+'_') and re.match(r'^'+re.escape(label)+r'_(PC|Walk_PC)$',k)}
   for k in wanted:
    for ei in keys.get(k,[]):dependencies(ei,set())
   if selected:break
  except (KeyError,ValueError,struct.error):continue
 if not selected:raise RuntimeError('无法定位该角色默认资源包；请先在游戏中查看该角色并等待下载完成。')
 progress('读取 '+str(len(selected))+' 个原资源包')
 env=UnityPy.load(*(str(p) for p in selected))
 # Read downloaded protobuf configurations when available; bundled Toys tables are only a fallback.
 def fields(b):
  p=0;result={}
  def varint():
   nonlocal p
   value=0;shift=0
   while True:
    x=b[p];p+=1;value|=(x&127)<<shift
    if x<128:return value
    shift+=7
  while p<len(b):
   tag=varint();n,w=tag>>3,tag&7
   if w==0:value=varint()
   elif w==1:value=struct.unpack_from('<q',b,p)[0];p+=8
   elif w==2:length=varint();value=b[p:p+length];p+=length
   elif w==5:value=struct.unpack_from('<i',b,p)[0];p+=4
   else:raise ValueError('Unsupported protobuf wire type')
   result.setdefault(n,[]).append(value)
  return result
 configs={}
 for obj in env.objects:
  if obj.type.name=='TextAsset' and obj.peek_name() in ('Skin','Character'):
   data=obj.read();raw=data.m_Script
   configs[data.m_Name]=fields(raw.encode('utf8','surrogateescape') if isinstance(raw,str) else raw)
 if 'Skin' in configs:
  default=None
  for raw in configs['Skin'].get(1,[]):
   for item in fields(raw).get(2,[]):
    f=fields(item)
    if f.get(3,[0])[0]==req['SkinItemId']:default=f
  if default:
   assets={}
   for slot,n in {'LandIcon':6,'Character':7,'SfwCharacter':8,'ProfilePhoto':14,'CharacterReady':17,'SfwCharacterReady':18,'CharacterThin':19,'SfwCharacterThin':20,'RolePhoto':23,'SfwRolePhoto':24}.items():
    if n in default:assets[slot]=default[n][0].decode('utf8')
   for slot,n,i in [('Bust0',15,0),('Bust1',15,1),('SfwBust0',16,0),('SfwBust1',16,1)]:
    if len(default.get(n,[]))>i:assets[slot]=default[n][i].decode('utf8')
   for slot,n,fallback in [('InGameCharacter',9,'Character'),('InGameCharacterSFW',10,'SfwCharacter')]:
    if n in default:assets[slot]=default[n][0].decode('utf8')
    elif fallback in assets:assets[slot]=assets[fallback]
   if 11 in default:
    level=default[11][0].decode('utf8');assets['asset:'+level]=level
   label=default[13][0].decode('utf8')
   assets['CharacterMap']=req['Assets'].get('CharacterMap','')
   for raw in configs.get('Character',{}).get(1,[]):
    f=fields(raw)
    if f.get(1,[0])[0]==req['HeroId'] and 23 in f:assets['CharacterMap']=f[23][0].decode('utf8')
   assets={k:v for k,v in assets.items() if v}
   for k in set(assets.values())|{label+'_PC',label+'_Walk_PC'}:
    for ei in keys.get(k,[]):dependencies(ei,set())
   env=UnityPy.load(*(str(p) for p in selected))
 byname={};objects={}
 for obj in env.objects:
  if obj.type.name in ('Texture2D','Sprite'):objects.setdefault(obj.peek_name(),[]).append(obj)
 manifest={'SchemaVersion':1,'Id':req['Id'],'Name':req['Name'],'Author':'','Description':'从本地游戏默认皮肤导出的制作模板','HeroId':req['HeroId'],'SkinItemId':req['SkinItemId'],'Images':{},'Texts':{},'Animations':{},'SpriteMappings':{},'UseOriginalSpriteLayout':True,'Layout':{'PivotX':0.5,'PivotY':0.1,'PixelsPerUnit':100,'Scale':1},'Media':{}}
 for slot,name in assets.items():
  candidates=objects.get(name,[])
  if not candidates:missing.append(slot+': '+name);continue
  obj=next((o for o in candidates if o.type.name=='Texture2D'),candidates[0]);data=obj.read()
  relative='images/'+re.sub(r'[^A-Za-z0-9_-]','_',name)+'.png';dest=out/relative;dest.parent.mkdir(parents=True,exist_ok=True);data.image.save(dest)
  manifest['Images'][slot]=relative
 metadata=[]
 for obj in env.objects:
  if obj.type.name!='Sprite':continue
  data=obj.read();name=data.m_Name
  if not re.match(r'^.+_\d+$',name):continue
  if name in byname:raise RuntimeError('默认动画包存在重复精灵名称：'+name)
  action=name.rsplit('_',1)[0];relative='animations/'+action+'/'+name+'.png'
  if not re.fullmatch(r'[A-Za-z0-9_-]+',action):continue
  im=data.image.convert('RGBA')
  if im.size!=(round(data.m_Rect.width),round(data.m_Rect.height)):raise RuntimeError('原精灵导出画布不一致：'+name)
  dest=out/relative;dest.parent.mkdir(parents=True,exist_ok=True);im.save(dest)
  byname[name]=relative;manifest['SpriteMappings'][name]=relative
  metadata.append({'Name':name,'Size':list(im.size),'Pivot':[data.m_Pivot.x,data.m_Pivot.y],'PPU':data.m_PixelsToUnits})
  if len(byname)%50==0:progress('已导出 '+str(len(byname))+' 张动画帧')
 for obj in env.objects:
  if obj.type.name!='AnimationClip':continue
  data=obj.read();t=obj.read_typetree();sequence=[]
  for pointer in data.m_ClipBindingConstant.pptrCurveMapping:
   s=pointer.deref_parse_as_object()
   if s.m_Name not in byname:sequence=[];break
   sequence.append(byname[s.m_Name])
  if sequence:manifest['Animations'][t['m_Name']]={'Frames':sequence,'DurationsMs':[],'Fps':t['m_SampleRate'],'Loop':t['m_MuscleClip']['m_LoopTime']}
 if not manifest['Images'] or not manifest['Animations']:raise RuntimeError('原图片或动画未完整定位，未创建模板。请在游戏中查看默认皮肤以下载所需资源。')
 (out/'character-skin.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
 (out/'source-metadata.json').write_text(json.dumps({'Catalog':str(catalog),'Bundles':[str(p) for p in selected],'Missing':missing,'Sprites':metadata},ensure_ascii=False,indent=2),encoding='utf8')
 (out/'README.md').write_text('# 默认角色皮肤模板\n\n由本地游戏安装目录及 Addressables 缓存提取。保持动画帧画布尺寸、文件名与透明边缘；原精灵映射沿用游戏时序和锚点。替换单帧使用 Toys 的逐帧面板。语音、视频与特效保持原版。\n\n缺失资源：\n'+('\n'.join(missing) or '无'),encoding='utf8')
 return {'Images':len(manifest['Images']),'Actions':len(manifest['Animations']),'Frames':len(byname),'Missing':missing}

if __name__=='__main__':
 req=json.loads(Path(sys.argv[1]).read_text(encoding='utf-8-sig'))
 try:result={'Success':True,**export(req)}
 except Exception as ex:result={'Success':False,'Error':str(ex),'Detail':traceback.format_exc()}
 Path(req['Result']).write_text(json.dumps(result,ensure_ascii=False),encoding='utf8')
 sys.exit(0 if result['Success'] else 1)
