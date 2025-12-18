#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
生成测试用的Stardew Valley mod目录结构
根据xlgChineseBack.json生成mods文件夹，每个mod都包含manifest.json
"""

import json
import os
from pathlib import Path

# Stardew Valley mod的默认manifest.json模板
DEFAULT_MANIFEST_TEMPLATE = {
    "Name": "",
    "Author": "Test Author",
    "Version": "1.0.0",
    "Description": "",
    "UniqueID": "",
    "EntryDll": None,
    "MinimumGameVersion": "1.5.6",
    "MinimumApiVersion": "3.14.0",
    "UpdateKeys": [],
    "ContentPackFor": None,
    "Dependencies": [],
    "Maps": {}
}

def load_translation_data(json_path):
    """加载xlgChineseBack.json文件"""
    with open(json_path, 'r', encoding='utf-8') as f:
        return json.load(f)

def create_manifest(mod_info):
    """根据mod信息创建manifest字典"""
    manifest = DEFAULT_MANIFEST_TEMPLATE.copy()
    manifest["Name"] = mod_info.get("Name", "Unknown Mod")
    manifest["Description"] = mod_info.get("Description", "No description provided")
    manifest["UniqueID"] = mod_info.get("UniqueID", "unknown.mod")
    
    # 处理内容包（如果Name中包含[CP]标记）
    if "[CP]" in mod_info.get("Name", ""):
        # 内容包需要指定父mod
        unique_id = mod_info.get("UniqueID", "")
        # 尝试从UniqueID推断父mod（通常是前缀）
        if unique_id and "." in unique_id:
            parent_id = unique_id.split(".")[0] + "." + unique_id.split(".")[1]
            # 对于已知的内容包，手动指定
            if "BetterJunimos" in unique_id:
                manifest["ContentPackFor"] = {"UniqueID": "hawkfalcon.BetterJunimos"}
            elif "VPP" in unique_id:
                manifest["ContentPackFor"] = {"UniqueID": "KediDili.VanillaPlusProfessions"}
            elif "Pierre" in unique_id or "DaisyNiko" in unique_id or "Shopkeeper" in unique_id or "Way" in unique_id or "Canon" in unique_id:
                manifest["ContentPackFor"] = {"UniqueID": "Pathoschild.ContentPatcher"}
    
    # 移除None值
    return {k: v for k, v in manifest.items() if v is not None}

def generate_mods_structure(json_path, output_dir="mods"):
    """
    根据JSON数据生成mod目录结构
    
    Args:
        json_path: xlgChineseBack.json文件路径
        output_dir: 输出的mods目录名称
    """
    # 获取脚本所在目录作为基准
    script_dir = Path(__file__).parent
    mods_dir = script_dir / output_dir
    
    # 创建mods目录
    mods_dir.mkdir(exist_ok=True)
    print(f"✓ 已创建mods目录: {mods_dir}")
    
    # 加载JSON数据
    try:
        mod_data = load_translation_data(json_path)
    except FileNotFoundError:
        print(f"✗ 错误：找不到文件 {json_path}")
        return
    except json.JSONDecodeError:
        print(f"✗ 错误：JSON文件格式不正确")
        return
    
    # 遍历每个mod并创建目录和manifest
    created_count = 0
    skipped_mods = []
    
    for mod_name, mod_info in mod_data.items():
        try:
            # 获取mod路径（可能包含子目录）
            mod_path = mod_info.get("Path", mod_name)
            mod_full_dir = mods_dir / mod_path
            
            # 创建mod目录
            mod_full_dir.mkdir(parents=True, exist_ok=True)
            
            # 创建manifest.json
            manifest = create_manifest(mod_info)
            manifest_path = mod_full_dir / "manifest.json"
            
            # 写入manifest.json（格式化输出）
            with open(manifest_path, 'w', encoding='utf-8') as f:
                json.dump(manifest, f, ensure_ascii=False, indent=2)
            
            created_count += 1
            print(f"✓ 已创建: {mod_path}")
            
        except Exception as e:
            print(f"✗ 创建 {mod_name} 时出错: {str(e)}")
            skipped_mods.append(mod_name)
    
    # 汇总信息
    print(f"\n{'='*50}")
    print(f"总结：")
    print(f"  已创建 mod 数量: {created_count}")
    print(f"  跳过的 mod 数量: {len(skipped_mods)}")
    if skipped_mods:
        print(f"  跳过的 mod: {', '.join(skipped_mods)}")
    print(f"  输出目录: {mods_dir}")
    print(f"{'='*50}\n")

if __name__ == "__main__":
    # 获取xlgChineseBack.json的路径
    script_dir = Path(__file__).parent
    json_file = script_dir / "docs" / "reference" / "xlgChineseBack.json"
    
    if not json_file.exists():
        print(f"✗ 错误：找不到 {json_file}")
        print(f"  请确保xlgChineseBack.json文件存在于 docs/reference/ 目录下")
        exit(1)
    
    print("开始生成Stardew Valley mod测试目录结构...\n")
    generate_mods_structure(str(json_file), "mods")
    print("✓ 生成完成！")
