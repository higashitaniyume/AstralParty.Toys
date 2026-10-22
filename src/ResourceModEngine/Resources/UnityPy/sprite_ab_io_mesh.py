"""Sprite 动画导出与回填脚本。

功能分为三块：
1. 从 bundle 导出动画帧与 clip 元数据。
2. 按普通模式或严格时间轴模式回填动画资源。
3. 使用 AssetsTools.NET 处理 UnityPy 不擅长的整包保存与对象级补丁。
"""

import argparse
import base64
import glob
import hashlib
import importlib.util
import json
import math
import os
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import time
from collections import defaultdict
from typing import Any, Dict, List, Set, Tuple
import random, traceback

PROGRESS_PREFIX = "[RML_PROGRESS]"

_OBJECT_READ_CACHE: Dict[int, Any] = {}
_OBJECT_DICT_CACHE: Dict[int, dict] = {}
_OBJECT_IMAGE_SIGNATURE_CACHE: Dict[int, Tuple[Tuple[int, int], str]] = {}
_FILE_IMAGE_CACHE: Dict[str, Any] = {}
_FILE_IMAGE_SIGNATURE_CACHE: Dict[str, Tuple[Tuple[int, int], str]] = {}

REQUIRED_PYTHON = (3, 12)
REQUIRED_PACKAGES = {
    "clr": "pythonnet",
    "UnityPy": "UnityPy",
    "PIL": "Pillow",
}


def _has_module(module_name: str) -> bool:
    return importlib.util.find_spec(module_name) is not None


def ensure_python312_dependencies():
    """在推荐的 Python 3.12 环境下自动补齐运行依赖。"""
    if sys.version_info[:2] != REQUIRED_PYTHON:
        print(
            f"[sprite_ab_io_mesh] Recommended Python version is {REQUIRED_PYTHON[0]}.{REQUIRED_PYTHON[1]}; "
            f"current version is {sys.version_info.major}.{sys.version_info.minor}."
        )
        return

    missing_packages = [package_name for module_name, package_name in REQUIRED_PACKAGES.items() if not _has_module(module_name)]
    if not missing_packages:
        return

    print(f"[sprite_ab_io_mesh] Installing missing packages: {', '.join(missing_packages)}")
    subprocess.check_call([sys.executable, "-m", "pip", "install", *missing_packages])


ensure_python312_dependencies()

import UnityPy
from PIL import Image


# ---------------------------------------------------------------------------
# 基础工具与进度输出
# ---------------------------------------------------------------------------

def ensure_dir(p):
    """确保目录存在。"""
    os.makedirs(p, exist_ok=True)


def sanitize_name(s: str) -> str:
    """把 Unity 资源名转换成适合文件系统使用的名字。"""
    return (s or "").replace("\\", "_").replace("/", "_").strip()


def round_int(x: float) -> int:
    """按 Python round 规则取整并返回 int。"""
    return int(round(x))


def emit_progress_setup(total: int, desc: str = ""):
    print(f"{PROGRESS_PREFIX} SETUP|{max(0, to_int(total, 0))}|{desc}", flush=True)


def emit_progress_step(desc: str, step: int = 1):
    print(f"{PROGRESS_PREFIX} STEP|{max(0, to_int(step, 0))}|{desc}", flush=True)


def emit_progress_done(desc: str = ""):
    print(f"{PROGRESS_PREFIX} DONE|0|{desc}", flush=True)


def clear_runtime_caches():
    """清空当前进程内的 UnityPy 对象与图片缓存。"""
    _OBJECT_READ_CACHE.clear()
    _OBJECT_DICT_CACHE.clear()
    _OBJECT_IMAGE_SIGNATURE_CACHE.clear()
    _FILE_IMAGE_CACHE.clear()
    _FILE_IMAGE_SIGNATURE_CACHE.clear()


def get_runtime_object_key(obj) -> int:
    return id(obj)


def cached_read(obj):
    """缓存 ObjectReader.read() 结果，避免同一流程重复解析对象。"""
    key = get_runtime_object_key(obj)
    cached_value = _OBJECT_READ_CACHE.get(key)
    if cached_value is not None:
        return cached_value
    value = obj.read()
    _OBJECT_READ_CACHE[key] = value
    return value


def cached_parse_as_dict(obj) -> dict:
    """缓存 ObjectReader.parse_as_dict() 结果，避免重复 typetree 解析。"""
    key = get_runtime_object_key(obj)
    cached_value = _OBJECT_DICT_CACHE.get(key)
    if cached_value is not None:
        return cached_value
    value = obj.parse_as_dict()
    _OBJECT_DICT_CACHE[key] = value
    return value


def invalidate_object_runtime_cache(obj):
    """对象被保存后清除相关缓存，避免后续读取到旧状态。"""
    key = get_runtime_object_key(obj)
    _OBJECT_READ_CACHE.pop(key, None)
    _OBJECT_DICT_CACHE.pop(key, None)
    _OBJECT_IMAGE_SIGNATURE_CACHE.pop(key, None)


def save_typetree_cached(obj, data: dict):
    """保存 typetree，并同步更新缓存中的 dict 视图。"""
    obj.save_typetree(data)
    key = get_runtime_object_key(obj)
    _OBJECT_DICT_CACHE[key] = data
    _OBJECT_READ_CACHE.pop(key, None)
    _OBJECT_IMAGE_SIGNATURE_CACHE.pop(key, None)


def load_cached_rgba_image(path: str) -> Image.Image:
    """缓存外部 PNG 的 RGBA 图像，复用重复帧或重复引用的图片。"""
    cached_image = _FILE_IMAGE_CACHE.get(path)
    if cached_image is not None:
        return cached_image
    image = Image.open(path).convert("RGBA")
    _FILE_IMAGE_CACHE[path] = image
    return image


def build_image_signature(image: Image.Image) -> Tuple[Tuple[int, int], str]:
    """生成图像尺寸与像素摘要，用于快速判断图片内容是否一致。"""
    rgba_image = image if image.mode == "RGBA" else image.convert("RGBA")
    digest = hashlib.blake2b(rgba_image.tobytes(), digest_size=16).hexdigest()
    return rgba_image.size, digest


def get_cached_file_image_signature(path: str) -> Tuple[Tuple[int, int], str]:
    """获取外部 PNG 的缓存摘要，避免重复做像素级全量比较。"""
    cached_signature = _FILE_IMAGE_SIGNATURE_CACHE.get(path)
    if cached_signature is not None:
        return cached_signature
    image = load_cached_rgba_image(path)
    signature = build_image_signature(image)
    _FILE_IMAGE_SIGNATURE_CACHE[path] = signature
    return signature


def get_cached_object_image_signature(obj) -> Tuple[Tuple[int, int], str]:
    """获取当前 Sprite 图像的缓存摘要。"""
    key = get_runtime_object_key(obj)
    cached_signature = _OBJECT_IMAGE_SIGNATURE_CACHE.get(key)
    if cached_signature is not None:
        return cached_signature
    image = cached_read(obj).image.convert("RGBA")
    signature = build_image_signature(image)
    _OBJECT_IMAGE_SIGNATURE_CACHE[key] = signature
    return signature


def images_match_by_signature(file_path: str, obj) -> Tuple[Image.Image, bool]:
    """返回替换图像以及与当前 Sprite 图像是否一致的判断结果。"""
    replacement_image = load_cached_rgba_image(file_path)
    replacement_signature = get_cached_file_image_signature(file_path)
    current_signature = get_cached_object_image_signature(obj)
    return replacement_image, replacement_signature == current_signature

def bytes_from_maybe_array(x) -> bytes:
    """把 UnityPy/.NET 可能返回的多种字节容器统一转成 bytes。"""
    if x is None: return b""
    if isinstance(x, (bytes, bytearray, memoryview)): return bytes(x)
    if hasattr(x, "data"):
        try: return bytes(x.data)
        except: pass
    if isinstance(x, (list, tuple)):
        try: return bytes(x)
        except: pass
    try: return bytes(x)
    except: return b""


def encode_bytes_for_json(value) -> str:
    raw = bytes_from_maybe_array(value)
    if not raw:
        return ""
    return base64.b64encode(raw).decode("ascii")


def decode_bytes_from_json(value) -> bytes:
    if not value:
        return b""
    try:
        return base64.b64decode(value)
    except Exception:
        return b""

def to_float(v, default=0.0):
    try: return float(v)
    except: return float(default)

def to_int(v, default=0):
    try: return int(v)
    except: return int(default)


def floats_close(a, b, tol=1e-6):
    return abs(to_float(a, 0.0) - to_float(b, 0.0)) <= tol


def env_flag(name: str) -> bool:
    value = (os.environ.get(name) or "").strip().lower()
    return value in ("1", "true", "yes", "on")


def use_loop_last_to_first(meta: dict) -> bool:
    """判断普通模式下是否要把循环动画最后一帧重写成第一帧。"""
    if not bool(meta.get("loop", True)):
        return False
    if env_flag("RML_DISABLE_LOOP_LAST_TO_FIRST"):
        return False
    if env_flag("RML_LOOP_LAST_TO_FIRST"):
        return True
    if env_flag("RML_APPEND_LOOP_CLOSING_FRAME"):
        return False
    return True

def get_loop_flag(clip) -> bool:
    settings = getattr(clip, "m_AnimationClipSettings", None)
    if settings is not None:
        for name in ("m_LoopTime", "m_LoopBlend"):
            try:
                value = getattr(settings, name)
                return bool(value)
            except Exception:
                pass
    return True

def get_clip_length(clip) -> float:
    try:
        muscle = getattr(clip, "m_MuscleClip", None)
        if muscle is not None:
            stop_time = getattr(muscle, "m_StopTime", None)
            if stop_time is not None:
                return to_float(stop_time, 0.0)
    except Exception:
        pass
    return 0.0

def get_sprite_rect(spr):
    tr = None
    try: tr = spr.m_RD.textureRect
    except Exception: tr = None
    if tr is None:
        try: tr = spr.m_Rect
        except Exception: tr = None
    return tr

def get_texture_pid(spr) -> int:
    try:
        tex_ptr = spr.m_RD.texture
        return int(getattr(tex_ptr, "path_id", 0) or 0)
    except Exception:
        return 0

def get_pixels_per_unit(spr) -> float:
    for name in ("m_PixelsToUnits", "pixelsToUnits"):
        try:
            value = getattr(spr, name)
            if value is not None:
                return to_float(value, 100.0)
        except Exception:
            pass
    return 100.0

def get_border(spr):
    border = None
    for name in ("m_Border", "border"):
        try:
            border = getattr(spr, name)
            if border is not None:
                break
        except Exception:
            pass
    if border is None:
        return [0.0, 0.0, 0.0, 0.0]

    result = []
    for name in ("x", "y", "z", "w"):
        try: result.append(to_float(getattr(border, name), 0.0))
        except Exception: result.append(0.0)
    while len(result) < 4:
        result.append(0.0)
    return result[:4]

def get_pivot01(spr, rect_w: int, rect_h: int):
    pivot = None
    for name in ("m_Pivot", "pivot"):
        try:
            pivot = getattr(spr, name)
            if pivot is not None:
                break
        except Exception:
            pass
    if pivot is None or rect_w <= 0 or rect_h <= 0:
        return [0.5, 0.5]
    try:
        return [
            to_float(getattr(pivot, "x", 0.5), 0.5),
            to_float(getattr(pivot, "y", 0.5), 0.5),
        ]
    except Exception:
        return [0.5, 0.5]


def copy_sprite_internal_meta(sprite_data: dict) -> dict:
    sprite_data = sprite_data or {}
    rd = sprite_data.get("m_RD") or {}
    vertex_data = rd.get("m_VertexData") or {}
    return {
        "isPolygon": bool(sprite_data.get("m_IsPolygon", False)),
        "rect": clone_json_value(sprite_data.get("m_Rect") or {}),
        "offset": clone_json_value(sprite_data.get("m_Offset") or {}),
        "pivot": clone_json_value(sprite_data.get("m_Pivot") or {}),
        "border": clone_json_value(sprite_data.get("m_Border") or {}),
        "pixelsToUnits": to_float(sprite_data.get("m_PixelsToUnits"), 100.0),
        "rd": {
            "textureRect": clone_json_value(rd.get("textureRect") or {}),
            "textureRectOffset": clone_json_value(rd.get("textureRectOffset") or {}),
            "atlasRectOffset": clone_json_value(rd.get("atlasRectOffset") or {}),
            "settingsRaw": clone_json_value(rd.get("settingsRaw")),
            "uvTransform": clone_json_value(rd.get("uvTransform") or {}),
            "downscaleMultiplier": clone_json_value(rd.get("downscaleMultiplier")),
            "vertexCount": to_int(vertex_data.get("m_VertexCount"), 0),
            "vertexData": encode_bytes_for_json(vertex_data.get("m_DataSize")),
            "indexBuffer": encode_bytes_for_json(rd.get("m_IndexBuffer")),
            "subMeshes": clone_json_value(rd.get("m_SubMeshes") or []),
        },
    }


def build_texture_entry(texture) -> dict:
    if texture is None:
        return {}
    return {
        "width": max(0, round_int(getattr(texture, "m_Width", 0))),
        "height": max(0, round_int(getattr(texture, "m_Height", 0))),
        "name": sanitize_name(getattr(texture, "m_Name", None) or ""),
    }

def clip_name_key(clip_name: str, path_id: int) -> str:
    return sanitize_name(clip_name or f"clip_{path_id}")

def build_sprite_entry(pid: int, spr, sprite_data: dict, file_name: str, default_duration: float):
    rect = get_sprite_rect(spr)
    rect_w = max(1, round_int(getattr(rect, "width", 0) if rect is not None else 0))
    rect_h = max(1, round_int(getattr(rect, "height", 0) if rect is not None else 0))
    return {
        "origName": sanitize_name(getattr(spr, "m_Name", None) or f"sprite_{pid}"),
        "origPathID": pid,
        "duration": default_duration,
        "pivot01": get_pivot01(spr, rect_w, rect_h),
        "border": get_border(spr),
        "rectPx": [rect_w, rect_h],
        "pixelsPerUnit": get_pixels_per_unit(spr),
        "sourceSprite": copy_sprite_internal_meta(sprite_data),
        "file": file_name,
    }

def dedupe_file_name(base_name: str, used_names: Dict[str, int]) -> str:
    stem = sanitize_name(base_name) or "sprite"
    count = used_names.get(stem, 0) + 1
    used_names[stem] = count
    return f"{stem}.png" if count == 1 else f"{stem}__{count}.png"

def clone_json_value(value):
    return json.loads(json.dumps(value))

def natural_suffix_sort_key(file_name: str):
    stem = os.path.splitext(os.path.basename(file_name))[0]
    prefix, number, _ = parse_trailing_number(stem)
    if number is None:
        return (sanitize_name(stem).lower(), 1, 0, file_name.lower())
    return (sanitize_name(prefix).lower(), 0, number, file_name.lower())

def list_clip_png_files(clip_dir: str) -> List[str]:
    files = []
    for entry in os.listdir(clip_dir):
        path = os.path.join(clip_dir, entry)
        if os.path.isfile(path) and entry.lower().endswith(".png"):
            files.append(entry)
    files.sort(key=natural_suffix_sort_key)
    return files

def normalize_vec2_list(value, default=None):
    default = default or [0.5, 0.5]
    if isinstance(value, list) and len(value) >= 2:
        return [to_float(value[0], default[0]), to_float(value[1], default[1])]
    return list(default)

def normalize_vec4_list(value, default=None):
    default = default or [0.0, 0.0, 0.0, 0.0]
    if isinstance(value, list) and len(value) >= 4:
        return [
            to_float(value[0], default[0]),
            to_float(value[1], default[1]),
            to_float(value[2], default[2]),
            to_float(value[3], default[3]),
        ]
    return list(default)

def parse_trailing_number(name: str):
    match = re.match(r"^(.*?)(\d+)$", sanitize_name(name or ""))
    if not match:
        return None, None, None
    prefix, digits = match.groups()
    return prefix, int(digits), len(digits)

def infer_frame_name_pattern(frames: List[dict], clip_name: str):
    next_index = 0
    width = 3
    prefix = f"{sanitize_name(clip_name)}_"
    for frame in frames:
        stem = os.path.splitext(frame.get("file") or "")[0]
        parsed_prefix, parsed_index, parsed_width = parse_trailing_number(stem)
        if parsed_index is not None:
            prefix = parsed_prefix
            width = max(width, parsed_width or 0)
            next_index = max(next_index, parsed_index + 1)
            continue

        parsed_prefix, parsed_index, parsed_width = parse_trailing_number(frame.get("origName") or "")
        if parsed_index is not None:
            prefix = parsed_prefix
            width = max(width, parsed_width or 0)
            next_index = max(next_index, parsed_index + 1)

    return prefix, width, next_index

def generate_random_meta_path_id(used_ids: Set[int]) -> int:
    while True:
        value = random.randint(-INT_64, INT_64)
        if value != 0 and value not in used_ids:
            used_ids.add(value)
            return value

