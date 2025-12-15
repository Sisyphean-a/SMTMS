#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
提取模组中文翻译脚本
用途：从所有模组的manifest.json文件中提取Name和Description属性，并保存到xlgChineseBack.json
"""

import os
import json
import re
import sys
from pathlib import Path
import codecs

# 设置控制台颜色
class Color:
    CYAN = '\033[96m'
    GREEN = '\033[92m'
    YELLOW = '\033[93m'
    RED = '\033[91m'
    DARK_GRAY = '\033[90m'
    DARK_CYAN = '\033[36m'
    END = '\033[0m'

def print_color(text, color):
    """打印彩色文本"""
    print(f"{color}{text}{Color.END}")

def main():
    # 获取当前脚本所在目录（应该是mods根目录）
    mods_root_path = os.path.dirname(os.path.abspath(__file__))
    
    print_color("开始扫描模组翻译...", Color.CYAN)
    
    # 存储结果的字典
    translations_data = {}
    
    # 递归查找所有manifest.json文件
    manifest_files = []
    for root, dirs, files in os.walk(mods_root_path):
        for file in files:
            if file.lower() == "manifest.json":
                manifest_files.append(os.path.join(root, file))
    
    print_color(f"找到 {len(manifest_files)} 个manifest.json文件", Color.GREEN)
    
    success_count = 0
    error_count = 0
    
    # 正则表达式模式
    name_pattern = re.compile(r'"Name"\s*:\s*"([^"]*)"')
    desc_pattern = re.compile(r'"Description"\s*:\s*"([^"]*)"')
    uniqueid_pattern = re.compile(r'"UniqueID"\s*:\s*"([^"]*)"', re.IGNORECASE)
    chinese_pattern = re.compile(r'[\u4e00-\u9fff]')
    updatekeys_pattern = re.compile(r'"UpdateKeys"\s*:\s*\[(.*?)\]', re.DOTALL)
    nexus_pattern = re.compile(r'"Nexus:(\d+)"')
    
    for manifest_file in manifest_files:
        try:
            # 获取模组文件夹名称
            mod_folder = os.path.basename(os.path.dirname(manifest_file))
            print_color(f"处理: {mod_folder}", Color.DARK_CYAN)
            
            # 尝试读取文件内容
            try:
                with codecs.open(manifest_file, 'r', encoding='utf-8') as f:
                    content = f.read()
            except UnicodeDecodeError:
                try:
                    # 尝试其他编码
                    with codecs.open(manifest_file, 'r', encoding='utf-8-sig') as f:
                        content = f.read()
                except UnicodeDecodeError:
                    with open(manifest_file, 'r', encoding='latin-1') as f:
                        content = f.read()
            
            # 移除JSON注释
            # 移除 /* */ 注释
            content = re.sub(r'/\*.*?\*/', '', content, flags=re.DOTALL)
            # 移除 // 注释
            content = re.sub(r'//.*?$', '', content, flags=re.MULTILINE)
            
            # 提取Name
            name_match = name_pattern.search(content)
            name = name_match.group(1) if name_match else None
            
            # 提取Description
            desc_match = desc_pattern.search(content)
            description = desc_match.group(1) if desc_match else None
            
            # 提取UniqueID
            uniqueid_match = uniqueid_pattern.search(content)
            unique_id = uniqueid_match.group(1) if uniqueid_match else None
            
            # 如果没有UniqueID，使用文件夹名称
            if not unique_id:
                unique_id = mod_folder
            
            # 提取UpdateKeys中的Nexus值
            nurl = None
            updatekeys_match = updatekeys_pattern.search(content)
            if updatekeys_match:
                updatekeys_content = updatekeys_match.group(1)
                nexus_match = nexus_pattern.search(updatekeys_content)
                if nexus_match:
                    nexus_id = nexus_match.group(1)
                    nurl = f"https://www.nexusmods.com/stardewvalley/mods/{nexus_id}"
            
            # 判断是否包含中文
            is_chinese = False
            if name and chinese_pattern.search(name):
                is_chinese = True
            if description and chinese_pattern.search(description):
                is_chinese = True
            
            # 如果有内容，则添加到结果中
            if name or description:
                relative_path = os.path.relpath(os.path.dirname(manifest_file), mods_root_path)
                
                translations_data[relative_path] = {
                    "UniqueID": unique_id,
                    "Name": name,
                    "Description": description,
                    "Path": relative_path,
                    "IsChinese": is_chinese,
                    "Nurl": nurl
                }
                
                print_color(f"  已提取: {mod_folder} - {name}", Color.GREEN)
                success_count += 1
            else:
                print_color(f"  未找到Name或Description: {mod_folder}", Color.YELLOW)
                error_count += 1
                
        except Exception as e:
            print_color(f"处理文件时出错: {manifest_file}", Color.RED)
            print_color(f"错误信息: {str(e)}", Color.RED)
            error_count += 1
    
    # 将结果保存到xlgChineseBack.json
    try:
        output_path = os.path.join(mods_root_path, "xlgChineseBack.json")
        
        if translations_data:
            with codecs.open(output_path, 'w', encoding='utf-8') as f:
                json.dump(translations_data, f, ensure_ascii=False, indent=2)
            
            print_color(f"\n完成! 已将翻译数据保存到: {output_path}", Color.CYAN)
            print_color(f"成功提取: {success_count} 个模组, 失败: {error_count} 个模组", Color.GREEN)
        else:
            print_color("\n警告: 没有提取到任何翻译数据", Color.YELLOW)
    
    except Exception as e:
        print_color("保存到JSON文件时出错", Color.RED)
        print_color(f"错误信息: {str(e)}", Color.RED)
    
    print_color("\n如有问题的模组，您可以手动编辑xlgChineseBack.json添加它们", Color.YELLOW)
    print_color("脚本执行完毕", Color.DARK_GRAY)

if __name__ == "__main__":
    # 在Windows上启用ANSI颜色支持
    if sys.platform == "win32":
        os.system("")  # 启用VT100模式
    main() 