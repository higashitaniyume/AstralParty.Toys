using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
namespace AstralParty.Toys.Services;
internal sealed record CharacterUnityObject(AssetsFileInstance File,AssetFileInfo Info,AssetTypeValueField Value);
internal sealed class CharacterUnityResources : IDisposable
{
    readonly AssetsManager manager=new();
    readonly Dictionary<string,AssetsFileInstance> files=new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<(AssetsFileInstance,long),CharacterUnityObject> objects=[];
    readonly Dictionary<(AssetsFileInstance,long),(int Width,int Height,byte[] Pixels)> textures=[];
    public IReadOnlyCollection<CharacterUnityObject> Objects=>objects.Values;
    public void Load(IEnumerable<string> paths,CancellationToken cancellationToken=default,bool legacyAnimation=false)
    {
        foreach(string path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if(files.Values.Any(x=>x.parentBundle?.path==path))continue;
            var bundle=manager.LoadBundleFile(path,true);
            for(int i=0;i<bundle.file.BlockAndDirInfo.DirectoryInfos.Count;i++)
            {
                string entryName=bundle.file.GetFileName(i);
                if(entryName.EndsWith(".resS",StringComparison.OrdinalIgnoreCase)||entryName.EndsWith(".resource",StringComparison.OrdinalIgnoreCase))continue;
                var file=manager.LoadAssetsFileFromBundle(bundle,i,false);if(file==null)continue;
                files[Path.GetFileName(file.path)]=file;
                foreach(var info in file.file.AssetInfos)
                {
                    if(legacyAnimation && info.TypeId is not (28 or 74 or 114 or 142 or 213))throw new InvalidDataException("不支持的旧式角色资源类型："+info.TypeId);
                    if(info.TypeId is not (28 or 49 or 74 or 213) && !(legacyAnimation && info.TypeId==114))continue;
                    var value=manager.GetBaseField(file,info);
                    objects[(file,info.PathId)]=new(file,info,value);
                }
            }
        }
    }
    public CharacterUnityObject Resolve(CharacterUnityObject owner,AssetTypeValueField pointer)
    {
        int id=pointer["m_FileID"].AsInt;long path=pointer["m_PathID"].AsLong;var file=owner.File;
        if(id!=0)
        {
            string name=Path.GetFileName(file.file.Metadata.Externals[id-1].PathName.Replace('\\','/'));
            if(!files.TryGetValue(name,out file!))throw new InvalidOperationException("缺少原资源依赖："+name);
        }
        return objects.TryGetValue((file,path),out var obj)?obj:throw new InvalidOperationException("无法定位精灵或纹理引用："+path);
    }
    public (int Width,int Height,byte[] Pixels) Texture(CharacterUnityObject obj)
    {
        var key=(obj.File,obj.Info.PathId);if(textures.TryGetValue(key,out var cached))return cached;
        var texture=TextureFile.ReadTextureFile(obj.Value);var data=texture.FillPictureData(obj.File);
        var pixels=DecodeTexture(texture,data)??throw new InvalidOperationException("无法解码纹理："+obj.Value["m_Name"].AsString);
        return textures[key]=(texture.m_Width,texture.m_Height,pixels);
    }
    internal static byte[]? DecodeTexture(TextureFile texture,byte[] data)
    {
        int width=texture.m_Width,height=texture.m_Height;
        // AssetsTools' BC7 decoder writes complete 4x4 blocks into an unpadded buffer.
        // Decode full blocks, then crop their right/top padding in Unity's bottom-up row order.
        if(texture.m_TextureFormat!=(int)TextureFormat.BC7 || ((width|height)&3)==0)
            return texture.DecodeTextureRaw(data,true);
        int paddedWidth=checked((width+3)&~3),paddedHeight=checked((height+3)&~3);
        if(width<=0||height<=0||data.LongLength<(long)paddedWidth*paddedHeight)
            throw new InvalidDataException("BC7 纹理尺寸或块数据不完整");
        byte[]? padded;
        try
        {
            texture.m_Width=paddedWidth;texture.m_Height=paddedHeight;
            padded=texture.DecodeTextureRaw(data,true);
        }
        finally { texture.m_Width=width;texture.m_Height=height; }
        if(padded==null)return null;
        var output=new byte[checked(width*height*4)];
        for(int y=0;y<height;y++)Buffer.BlockCopy(padded,y*paddedWidth*4,output,y*width*4,width*4);
        return output;
    }
    public static void SavePng(string path,int width,int height,byte[] bottomUp)
    {
        var topDown=new byte[bottomUp.Length];int stride=width*4;
        for(int y=0;y<height;y++)Buffer.BlockCopy(bottomUp,y*stride,topDown,(height-1-y)*stride,stride);
        var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,topDown,stride);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream=File.Create(path);encoder.Save(stream);
    }
    public void ExportTexture(CharacterUnityObject obj,string path)
    {var t=Texture(obj);SavePng(path,t.Width,t.Height,t.Pixels);}
    public void ExportSprite(CharacterUnityObject obj,string path)
    {
        var s=obj.Value;
        if(!s["m_SpriteAtlas"].IsDummy && s["m_SpriteAtlas"]["m_PathID"].AsLong!=0)
            throw new InvalidOperationException("该精灵使用独立 SpriteAtlas，目前尚不支持导出："+s["m_Name"].AsString);
        var rd=s["m_RD"];var texture=Texture(Resolve(obj,rd["texture"]));
        if(!rd["alphaTexture"].IsDummy && rd["alphaTexture"]["m_PathID"].AsLong!=0)
            throw new InvalidOperationException("该精灵使用独立透明纹理，目前尚不支持导出："+s["m_Name"].AsString);
        int width=(int)Math.Round(s["m_Rect"]["width"].AsFloat),height=(int)Math.Round(s["m_Rect"]["height"].AsFloat);
        var output=new byte[width*height*4];
        var vertices=rd["m_VertexData"];var channels=vertices["m_Channels"]["Array"].Children;
        byte[] data=vertices["m_DataSize"].AsByteArray;
        int count=vertices["m_VertexCount"].AsInt;
        var streams=new Dictionary<int,(int Stride,int Offset)>();
        foreach(var channel in channels)
        {
            int dimension=channel["dimension"].AsInt&15;if(dimension==0)continue;
            int format=channel["format"].AsInt;if(format!=0)throw new InvalidOperationException("尚不支持该精灵的顶点格式："+format);
            int stream=channel["stream"].AsInt,end=channel["offset"].AsInt+dimension*4;
            streams[stream]=(Math.Max(streams.GetValueOrDefault(stream).Stride,end),0);
        }
        int offset=0;foreach(int stream in streams.Keys.Order().ToArray()){var value=streams[stream];streams[stream]=(value.Stride,offset);offset=(offset+value.Stride*count+15)&~15;}
        float Read(int index,AssetTypeValueField channel,int component){var stream=streams[channel["stream"].AsInt];return BitConverter.ToSingle(data,stream.Offset+index*stream.Stride+channel["offset"].AsInt+component*4);}
        var position=channels[0];var uv=channels[4];
        var points=Enumerable.Range(0,count).Select(i=>(X:Read(i,position,0),Y:Read(i,position,1),U:Read(i,uv,0),V:Read(i,uv,1))).ToArray();
        float minX=points.Min(v=>v.X),minY=points.Min(v=>v.Y),ppu=s["m_PixelsToUnits"].AsFloat;
        var indexField=rd["m_IndexBuffer"];byte[] indices=indexField.Value!=null?indexField.AsByteArray:indexField["Array"].AsByteArray;
        for(int tri=0;tri<indices.Length;tri+=6)
        {
            var a=points[BitConverter.ToUInt16(indices,tri)];var b=points[BitConverter.ToUInt16(indices,tri+2)];var c=points[BitConverter.ToUInt16(indices,tri+4)];
            double ax=Math.Round((a.X-minX)*ppu),ay=Math.Round((a.Y-minY)*ppu),bx=Math.Round((b.X-minX)*ppu),by=Math.Round((b.Y-minY)*ppu),cx=Math.Round((c.X-minX)*ppu),cy=Math.Round((c.Y-minY)*ppu);
            double det=(by-cy)*(ax-cx)+(cx-bx)*(ay-cy);if(Math.Abs(det)<1e-8)continue;
            int x0=Math.Max(0,(int)Math.Floor(Math.Min(ax,Math.Min(bx,cx)))),x1=Math.Min(width-1,(int)Math.Ceiling(Math.Max(ax,Math.Max(bx,cx))));
            int y0=Math.Max(0,(int)Math.Floor(Math.Min(ay,Math.Min(by,cy)))),y1=Math.Min(height-1,(int)Math.Ceiling(Math.Max(ay,Math.Max(by,cy))));
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
            {
                double px=x+.5,py=y+.5,wa=((by-cy)*(px-cx)+(cx-bx)*(py-cy))/det,wb=((cy-ay)*(px-cx)+(ax-cx)*(py-cy))/det,wc=1-wa-wb;
                if(wa< -1e-7||wb< -1e-7||wc< -1e-7)continue;
                int tx=Math.Clamp((int)Math.Floor(wa*Math.Round(a.U*texture.Width)+wb*Math.Round(b.U*texture.Width)+wc*Math.Round(c.U*texture.Width)),0,texture.Width-1),ty=Math.Clamp((int)Math.Floor(wa*Math.Round(a.V*texture.Height)+wb*Math.Round(b.V*texture.Height)+wc*Math.Round(c.V*texture.Height)),0,texture.Height-1);
                Buffer.BlockCopy(texture.Pixels,(ty*texture.Width+tx)*4,output,(y*width+x)*4,4);
            }
        }
        SavePng(path,width,height,output);
    }
    public void ReleaseTextures()=>textures.Clear();
    public void Dispose()=>manager.UnloadAll();
}