def derive_frame_duration(meta: dict, frames: List[dict], keyframes: List[dict]) -> float:
    for frame in frames:
        duration = to_float(frame.get("duration"), 0.0)
        if duration > 0:
            return duration

    key_times = [to_float(entry.get("time"), -1.0) for entry in keyframes]
    for idx in range(len(key_times) - 1):
        delta = key_times[idx + 1] - key_times[idx]
        if delta > 0:
            return delta

    frame_count = max(0, to_int(meta.get("frameCount"), len(keyframes) or len(frames)))
    clip_length = to_float(meta.get("length"), 0.0)
    if frame_count > 0 and clip_length > 0:
        return clip_length / float(frame_count)

    sample_rate = to_float(meta.get("sampleRate"), 0.0)
    if sample_rate > 0:
        return 1.0 / sample_rate

    return 0.0


# ---------------------------------------------------------------------------
# 动画导出与时间轴元数据
# ---------------------------------------------------------------------------

def get_dense_frame_count_from_clip_dict(clip_dict: dict) -> int:
    return to_int(
        clip_dict.get("m_MuscleClip", {}).get("m_Clip", {}).get("data", {}).get("m_DenseClip", {}).get("m_FrameCount"),
        0,
    )


def u32_to_float(value: int) -> float:
    return struct.unpack(">f", struct.pack(">I", to_int(value, 0) & 0xFFFFFFFF))[0]


def decode_streamed_clip_segments(streamed_data: List[int], clip_length: float) -> List[dict]:
    raw = [(to_int(value, 0) & 0xFFFFFFFF) for value in (streamed_data or [])]
    if len(raw) < 2 or (len(raw) - 2) % 7 != 0:
        return []

    segment_count = (len(raw) - 2) // 7
    if segment_count <= 0:
        return []

    segments = []
    current_time = 0.0
    for index in range(segment_count):
        base = 2 + index * 7
        raw_segment = raw[base:base + 7]
        next_time = u32_to_float(raw_segment[5]) if len(raw_segment) >= 6 else math.inf
        end_time = next_time
        if index == segment_count - 1 or not math.isfinite(end_time):
            end_time = clip_length if clip_length > current_time else current_time
        elif end_time < current_time:
            end_time = current_time

        segments.append({
            "index": index,
            "time": current_time,
            "endTime": end_time,
            "raw": raw_segment,
        })
        current_time = end_time

    return segments


def build_source_timeline_meta(clip_dict: dict, clip_length: float, keyframes: List[dict], unique_frame_count: int) -> dict:
    """从 AnimationClip 中提取严格回填所需的原始时间轴信息。"""
    streamed_data = clip_dict.get("m_MuscleClip", {}).get("m_Clip", {}).get("data", {}).get("m_StreamedClip", {}).get("data", []) or []
    streamed_data = [(to_int(value, 0) & 0xFFFFFFFF) for value in streamed_data]
    streamed_segments = decode_streamed_clip_segments(streamed_data, clip_length)

    timeline_keyframes = []
    if streamed_segments and len(streamed_segments) == len(keyframes):
        for segment, keyframe in zip(streamed_segments, keyframes):
            timeline_keyframes.append({
                "index": segment["index"],
                "time": segment["time"],
                "endTime": segment["endTime"],
                "pathId": to_int(keyframe.get("pathId"), 0),
                "file": keyframe.get("file") or "",
            })
    else:
        for index, keyframe in enumerate(keyframes):
            start_time = to_float(keyframe.get("time"), 0.0)
            if index + 1 < len(keyframes):
                end_time = to_float(keyframes[index + 1].get("time"), start_time)
            else:
                end_time = clip_length if clip_length > start_time else start_time
            if end_time < start_time:
                end_time = start_time
            timeline_keyframes.append({
                "index": index,
                "time": start_time,
                "endTime": end_time,
                "pathId": to_int(keyframe.get("pathId"), 0),
                "file": keyframe.get("file") or "",
            })

    return {
        "mode": "streamed" if streamed_segments else "keyframes",
        "slotCount": len(keyframes),
        "uniqueFrameCount": unique_frame_count,
        "stopTime": clip_length,
        "denseFrameCount": get_dense_frame_count_from_clip_dict(clip_dict),
        "streamedClipData": streamed_data,
        "keyframes": timeline_keyframes,
    }

def sync_clip_meta_with_pngs(clip_dir: str, meta: dict) -> Tuple[dict, bool]:
    """普通模式下按当前 PNG 列表重建 clip.json 的帧清单与均匀时间轴。"""
    png_files = list_clip_png_files(clip_dir)
    if not png_files:
        return meta, False

    frames = [clone_json_value(frame) for frame in (meta.get("frames") or [])]
    keyframes = [clone_json_value(entry) for entry in (meta.get("keyframes") or [])]
    clip_name = meta.get("clipName") or os.path.basename(clip_dir)

    ordered_files = []
    used_files = set()
    for frame in frames:
        file_name = frame.get("file") or ""
        if file_name in png_files and file_name not in used_files:
            ordered_files.append(file_name)
            used_files.add(file_name)
    for file_name in png_files:
        if file_name not in used_files:
            ordered_files.append(file_name)
            used_files.add(file_name)

    frame_duration = derive_frame_duration(meta, frames, keyframes)
    default_frame = clone_json_value(frames[-1] if frames else {})
    default_pivot01 = normalize_vec2_list(default_frame.get("pivot01"), [0.5, 0.5])
    default_border = normalize_vec4_list(default_frame.get("border"), [0.0, 0.0, 0.0, 0.0])
    default_rect = default_frame.get("rectPx") if isinstance(default_frame.get("rectPx"), list) and len(default_frame.get("rectPx")) >= 2 else [1, 1]
    prefix, width, next_index = infer_frame_name_pattern(frames, clip_name)

    frame_by_file = {frame.get("file") or "": frame for frame in frames if frame.get("file")}
    used_ids = {to_int(frame.get("origPathID"), 0) for frame in frames if to_int(frame.get("origPathID"), 0) != 0}
    for entry in keyframes:
        path_id = to_int(entry.get("pathId"), 0)
        if path_id != 0:
            used_ids.add(path_id)

    updated_frames = []
    updated_keyframes = []

    for idx, file_name in enumerate(ordered_files):
        existing = frame_by_file.get(file_name)
        if existing is not None:
            frame = clone_json_value(existing)
            frame["duration"] = frame_duration
            frame["pivot01"] = normalize_vec2_list(frame.get("pivot01"), default_pivot01)
            frame["border"] = normalize_vec4_list(frame.get("border"), default_border)
            if not isinstance(frame.get("rectPx"), list) or len(frame.get("rectPx")) < 2:
                frame["rectPx"] = list(default_rect)
            frame["file"] = file_name
        else:
            name_number = next_index
            next_index += 1
            orig_name = f"{prefix}{name_number:0{width}d}"
            path_id = generate_random_meta_path_id(used_ids)
            frame = {
                "origName": orig_name,
                "origPathID": path_id,
                "duration": frame_duration,
                "pivot01": list(default_pivot01),
                "border": list(default_border),
                "rectPx": list(default_rect),
                "file": file_name,
            }

        updated_frames.append(frame)
        updated_keyframes.append({
            "index": idx,
            "time": frame_duration * idx,
            "pathId": to_int(frame.get("origPathID"), 0),
            "spriteName": frame.get("origName") or f"sprite_{idx}",
            "file": file_name,
        })

    updated_meta = clone_json_value(meta)
    updated_meta["frames"] = updated_frames
    updated_meta["keyframes"] = updated_keyframes
    updated_meta["frameCount"] = len(updated_keyframes)
    updated_meta["spritePathIds"] = [to_int(entry.get("pathId"), 0) for entry in updated_keyframes]
    updated_meta["length"] = frame_duration * len(updated_keyframes)

    changed = json.dumps(updated_meta, ensure_ascii=False, sort_keys=True) != json.dumps(meta, ensure_ascii=False, sort_keys=True)
    return updated_meta, changed


def maybe_append_loop_closing_frame(meta: dict, resolved_frames: List[dict], original_count: int) -> List[dict]:
    if not env_flag("RML_APPEND_LOOP_CLOSING_FRAME"):
        return resolved_frames
    if not bool(meta.get("loop", True)) or len(resolved_frames) <= 1:
        return resolved_frames

    result = list(resolved_frames)
    first_frame = dict(resolved_frames[0])
    last_time = to_float(resolved_frames[-1].get("time"), float(len(resolved_frames) - 1))
    frame_duration = derive_frame_duration(meta, meta.get("frames") or [], meta.get("keyframes") or [])
    if frame_duration <= 0:
        sample_rate = to_float(meta.get("sampleRate"), 30.0)
        frame_duration = (1.0 / sample_rate) if sample_rate > 0 else (1.0 / 30.0)
    first_frame["time"] = last_time + frame_duration
    first_frame["index"] = len(result)
    first_frame["is_loop_closing_frame"] = True
    if first_frame.get("frame_meta") is None and original_count > 0:
        first_frame["frame_meta"] = {}
    result.append(first_frame)
    return result


def maybe_rewrite_last_frame_to_first(meta: dict, resolved_frames: List[dict]) -> List[dict]:
    if not use_loop_last_to_first(meta) or len(resolved_frames) <= 1:
        return resolved_frames

    result = list(resolved_frames)
    first_frame = dict(result[0])
    last_frame = dict(result[-1])
    first_frame["index"] = last_frame.get("index", len(result) - 1)
    first_frame["time"] = last_frame.get("time", first_frame.get("time", 0.0))
    first_frame["frame_meta"] = first_frame.get("frame_meta") or last_frame.get("frame_meta") or {}
    first_frame["is_loop_last_to_first"] = True
    result[-1] = first_frame
    return result

def gather_clip_data(env) -> Dict[str, dict]:
    """扫描 bundle 中的 AnimationClip，并导出每个动画对应的 Sprite 元数据。"""
    sprites_by_pid = {}
    sprite_data_by_pid = {}
    for obj in env.objects:
        if obj.type.name != "Sprite":
            continue
        try:
            sprites_by_pid[obj.path_id] = obj.read()
        except Exception:
            pass
        try:
            sprite_data_by_pid[obj.path_id] = obj.parse_as_dict()
        except Exception:
            pass

    clips = {}
    for obj in env.objects:
        if obj.type.name != "AnimationClip":
            continue
        try:
            clip = obj.read()
        except Exception:
            continue
        try:
            clip_dict = obj.parse_as_dict()
        except Exception:
            clip_dict = {}

        clip_name = getattr(clip, "m_Name", None) or f"clip_{obj.path_id}"
        clip_key = clip_name_key(clip_name, obj.path_id)
        sample_rate = to_float(getattr(clip, "m_SampleRate", 0.0), 0.0)
        length = get_clip_length(clip)

        keyframes = []
        pptr_curves = getattr(clip, "m_PPtrCurves", None) or getattr(clip, "pptrCurves", None)
        if pptr_curves:
            for c in pptr_curves:
                attr = getattr(c, "attribute", "") or getattr(c, "m_Attribute", "")
                if attr not in ("m_Sprite", "sprite", "Sprite"):
                    continue
                for idx, kf in enumerate(getattr(c, "curve", []) or []):
                    sp_ptr = getattr(kf, "value", None)
                    pid = int(getattr(sp_ptr, "path_id", 0) or 0) if sp_ptr else 0
                    if pid and pid in sprites_by_pid:
                        keyframes.append({
                            "index": len(keyframes),
                            "time": to_float(getattr(kf, "time", idx / sample_rate if sample_rate > 0 else idx), idx),
                            "pathId": pid,
                        })

        if not keyframes:
            mapping = getattr(clip, "m_ClipBindingConstant", None)
            pmap = getattr(mapping, "pptrCurveMapping", None) if mapping else None
            if pmap:
                for idx, pp in enumerate(pmap):
                    pid = int(getattr(pp, "path_id", 0) or 0)
                    if pid and pid in sprites_by_pid:
                        keyframes.append({
                            "index": len(keyframes),
                            "time": idx / sample_rate if sample_rate > 0 else idx,
                            "pathId": pid,
                        })

        if not keyframes:
            obj_curves = getattr(clip, "m_ObjectCurves", None)
            if obj_curves:
                for c in obj_curves:
                    for idx, kf in enumerate(getattr(c, "curve", []) or []):
                        sp_ptr = getattr(kf, "value", None)
                        pid = int(getattr(sp_ptr, "path_id", 0) or 0) if sp_ptr else 0
                        if pid and pid in sprites_by_pid:
                            keyframes.append({
                                "index": len(keyframes),
                                "time": to_float(getattr(kf, "time", idx / sample_rate if sample_rate > 0 else idx), idx),
                                "pathId": pid,
                            })

        if not keyframes:
            continue

        ordered_pids = [entry["pathId"] for entry in keyframes]
        first_sprite = sprites_by_pid.get(ordered_pids[0]) if ordered_pids else None
        texture_pid = get_texture_pid(first_sprite) if first_sprite else 0
        default_duration = (1.0 / sample_rate) if sample_rate > 0 else 0.0

        unique_pids = []
        seen_pids = set()
        for pid in ordered_pids:
            if pid in seen_pids:
                continue
            seen_pids.add(pid)
            unique_pids.append(pid)

        frame_entries = []
        used_file_names = {}
        for pid in unique_pids:
            spr = sprites_by_pid.get(pid)
            sprite_data = sprite_data_by_pid.get(pid, {})
            sp_name = sanitize_name(getattr(spr, "m_Name", None) or f"sprite_{pid}") if spr else f"sprite_{pid}"
            file_name = dedupe_file_name(sp_name, used_file_names)
            frame_entries.append(build_sprite_entry(pid, spr, sprite_data, file_name, default_duration))

        file_by_pid = {frame["origPathID"]: frame["file"] for frame in frame_entries}
        name_by_pid = {frame["origPathID"]: frame["origName"] for frame in frame_entries}
        source_texture = None
        if texture_pid:
            source_texture = build_texture_entry(getattr(first_sprite.m_RD, "texture", None).read() if first_sprite and getattr(first_sprite, "m_RD", None) and getattr(first_sprite.m_RD, "texture", None) else None)

        for entry in keyframes:
            pid = entry["pathId"]
            entry["spriteName"] = name_by_pid.get(pid, f"sprite_{pid}")
            entry["file"] = file_by_pid.get(pid, "")

        source_timeline = build_source_timeline_meta(clip_dict, length, keyframes, len(frame_entries))
        timeline_keyframes = source_timeline.get("keyframes") or []
        if len(timeline_keyframes) == len(keyframes):
            for entry, timeline_entry in zip(keyframes, timeline_keyframes):
                entry["time"] = to_float(timeline_entry.get("time"), entry.get("time"))
                entry["endTime"] = to_float(timeline_entry.get("endTime"), entry["time"])

        clips[clip_key] = {
            "clipName": clip_name,
            "clipPathId": obj.path_id,
            "unityVersion": "",
            "length": length,
            "loop": get_loop_flag(clip),
            "originalSampleRate": sample_rate,
            "sampleRate": sample_rate,
            "frameCount": len(keyframes),
            "pixelsPerUnit": get_pixels_per_unit(first_sprite) if first_sprite else 100.0,
            "texturePathId": texture_pid,
            "sourceTexture": source_texture or {},
            "spritePathIds": ordered_pids,
            "frames": frame_entries,
            "keyframes": keyframes,
            "sourceTimeline": source_timeline,
        }

    return clips

INT_64 = 2 ** 63 - 1
_ASSETS_TOOLS = None
_ASSETS_CLASSDB_LOADED = False


# ---------------------------------------------------------------------------
# 动画对象索引与 AssetsTools.NET 桥接
# ---------------------------------------------------------------------------

class SpriteAnimation:
    """聚合 AnimationClip、Sprite 槽位与关联 MonoBehaviour 的运行时视图。"""

    def __init__(self, animation_clip):
        self.anim_name = animation_clip.peek_name() or ""
        self.animation_clip = animation_clip
        self._animation_clip_data = None
        self.mono_behaviour = None
        self.sprites = []
        self.texture = None
        self._sprite_name_format = None

    @property
    def animation_clip_data(self):
        """延迟解析 AnimationClip typetree，避免重复读取。"""
        if self._animation_clip_data is None:
            self._animation_clip_data = cached_parse_as_dict(self.animation_clip)
        return self._animation_clip_data

    @property
    def sprite_name_format(self):
        """根据当前动画首帧名推断新增 Sprite 的命名模板。"""
        if self._sprite_name_format:
            return self._sprite_name_format
        if not self.sprites:
            self._sprite_name_format = f"{sanitize_name(self.anim_name)}_{{}}"
            return self._sprite_name_format
        sprite_name, _, _ = self.sprites[0]
        sep_at = sprite_name.rfind("_")
        if sep_at == -1:
            self._sprite_name_format = f"{sanitize_name(self.anim_name)}_{{}}"
        else:
            width = len(sprite_name[sep_at + 1:])
            self._sprite_name_format = f"{sprite_name[:sep_at + 1]}{{:0{width}d}}"
        return self._sprite_name_format


