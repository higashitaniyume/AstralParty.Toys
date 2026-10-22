using AssetsTools.NET;
using AssetsTools.NET.Extra;
using ProtoBuf;
using ProtoUntyped;
using ResourceModLoader.Module;
using ResourceModLoader.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ResourceModLoader.Mod.Patch
{
    class BinPatch : IPatch
    {
        byte[] bytes;
        AssetTypeValueField field = null;
        List<Tuple<int, int, byte[],string>> applyList = new List<Tuple<int, int, byte[], string>>();
        public void Init(AssetsManager manager, AssetsFileInstance assets, AssetFileInfo file)
        {
            field = manager.GetBaseField(assets, file);
            bytes = field["m_Script"].AsByteArray;
        }
        private byte[] FromStrAuto(string str)
        {
            if(str.StartsWith("\"") && str.EndsWith("\""))
            {
                return Encoding.UTF8.GetBytes(str.Substring(1, str.Length - 2));
            }
            else if (str.StartsWith("0x"))
            {
                return Convert.FromHexString(str.Substring(2));
            }
            else
            {
                return Convert.FromBase64String(str);
            }
        }
        public bool PerformPatch(string source)
        {
            var patches = File.ReadAllText(source).Split("\n");
            bool hasPat = false;
            foreach (var pat in patches)
            {
                if (!pat.Contains("@@@"))
                    continue;
                var p = pat.Trim().Split("@@@", 2);
                var from= p[0].Trim();
                var target = p[1].Trim();
                byte[] fromBuf = FromStrAuto(from);
                byte[] targetBuf = FromStrAuto(target);


                int currentIdx = 0;
                int matchIndex = bytes.AsSpan(currentIdx).IndexOf(fromBuf);
                while (matchIndex >= 0)
                {
                    hasPat = true;
                    applyList.Add(new Tuple<int, int, byte[], string>(currentIdx + matchIndex, fromBuf.Length, targetBuf,source));
                    currentIdx += matchIndex +  fromBuf.Length;
                    matchIndex = bytes.AsSpan(currentIdx).IndexOf(fromBuf);
                }
            }
            return hasPat;
        }

        public void Finalize(AssetsManager manager, AssetsFileInstance assets, AssetFileInfo file)
        {
            applyList.Sort((a, b) => a.Item1 - b.Item1);
            int size = bytes.Length;
            foreach (var (s, l, d,_) in applyList)
                size += (d.Length - l);
            byte[] result = new byte[size];
            int lastOriginalDataOffset = -1;
            int deltaOffset= 0;
            foreach (var (replaceStart,replaceLength,replaceData,src) in applyList)
            {
                if(replaceStart <= lastOriginalDataOffset)
                {
                    Report.Warning(src, "重叠的替换区间被跳过");
                    continue;
                }
                //假设上次原文件替换0到5（位置lastOriginalDataOffset = 5），替换长度7，dataoffset=1，下一段复制9，长度5(9,10,11,12,13被复制）
                //1.源文件6-8 => 7-9
                Array.Copy(bytes, lastOriginalDataOffset + 1, result, lastOriginalDataOffset + 1 + deltaOffset, replaceStart - lastOriginalDataOffset - 1);
                //2.替换数据
                Array.Copy(replaceData, 0, result, replaceStart + deltaOffset, replaceData.Length);

                lastOriginalDataOffset = replaceStart + replaceLength - 1;

                deltaOffset += (replaceData.Length - replaceLength);
            }
            Array.Copy(bytes, lastOriginalDataOffset + 1, result, lastOriginalDataOffset + 1 + deltaOffset, bytes.Length - lastOriginalDataOffset - 1);


            field["m_Script"].AsByteArray = result;
            file.SetNewData(field);
        }
    }
}
