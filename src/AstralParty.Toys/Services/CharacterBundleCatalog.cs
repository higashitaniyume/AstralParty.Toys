using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace AstralParty.Toys.Services;
internal sealed class CharacterBundleCatalog
{
    readonly JsonDocument document;
    readonly List<(string? Key,int[] Entries)> buckets=[];
    readonly int[][] entries;
    readonly Dictionary<string,List<string>> files=new(StringComparer.OrdinalIgnoreCase);
    public string CatalogPath {get;}
    public CharacterBundleCatalog(string game,string cache,CancellationToken cancellationToken=default)
    {
        var roots=Directory.GetDirectories(game,"*_Data").Select(x=>Path.Combine(x,"StreamingAssets","aa")).Append(cache).Where(Directory.Exists).Distinct().ToArray();
        CatalogPath=roots.SelectMany(x=>Directory.EnumerateFiles(x,"catalog*.json",SearchOption.AllDirectories)).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()??throw new InvalidOperationException("未找到 Addressables JSON catalog，请先启动游戏完成资源更新");
        document=JsonDocument.Parse(File.ReadAllText(CatalogPath));var d=document.RootElement;
        var kd=Convert.FromBase64String(d.GetProperty("m_KeyDataString").GetString()!);var bd=Convert.FromBase64String(d.GetProperty("m_BucketDataString").GetString()!);var ed=Convert.FromBase64String(d.GetProperty("m_EntryDataString").GetString()!);
        int p=4;for(int i=0;i<BitConverter.ToInt32(bd,0);i++)
        {
            int offset=BitConverter.ToInt32(bd,p),count=BitConverter.ToInt32(bd,p+4);p+=8;
            var indexes=new int[count];for(int j=0;j<count;j++){indexes[j]=BitConverter.ToInt32(bd,p);p+=4;}
            byte type=kd[offset];string? key=null;if(type is 0 or 1){int length=BitConverter.ToInt32(kd,offset+1);key=(type==0?Encoding.ASCII:Encoding.Unicode).GetString(kd,offset+5,length);}buckets.Add((key,indexes));
        }
        entries=Enumerable.Range(0,BitConverter.ToInt32(ed,0)).Select(i=>Enumerable.Range(0,7).Select(j=>BitConverter.ToInt32(ed,4+i*28+j*4)).ToArray()).ToArray();
        foreach(var root in roots)
        foreach(var file in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Where(x=>x.EndsWith(".bundle",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(x)=="__data"))
        foreach(Match hash in Regex.Matches(file,"[a-fA-F0-9]{32}"))
        {cancellationToken.ThrowIfCancellationRequested();if(!files.TryGetValue(hash.Value,out var list))files[hash.Value]=list=[];list.Add(file);}
    }
    public List<string> Resolve(IEnumerable<string> keys)
    {
        var wanted=keys.ToHashSet(StringComparer.Ordinal);var result=new List<string>();var seen=new HashSet<int>();
        void Visit(int index)
        {
            if(!seen.Add(index))return;var e=entries[index];string iid=document.RootElement.GetProperty("m_InternalIds")[e[0]].GetString()!;
            if(iid.Contains(".bundle"))foreach(Match hash in Regex.Matches(iid,"[a-fA-F0-9]{32}"))
            {
                if(!files.TryGetValue(hash.Value,out var candidates))continue;
                string file=candidates.Distinct().OrderByDescending(x=>Path.GetFileName(x)=="__data").ThenByDescending(File.GetLastWriteTimeUtc).First();if(!result.Contains(file))result.Add(file);break;
            }
            if(e[2]>=0&&e[2]<buckets.Count)foreach(int child in buckets[e[2]].Entries)Visit(child);
        }
        foreach(var bucket in buckets)if(bucket.Key!=null&&wanted.Contains(bucket.Key))foreach(int index in bucket.Entries)Visit(index);
        return result;
    }
}