def ensure_assets_tools():
    """加载 AssetsTools.NET 及其依赖，用于安全地重写 bundle。"""
    global _ASSETS_TOOLS, _ASSETS_CLASSDB_LOADED
    if _ASSETS_TOOLS is not None:
        return _ASSETS_TOOLS

    try:
        import clr  # type: ignore[import-not-found]
    except Exception as ex:
        raise RuntimeError("import-clips 需要 pythonnet(clr) 支持，请改用 Python 3.12/3.11 并安装 pythonnet") from ex

    libs_dir = os.path.join(os.path.dirname(__file__), "libs")
    dll_path = os.path.join(libs_dir, "AssetsTools.NET.dll")
    classdb_path = os.path.join(libs_dir, "classdata.tpk")
    if not os.path.isfile(dll_path):
        raise RuntimeError(f"未找到 AssetsTools.NET.dll: {dll_path}")
    if not os.path.isfile(classdb_path):
        raise RuntimeError(f"未找到 classdata.tpk: {classdb_path}")

    clr.AddReference(dll_path)
    from System import Array, Byte  # type: ignore[import-not-found]
    from System.Linq import Enumerable  # type: ignore[import-not-found]
    from AssetsTools.NET import (  # type: ignore[import-not-found]
        AssetBundleCompressionType,
        AssetFileInfo,
        AssetTypeArrayInfo,
        AssetsFileReader,
        AssetsFileWriter,
        ContentReplacerFromAssets,
        ContentReplacerFromBuffer,
    )
    from AssetsTools.NET.Extra import AssetClassID, AssetsManager  # type: ignore[import-not-found]

    _ASSETS_TOOLS = {
        "Array": Array,
        "Byte": Byte,
        "Enumerable": Enumerable,
        "manager": AssetsManager(),
        "AssetsManager": AssetsManager,
        "AssetBundleCompressionType": AssetBundleCompressionType,
        "AssetClassID": AssetClassID,
        "AssetFileInfo": AssetFileInfo,
        "AssetTypeArrayInfo": AssetTypeArrayInfo,
        "AssetsFileReader": AssetsFileReader,
        "AssetsFileWriter": AssetsFileWriter,
        "ContentReplacerFromAssets": ContentReplacerFromAssets,
        "ContentReplacerFromBuffer": ContentReplacerFromBuffer,
        "classdb": classdb_path,
    }
    return _ASSETS_TOOLS


def ensure_classdb_loaded(manager, classdb_path: str):
    """确保 class database 只被加载一次。"""
    global _ASSETS_CLASSDB_LOADED
    if _ASSETS_CLASSDB_LOADED:
        return
    manager.LoadClassPackage(classdb_path)
    _ASSETS_CLASSDB_LOADED = True


def float_to_u32(value: float) -> int:
    return struct.unpack(">I", struct.pack(">f", float(value)))[0]


def set_streamed_clip_data(anim_data: dict, streamed_data: List[int]):
    anim_data["m_MuscleClip"]["m_Clip"]["data"]["m_StreamedClip"]["data"] = [
        (to_int(value, 0) & 0xFFFFFFFF) for value in (streamed_data or [])
    ]


def apply_sprite_import_metadata(sprite_data: dict, frame_meta: dict, clip_pixels_per_unit: float):
    """把导出时记录的 Sprite 元信息回写到目标 Sprite。"""
    frame_meta = frame_meta or {}
    desired_name = sanitize_name(frame_meta.get("origName") or frame_meta.get("spriteName") or sprite_data.get("m_Name") or "")
    if desired_name:
        sprite_data["m_Name"] = desired_name

    source_sprite = frame_meta.get("sourceSprite") or {}
    if isinstance(source_sprite.get("isPolygon"), bool):
        sprite_data["m_IsPolygon"] = source_sprite.get("isPolygon")
    if isinstance(source_sprite.get("offset"), dict) and source_sprite.get("offset"):
        sprite_data["m_Offset"] = clone_json_value(source_sprite.get("offset"))
    if isinstance(source_sprite.get("pivot"), dict) and source_sprite.get("pivot"):
        sprite_data["m_Pivot"] = clone_json_value(source_sprite.get("pivot"))
    if isinstance(source_sprite.get("border"), dict) and source_sprite.get("border"):
        sprite_data["m_Border"] = clone_json_value(source_sprite.get("border"))

    pixels_per_unit = to_float(frame_meta.get("pixelsPerUnit"), 0.0)
    if pixels_per_unit <= 0:
        pixels_per_unit = to_float(source_sprite.get("pixelsToUnits"), 0.0)
    if pixels_per_unit <= 0:
        pixels_per_unit = to_float(clip_pixels_per_unit, to_float(sprite_data.get("m_PixelsToUnits"), 100.0))
    if pixels_per_unit > 0:
        sprite_data["m_PixelsToUnits"] = pixels_per_unit

    rd = sprite_data.get("m_RD") or {}
    source_rd = source_sprite.get("rd") or {}
    for key in ("textureRectOffset", "atlasRectOffset", "uvTransform"):
        value = source_rd.get(key)
        if isinstance(value, dict) and value:
            rd[key] = clone_json_value(value)
    for key in ("settingsRaw", "downscaleMultiplier"):
        value = source_rd.get(key)
        if value is not None:
            rd[key] = clone_json_value(value)


def get_preserve_timeline_texture_size(meta: dict, target_frames: List[dict]) -> Tuple[int, int]:
    source_texture = meta.get("sourceTexture") or {}
    width = max(0, round_int(source_texture.get("width", 0))) if isinstance(source_texture, dict) else 0
    height = max(0, round_int(source_texture.get("height", 0))) if isinstance(source_texture, dict) else 0
    if width > 0 and height > 0:
        return width, height

    max_x = 0
    max_y = 0
    for entry in target_frames:
        frame_meta = entry.get("frame_meta") or {}
        source_sprite = frame_meta.get("sourceSprite") or {}
        rect = source_sprite.get("rect") or {}
        tex_rect = (source_sprite.get("rd") or {}).get("textureRect") or {}
        if isinstance(rect, dict):
            max_x = max(max_x, round_int(rect.get("x", 0)) + max(0, round_int(rect.get("width", 0))))
            max_y = max(max_y, round_int(rect.get("y", 0)) + max(0, round_int(rect.get("height", 0))))
        if isinstance(tex_rect, dict):
            max_x = max(max_x, round_int(tex_rect.get("x", 0)) + max(0, round_int(tex_rect.get("width", 0))))
            max_y = max(max_y, round_int(tex_rect.get("y", 0)) + max(0, round_int(tex_rect.get("height", 0))))

    return max(1, max_x), max(1, max_y)


def build_preserve_timeline_texture_rect(frame_meta: dict, replacement_image: Image.Image) -> dict:
    source_sprite = (frame_meta or {}).get("sourceSprite") or {}
    source_rd = source_sprite.get("rd") or {}
    texture_rect = source_rd.get("textureRect") or {}
    if not isinstance(texture_rect, dict) or not texture_rect:
        return {
            "x": 0.0,
            "y": 0.0,
            "width": float(replacement_image.width),
            "height": float(replacement_image.height),
        }

    expected_w = max(1, round_int(texture_rect.get("width", replacement_image.width)))
    expected_h = max(1, round_int(texture_rect.get("height", replacement_image.height)))
    if replacement_image.size != (expected_w, expected_h):
        raise RuntimeError(
            f"[IMPORT-CLIPS] 完整时间轴回填要求 PNG 尺寸与源 Sprite 一致: {frame_meta.get('origName') or frame_meta.get('file') or 'unknown'}，"
            f"源尺寸 {expected_w}x{expected_h}，当前 {replacement_image.width}x{replacement_image.height}"
        )

    return {
        "x": float(to_float(texture_rect.get("x"), 0.0)),
        "y": float(to_float(texture_rect.get("y"), 0.0)),
        "width": float(expected_w),
        "height": float(expected_h),
    }


def apply_source_sprite_mesh(sprite_data: dict, frame_meta: dict):
    source_sprite = (frame_meta or {}).get("sourceSprite") or {}
    source_rd = source_sprite.get("rd") or {}
    vertex_data = sprite_data.get("m_RD", {}).get("m_VertexData", {})

    raw_vertex_data = decode_bytes_from_json(source_rd.get("vertexData"))
    raw_index_buffer = decode_bytes_from_json(source_rd.get("indexBuffer"))
    vertex_count = to_int(source_rd.get("vertexCount"), 0)
    if raw_vertex_data and vertex_count > 0:
        vertex_data["m_VertexCount"] = vertex_count
        vertex_data["m_DataSize"] = raw_vertex_data
    if raw_index_buffer:
        sprite_data["m_RD"]["m_IndexBuffer"] = raw_index_buffer
    sub_meshes = source_rd.get("subMeshes")
    if isinstance(sub_meshes, list) and sub_meshes:
        sprite_data["m_RD"]["m_SubMeshes"] = clone_json_value(sub_meshes)


def rebuild_sprite_from_source_meta(sprite_data: dict,
                                    atlas_size: Tuple[int, int],
                                    replacement_image: Image.Image,
                                    frame_meta: dict,
                                    clip_pixels_per_unit: float):
    """严格模式下按源 Sprite 的 rect、offset、mesh 与纹理布局完整回写。"""
    frame_meta = frame_meta or {}
    apply_sprite_import_metadata(sprite_data, frame_meta, clip_pixels_per_unit)

    source_sprite = frame_meta.get("sourceSprite") or {}
    source_rd = source_sprite.get("rd") or {}
    texture_rect = build_preserve_timeline_texture_rect(frame_meta, replacement_image)
    source_rect = source_sprite.get("rect") or {}
    source_offset = source_sprite.get("offset") or {}

    if isinstance(source_rect, dict) and source_rect:
        sprite_data["m_Rect"] = clone_json_value(source_rect)
    if isinstance(source_offset, dict) and source_offset:
        sprite_data["m_Offset"] = clone_json_value(source_offset)

    rd = sprite_data["m_RD"]
    rd["textureRect"] = clone_json_value(texture_rect)
    for key in ("textureRectOffset", "atlasRectOffset", "uvTransform"):
        value = source_rd.get(key)
        if isinstance(value, dict):
            rd[key] = clone_json_value(value)
    for key in ("settingsRaw", "downscaleMultiplier"):
        value = source_rd.get(key)
        if value is not None:
            rd[key] = clone_json_value(value)

    apply_source_sprite_mesh(sprite_data, frame_meta)
    return texture_rect


def load_animations(env) -> Dict[str, SpriteAnimation]:
    """建立动画名到 SpriteAnimation 的索引，并关联 MonoBehaviour sprites 列表。"""
    animations: Dict[str, SpriteAnimation] = {}
    mono_behaviours = []
    objects = {}
    for obj in env.objects:
        objects[obj.path_id] = obj
        if obj.type.name == "AnimationClip":
            animation = SpriteAnimation(obj)
            animations[(animation.anim_name or "").lower()] = animation
        elif obj.type.name == "MonoBehaviour":
            mono_behaviours.append(obj)

    sprites_of_animations = {}
    for animation in animations.values():
        anim_data = animation.animation_clip_data
        anim_pptr_mapping = anim_data.get("m_ClipBindingConstant", {}).get("pptrCurveMapping", [])
        for entry in anim_pptr_mapping:
            obj = objects.get(entry.get("m_PathID"))
            if not obj:
                continue
            sprites_of_animations[obj.path_id] = animation

            obj_name = obj.peek_name() or f"sprite_{obj.path_id}"
            idx = -1
            sep_at = obj_name.rfind("_")
            if sep_at != -1:
                try:
                    idx = int(obj_name[sep_at + 1:])
                except Exception:
                    idx = -1

            animation.sprites.append((obj_name, obj, idx))
            if animation.texture is None:
                try:
                    texture_pid = cached_parse_as_dict(obj)["m_RD"]["texture"]["m_PathID"]
                    texture = objects.get(texture_pid)
                    if texture:
                        animation.texture = texture
                except Exception:
                    pass

    for mono_behaviour in mono_behaviours:
        try:
            mono_data = cached_parse_as_dict(mono_behaviour)
        except Exception:
            continue
        sprites = mono_data.get("sprites")
        if not isinstance(sprites, list):
            continue
        for sprite in sprites:
            sprite_obj = objects.get(sprite.get("m_PathID"))
            if not sprite_obj:
                continue
            animation = sprites_of_animations.get(sprite_obj.path_id)
            if animation:
                animation.mono_behaviour = mono_behaviour
                break

    return animations


def collect_track_bindings(env):
    """收集 AnimationPlayableAsset 与 Track Clip 的映射，便于同步轨道时长。"""
    anim_id_to_playable_asset_id = {}
    playable_asset_id_to_track_clip = {}
    tracks = []

    for obj in env.objects:
        if obj.type.name != "MonoBehaviour":
            continue
        try:
            mono_data = cached_parse_as_dict(obj)
        except Exception:
            continue
        if "m_Clips" not in mono_data and "m_Clip" not in mono_data:
            continue
        if "m_Clips" in mono_data:
            for clip in mono_data["m_Clips"]:
                asset = clip.get("m_Asset") or {}
                playable_asset_id_to_track_clip[asset.get("m_PathID")] = clip
            tracks.append((obj, mono_data))
        else:
            clip = mono_data.get("m_Clip") or {}
            anim_id_to_playable_asset_id[clip.get("m_PathID")] = obj.path_id

    return anim_id_to_playable_asset_id, playable_asset_id_to_track_clip, tracks


def collect_used_sprite_render_key_ids(env, exclude_path_ids: Set[int] = None) -> Set[int]:
    exclude_path_ids = exclude_path_ids or set()
    used_ids: Set[int] = set()
    for obj in env.objects:
        if obj.type.name != "Sprite" or obj.path_id in exclude_path_ids:
            continue
        try:
            sprite_data = cached_parse_as_dict(obj)
        except Exception:
            continue
        render_key = sprite_data.get("m_RenderDataKey")
        if isinstance(render_key, list) and len(render_key) >= 2:
            used_ids.add(to_int(render_key[1], 0))
    return used_ids


