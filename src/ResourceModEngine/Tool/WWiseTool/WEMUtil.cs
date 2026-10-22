using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Tool.WWiseTool
{
    class WEMUtil
    {
        public static double? GetDurationMs(BinaryReader br)
        {
            uint ckid = br.ReadUInt32();
            if(ckid != 1179011410)return null;
            uint len = br.ReadUInt32();
            br.ReadUInt32();
            long endPos = br.BaseStream.Position + len;

            long bps = 0;
            long dataSz = 0;
            while(br.BaseStream.Position < endPos)
            {
                uint type = br.ReadUInt32();
                uint size = br.ReadUInt32();
                long tp = br.BaseStream.Position;

                if(type == 544501094)//fmt
                {
                    br.ReadUInt16();//format
                    br.ReadUInt16();//channels
                    br.ReadUInt32 ();//sample perS
                    bps = br.ReadUInt32();//byte rate
                }
                else if(type == 1635017060)//data
                {
                    dataSz = size;
                }
                if (dataSz != 0 && bps != 0) break;
                br.BaseStream.Position = tp + size;
            }
            return 1000.0 * dataSz / bps;
        }
    }
}
