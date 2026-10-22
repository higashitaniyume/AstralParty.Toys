using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Tool.WWiseTool
{
    class BnkReaderUtil
    {
        // 枚举常量（仅用于条件判断）
        private enum Ak3DPositionType
        {
            Emitter = 0,
            EmitterWithAutomation = 1,
            ListenerWithAutomation = 2
        }
        public enum AudioPositioningBehavior : byte
        {
            OverrideParent = 0b_0000_0001,
            TwoDimensional = 0b_0000_0010,
            Enable2dPanner = 0b_0000_0100,
            ThreeDimensional = 0b_0000_1000,
            EnableSpatialization = 0b_0001_0000,
            UserDefinedShouldLoop = 0b_0010_0000,
            UpdateAtEachFrame = 0b_0100_0000,
            IgnoreListenerOrientation = 0b_1000_0000
        }
        public enum AudioAuxSendsBehavior : byte
        {
            OverrideGameDefined = 0b_0001,
            UseGameDefinedAuxSends = 0b_0010,
            OverrideUserDefined = 0b_0100,
            OverrideAuxSends = 0b_1000
        }
        // 跳过整个 NodeBaseParams
        public static void SkipNodeBaseParams(BinaryReader br)
        {
            // 1. NodeInitialFxParams
            SkipNodeInitialFxParams(br);

            // 2. override_attachment_params (u8)
            br.ReadByte();

            br.ReadByte();
            br.ReadByte();

            // 3. override_bus_id (u32)
            br.ReadUInt32();

            // 4. direct_parent_id (u32)
            br.ReadUInt32();

            // 5. unknown_flags (u8)
            br.ReadByte();

            // 6. NodeInitialParams
            SkipNodeInitialParams(br);

            // 7. PositioningParams
            SkipPositioningParams(br);

            // 8. AuxParams
            SkipAuxParams(br);

            // 10. StateChunk
            SkipStateChunk(br);

            // 11. InitialRTPC
            SkipInitialRTPC(br);
        }

        // 跳过 NodeInitialFxParams
        private static void SkipNodeInitialFxParams(BinaryReader br)
        {
            byte isOverrideParentFx = br.ReadByte();  // is_override_parent_fx
            byte fxChunkCount = br.ReadByte();        // fx_chunk_count

            if (fxChunkCount > 0)
            {
                br.ReadByte(); // fx_bypass_bits
            }

            for (int i = 0; i < fxChunkCount; i++)
            {
                br.ReadByte();   // fx_index
                br.ReadUInt32(); // fx_id
                br.ReadByte();   // is_share_set
                br.ReadByte();   // is_rendered
            }
        }

        // 跳过 NodeInitialParams
        private static void SkipNodeInitialParams(BinaryReader br)
        {
            // prop_initial_values: Vec<PropBundle>
            byte propCount = br.ReadByte();
            // 读取类型 ID 列表（每个 1 字节）并跳过
            for (int i = 0; i < propCount; i++)
                br.ReadByte();
            // 跳过值列表（每个 PropBundle 均为 4 字节）
            for (int i = 0; i < propCount; i++)
                br.ReadInt32(); // 4 字节，用 Int32 读可保持流位置

            // prop_ranged_modifiers: PropRangedModifiers
            byte rangedCount = br.ReadByte();
            for (int i = 0; i < rangedCount; i++)
            {
                br.ReadByte();   // prop_type
                br.ReadSingle(); // min
                br.ReadSingle(); // max
            }
        }

        // 跳过 PositioningParams
        private static void SkipPositioningParams(BinaryReader br)
        {
            // 第一个字节：包含 5 个位域
            byte b1 = br.ReadByte();
            // AuxParams
            br.ReadByte();

            bool listenerRelativeRouting = ((b1 >> 1) & 1) == 1;
            Ak3DPositionType threeDPosType = (Ak3DPositionType)((b1 >> 5) & 0x03);

            // 如果 listenerRelativeRouting == false，存在第二个字节（5个布尔位 + 3位 spatialization mode）
            if (listenerRelativeRouting)
            {
                byte b2 = br.ReadByte();
                // 无需解析具体字段，只移动位置
            }

            // 如果 threeDPosType == Emitter，则存在后续字段
            if (threeDPosType != Ak3DPositionType.Emitter)
            {
                br.ReadByte();   // path_mode (u8)
                br.ReadInt32();  // transition_time (i32)

                uint vertexCount = br.ReadUInt32();
                for (int i = 0; i < vertexCount; i++)
                {
                    br.ReadSingle(); // x
                    br.ReadSingle(); // y
                    br.ReadSingle(); // z
                    br.ReadInt32();  // duration
                }

                uint pathItemCount = br.ReadUInt32();
                for (int i = 0; i < pathItemCount; i++)
                {
                    br.ReadUInt32(); // vertices_offset
                    br.ReadUInt32(); // vertices_count
                }

                for (int i = 0; i < pathItemCount; i++)
                {
                    br.ReadSingle(); // range_x
                    br.ReadSingle(); // range_y
                    br.ReadSingle(); // range_z
                }
            }
        }

        // 跳过 AuxParams
        private static void SkipAuxParams(BinaryReader br)
        {
            byte b = br.ReadByte();
            bool hasAux = ((b >> 2) & 1) == 1; // 提取 has_aux 位（从右数第3位）

            if (hasAux)
            {
                br.ReadUInt32(); // aux1
                br.ReadUInt32(); // aux2
                br.ReadUInt32(); // aux3
                br.ReadUInt32(); // aux4
            }

            br.ReadUInt32(); // reflections_aux_bus
        }

        // 跳过 AdvSettingsParams
        private static void SkipAdvSettingsParams(BinaryReader br)
        {
            // 第一个字节：8个布尔位
            br.ReadByte();
            br.ReadByte(); // virtual_queue_behavior (u8)
            br.ReadUInt16(); // max_instance_count (u16)
            br.ReadByte(); // below_threshold_behavior (u8)
                           // 第二个字节：8个布尔位
            br.ReadByte();
        }

        // 跳过 StateChunk
        private static void SkipStateChunk(BinaryReader br)
        {
            ushort statePropertyCount = br.ReadUInt16();
            for (int i = 0; i < statePropertyCount; i++)
            {
                br.ReadByte(); // property (AkPropID)
                br.ReadByte(); // accum_type (AkRtpcAccum)
                br.ReadByte(); // in_db
            }

            ushort stateGroupCount = br.ReadUInt16();
            for (int i = 0; i < stateGroupCount; i++)
            {
                br.ReadUInt32(); // state_group_id
                br.ReadByte();   // sync_type (AkSyncTypeU8)
                byte stateCount = br.ReadByte();
                for (int j = 0; j < stateCount; j++)
                {
                    br.ReadUInt32(); // state_id
                    br.ReadUInt32(); // state_instance_id
                }
            }
        }

        // 跳过 InitialRTPC
        private static void SkipInitialRTPC(BinaryReader br)
        {
            ushort count = br.ReadUInt16();
            for (int i = 0; i < count; i++)
            {
                br.ReadUInt32(); // id
                br.ReadByte();   // rtpc_type
                br.ReadByte();   // rtpc_accum
                br.ReadByte();   // param_id
                br.ReadUInt32(); // curve_id
                br.ReadByte();   // curve_scaling
                ushort pointCount = br.ReadUInt16();
                for (int j = 0; j < pointCount; j++)
                {
                    br.ReadSingle(); // from
                    br.ReadSingle(); // to
                    br.ReadUInt32(); // interpolation (AkCurveInterpolation)
                }
            }
        }
        public static void SkipMusicTransNodeParams(BinaryReader br)
        {
           SkipMusicNodeParams(br);
            uint transitionRuleCount = br.ReadUInt32();
            for (int i = 0; i < transitionRuleCount; i++)
            {
                uint srcCount = br.ReadUInt32();
                for (int j = 0; j < srcCount; j++)
                    br.ReadInt32(); // source_id

                uint dstCount = br.ReadUInt32();
                for (int j = 0; j < dstCount; j++)
                    br.ReadInt32(); // destination_id

                // source_transition_rule
                br.ReadInt32();  // transition_time
                br.ReadUInt32(); // fade_curve (AkCurveInterpolation)
                br.ReadInt32();  // fade_offet
                br.ReadUInt32(); // sync_type (AkSyncType)
                br.ReadUInt32(); // clue_filter_hash
                br.ReadByte();   // play_post_exit

                // destination_transition_rule
                br.ReadInt32();  // transition_time
                br.ReadUInt32(); // fade_curve
                br.ReadInt32();  // fade_offet
                br.ReadUInt32(); // clue_filter_hash
                br.ReadInt32();  // jump_to_id
                br.ReadUInt16(); // jump_to_type
                br.ReadUInt16(); // entry_type
                br.ReadByte();   // play_pre_entry
                br.ReadByte();   // destination_match_source_cue_name

                byte allocFlag = br.ReadByte();
                if (allocFlag != 0) // alloc_trans_object_flag != 0 -> read transition_object
                {
                    br.ReadUInt32(); // segment_id
                                     // fade_out
                    br.ReadInt32();  // transition_time
                    br.ReadUInt32(); // curve
                    br.ReadInt32();  // offset
                                     // fade_in
                    br.ReadInt32();  // transition_time
                    br.ReadUInt32(); // curve
                    br.ReadInt32();  // offset
                    br.ReadByte();   // play_pre_entry
                    br.ReadByte();   // play_post_exit
                }
            }
        }
        public static void SkipMusicNodeParams(BinaryReader br)
        {
            // 1. MusicNodeParams
            br.ReadByte(); // flags
            SkipNodeBaseParams(br); // node_base_params

            // children
            uint childCount = br.ReadUInt32();
            for (int i = 0; i < childCount; i++)
                br.ReadUInt32(); // child id
            SkipStingerAndMeters(br);
        }

        public static void SkipStingerAndMeters(BinaryReader br)
        {

            // meter_info
            br.ReadDouble(); // grid_period
            br.ReadDouble(); // grid_offset
            br.ReadSingle(); // tempo
            br.ReadByte();   // time_signature_beat_count
            br.ReadByte();   // time_signature_beat_value
            br.ReadByte();   // meter_info_flag

            // stingers
            uint stingerCount = br.ReadUInt32();
            for (int i = 0; i < stingerCount; i++)
            {
                br.ReadUInt32(); // trigger_id
                br.ReadUInt32(); // segment_id
                br.ReadUInt32(); // sync_play_at (AkSyncType)
                br.ReadUInt32(); // cue_filter_hash
                br.ReadInt32();  // dont_repeat_time
                br.ReadUInt32(); // segment_look_head_count
            }
        }
    }
}