def update_sprites(bundle_path: str, update_list: List[dict], temp_file: str):
    """在帧数增减时直接修改 Sprite 资产结构，补齐或移除 Sprite 对象。"""
    assets = ensure_assets_tools()
    manager = assets["manager"]
    AssetClassID = assets["AssetClassID"]
    AssetFileInfo = assets["AssetFileInfo"]
    AssetTypeArrayInfo = assets["AssetTypeArrayInfo"]
    AssetsFileWriter = assets["AssetsFileWriter"]

    manager.UnloadAll()
    ensure_classdb_loaded(manager, assets["classdb"])

    bundle = manager.LoadBundleFile(os.path.abspath(bundle_path))
    bundle_file_inst = manager.LoadAssetsFileFromBundle(bundle, 0)
    bundle_file = bundle_file_inst.file

    bundle_data_info = bundle_file.GetAssetsOfType(AssetClassID.AssetBundle)[0]
    bundle_data = manager.GetBaseField(bundle_file_inst, bundle_data_info)
    rand = random.Random()

    def add_sprites(needly: int, last_sprite_id: int, id_offset=0, name_format="new_sprite_{}"):
        sprite_id_added = []

        last_sprite_info = bundle_file.GetAssetInfo(last_sprite_id)
        last_sprite_field = manager.GetBaseField(bundle_file_inst, last_sprite_info)
        used_render_key_ids: Set[int] = set()
        for sprite_info in bundle_file.GetAssetsOfType(AssetClassID.Sprite):
            sprite_field = manager.GetBaseField(bundle_file_inst, sprite_info)
            try:
                used_render_key_ids.add(int(sprite_field.Get("m_RenderDataKey.second").AsLong))
            except Exception:
                continue

        for i in range(needly):
            path_id = None
            while not path_id or bundle_file.GetAssetInfo(path_id) is not None:
                path_id = rand.randint(-INT_64, INT_64)
            last_sprite_field.Get("m_Name").AsString = name_format.format(i + id_offset)
            render_key_id = generate_random_meta_path_id(used_render_key_ids)
            last_sprite_field.Get("m_RenderDataKey.second").AsLong = int(render_key_id)
            new_info = AssetFileInfo.Create(bundle_file, path_id, int(AssetClassID.Sprite))
            new_info.SetNewData(last_sprite_field)
            bundle_file.Metadata.AddAssetInfo(new_info)
            sprite_id_added.append(path_id)

        preload_insert_at = []
        preload_insert_entry = None
        m_preload_table = bundle_data.Get("m_PreloadTable.Array")
        for i in range(m_preload_table.AsArray.size):
            entry = m_preload_table.Get(i)
            path_id = entry.Get("m_PathID").AsLong
            if path_id != last_sprite_id:
                continue
            if preload_insert_entry is None:
                preload_insert_entry = entry
            preload_insert_at.append(i)
        if preload_insert_entry is None:
            return []

        for insert_at in reversed(preload_insert_at):
            for i, path_id in enumerate(sprite_id_added):
                clone = preload_insert_entry.Clone()
                clone.Get("m_PathID").AsLong = path_id
                m_preload_table.Children.Insert(insert_at + i + 1, clone)
        m_preload_table.AsArray = AssetTypeArrayInfo(m_preload_table.AsArray.size + needly * len(preload_insert_at))

        m_container = bundle_data.Get("m_Container.Array")
        last_sprite_index = -1
        last_sprite_entry = None
        second_updated = {}

        for i in range(m_container.AsArray.size):
            entry = m_container.Get(i)
            path_id = entry.Get("second.asset.m_PathID").AsLong
            if path_id == last_sprite_id:
                last_sprite_entry = entry
                last_sprite_index = i

            preload_index = entry.Get("second.preloadIndex").AsInt
            preload_size = entry.Get("second.preloadSize").AsInt
            data_updated = second_updated.get(preload_index)
            if data_updated is None:
                offset = 0
                size = 0
                for inserted in preload_insert_at:
                    if inserted < preload_index:
                        offset += len(sprite_id_added)
                    if preload_index <= inserted < preload_index + preload_size:
                        size += len(sprite_id_added)
                data_updated = {
                    "preloadIndex": preload_index + offset,
                    "preloadSize": preload_size + size,
                }
                second_updated[preload_index] = data_updated

            entry.Get("second.preloadIndex").AsInt = data_updated["preloadIndex"]
            entry.Get("second.preloadSize").AsInt = data_updated["preloadSize"]

        if last_sprite_entry is None:
            return []

        for i, path_id in enumerate(sprite_id_added):
            clone = last_sprite_entry.Clone()
            clone.Get("second.asset.m_PathID").AsLong = path_id
            m_container.Children.Insert(last_sprite_index + i + 1, clone)
        m_container.AsArray = AssetTypeArrayInfo(m_container.AsArray.size + len(sprite_id_added))
        return sprite_id_added

    def remove_sprites(needly: int, sprites: List[Tuple[str, Any, int]]):
        sprite_id_removed = []
        for i in range(needly):
            _, sprite, _ = sprites[-(i + 1)]
            bundle_file.Metadata.RemoveAssetInfo(bundle_file.Metadata.GetAssetInfo(sprite.path_id))
            sprite_id_removed.append(sprite.path_id)

        m_preload_table = bundle_data.Get("m_PreloadTable.Array")
        preload_removed = []
        for i in range(m_preload_table.AsArray.size):
            entry = m_preload_table.Get(i)
            if entry.Get("m_PathID").AsLong in sprite_id_removed:
                preload_removed.append(i)
        for i in reversed(preload_removed):
            m_preload_table.Children.RemoveAt(i)
        m_preload_table.AsArray = AssetTypeArrayInfo(m_preload_table.AsArray.size - len(preload_removed))

        m_container = bundle_data.Get("m_Container.Array")
        container_removed = []
        second_updated = {}
        for i in range(m_container.AsArray.size):
            entry = m_container.Get(i)
            path_id = entry.Get("second.asset.m_PathID").AsLong
            if path_id in sprite_id_removed:
                container_removed.append(i)
                continue

            preload_index = entry.Get("second.preloadIndex").AsInt
            preload_size = entry.Get("second.preloadSize").AsInt
            data_updated = second_updated.get(preload_index)
            if data_updated is None:
                offset = 0
                size = 0
                for removed in preload_removed:
                    if removed < preload_index:
                        offset -= 1
                    if preload_index <= removed < preload_index + preload_size:
                        size -= 1
                data_updated = {
                    "preloadIndex": preload_index + offset,
                    "preloadSize": preload_size + size,
                }
                second_updated[preload_index] = data_updated

            entry.Get("second.preloadIndex").AsInt = data_updated["preloadIndex"]
            entry.Get("second.preloadSize").AsInt = data_updated["preloadSize"]

        for i in reversed(container_removed):
            m_container.Children.RemoveAt(i)
        m_container.AsArray = AssetTypeArrayInfo(m_container.AsArray.size - len(container_removed))
        return sprite_id_removed

    changed = []
    for update in update_list:
        anim = update["anim"]
        expected = update["expected"]
        current = len(anim.sprites)
        delta = expected - current
        if delta == 0:
            continue
        if delta > 0:
            changed.append((anim.anim_name.lower(), True, add_sprites(delta, anim.sprites[-1][1].path_id, anim.sprites[-1][2] + 1, anim.sprite_name_format)))
        else:
            changed.append((anim.anim_name.lower(), False, remove_sprites(-delta, anim.sprites)))

    bundle_data_info.SetNewData(bundle_data)
    bundle.file.BlockAndDirInfo.DirectoryInfos[0].SetNewData(bundle_file)
    writer = AssetsFileWriter(os.path.abspath(temp_file))
    bundle.file.Write(writer)
    writer.Close()
    manager.UnloadAll()
    return changed


def build_pointer_array(existing: List[dict], path_ids: List[int]) -> List[dict]:
    template = dict(existing[-1]) if existing else {"m_FileID": 0, "m_PathID": 0}
    result = []
    for path_id in path_ids:
        clone = dict(template)
        clone["m_PathID"] = int(path_id)
        result.append(clone)
    return result


def infer_frame_desired_sprite_name(frame: dict) -> str:
    frame_meta = (frame or {}).get("frame_meta") or {}
    desired_name = (
        frame_meta.get("origName")
        or frame_meta.get("spriteName")
        or os.path.splitext((frame or {}).get("file") or "")[0]
    )
    return sanitize_name(desired_name or "")


def get_sprite_slot_index(sprite_obj) -> int:
    obj_name = sprite_obj.peek_name() or ""
    sep_at = obj_name.rfind("_")
    if sep_at != -1:
        try:
            return int(obj_name[sep_at + 1:])
        except Exception:
            return -1
    return -1


def resolve_sprite_slots_for_frames(sprite_slots: List[tuple], target_frames: List[dict], all_sprites_by_texture) -> List[tuple]:
    used_path_ids = set()
    resolved_slots = []

    for index, slot in enumerate(sprite_slots):
        _, sprite_obj, _ = slot
        texture_pid = to_int(cached_parse_as_dict(sprite_obj).get("m_RD", {}).get("texture", {}).get("m_PathID"), 0)
        desired_name = infer_frame_desired_sprite_name(target_frames[index]) if index < len(target_frames) else ""
        candidates = list(all_sprites_by_texture.get(texture_pid, []))
        chosen = None

        if desired_name:
            for candidate in candidates:
                if candidate.path_id in used_path_ids:
                    continue
                if sanitize_name(candidate.peek_name() or "") == desired_name:
                    chosen = candidate
                    break

        if chosen is None and sprite_obj.path_id not in used_path_ids:
            chosen = sprite_obj

        if chosen is None:
            for candidate in candidates:
                if candidate.path_id not in used_path_ids:
                    chosen = candidate
                    break

        if chosen is None:
            chosen = sprite_obj

        used_path_ids.add(chosen.path_id)
        resolved_slots.append((chosen.peek_name() or f"sprite_{chosen.path_id}", chosen, get_sprite_slot_index(chosen)))

    return resolved_slots


def ensure_streamed_clip_data(anim_data: dict, keyframes: List[dict], sample_rate: float):
    target_count = len(keyframes)
    existing = anim_data.get("m_MuscleClip", {}).get("m_Clip", {}).get("data", {}).get("m_StreamedClip", {}).get("data", []) or []
    header0 = to_int(existing[0], 4286578687) if len(existing) >= 1 else 4286578687
    header1 = to_int(existing[1], 1) if len(existing) >= 2 else 1
    data = [0] * (target_count * 7 + 2)
    if len(data) >= 2:
        data[0] = header0
        data[1] = header1
    for index in range(max(0, target_count)):
        base = 2 + index * 7
        data[base + 4] = float_to_u32(index)
        if index < target_count - 1:
            next_time = to_float(keyframes[index + 1].get("time"), (index + 1) / sample_rate if sample_rate > 0 else index + 1)
            data[base + 5] = float_to_u32(next_time)
            data[base + 6] = 1
        else:
            data[base + 5] = float_to_u32(math.inf)
            data[base + 6] = 0
    anim_data["m_MuscleClip"]["m_Clip"]["data"]["m_StreamedClip"]["data"] = data


def resolve_clip_frames(meta: dict, clip_dir: str, keep_original_timeline: bool) -> List[dict]:
    """把 clip.json 中的帧定义解析成实际导入计划。"""
    keyframes = [clone_json_value(entry) for entry in (meta.get("keyframes") or [])]
    frames = [clone_json_value(frame) for frame in (meta.get("frames") or [])]

    if not keyframes and frames:
        frame_duration = derive_frame_duration(meta, frames, keyframes)
        keyframes = [
            {
                "index": idx,
                "time": frame_duration * idx,
                "file": frame.get("file") or "",
            }
            for idx, frame in enumerate(frames)
        ]

    frame_by_file = {frame.get("file") or "": frame for frame in frames if frame.get("file")}
    resolved_frames = []
    for idx, keyframe in enumerate(keyframes):
        file_name = keyframe.get("file") or ""
        if not file_name and idx < len(frames):
            file_name = frames[idx].get("file") or ""
        image_path = os.path.join(clip_dir, file_name) if file_name else ""
        if not image_path or not os.path.isfile(image_path):
            raise RuntimeError(f"[IMPORT-CLIPS] 缺少帧图片: {os.path.join(clip_dir, file_name) if file_name else clip_dir}")

        frame_meta = frame_by_file.get(file_name)
        if frame_meta is None and idx < len(frames):
            frame_meta = frames[idx]

        resolved_frames.append({
            "index": idx,
            "time": to_float(keyframe.get("time"), idx),
            "endTime": to_float(keyframe.get("endTime"), to_float(keyframe.get("time"), idx)),
            "file": file_name,
            "image_path": image_path,
            "frame_meta": frame_meta or {},
        })

    if not keep_original_timeline:
        resolved_frames = maybe_append_loop_closing_frame(meta, resolved_frames, len(keyframes))
        resolved_frames = maybe_rewrite_last_frame_to_first(meta, resolved_frames)

    return resolved_frames


def validate_preserve_timeline_meta(clip_dir: str, meta: dict, animation: SpriteAnimation):
    """校验严格模式前置条件，避免在不兼容输入上强行回填。"""
    timeline = meta.get("sourceTimeline")
    if not isinstance(timeline, dict):
        raise RuntimeError(f"[IMPORT-CLIPS] {clip_dir} 的 clip.json 不包含 sourceTimeline，无法执行完整时间轴回填")

    source_slot_count = to_int(timeline.get("slotCount"), 0)
    source_unique_frame_count = to_int(timeline.get("uniqueFrameCount"), len(meta.get("frames") or []))
    actual_png_count = len(list_clip_png_files(clip_dir))
    keyframes = meta.get("keyframes") or []
    clip_display_name = meta.get("clipName") or os.path.basename(clip_dir)

    if source_slot_count <= 0:
        raise RuntimeError(f"[IMPORT-CLIPS] {clip_dir} 的 sourceTimeline.slotCount 无效，无法执行完整时间轴回填")
    if len(animation.sprites) != source_slot_count:
        raise RuntimeError(
            f"[IMPORT-CLIPS] 完整时间轴回填要求源 AB 动画槽位数保持不变: {clip_display_name}，"
            f"源导出 {source_slot_count}，当前包内 {len(animation.sprites)}。如需增减帧，请改用默认 import-clips。"
        )
    if actual_png_count != source_unique_frame_count:
        raise RuntimeError(
            f"[IMPORT-CLIPS] 完整时间轴回填要求 PNG 数量与导出时一致: {clip_display_name}，"
            f"导出时 {source_unique_frame_count}，当前目录 {actual_png_count}。如需增减帧，请改用默认 import-clips。"
        )
    if len(keyframes) != source_slot_count:
        raise RuntimeError(
            f"[IMPORT-CLIPS] 完整时间轴回填要求 clip.json 的 keyframes 数量与源动画一致: {clip_display_name}，"
            f"导出时 {source_slot_count}，当前 clip.json 为 {len(keyframes)}。如需增减帧，请改用默认 import-clips。"
        )


def align_up(value: int, alignment: int) -> int:
    if alignment <= 0:
        return value
    remainder = value % alignment
    return value if remainder == 0 else value + (alignment - remainder)


def get_sprite_uv_offset(vertex_count: int) -> int:
    return align_up(max(0, vertex_count) * 3 * 4, 16)


def pack_sprite_vertex_streams(positions: List[Tuple[float, float, float]], uvs: List[Tuple[float, float]]) -> bytes:
    raw = bytearray()
    for x, y, z in positions:
        raw.extend(struct.pack("<fff", float(x), float(y), float(z)))

    uv_offset = get_sprite_uv_offset(len(positions))
    if len(raw) < uv_offset:
        raw.extend(b"\x00" * (uv_offset - len(raw)))

    for u, v in uvs:
        raw.extend(struct.pack("<ff", float(u), float(v)))
    return bytes(raw)


def remove_duplicate_points(points: List[Tuple[float, float]], tol: float = 1e-4) -> List[Tuple[float, float]]:
    result: List[Tuple[float, float]] = []
    for x, y in points:
        if result and abs(result[-1][0] - x) <= tol and abs(result[-1][1] - y) <= tol:
            continue
        result.append((float(x), float(y)))
    if len(result) > 1 and abs(result[0][0] - result[-1][0]) <= tol and abs(result[0][1] - result[-1][1]) <= tol:
        result.pop()
    return result


def rdp_simplify(points: List[Tuple[float, float]], epsilon: float) -> List[Tuple[float, float]]:
    if len(points) <= 2:
        return list(points)

    x1, y1 = points[0]
    x2, y2 = points[-1]
    dx = x2 - x1
    dy = y2 - y1
    denom = dx * dx + dy * dy

    max_dist = -1.0
    split_index = -1
    for index in range(1, len(points) - 1):
        px, py = points[index]
        if denom <= 1e-8:
            dist = math.hypot(px - x1, py - y1)
        else:
            t = ((px - x1) * dx + (py - y1) * dy) / denom
            proj_x = x1 + t * dx
            proj_y = y1 + t * dy
            dist = math.hypot(px - proj_x, py - proj_y)
        if dist > max_dist:
            max_dist = dist
            split_index = index

    if max_dist <= epsilon or split_index <= 0:
        return [points[0], points[-1]]

    left = rdp_simplify(points[:split_index + 1], epsilon)
    right = rdp_simplify(points[split_index:], epsilon)
    return left[:-1] + right


def polygon_signed_area(points: List[Tuple[float, float]]) -> float:
    area = 0.0
    count = len(points)
    for index in range(count):
        x1, y1 = points[index]
        x2, y2 = points[(index + 1) % count]
        area += x1 * y2 - x2 * y1
    return area * 0.5


def cross2(a: Tuple[float, float], b: Tuple[float, float], c: Tuple[float, float]) -> float:
    return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])


def point_in_triangle(point: Tuple[float, float], a: Tuple[float, float], b: Tuple[float, float], c: Tuple[float, float]) -> bool:
    c1 = cross2(point, a, b)
    c2 = cross2(point, b, c)
    c3 = cross2(point, c, a)
    has_neg = (c1 < -1e-6) or (c2 < -1e-6) or (c3 < -1e-6)
    has_pos = (c1 > 1e-6) or (c2 > 1e-6) or (c3 > 1e-6)
    return not (has_neg and has_pos)


def triangulate_polygon(points: List[Tuple[float, float]]) -> List[int]:
    count = len(points)
    if count < 3:
        return []

    order = list(range(count))
    if polygon_signed_area(points) < 0:
        order.reverse()

    triangles: List[int] = []
    guard = 0
    while len(order) > 3 and guard < count * count:
        ear_found = False
        for idx in range(len(order)):
            prev_index = order[(idx - 1) % len(order)]
            curr_index = order[idx]
            next_index = order[(idx + 1) % len(order)]
            a = points[prev_index]
            b = points[curr_index]
            c = points[next_index]
            if cross2(a, b, c) <= 1e-6:
                continue
            blocked = False
            for other in order:
                if other in (prev_index, curr_index, next_index):
                    continue
                if point_in_triangle(points[other], a, b, c):
                    blocked = True
                    break
            if blocked:
                continue
            triangles.extend([prev_index, curr_index, next_index])
            del order[idx]
            ear_found = True
            break
        if not ear_found:
            break
        guard += 1

    if len(order) == 3:
        triangles.extend(order)
    return triangles


def alpha_row_bounds(image: Image.Image, alpha_threshold: int = 1) -> List[Tuple[float, float, float]]:
    alpha = image.getchannel("A")
    width, height = image.size
    rows: List[Tuple[float, float, float]] = []
    for y_top in range(height):
        left = None
        right = None
        for x in range(width):
            if alpha.getpixel((x, y_top)) >= alpha_threshold:
                left = x
                break
        if left is None:
            continue
        for x in range(width - 1, -1, -1):
            if alpha.getpixel((x, y_top)) >= alpha_threshold:
                right = x
                break
        if right is None:
            continue
        y_center = height - (y_top + 0.5)
        rows.append((float(y_center), float(left) + 0.5, float(right) + 0.5))
    return rows


def build_tight_mesh_polygon(image: Image.Image) -> List[Tuple[float, float]]:
    rows = alpha_row_bounds(image)
    width, height = image.size
    if not rows:
        return [(0.0, 0.0), (float(width), 0.0), (float(width), float(height)), (0.0, float(height))]

    right_chain = [(right, y_center) for y_center, _, right in rows]
    left_chain = [(left, y_center) for y_center, left, _ in rows]

    polygon: List[Tuple[float, float]] = []
    for epsilon in (1.0, 2.0, 3.0, 4.0, 6.0, 8.0):
        simplified_right = remove_duplicate_points(rdp_simplify(right_chain, epsilon))
        simplified_left = remove_duplicate_points(rdp_simplify(left_chain, epsilon))
        candidate = remove_duplicate_points(simplified_right + list(reversed(simplified_left)))
        if len(candidate) < 4:
            continue
        polygon = candidate
        if len(candidate) <= 11:
            break

    if len(polygon) < 4:
        left = min(bound_left for _, bound_left, _ in rows)
        right = max(bound_right for _, _, bound_right in rows) + 1.0
        top = rows[0][0] + 0.5
        bottom = rows[-1][0] - 0.5
        polygon = [(left, top), (right, top), (right, bottom), (left, bottom)]

    return polygon


def infer_mod_atlas_cell_size(texture_plan: dict, target_frames: List[dict]) -> Tuple[int, int]:
    widths = []
    heights = []
    for entry in target_frames:
        frame_meta = entry.get("frame_meta") or {}
        source_sprite = frame_meta.get("sourceSprite") or {}
        source_rect = source_sprite.get("rect") or {}
        source_rect_w = max(0, round_int(source_rect.get("width", 0))) if isinstance(source_rect, dict) else 0
        source_rect_h = max(0, round_int(source_rect.get("height", 0))) if isinstance(source_rect, dict) else 0
        if source_rect_w > 0 and source_rect_h > 0:
            widths.append(source_rect_w)
            heights.append(source_rect_h)
            continue
        try:
            sprite_data = cached_parse_as_dict(entry["sprite_obj"])
        except Exception:
            continue
        rect = sprite_data.get("m_Rect") or {}
        widths.append(max(1, round_int(rect.get("width", 0))))
        heights.append(max(1, round_int(rect.get("height", 0))))

    fallback_sizes = []
    for entry in target_frames:
        try:
            with Image.open(entry["image_path"]) as replacement_image:
                fallback_sizes.append(replacement_image.size)
        except Exception:
            continue

    if fallback_sizes:
        max_image_w = max(width for width, _ in fallback_sizes)
        max_image_h = max(height for _, height in fallback_sizes)
        fallback_w = int(math.ceil(max_image_w / 32.0) * 32)
        fallback_h = int(math.ceil(max_image_h / 16.0) * 16)
    else:
        fallback_w = 224
        fallback_h = 304

    if widths and heights:
        existing_w = max(widths)
        existing_h = max(heights)
        if existing_w <= fallback_w * 1.5 and existing_h <= fallback_h * 1.5:
            return existing_w, existing_h

    return fallback_w, fallback_h


def infer_mod_atlas_columns(texture_data, cell_w: int, cell_h: int, frame_count: int) -> int:
    max_texture_size = max(4096, to_int(os.environ.get("RML_MAX_ATLAS_SIZE"), 16384))
    existing_width = max(0, round_int(getattr(texture_data, "m_Width", 0)))
    max_cols = max(1, max_texture_size // max(1, cell_w))
    max_rows = max(1, max_texture_size // max(1, cell_h))

    if cell_w > 0 and existing_width > 0 and existing_width % cell_w == 0:
        existing_cols = max(1, existing_width // cell_w)
        existing_rows = int(math.ceil(frame_count / float(existing_cols))) if existing_cols > 0 else frame_count
        if existing_cols <= max(1, frame_count) and existing_cols <= max_cols and existing_rows <= max_rows:
            return existing_cols

    if frame_count <= 0:
        return 1

    if max_cols <= 1:
        return 1

    min_required_cols = max(1, int(math.ceil(frame_count / float(max_rows))))
    preferred = 4 if frame_count >= 4 else frame_count
    cols = max(min_required_cols, preferred)
    cols = min(max_cols, max(1, cols))
    while cols < max_cols and math.ceil(frame_count / float(cols)) * cell_h > max_texture_size:
        cols += 1
    return max(1, min(cols, frame_count))


def get_existing_cell_rect(sprite_data: dict, cell_w: int, cell_h: int, index: int, cols: int, rows: int, prefer_existing: bool = True) -> Dict[str, float]:
    if prefer_existing:
        rect = sprite_data.get("m_Rect") or {}
        width = max(1, round_int(rect.get("width", cell_w)))
        height = max(1, round_int(rect.get("height", cell_h)))
        x = round_int(rect.get("x", 0))
        y = round_int(rect.get("y", 0))
        if width > 0 and height > 0:
            return {"x": float(x), "y": float(y), "width": float(width), "height": float(height)}

    col = index % cols
    row_from_top = index // cols
    return {
        "x": float(col * cell_w),
        "y": float((rows - 1 - row_from_top) * cell_h),
        "width": float(cell_w),
        "height": float(cell_h),
    }


def choose_texture_rect_offset(sprite_data: dict, cell_rect: dict, image_size: Tuple[int, int], frame_meta: dict = None) -> Tuple[float, float]:
    image_w, image_h = image_size
    frame_meta = frame_meta or {}
    source_sprite = frame_meta.get("sourceSprite") or {}
    source_rd = source_sprite.get("rd") or {}
    source_texture_rect = source_rd.get("textureRect") or {}
    source_rect_offset = source_rd.get("textureRectOffset") or {}
    if isinstance(source_texture_rect, dict) and source_texture_rect:
        source_w = round_int(source_texture_rect.get("width", 0))
        source_h = round_int(source_texture_rect.get("height", 0))
        if source_w == image_w and source_h == image_h and isinstance(source_rect_offset, dict):
            return (
                float(to_float(source_rect_offset.get("x"), 0.0)),
                float(to_float(source_rect_offset.get("y"), 0.0)),
            )

    rd = sprite_data.get("m_RD") or {}
    texture_rect = rd.get("textureRect") or {}
    cell_w = max(1, round_int(cell_rect.get("width", image_w)))
    cell_h = max(1, round_int(cell_rect.get("height", image_h)))
    if texture_rect:
        cell_x = to_float(cell_rect.get("x"), 0.0)
        cell_y = to_float(cell_rect.get("y"), 0.0)
        old_w = round_int(texture_rect.get("width", 0))
        old_h = round_int(texture_rect.get("height", 0))
        if old_w == image_w and old_h == image_h:
            offset_x = to_float(texture_rect.get("x"), cell_x) - cell_x
            offset_y = to_float(texture_rect.get("y"), cell_y) - cell_y
            if 0.0 <= offset_x <= max(0.0, cell_w - image_w) and 0.0 <= offset_y <= max(0.0, cell_h - image_h):
                return (offset_x, offset_y)

    offset_x = max(0.0, min(float(cell_w - image_w), math.floor((cell_w - image_w) / 2.0)))
    offset_y = max(0.0, min(float(cell_h - image_h), cell_h - image_h - 10.0))
    return float(offset_x), float(offset_y)


def rebuild_sprite_as_mod_cell(sprite_data: dict,
                               atlas_size: Tuple[int, int],
                               cell_rect: dict,
                               replacement_image: Image.Image,
                               pixels_per_unit: float,
                               pivot01,
                               border=None,
                               frame_meta: dict = None,
                               force_rect_mesh: bool = False):
    """普通模式下把 Sprite 重建到新的图集单元中。"""
    atlas_w, atlas_h = atlas_size
    cell_x = to_float(cell_rect.get("x"), 0.0)
    cell_y = to_float(cell_rect.get("y"), 0.0)
    cell_w = max(1, round_int(cell_rect.get("width", replacement_image.width)))
    cell_h = max(1, round_int(cell_rect.get("height", replacement_image.height)))
    pivot = normalize_vec2_list(pivot01, [0.5, 0.0])
    sprite_border = normalize_vec4_list(border, [0.0, 0.0, 0.0, 0.0])

    offset_x, offset_y = choose_texture_rect_offset(sprite_data, cell_rect, replacement_image.size, frame_meta)
    texture_rect = {
        "x": float(cell_x + offset_x),
        "y": float(cell_y + offset_y),
        "width": float(replacement_image.width),
        "height": float(replacement_image.height),
    }

    if force_rect_mesh:
        polygon = [
            (0.0, 0.0),
            (float(replacement_image.width), 0.0),
            (float(replacement_image.width), float(replacement_image.height)),
            (0.0, float(replacement_image.height)),
        ]
        triangles = [0, 1, 2, 0, 2, 3]
    else:
        polygon = build_tight_mesh_polygon(replacement_image)
        if len(polygon) < 3:
            polygon = [
                (0.0, 0.0),
                (float(replacement_image.width), 0.0),
                (float(replacement_image.width), float(replacement_image.height)),
                (0.0, float(replacement_image.height)),
            ]

        triangles = triangulate_polygon(polygon)
        if len(triangles) < 3:
            polygon = [
                (0.0, 0.0),
                (float(replacement_image.width), 0.0),
                (float(replacement_image.width), float(replacement_image.height)),
                (0.0, float(replacement_image.height)),
            ]
            triangles = [0, 1, 2, 0, 2, 3]

    if len(triangles) < 3:
        polygon = [
            (0.0, 0.0),
            (float(replacement_image.width), 0.0),
            (float(replacement_image.width), float(replacement_image.height)),
            (0.0, float(replacement_image.height)),
        ]
        triangles = [0, 1, 2, 0, 2, 3]

    ppu = max(1e-6, to_float(pixels_per_unit, 100.0))
    pivot_px_x = cell_w * to_float(pivot[0], 0.5)
    pivot_px_y = cell_h * to_float(pivot[1], 0.0)
    atlas_origin_x = texture_rect["x"]
    atlas_origin_y = texture_rect["y"]

    positions = []
    uvs = []
    for px, py in polygon:
        atlas_px_x = atlas_origin_x + px
        atlas_px_y = atlas_origin_y + py
        positions.append(((atlas_px_x - cell_x - pivot_px_x) / ppu, (atlas_px_y - cell_y - pivot_px_y) / ppu, 0.0))
        uvs.append((atlas_px_x / float(atlas_w), atlas_px_y / float(atlas_h)))

    raw = pack_sprite_vertex_streams(positions, uvs)

    index_bytes = struct.pack("<" + "H" * len(triangles), *triangles)
    min_x = min(pos[0] for pos in positions)
    max_x = max(pos[0] for pos in positions)
    min_y = min(pos[1] for pos in positions)
    max_y = max(pos[1] for pos in positions)

    rd = sprite_data["m_RD"]
    vertex_data = rd["m_VertexData"]
    vertex_data["m_VertexCount"] = len(positions)
    vertex_data["m_DataSize"] = raw
    rd["m_IndexBuffer"] = index_bytes
    if rd.get("m_SubMeshes"):
        rd["m_SubMeshes"][0]["firstByte"] = 0
        rd["m_SubMeshes"][0]["indexCount"] = len(triangles)
        rd["m_SubMeshes"][0]["topology"] = 0
        rd["m_SubMeshes"][0]["baseVertex"] = 0
        rd["m_SubMeshes"][0]["firstVertex"] = 0
        rd["m_SubMeshes"][0]["vertexCount"] = len(positions)
        rd["m_SubMeshes"][0]["localAABB"] = {
            "m_Center": {"x": float((min_x + max_x) * 0.5), "y": float((min_y + max_y) * 0.5), "z": 0.0},
            "m_Extent": {"x": float((max_x - min_x) * 0.5), "y": float((max_y - min_y) * 0.5), "z": 0.0},
        }

    rd["textureRect"] = dict(texture_rect)
    if "textureRectOffset" in rd:
        rd["textureRectOffset"] = {"x": float(offset_x), "y": float(offset_y)}
    if "atlasRectOffset" in rd:
        rd["atlasRectOffset"] = {"x": -1.0, "y": -1.0}
    sprite_data["m_IsPolygon"] = False
    sprite_data["m_Pivot"] = {"x": float(pivot[0]), "y": float(pivot[1])}
    sprite_data["m_Border"] = {
        "x": float(sprite_border[0]),
        "y": float(sprite_border[1]),
        "z": float(sprite_border[2]),
        "w": float(sprite_border[3]),
    }
    sprite_data["m_Rect"] = {
        "x": float(cell_x),
        "y": float(cell_y),
        "width": float(cell_w),
        "height": float(cell_h),
    }
    sprite_data["m_Offset"] = {"x": 0.0, "y": float(-cell_h / 2.0)}

    return texture_rect


def scan_bundle(bundle_path: str):
    """输出 bundle 内对象类型统计，便于排查资源结构。"""
    env = UnityPy.load(bundle_path)
    counts = defaultdict(int)
    for obj in env.objects:
        counts[obj.type.name] += 1
    print("[SCAN] objects by type:")
    for k in sorted(counts): print(f"  {k:16s} : {counts[k]}")
    print(f"[SCAN] total objects: {sum(counts.values())}")

# ---------------------------------------------------------------------------
# 导出命令与直接 Sprite 回填命令
# ---------------------------------------------------------------------------

def export_sprites(bundle_path: str, out_dir: str, all_sprites: bool, unique_names: bool, group_by_texture: bool):
    """导出动画引用到的 Sprite 帧，以及严格模式所需的 clip 元数据。"""
    emit_progress_setup(2, f"开始导出 {os.path.basename(bundle_path)}")
    env = UnityPy.load(bundle_path)
    emit_progress_step("已加载资源包")

    sprites_by_pid = {}
    textures_by_pid = {}
    clip_data = gather_clip_data(env)
    emit_progress_step(f"已扫描动画与精灵，动画数 {len(clip_data)}")
    for obj in env.objects:
        if obj.type.name == "Sprite":
            try: sprites_by_pid[obj.path_id] = obj.read()
            except: pass
        elif obj.type.name == "Texture2D":
            try: textures_by_pid[obj.path_id] = obj.read()
            except: pass

    total_export = 0

    if not all_sprites and clip_data:
        emit_progress_setup(max(1, len(clip_data)), f"准备导出 {len(clip_data)} 个动画")
        for clip_dir_name, meta in clip_data.items():
            pid_set = meta["spritePathIds"]
            clip_dir = os.path.join(out_dir, clip_dir_name); ensure_dir(clip_dir)
            file_by_pid = {frame["origPathID"]: frame["file"] for frame in meta.get("frames", [])}
            exported = set()
            for pid in pid_set:
                if pid in exported:
                    continue
                exported.add(pid)
                spr = sprites_by_pid.get(pid)
                if not spr: continue
                img = spr.image
                fname = file_by_pid.get(pid)
                if not fname:
                    sp_name = sanitize_name(spr.m_Name or f"sprite_{pid}")
                    fname = f"{sp_name}.png"
                img.save(os.path.join(clip_dir, fname)); total_export += 1
            with open(os.path.join(clip_dir, "clip.json"), "w", encoding="utf-8") as f:
                json.dump(meta, f, ensure_ascii=False, indent=2)
            emit_progress_step(f"已导出动画 {meta.get('clipName') or clip_dir_name}")
        emit_progress_done(f"导出完成，共 {len(clip_data)} 个动画，{total_export} 张 sprite")
        print(f"[EXPORT] {bundle_path} → {len(clip_data)} 个动画，共导出 {total_export} 张 sprite 到 {out_dir}")
        return

    groups = defaultdict(list)
    for pid, spr in sprites_by_pid.items():
        key = "ALL"
        if group_by_texture:
            try:
                tex_ptr = spr.m_RD.texture
                tex = textures_by_pid.get(tex_ptr.path_id) if tex_ptr else None
                key = sanitize_name(tex.m_Name) if tex and tex.m_Name else "ALL"
            except:
                key = "ALL"
        groups[key].append((pid, spr))

    emit_progress_setup(max(1, len(groups)), f"准备导出全部精灵，共 {sum(len(items) for items in groups.values())} 张")
    for key, items in groups.items():
        base = os.path.join(out_dir, "ALL", key) if group_by_texture else os.path.join(out_dir, "ALL")
        ensure_dir(base)
        for pid, spr in items:
            try:
                img = spr.image
                sp_name = sanitize_name(spr.m_Name or f"sprite_{pid}")
                fname = f"{sp_name}__pid{pid}.png" if unique_names else f"{sp_name}.png"
                img.save(os.path.join(base, fname)); total_export += 1
            except Exception as e:
                print(f"[WARN] 导出 {spr.m_Name} 失败：{e}")
        emit_progress_step(f"已导出分组 {key}")
    emit_progress_done(f"导出全部精灵完成，共 {total_export} 张")
    print(f"[EXPORT-ALL] {bundle_path} → 导出全部 Sprite 共 {total_export} 张 到 {out_dir}")

def get_sprite_mesh(spr) -> Tuple[List[Tuple[float,float,float]], List[Tuple[float,float]], List[int]]:
    vd = spr.m_RD.m_VertexData
    vcount = getattr(vd, "m_VertexCount", 0)
    raw = None
    for attr in ("_typelessdata", "m_DataSize", "m_Data"):
        if hasattr(vd, attr):
            raw = bytes_from_maybe_array(getattr(vd, attr))
            if raw: break
    if not raw or vcount <= 0: return [], [], []
    pos_bytes = vcount * 3 * 4
    uv_bytes = vcount * 2 * 4
    uv_first = False
    if len(raw) < pos_bytes + uv_bytes:
        if len(raw) >= uv_bytes + pos_bytes: uv_first = True
        else: return [], [], []
    floats = lambda b: struct.unpack("<" + "f" * (len(b) // 4), b)
    if not uv_first:
        pos_f = floats(raw[:pos_bytes]); uv_f = floats(raw[pos_bytes:pos_bytes + uv_bytes])
    else:
        uv_f = floats(raw[:uv_bytes]); pos_f = floats(raw[uv_bytes:uv_bytes + pos_bytes])
    positions = [(pos_f[i*3+0], pos_f[i*3+1], pos_f[i*3+2]) for i in range(vcount)]
    uvs = [(uv_f[i*2+0], uv_f[i*2+1]) for i in range(vcount)]
    ib_raw = bytes_from_maybe_array(spr.m_RD.m_IndexBuffer)
    idx = [int.from_bytes(ib_raw[i:i+2], "little") for i in range(0, len(ib_raw), 2)]
    return positions, uvs, idx

def sample_bilinear(img: Image.Image, x: float, y: float):
    w, h = img.size
    if w == 0 or h == 0: return (0,0,0,0)
    x = max(0.0, min(x, w - 1.001)); y = max(0.0, min(y, h - 1.001))
    x0 = int(math.floor(x)); x1 = min(x0 + 1, w - 1)
    y0 = int(math.floor(y)); y1 = min(y0 + 1, h - 1)
    fx = x - x0; fy = y - y0
    c00 = img.getpixel((x0, y0)); c10 = img.getpixel((x1, y0))
    c01 = img.getpixel((x0, y1)); c11 = img.getpixel((x1, y1))
    def lerp(a,b,t): return a + (b - a) * t
    rgba = [int(round(lerp(lerp(c00[ch], c10[ch], fx), lerp(c01[ch], c11[ch], fx), fy))) for ch in range(4)]
    return tuple(rgba)

def alpha_over(dst, src):
    sr, sg, sb, sa = src; dr, dg, db, da = dst
    if sa == 0: return dst
    inv = 255 - sa
    r = (sr * 255 + dr * inv) // 255
    g = (sg * 255 + dg * inv) // 255
    b = (sb * 255 + db * inv) // 255
    a = sa + (da * inv) // 255
    return (r, g, b, a)

def rasterize_tri(base_img: Image.Image,
                  dest_tri: List[Tuple[float,float]],
                  src_img: Image.Image,
                  src_tri: List[Tuple[float,float]]):
    (x0, y0), (x1, y1), (x2, y2) = dest_tri
    (u0, v0), (u1, v1), (u2, v2) = src_tri

    def edge(ax, ay, bx, by, px, py): return (px - ax)*(by - ay) - (py - ay)*(bx - ax)

    area = edge(x0, y0, x1, y1, x2, y2)
    if area == 0: return
    minx = max(0, int(math.floor(min(x0, x1, x2))))
    maxx = min(base_img.width - 1, int(math.ceil(max(x0, x1, x2))))
    miny = max(0, int(math.floor(min(y0, y1, y2))))
    maxy = min(base_img.height - 1, int(math.ceil(max(y0, y1, y2))))

    pix = base_img.load()
    for iy in range(miny, maxy + 1):
        for ix in range(minx, maxx + 1):
            px = ix + 0.5; py = iy + 0.5
            w0 = edge(x1, y1, x2, y2, px, py)
            w1 = edge(x2, y2, x0, y0, px, py)
            w2 = edge(x0, y0, x1, y1, px, py)
            if (area > 0 and (w0 < 0 or w1 < 0 or w2 < 0)) or (area < 0 and (w0 > 0 or w1 > 0 or w2 > 0)):
                continue
            lam0 = w0 / area; lam1 = w1 / area; lam2 = w2 / area
            su = lam0*u0 + lam1*u1 + lam2*u2
            sv = lam0*v0 + lam1*v1 + lam2*v2
            src_rgba = sample_bilinear(src_img, su, sv)
            if src_rgba[3] == 0: continue
            pix[ix, iy] = alpha_over(pix[ix, iy], src_rgba)

def paste_sprite_rect(tex_img: Image.Image, spr, repl_img: Image.Image):
    tr = spr.m_RD.textureRect if getattr(spr, "m_RD", None) else spr.m_Rect
    x = round_int(tr.x); y = round_int(tr.y); w = round_int(tr.width); h = round_int(tr.height)
    if w <= 0 or h <= 0: return False
    H = tex_img.height; top = H - (y + h); left = x
    left = max(0, min(left, tex_img.width - w)); top = max(0, min(top, tex_img.height - h))
    if repl_img.size != (w, h): repl_img = repl_img.resize((w, h), resample=Image.LANCZOS)
    tex_img.alpha_composite(repl_img.convert("RGBA"), (left, top)); return True

def paste_sprite_mesh(tex_img: Image.Image, spr, repl_img: Image.Image):
    positions, uvs, indices = get_sprite_mesh(spr)
    if not uvs or not indices: return False
    tr = spr.m_RD.textureRect if getattr(spr, "m_RD", None) else spr.m_Rect
    rx, ry = float(tr.x), float(tr.y)
    rw, rh = max(1.0, float(tr.width)), max(1.0, float(tr.height))
    if repl_img.size != (int(round(rw)), int(round(rh))):
        repl_img = repl_img.resize((int(round(rw)), int(round(rh))), resample=Image.LANCZOS)
    W, H = tex_img.width, tex_img.height
    dest_pts: List[Tuple[float, float]] = []
    src_pts: List[Tuple[float, float]] = []
    for (u, v) in uvs:
        dx = u * W; dy = H - v * H; dest_pts.append((dx, dy))
        px = u * W - rx; py_bottom = v * H - ry
        sx = max(0.0, min(px, rw - 1.0)); sy = max(0.0, min(rh - py_bottom, rh - 1.0))
        src_pts.append((sx, sy))
    for i in range(0, len(indices), 3):
        i0, i1, i2 = indices[i], indices[i+1], indices[i+2]
        if i0 >= len(dest_pts) or i1 >= len(dest_pts) or i2 >= len(dest_pts): continue
        dtri = [dest_pts[i0], dest_pts[i1], dest_pts[i2]]
        stri = [src_pts[i0], src_pts[i1], src_pts[i2]]
        rasterize_tri(tex_img, dtri, repl_img, stri)
    return True

PID_RE = re.compile(r"__pid(-?\d+)$", re.IGNORECASE)

def collect_replacement_images(images_root: str):
    by_name, by_pid = {}, {}
    for path in glob.glob(os.path.join(images_root, "**", "*.png"), recursive=True):
        base = os.path.splitext(os.path.basename(path))[0]
        m = PID_RE.search(base)
        if m:
            try:
                pid = int(m.group(1)); by_pid[pid] = path
                main = base[:m.start()]
                if main and main not in by_name: by_name[main] = path
                continue
            except: pass
        by_name[base] = path
    return by_name, by_pid

def has_streamed_texture_data(textures) -> bool:
    """判断纹理是否依赖外部流数据，决定保存时是否必须整包重封。"""
    for texture in textures:
        texture_data = texture
        stream_data = getattr(texture_data, "m_StreamData", None)
        if stream_data is None and hasattr(texture, "read"):
            try:
                texture_data = texture.read()
                stream_data = getattr(texture_data, "m_StreamData", None)
            except Exception:
                continue
        if stream_data and getattr(stream_data, "path", ""):
            return True
    return False

def collect_bundle_file_bytes(env) -> Dict[str, bytes]:
    """收集 UnityPy 当前内存态下的 bundle 内部文件字节。"""
    timing_enabled = env_flag("RML_PRINT_TIMING")
    bundle_files = {}
    file_timings: List[Tuple[str, float, int]] = []
    total_collect_start = time.perf_counter() if timing_enabled else 0.0
    for name, value in env.file.files.items():
        file_start = time.perf_counter() if timing_enabled else 0.0
        if hasattr(value, "save"):
            try:
                file_bytes = bytes(value.save())
                bundle_files[name] = file_bytes
                if timing_enabled:
                    file_timings.append((name, time.perf_counter() - file_start, len(file_bytes)))
                continue
            except Exception as ex:
                raise RuntimeError(f"序列化内部文件失败: {name}: {ex}") from ex

        if hasattr(value, "bytes"):
            try:
                file_bytes = bytes(value.bytes)
                bundle_files[name] = file_bytes
                if timing_enabled:
                    file_timings.append((name, time.perf_counter() - file_start, len(file_bytes)))
                continue
            except Exception as ex:
                raise RuntimeError(f"读取内部文件失败: {name}: {ex}") from ex

        raise RuntimeError(f"不支持导出 bundle 内部文件: {name} ({type(value)!r})")

    if timing_enabled:
        total_collect_seconds = time.perf_counter() - total_collect_start
        print(f"[TIMING] collect_bundle_files_seconds={total_collect_seconds:.3f}")
        accounted_seconds = sum(item[1] for item in file_timings)
        for file_name, file_seconds, byte_count in sorted(file_timings, key=lambda item: item[1], reverse=True):
            percent = (file_seconds / accounted_seconds * 100.0) if accounted_seconds > 0 else 0.0
            print(
                f"[TIMING] collect_bundle_file name={file_name} seconds={file_seconds:.3f} "
                f"percent={percent:.2f} bytes={byte_count}"
            )

    return bundle_files


def read_bundle_entry_bytes(bundle_file, index: int) -> bytes:
    long_start, long_length = bundle_file.GetFileRange(index)
    bundle_file.DataReader.Position = long_start
    return bytes(bundle_file.DataReader.ReadBytes(int(long_length)))


def read_asset_object_bytes(asset_file, asset_info):
    """直接返回 .NET Byte[]，避免对象 payload 往返 Python bytes。"""
    start = asset_info.GetAbsoluteByteOffset(asset_file)
    size = asset_info.ByteSize
    asset_file.Reader.Position = int(start)
    return asset_file.Reader.ReadBytes(int(size))


def serialize_assets_file_to_bytes(asset_file, AssetsFileWriter, memory_stream_cls) -> bytes:
    """把 AssetsFile 直接序列化到内存，避免中间临时文件。"""
    memory_stream = memory_stream_cls()
    asset_writer = AssetsFileWriter(memory_stream)
    try:
        asset_file.Write(asset_writer)
        asset_writer.Flush()
        return bytes(memory_stream.ToArray())
    finally:
        asset_writer.Close()
        memory_stream.Close()


def build_object_patches_from_serialized_files(original_bundle, original_assets, incoming_bundle_files: Dict[str, bytes], temp_dir: str):
    """按内部 assets 文件直连 diff，构造最小对象级补丁集合。"""
    assets = ensure_assets_tools()
    incoming_manager = assets["AssetsManager"]()
    Byte = assets["Byte"]
    Enumerable = assets["Enumerable"]
    timing_enabled = env_flag("RML_PRINT_TIMING")
    temp_write_seconds = 0.0
    incoming_load_seconds = 0.0
    lookup_build_seconds = 0.0
    original_read_seconds = 0.0
    incoming_read_seconds = 0.0
    compare_seconds = 0.0
    compared_object_count = 0
    changed_object_count = 0
    compared_object_bytes = 0
    try:
        original_names = original_bundle.file.GetAllFileNames()
        original_entry_index_by_name = {}
        original_asset_index_by_name = {}

        for original_index in range(original_names.Count):
            name = str(original_names[original_index])
            original_entry_index_by_name[name] = original_index
            if original_bundle.file.IsAssetsFile(original_index):
                original_asset_index_by_name[name] = original_index

        patches = []
        for incoming_name, incoming_bytes in incoming_bundle_files.items():
            original_index = original_entry_index_by_name.get(incoming_name)
            if original_index is None:
                raise RuntimeError(f"原始 bundle 中不存在内部文件: {incoming_name}")

            if not original_bundle.file.IsAssetsFile(original_index):
                original_bytes = read_bundle_entry_bytes(original_bundle.file, original_index)
                if len(incoming_bytes) != len(original_bytes) or incoming_bytes != original_bytes:
                    raise RuntimeError(f"非 assets 内部文件发生变化，无法安全增量回写: {incoming_name}")
                continue

            original_asset = original_assets[original_index]
            if original_asset is None:
                continue

            incoming_asset_path = os.path.join(temp_dir, f"incoming_{original_index}.assets")
            write_start = time.perf_counter() if timing_enabled else 0.0
            with open(incoming_asset_path, "wb") as f:
                f.write(incoming_bytes)
            if timing_enabled:
                temp_write_seconds += time.perf_counter() - write_start

            load_start = time.perf_counter() if timing_enabled else 0.0
            incoming_asset = incoming_manager.LoadAssetsFile(os.path.abspath(incoming_asset_path), False)
            if timing_enabled:
                incoming_load_seconds += time.perf_counter() - load_start
            if incoming_asset is None:
                raise RuntimeError(f"加载序列化后的 assets 文件失败: {incoming_name}")

            incoming_assets_file = incoming_asset.file
            incoming_lookup = {}
            lookup_start = time.perf_counter() if timing_enabled else 0.0
            for incoming_file in incoming_assets_file.AssetInfos:
                incoming_lookup[int(incoming_file.PathId)] = incoming_file
            if timing_enabled:
                lookup_build_seconds += time.perf_counter() - lookup_start

            for original_file in original_asset.file.AssetInfos:
                original_path_id = int(original_file.PathId)
                incoming_file = incoming_lookup.get(original_path_id)
                if incoming_file is None:
                    continue

                # 两边都保持为 .NET Byte[]，把大对象比较留在 CLR 侧完成。
                original_read_start = time.perf_counter() if timing_enabled else 0.0
                original_object_bytes = read_asset_object_bytes(original_asset.file, original_file)
                if timing_enabled:
                    original_read_seconds += time.perf_counter() - original_read_start

                incoming_read_start = time.perf_counter() if timing_enabled else 0.0
                incoming_object_bytes = read_asset_object_bytes(incoming_assets_file, incoming_file)
                if timing_enabled:
                    incoming_read_seconds += time.perf_counter() - incoming_read_start
                    compared_object_count += 1
                    compared_object_bytes += int(original_object_bytes.Length)

                compare_start = time.perf_counter() if timing_enabled else 0.0
                is_equal = Enumerable.SequenceEqual[Byte](original_object_bytes, incoming_object_bytes)
                if timing_enabled:
                    compare_seconds += time.perf_counter() - compare_start

                if not is_equal:
                    if timing_enabled:
                        changed_object_count += 1
                    patches.append((original_index, original_path_id, incoming_object_bytes))

        if timing_enabled:
            print(f"[TIMING] patch_build_temp_write_seconds={temp_write_seconds:.3f}")
            print(f"[TIMING] patch_build_incoming_load_seconds={incoming_load_seconds:.3f}")
            print(f"[TIMING] patch_build_lookup_seconds={lookup_build_seconds:.3f}")
            print(f"[TIMING] patch_build_original_read_seconds={original_read_seconds:.3f}")
            print(f"[TIMING] patch_build_incoming_read_seconds={incoming_read_seconds:.3f}")
            print(
                f"[TIMING] patch_build_compare_seconds={compare_seconds:.3f} "
                f"objects={compared_object_count} changed={changed_object_count} bytes={compared_object_bytes}"
            )

        return patches
    finally:
        incoming_manager.UnloadAll()


def patch_bundle_objects_with_assetstools(bundle_path: str, incoming_bundle_files: Dict[str, bytes], out_path: str, done_label: str = "[DONE]"):
    """把 UnityPy 产生的对象差异安全地回写到原始 bundle。"""
    assets = ensure_assets_tools()
    manager = assets["manager"]
    AssetsFileReader = assets["AssetsFileReader"]
    AssetsFileWriter = assets["AssetsFileWriter"]
    ContentReplacerFromAssets = assets["ContentReplacerFromAssets"]
    ContentReplacerFromBuffer = assets["ContentReplacerFromBuffer"]
    AssetBundleCompressionType = assets["AssetBundleCompressionType"]

    timing_enabled = env_flag("RML_PRINT_TIMING")
    tmp_uncompressed = out_path + ".uncompressed"
    temp_dir = tempfile.mkdtemp(prefix="rml_asset_patch_")
    try:
        for path in (out_path, tmp_uncompressed):
            if os.path.exists(path):
                os.remove(path)

        manager.UnloadAll()
        bundle = manager.LoadBundleFile(os.path.abspath(bundle_path), False)
        asset_count = bundle.file.GetAllFileNames().Count
        original_assets = [manager.LoadAssetsFileFromBundle(bundle, i) if bundle.file.IsAssetsFile(i) else None for i in range(asset_count)]

        patch_build_start = time.perf_counter() if timing_enabled else 0.0
        patches = build_object_patches_from_serialized_files(bundle, original_assets, incoming_bundle_files, temp_dir)
        patch_build_seconds = (time.perf_counter() - patch_build_start) if timing_enabled else None

        if not patches:
            shutil.copyfile(bundle_path, out_path)
            print(f"{done_label} 未检测到对象级差异，已复制原始 AB：{out_path}")
            return

        payload_array_seconds = 0.0
        payload_replacer_seconds = 0.0
        payload_total_bytes = 0
        for asset_index, path_id, payload in patches:
            if timing_enabled:
                payload_total_bytes += int(payload.Length)
                payload_replacer_start = time.perf_counter()
            # payload 已经是 .NET Byte[]，可直接交给 replacer，避免 Array[Byte](payload)。
            replacer = ContentReplacerFromBuffer(payload)
            if timing_enabled:
                payload_replacer_seconds += time.perf_counter() - payload_replacer_start
            original_assets[asset_index].file.GetAssetInfo(path_id).Replacer = replacer

        asset_from_assets_seconds = 0.0
        for asset_index, asset in enumerate(original_assets):
            if asset is None:
                continue
            if timing_enabled:
                asset_from_assets_start = time.perf_counter()
            # 整份 assets 直接复用 AssetsFile，避免整文件序列化后再拷贝成新的 .NET byte[]。
            bundle_replacer = ContentReplacerFromAssets(asset.file)
            if timing_enabled:
                asset_from_assets_seconds += time.perf_counter() - asset_from_assets_start
            bundle.file.BlockAndDirInfo.DirectoryInfos[asset_index].Replacer = bundle_replacer

        write_start = time.perf_counter() if timing_enabled else 0.0
        tmp_writer = AssetsFileWriter(os.path.abspath(tmp_uncompressed))
        bundle.file.Write(tmp_writer)
        tmp_writer.Close()
        write_seconds = (time.perf_counter() - write_start) if timing_enabled else None

        bundle.file.Close()
        read_start = time.perf_counter() if timing_enabled else 0.0
        tmp_reader = AssetsFileReader(os.path.abspath(tmp_uncompressed))
        bundle.file.Read(tmp_reader)
        read_seconds = (time.perf_counter() - read_start) if timing_enabled else None

        pack_start = time.perf_counter() if timing_enabled else 0.0
        bundle_writer = AssetsFileWriter(os.path.abspath(out_path))
        bundle.file.Pack(bundle_writer, AssetBundleCompressionType.LZ4, False, None)
        bundle_writer.Close()
        pack_seconds = (time.perf_counter() - pack_start) if timing_enabled else None
        tmp_reader.Close()
        manager.UnloadAll()

        if os.path.exists(tmp_uncompressed):
            os.remove(tmp_uncompressed)

        if timing_enabled:
            print(f"[TIMING] patch_build_patches_seconds={patch_build_seconds:.3f}")
            print(f"[TIMING] payload_array_seconds={payload_array_seconds:.3f} payload_bytes={payload_total_bytes}")
            print(f"[TIMING] payload_replacer_seconds={payload_replacer_seconds:.3f} payload_bytes={payload_total_bytes}")
            print(f"[TIMING] asset_from_assets_seconds={asset_from_assets_seconds:.3f}")
            print(f"[TIMING] patch_write_seconds={write_seconds:.3f}")
            print(f"[TIMING] patch_read_seconds={read_seconds:.3f}")
            print(f"[TIMING] patch_pack_seconds={pack_seconds:.3f}")

        print(f"{done_label} 写出新 AB：{out_path}")
    finally:
        if os.path.exists(tmp_uncompressed):
            try:
                os.remove(tmp_uncompressed)
            except Exception:
                pass
        shutil.rmtree(temp_dir, ignore_errors=True)
        manager.UnloadAll()


def repack_bundle_with_assetstools(bundle_path: str, out_path: str, bundle_files: Dict[str, bytes], done_label: str = "[DONE]"):
    """整包重封所有内部文件，主要用于 Sprite 资产结构发生变化的场景。"""
    assets = ensure_assets_tools()
    manager = assets["manager"]
    AssetsFileReader = assets["AssetsFileReader"]
    AssetsFileWriter = assets["AssetsFileWriter"]
    ContentReplacerFromBuffer = assets["ContentReplacerFromBuffer"]
    AssetBundleCompressionType = assets["AssetBundleCompressionType"]
    Array = assets["Array"]
    Byte = assets["Byte"]

    timing_enabled = env_flag("RML_PRINT_TIMING")
    tmp_uncompressed = out_path + ".uncompressed"
    for path in (out_path, tmp_uncompressed):
        if os.path.exists(path):
            os.remove(path)

    manager.UnloadAll()
    bundle = manager.LoadBundleFile(os.path.abspath(bundle_path), False)
    directory_infos = bundle.file.BlockAndDirInfo.DirectoryInfos
    name_to_index = {directory_infos[i].Name: i for i in range(directory_infos.Count)}

    for name, data in bundle_files.items():
        index = name_to_index.get(name)
        if index is None:
            raise RuntimeError(f"原始 bundle 中不存在内部文件: {name}")
        directory_infos[index].Replacer = ContentReplacerFromBuffer(Array[Byte](data))

    write_start = time.perf_counter() if timing_enabled else 0.0
    tmp_writer = AssetsFileWriter(os.path.abspath(tmp_uncompressed))
    bundle.file.Write(tmp_writer)
    tmp_writer.Close()
    write_seconds = (time.perf_counter() - write_start) if timing_enabled else None

    bundle.file.Close()
    read_start = time.perf_counter() if timing_enabled else 0.0
    tmp_reader = AssetsFileReader(os.path.abspath(tmp_uncompressed))
    bundle.file.Read(tmp_reader)
    read_seconds = (time.perf_counter() - read_start) if timing_enabled else None

    pack_start = time.perf_counter() if timing_enabled else 0.0
    bundle_writer = AssetsFileWriter(os.path.abspath(out_path))
    bundle.file.Pack(bundle_writer, AssetBundleCompressionType.LZ4, False, None)
    bundle_writer.Close()
    pack_seconds = (time.perf_counter() - pack_start) if timing_enabled else None
    tmp_reader.Close()
    manager.UnloadAll()

    if os.path.exists(tmp_uncompressed):
        os.remove(tmp_uncompressed)

    if timing_enabled:
        print(f"[TIMING] repack_write_seconds={write_seconds:.3f}")
        print(f"[TIMING] repack_read_seconds={read_seconds:.3f}")
        print(f"[TIMING] repack_pack_seconds={pack_seconds:.3f}")

    print(f"{done_label} 写出新 AB：{out_path}")

def save_bundle_env(env, bundle_path: str, out_path: str, textures, done_label: str = "[DONE]", force_full_repack: bool = False):
    """根据资源变化类型选择对象级补丁或整包重封的保存策略。"""
    if not out_path:
        root, ext = os.path.splitext(bundle_path)
        out_path = root + "_patched" + (ext or "")

    if force_full_repack or has_streamed_texture_data(textures):
        bundle_files = collect_bundle_file_bytes(env)
        if force_full_repack:
            print("[SAVE] 检测到 Sprite 资产结构变更，使用 AssetsTools.NET 整文件重封包")
        else:
            print("[SAVE] 检测到外部流资源，使用 AssetsTools.NET 重封包并保留内部文件替换")
        repack_bundle_with_assetstools(bundle_path, out_path, bundle_files, done_label)
        return

    incoming_bundle_files = collect_bundle_file_bytes(env)
    patch_bundle_objects_with_assetstools(bundle_path, incoming_bundle_files, out_path, done_label)

def import_sprites(bundle_path: str, images_root: str, out_path: str = None, mode: str = "rect", match: str = "name"):
    """直接按 Sprite 名称或 path_id 回填纹理，不处理动画时间轴。"""
    clear_runtime_caches()
    env = UnityPy.load(bundle_path)

    textures_by_pid = {}
    for obj in env.objects:
        if obj.type.name == "Texture2D":
            try: textures_by_pid[obj.path_id] = cached_read(obj)
            except: pass

    sprites_group_by_tex = defaultdict(list)  # tex_pid -> [(spr, spr_pid)]
    for obj in env.objects:
        if obj.type.name != "Sprite":
            continue
        try:
            spr = cached_read(obj)
        except Exception:
            continue
        tex_ptr = spr.m_RD.texture
        if tex_ptr and tex_ptr.path_id in textures_by_pid:
            sprites_group_by_tex[tex_ptr.path_id].append((spr, obj.path_id))

    name_map, pid_map = collect_replacement_images(images_root)
    if not name_map and not pid_map:
        print(f"[IMPORT] 未在 {images_root} 找到任何 *.png，结束"); return

    def choose_path(spr, pid):
        if match == "name":
            return name_map.get(spr.m_Name)
        elif match == "pid":
            return pid_map.get(pid)
        else:
            return pid_map.get(pid) or name_map.get(spr.m_Name)

    def paste(tex_img, spr, repl_img):
        return paste_sprite_mesh(tex_img, spr, repl_img) if mode == "mesh" else paste_sprite_rect(tex_img, spr, repl_img)

    changed_textures = 0
    for tex_pid, sprite_list in sprites_group_by_tex.items():
        tex = textures_by_pid[tex_pid]
        base_img = tex.image.convert("RGBA")
        applied = 0
        for spr, pid in sprite_list:
            path = choose_path(spr, pid)
            if not path: continue
            try:
                repl = Image.open(path).convert("RGBA")
                if paste(base_img, spr, repl): applied += 1
            except Exception as e:
                print(f"[ERR] 贴回 {spr.m_Name} 失败：{e}")
        if applied:
            tex.set_image(base_img)
            tex.save()
            changed_textures += 1
            print(f"[IMPORT-{mode}|{match}] 纹理 {tex.m_Name or tex_pid} 应用 {applied} 张替换图")

    if changed_textures == 0:
        print("[IMPORT] 没有任何纹理被修改，未输出新包")
        return

    save_bundle_env(env, bundle_path, out_path, textures_by_pid.values())

    # if changed_textures == 0:
    #     print("[IMPORT] 没有任何纹理被修改，未输出新包"); return
    # if not out_path:
    #     root, ext = os.path.splitext(bundle_path); out_path = root + "_patched" + (ext or "")
    # with open(out_path, "wb") as f:
    #     f.write(env.file.save(packer="original"))
    # print(f"[DONE] 写出新 AB：{out_path}")

# ---------------------------------------------------------------------------
# 动画 clip 导入主流程
# ---------------------------------------------------------------------------

def import_clip_folders(bundle_path: str, export_root: str, out_path: str = None, preserve_timeline: bool = False):
    """按导出的 clip.json 与 PNG 批量回填动画。

    默认模式会重建均匀时间轴并允许增删帧。
    严格模式会保留原时间轴与原 Sprite 布局，但要求槽位数和 PNG 尺寸完全兼容。
    """
    timing_enabled = env_flag("RML_PRINT_TIMING")
    total_start = time.perf_counter() if timing_enabled else 0.0
    save_seconds = None
    clear_runtime_caches()
    dir_entries = sorted(os.listdir(export_root))
    emit_progress_setup(max(1, len(dir_entries)), f"扫描导出目录 {os.path.basename(export_root)}")
    clip_dirs = []
    for entry in dir_entries:
        clip_dir = os.path.join(export_root, entry)
        if not os.path.isdir(clip_dir):
            continue
        json_path = os.path.join(clip_dir, "clip.json")
        if not os.path.isfile(json_path):
            continue
        with open(json_path, "r", encoding="utf-8-sig") as f:
            meta = json.load(f)
        if not preserve_timeline:
            meta, changed = sync_clip_meta_with_pngs(clip_dir, meta)
            if changed:
                with open(json_path, "w", encoding="utf-8") as f:
                    json.dump(meta, f, ensure_ascii=False, indent=2)
                print(f"[IMPORT-CLIPS] 已按 PNG 数量同步 clip.json: {clip_dir}")
        clip_dirs.append((clip_dir, meta))
        emit_progress_step(f"已读取 {entry}")

    if not clip_dirs:
        emit_progress_done("未找到任何 clip.json")
        print(f"[IMPORT-CLIPS] 在 {export_root} 未找到任何 clip.json")
        return

    if not out_path:
        root, ext = os.path.splitext(os.path.basename(bundle_path))
        out_path = os.path.join(export_root, f"{root}_patched{ext or '.bundle'}")

    emit_progress_done(f"已发现 {len(clip_dirs)} 个动画目录")
    emit_progress_setup(2, f"加载目标资源包 {os.path.basename(bundle_path)}")
    env = UnityPy.load(bundle_path)
    emit_progress_step("目标资源包已加载")
    animations = load_animations(env)
    emit_progress_step(f"已读取动画绑定，共 {len(animations)} 条")
    plans = {}

    emit_progress_setup(max(1, len(clip_dirs)), f"规划待回填动画，共 {len(clip_dirs)} 个目录")
    for clip_dir, meta in clip_dirs:
        clip_name = (meta.get("clipName") or os.path.basename(clip_dir)).strip().lower()
        clip_path_id = to_int(meta.get("clipPathId"), 0)
        animation = animations.get(clip_name)
        if animation is None and clip_path_id:
            for candidate in animations.values():
                if candidate.animation_clip.path_id == clip_path_id:
                    animation = candidate
                    break
        if animation is None:
            print(f"[IMPORT-CLIPS] 跳过未匹配动画: {clip_name}")
            continue

        try:
            if preserve_timeline:
                validate_preserve_timeline_meta(clip_dir, meta, animation)
            resolved_frames = resolve_clip_frames(meta, clip_dir, preserve_timeline)
        except RuntimeError as ex:
            print(str(ex))
            continue

        if not resolved_frames:
            print(f"[IMPORT-CLIPS] 跳过无有效帧的动画目录: {clip_dir}")
            continue

        plans[clip_name] = {
            "clip_dir": clip_dir,
            "meta": meta,
            "clip_name": clip_name,
            "clip_path_id": animation.animation_clip.path_id,
            "original_count": len(animation.sprites),
            "target_count": len(resolved_frames),
            "frames": resolved_frames,
            "preserve_timeline": preserve_timeline,
        }
        emit_progress_step(f"已规划动画 {meta.get('clipName') or clip_name}")

    if not plans:
        emit_progress_done("没有任何动画可更新")
        print("[IMPORT-CLIPS] 没有任何动画可更新，未输出新包")
        return
    emit_progress_done(f"待更新动画 {len(plans)} 个")

    updates = []
    for clip_name, plan in plans.items():
        animation = animations.get(clip_name)
        if animation is None:
            continue
        if plan["target_count"] != len(animation.sprites):
            updates.append({
                "anim": animation,
                "expected": plan["target_count"],
            })

    temp_file = None
    work_bundle_path = bundle_path
    sprite_asset_changes = []
    if updates:
        emit_progress_setup(1, f"准备调整 Sprite 资产结构，共 {len(updates)} 个动画")
        fd, temp_file = tempfile.mkstemp(prefix="rml_sprite_anim_", suffix=".bundle")
        os.close(fd)
        try:
            sprite_asset_changes = update_sprites(bundle_path, updates, temp_file)
            work_bundle_path = temp_file
            emit_progress_step("Sprite 资产结构调整完成")
        except Exception:
            if temp_file and os.path.isfile(temp_file):
                os.remove(temp_file)
            raise
        emit_progress_done("Sprite 资产结构阶段完成")

    try:
        emit_progress_setup(1, "重新加载更新后的资源包")
        clear_runtime_caches()
        env = UnityPy.load(work_bundle_path)
        animations = load_animations(env)
        content_modified = bool(sprite_asset_changes)
        emit_progress_step("资源包重新加载完成")
        emit_progress_done("准备进入动画回填")

        if sprite_asset_changes:
            for anim_name, is_add, changed_ids in sprite_asset_changes:
                animation = animations.get(anim_name)
                if animation is None:
                    continue

                anim_data = animation.animation_clip_data
                anim_pptr_mapping = anim_data.get("m_ClipBindingConstant", {}).get("pptrCurveMapping", [])
                mono_data = cached_parse_as_dict(animation.mono_behaviour) if animation.mono_behaviour is not None else None
                mono_sprites = mono_data.get("sprites", []) if mono_data is not None else None

                if is_add:
                    pptr_template = dict(anim_pptr_mapping[-1]) if anim_pptr_mapping else {"m_FileID": 0, "m_PathID": 0}
                    mono_template = dict(mono_sprites[-1]) if mono_sprites else {"m_FileID": 0, "m_PathID": 0}
                    for path_id in changed_ids:
                        clone = dict(pptr_template)
                        clone["m_PathID"] = int(path_id)
                        anim_pptr_mapping.append(clone)
                        if mono_sprites is not None:
                            mono_clone = dict(mono_template)
                            mono_clone["m_PathID"] = int(path_id)
                            mono_sprites.append(mono_clone)
                else:
                    removed = set(int(path_id) for path_id in changed_ids)
                    anim_data["m_ClipBindingConstant"]["pptrCurveMapping"] = [
                        entry for entry in anim_pptr_mapping if to_int(entry.get("m_PathID"), 0) not in removed
                    ]
                    if mono_sprites is not None:
                        mono_data["sprites"] = [
                            entry for entry in mono_sprites if to_int(entry.get("m_PathID"), 0) not in removed
                        ]

                save_typetree_cached(animation.animation_clip, anim_data)
                if mono_data is not None:
                    save_typetree_cached(animation.mono_behaviour, mono_data)

            with open(work_bundle_path, "wb") as f:
                f.write(env.file.save(packer="original"))
            clear_runtime_caches()
            env = UnityPy.load(work_bundle_path)
            animations = load_animations(env)

        anim_id_to_playable_asset_id, playable_asset_id_to_track_clip, tracks = collect_track_bindings(env)

        all_sprites_by_texture = defaultdict(list)
        for obj in env.objects:
            if obj.type.name != "Sprite":
                continue
            try:
                texture_pid = to_int(cached_parse_as_dict(obj)["m_RD"]["texture"]["m_PathID"], 0)
                all_sprites_by_texture[texture_pid].append(obj)
            except Exception:
                pass

        texture_plans = {}
        updated_animations = 0

        emit_progress_setup(max(1, len(plans)), f"更新动画时间轴与绑定，共 {len(plans)} 个")
        for clip_name, plan in plans.items():
            animation = animations.get(clip_name)
            if animation is None or not animation.sprites:
                print(f"[IMPORT-CLIPS] 资产更新后找不到动画: {clip_name}")
                emit_progress_step(f"跳过动画 {clip_name}")
                continue

            sprite_slots = animation.sprites[:plan["target_count"]]
            if len(sprite_slots) < plan["target_count"]:
                print(f"[IMPORT-CLIPS] 动画帧数不足: {clip_name}，期望 {plan['target_count']} 实际 {len(sprite_slots)}")
                continue

            if plan["preserve_timeline"]:
                sprite_slots = resolve_sprite_slots_for_frames(sprite_slots, plan["frames"], all_sprites_by_texture)

            path_ids = [sprite.path_id for _, sprite, _ in sprite_slots]
            if not plan["preserve_timeline"] and use_loop_last_to_first(plan["meta"]) and len(path_ids) > 1:
                path_ids[-1] = path_ids[0]
            anim_data = animation.animation_clip_data
            existing_mapping = anim_data.get("m_ClipBindingConstant", {}).get("pptrCurveMapping", [])
            existing_mapping_ids = [to_int(entry.get("m_PathID"), 0) for entry in existing_mapping]
            animation_modified = False
            if existing_mapping_ids != path_ids:
                anim_data["m_ClipBindingConstant"]["pptrCurveMapping"] = build_pointer_array(existing_mapping, path_ids)
                animation_modified = True

            if animation.mono_behaviour is not None:
                mono_data = cached_parse_as_dict(animation.mono_behaviour)
                mono_sprites = mono_data.get("sprites")
                if isinstance(mono_sprites, list):
                    mono_sprite_ids = [to_int(entry.get("m_PathID"), 0) for entry in mono_sprites]
                    if mono_sprite_ids != path_ids:
                        mono_data["sprites"] = build_pointer_array(mono_sprites, path_ids)
                        save_typetree_cached(animation.mono_behaviour, mono_data)

            delta = plan["target_count"] - plan["original_count"]
            if delta != 0:
                anim_data["m_MuscleClipSize"] += delta * 7 * 4
                value_array_delta = anim_data.get("m_MuscleClip", {}).get("m_ValueArrayDelta", [])
                if value_array_delta:
                    value_array_delta[0]["m_Stop"] = to_float(value_array_delta[0].get("m_Stop"), 0.0) + delta
                animation_modified = True

            sample_rate = to_float(anim_data.get("m_SampleRate"), to_float(plan["meta"].get("sampleRate"), 12.0))
            if plan["preserve_timeline"]:
                source_timeline = plan["meta"].get("sourceTimeline") or {}
                clip_length = to_float(source_timeline.get("stopTime"), to_float(plan["meta"].get("length"), 0.0))
                desired_dense_count = to_int(source_timeline.get("denseFrameCount"), plan["target_count"] + 2)
                streamed_clip_data = source_timeline.get("streamedClipData") or []
                timeline_keyframes = source_timeline.get("keyframes") or []
                strict_frames = []
                for index, frame in enumerate(plan["frames"]):
                    strict_frame = dict(frame)
                    if index < len(timeline_keyframes):
                        strict_frame["time"] = to_float(timeline_keyframes[index].get("time"), strict_frame.get("time", index))
                        strict_frame["endTime"] = to_float(timeline_keyframes[index].get("endTime"), strict_frame["time"])
                    strict_frames.append(strict_frame)
                plan["frames"] = strict_frames
            else:
                frame_duration = derive_frame_duration(plan["meta"], plan["meta"].get("frames") or [], plan["meta"].get("keyframes") or [])
                clip_length = to_float(plan["meta"].get("length"), 0.0)
                if clip_length <= 0:
                    clip_length = frame_duration * plan["target_count"]
                elif frame_duration > 0 and plan["target_count"] > 0:
                    clip_length = frame_duration * plan["target_count"]
                if use_loop_last_to_first(plan["meta"]) and frame_duration > 0 and plan["target_count"] > 1:
                    clip_length = frame_duration * (plan["target_count"] - 1)
                if env_flag("RML_TRIM_LOOP_GAP") and bool(plan["meta"].get("loop", True)) and frame_duration > 0 and plan["target_count"] > 1:
                    clip_length = frame_duration * (plan["target_count"] - 1)
                desired_dense_count = plan["target_count"] + 2
                streamed_clip_data = []
            current_stop_time = anim_data.get("m_MuscleClip", {}).get("m_StopTime")
            current_dense_count = anim_data.get("m_MuscleClip", {}).get("m_Clip", {}).get("data", {}).get("m_DenseClip", {}).get("m_FrameCount")
            current_streamed_clip_data = anim_data.get("m_MuscleClip", {}).get("m_Clip", {}).get("data", {}).get("m_StreamedClip", {}).get("data", []) or []
            if not floats_close(current_stop_time, clip_length):
                anim_data["m_MuscleClip"]["m_StopTime"] = clip_length
                animation_modified = True
            if to_int(current_dense_count, desired_dense_count) != desired_dense_count:
                anim_data["m_MuscleClip"]["m_Clip"]["data"]["m_DenseClip"]["m_FrameCount"] = desired_dense_count
                animation_modified = True
            if plan["preserve_timeline"] and streamed_clip_data:
                normalized_existing_stream = [(to_int(value, 0) & 0xFFFFFFFF) for value in current_streamed_clip_data]
                normalized_target_stream = [(to_int(value, 0) & 0xFFFFFFFF) for value in streamed_clip_data]
                if normalized_existing_stream != normalized_target_stream:
                    animation_modified = True
            if animation_modified:
                if plan["preserve_timeline"] and streamed_clip_data:
                    set_streamed_clip_data(anim_data, streamed_clip_data)
                else:
                    ensure_streamed_clip_data(anim_data, plan["frames"], sample_rate)
                save_typetree_cached(animation.animation_clip, anim_data)
                content_modified = True

            if animation.mono_behaviour is not None:
                mono_data = cached_parse_as_dict(animation.mono_behaviour)
                current_mono_ids = [to_int(entry.get("m_PathID"), 0) for entry in (mono_data.get("sprites", []) or [])]
                if current_mono_ids != path_ids:
                    mono_data["sprites"] = build_pointer_array(mono_data.get("sprites", []), path_ids)
                    save_typetree_cached(animation.mono_behaviour, mono_data)
                    animation_modified = True
                    content_modified = True

            playable_asset_id = anim_id_to_playable_asset_id.get(animation.animation_clip.path_id)
            if playable_asset_id is not None:
                track_clip = playable_asset_id_to_track_clip.get(playable_asset_id)
                if track_clip is not None:
                    if not floats_close(track_clip.get("m_Duration"), clip_length):
                        track_clip["m_Duration"] = clip_length
                        animation_modified = True
                        content_modified = True

            if animation.texture is None:
                print(f"[IMPORT-CLIPS] 动画缺少贴图引用: {clip_name}")
                continue

            texture_pid = animation.texture.path_id
            texture_plan = texture_plans.get(texture_pid)
            if texture_plan is None:
                texture_plan = {
                    "texture_obj": animation.texture,
                    "frames": [],
                    "all_sprites": list(all_sprites_by_texture.get(texture_pid, [])),
                    "preserve_timeline": plan["preserve_timeline"],
                    "meta": plan["meta"],
                }
                texture_plans[texture_pid] = texture_plan

            for index, frame in enumerate(plan["frames"]):
                sprite_obj = sprite_slots[index][1]
                sprite_data = cached_parse_as_dict(sprite_obj)
                slot_rect = dict(sprite_data["m_RD"]["textureRect"])
                texture_plan["frames"].append({
                    "sprite_obj": sprite_obj,
                    "image_path": frame["image_path"],
                    "rect": slot_rect,
                    "is_added": index >= plan["original_count"],
                    "preserve_geometry": plan["target_count"] == plan["original_count"],
                    "clip_pixels_per_unit": to_float(plan["meta"].get("pixelsPerUnit"), 100.0),
                    "pivot01": (frame.get("frame_meta") or {}).get("pivot01"),
                    "border": (frame.get("frame_meta") or {}).get("border"),
                    "frame_meta": frame.get("frame_meta") or {},
                })

            if animation_modified:
                updated_animations += 1
                print(f"[IMPORT-CLIPS] 已重建动画绑定: {clip_name}，目标帧数 {plan['target_count']}")
            else:
                print(f"[IMPORT-CLIPS] 动画绑定无需改动: {clip_name}")
            emit_progress_step(f"已处理动画 {clip_name}")

        emit_progress_done(f"动画阶段完成，实际更新 {updated_animations} 个")

        for obj, mono_data in tracks:
            save_typetree_cached(obj, mono_data)

        changed_textures = 0
        force_rect_mesh = env_flag("RML_FORCE_RECT_MESH")
        emit_progress_setup(max(1, len(texture_plans)), f"更新贴图图集，共 {len(texture_plans)} 个纹理")
        for texture_pid, texture_plan in texture_plans.items():
            if not texture_plan["frames"]:
                emit_progress_step(f"跳过空纹理 {texture_pid}")
                continue

            texture_data = cached_read(texture_plan["texture_obj"])
            target_frames = texture_plan["frames"]
            force_grid_layout = any(entry.get("is_added") for entry in target_frames)
            render_key_assignments = {}
            if force_grid_layout:
                target_path_ids = {entry["sprite_obj"].path_id for entry in target_frames}
                used_render_key_ids = collect_used_sprite_render_key_ids(env, target_path_ids)
                for entry in target_frames:
                    render_key_assignments[entry["sprite_obj"].path_id] = generate_random_meta_path_id(used_render_key_ids)
            unchanged_frames = 0
            replacement_frames = []
            for entry in target_frames:
                try:
                    replacement_image, is_same = images_match_by_signature(entry["image_path"], entry["sprite_obj"])
                    if is_same:
                        unchanged_frames += 1
                    replacement_frames.append((entry, replacement_image, is_same))
                except Exception as ex:
                    print(f"[IMPORT-CLIPS] 贴图写入失败: {entry['image_path']} -> {ex}")

            if replacement_frames and unchanged_frames == len(target_frames):
                print(f"[IMPORT-CLIPS] 所有帧图片均未变化，跳过纹理保存: {texture_pid}")
                emit_progress_step(f"纹理无需更新 {texture_pid}")
                continue

            if texture_plan.get("preserve_timeline"):
                atlas_width, atlas_height = get_preserve_timeline_texture_size(texture_plan.get("meta") or {}, target_frames)
                cell_w, cell_h = 0, 0
                cols, rows = 0, 0
            else:
                cell_w, cell_h = infer_mod_atlas_cell_size(texture_plan, target_frames)
                cols = infer_mod_atlas_columns(texture_data, cell_w, cell_h, len(target_frames))
                rows = max(1, round_int(math.ceil(len(target_frames) / float(cols))))
                atlas_width = cell_w * cols
                atlas_height = cell_h * rows
            base_image = Image.new("RGBA", (atlas_width, atlas_height), (0, 0, 0, 0))

            for index, (entry, replacement_image, _) in enumerate(replacement_frames):
                sprite_obj = entry["sprite_obj"]
                sprite_data = cached_parse_as_dict(sprite_obj)
                apply_sprite_import_metadata(
                    sprite_data,
                    entry.get("frame_meta") or {},
                    to_float(entry.get("clip_pixels_per_unit"), 100.0),
                )
                assigned_render_key = render_key_assignments.get(sprite_obj.path_id)
                if assigned_render_key is not None:
                    render_key = sprite_data.get("m_RenderDataKey")
                    if isinstance(render_key, list) and len(render_key) >= 2:
                        render_key[1] = int(assigned_render_key)
                if texture_plan.get("preserve_timeline"):
                    new_rect = rebuild_sprite_from_source_meta(
                        sprite_data,
                        (atlas_width, atlas_height),
                        replacement_image,
                        entry.get("frame_meta") or {},
                        to_float(entry.get("clip_pixels_per_unit"), 100.0),
                    )
                else:
                    cell_rect = get_existing_cell_rect(sprite_data, cell_w, cell_h, index, cols, rows, prefer_existing=not force_grid_layout)
                    pivot01 = entry.get("pivot01") if isinstance(entry.get("pivot01"), list) and len(entry.get("pivot01")) >= 2 else [0.5, 0.0]
                    border = entry.get("border") if isinstance(entry.get("border"), list) and len(entry.get("border")) >= 4 else [0.0, 0.0, 0.0, 0.0]
                    new_rect = rebuild_sprite_as_mod_cell(
                        sprite_data,
                        (atlas_width, atlas_height),
                        cell_rect,
                        replacement_image,
                        sprite_data.get("m_PixelsToUnits", entry.get("clip_pixels_per_unit", 100.0)),
                        pivot01,
                        border,
                        entry.get("frame_meta") or {},
                        force_rect_mesh=force_rect_mesh,
                    )
                left = round_int(new_rect["x"])
                top = atlas_height - round_int(new_rect["y"] + new_rect["height"])
                base_image.alpha_composite(replacement_image, (left, top))
                save_typetree_cached(sprite_obj, sprite_data)
                entry["rect"] = new_rect

            texture_data.set_image(base_image)
            texture_data.save()
            invalidate_object_runtime_cache(texture_plan["texture_obj"])
            changed_textures += 1
            content_modified = True
            mesh_mode = "rect" if force_rect_mesh else "tight"
            print(f"[IMPORT-CLIPS] 已重建 embedded 纹理 {texture_pid}，atlas={atlas_width}x{atlas_height}，mesh={mesh_mode}，应用 {len(texture_plan['frames'])} 帧")
            emit_progress_step(f"已更新纹理 {texture_pid}")

        emit_progress_done(f"贴图阶段完成，实际更新 {changed_textures} 个纹理")

        if not content_modified:
            shutil.copyfile(bundle_path, out_path)
            emit_progress_setup(1, "没有检测到修改，复制原始资源包")
            emit_progress_step("已复制原始资源包")
            emit_progress_done(f"未修改，输出 {os.path.basename(out_path)}")
            print(f"[IMPORT-CLIPS] 未检测到实际内容变化，已复制原始 AB 到: {out_path}")
            return

        emit_progress_setup(1, "保存修改后的资源包")
        save_start = time.perf_counter() if timing_enabled else 0.0
        save_bundle_env(
            env,
            bundle_path,
            out_path,
            [texture_plan["texture_obj"] for texture_plan in texture_plans.values()],
            force_full_repack=bool(sprite_asset_changes),
        )
        if timing_enabled:
            save_seconds = time.perf_counter() - save_start
        emit_progress_step("资源包保存完成")
    finally:
        if temp_file and os.path.isfile(temp_file):
            try:
                os.remove(temp_file)
            except Exception:
                pass
        if timing_enabled:
            total_seconds = time.perf_counter() - total_start
            if save_seconds is not None:
                print(f"[TIMING] save_seconds={save_seconds:.3f}")
            print(f"[TIMING] total_seconds={total_seconds:.3f}")

def main():
    """命令行入口。"""
    ap = argparse.ArgumentParser(description="导出/回填 AnimationClip 引用的 Sprites（支持压缩曲线/mesh_blit，匹配方式可选）")
    sub = ap.add_subparsers(dest="cmd")

    ap_scan = sub.add_parser("scan", help="统计包内对象类型数量")
    ap_scan.add_argument("bundle")

    ap_exp = sub.add_parser("export", help="导出 Sprites 到 动画名/精灵名.png（支持压缩曲线映射）")
    ap_exp.add_argument("bundle"); ap_exp.add_argument("out_dir")
    ap_exp.add_argument("--all-sprites", action="store_true", help="忽略动画引用，导出全部 Sprite 兜底")
    ap_exp.add_argument("--unique-names", action="store_true", help="文件名追加 __pid<id> 避免同名冲突")
    ap_exp.add_argument("--group-by-texture", action="store_true", help="导出全部时按 Texture2D 名称分组")

    ap_imp = sub.add_parser("import", help="按名称/路径ID/自动匹配回填，支持 mesh_blit")
    ap_imp.add_argument("bundle"); ap_imp.add_argument("images_root")
    ap_imp.add_argument("-o", "--output")
    ap_imp.add_argument("--mode", choices=["rect", "mesh"], default="rect", help="回填方式：rect=矩形；mesh=网格逐三角")
    ap_imp.add_argument("--match", choices=["name", "pid", "auto"], default="name", help="匹配方式：name=按 sprite 名；pid=按 __pid 后缀；auto=pid优先后名")

    ap_imp_clips = sub.add_parser("import-clips", help="按导出的 clip.json + PNG 回填动画，并输出原文件名_patched.bundle")
    ap_imp_clips.add_argument("bundle")
    ap_imp_clips.add_argument("export_root")
    ap_imp_clips.add_argument("-o", "--output")
    ap_imp_clips.add_argument("--preserve-timeline", action="store_true", help="严格按导出时的原始时间轴回填；要求 PNG 数量与源动画槽位数保持一致，不允许增减帧")

    args = ap.parse_args()
    if args.cmd == "scan":
        scan_bundle(args.bundle)
    elif args.cmd == "export":
        export_sprites(args.bundle, args.out_dir, args.all_sprites, args.unique_names, args.group_by_texture)
    elif args.cmd == "import":
        import_sprites(args.bundle, args.images_root, args.output, args.mode, args.match)
    elif args.cmd == "import-clips":
        try:
            import_clip_folders(args.bundle, args.export_root, args.output, args.preserve_timeline)
        except Exception:
            traceback.print_exc()
            raise
    else:
        ap.print_help()

if __name__ == "__main__":
    main()



