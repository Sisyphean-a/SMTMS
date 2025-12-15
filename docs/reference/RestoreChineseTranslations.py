#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
恢复模组中文翻译脚本
用途：将xlgChineseBack.json中保存的Name和Description属性恢复到各个模组的manifest.json文件中
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
    
    # 检查备份文件是否存在
    backup_file_path = os.path.join(mods_root_path, "xlgChineseBack.json")
    if not os.path.exists(backup_file_path):
        print_color("错误: 未找到翻译备份文件 xlgChineseBack.json", Color.RED)
        print_color("请先运行 ExtractChineseTranslations.py 创建备份文件", Color.YELLOW)
        return
    
    # 读取备份数据
    try:
        with codecs.open(backup_file_path, 'r', encoding='utf-8') as f:
            translations_data = json.load(f)
        
        print_color(f"已加载备份数据，共 {len(translations_data)} 个模组的翻译", Color.CYAN)
    except Exception as e:
        print_color("读取备份文件时出错", Color.RED)
        print_color(f"错误信息: {str(e)}", Color.RED)
        return
    
    # 恢复的计数器
    restored_count = 0
    failed_count = 0
    skipped_count = 0
    
    # 中文检测正则表达式
    chinese_pattern = re.compile(r'[\u4e00-\u9fff]')
    
    # 处理每个模组
    for mod_path, mod_data in translations_data.items():
        # 构建manifest.json的完整路径
        manifest_path = os.path.join(mods_root_path, mod_path, "manifest.json")
        
        # 检查文件是否存在
        if not os.path.exists(manifest_path):
            print_color(f"跳过: 未找到 {manifest_path}", Color.YELLOW)
            skipped_count += 1
            continue
        
        try:
            print_color(f"正在处理: {mod_path}", Color.DARK_CYAN)
            
            # 读取文件内容
            try:
                with codecs.open(manifest_path, 'r', encoding='utf-8') as f:
                    content = f.read()
            except UnicodeDecodeError:
                try:
                    # 尝试其他编码
                    with codecs.open(manifest_path, 'r', encoding='utf-8-sig') as f:
                        content = f.read()
                except UnicodeDecodeError:
                    with open(manifest_path, 'r', encoding='latin-1') as f:
                        content = f.read()
            
            # 跳过空文件
            if not content.strip():
                print_color("  跳过空文件", Color.YELLOW)
                skipped_count += 1
                continue
            
            # 检查是否有中文内容需要恢复
            should_update = False
            if "IsChinese" in mod_data:
                should_update = mod_data["IsChinese"]
            else:
                # 如果没有IsChinese标记，检查是否有中文内容
                if (mod_data.get("Name") and chinese_pattern.search(mod_data["Name"])) or \
                   (mod_data.get("Description") and chinese_pattern.search(mod_data["Description"])):
                    should_update = True
            
            if not should_update:
                print_color("  跳过非中文翻译", Color.DARK_GRAY)
                skipped_count += 1
                continue
            
            # 使用正则表达式直接更新内容
            updated = False
            
            # 更新Name
            if mod_data.get("Name"):
                # 转义特殊字符
                escaped_name = mod_data["Name"].replace('\\', '\\\\').replace('"', '\\"')
                
                if re.search(r'"Name"\s*:\s*"[^"]*"', content):
                    content = re.sub(r'("Name"\s*:\s*")[^"]*(")', r'\1' + escaped_name + r'\2', content)
                    updated = True
                    print_color(f"  已更新Name: {mod_data['Name']}", Color.GREEN)
            
            # 更新Description
            if mod_data.get("Description"):
                # 转义特殊字符
                escaped_desc = mod_data["Description"].replace('\\', '\\\\').replace('"', '\\"')
                
                if re.search(r'"Description"\s*:\s*"[^"]*"', content):
                    content = re.sub(r'("Description"\s*:\s*")[^"]*(")', r'\1' + escaped_desc + r'\2', content)
                    updated = True
                    print_color("  已更新Description", Color.GREEN)
            
            # 保存更新的文件
            if updated:
                with codecs.open(manifest_path, 'w', encoding='utf-8') as f:
                    f.write(content)
                restored_count += 1
            else:
                print_color("  未找到可更新的字段", Color.YELLOW)
                failed_count += 1
                
        except Exception as e:
            print_color(f"更新 {mod_path} 时出错", Color.RED)
            print_color(f"错误信息: {str(e)}", Color.RED)
            failed_count += 1
    
    print_color("\n恢复完成!", Color.CYAN)
    print_color(f"成功更新了 {restored_count} 个模组的翻译", Color.GREEN)
    print_color(f"跳过了 {skipped_count} 个模组", Color.DARK_GRAY)
    if failed_count > 0:
        print_color(f"有 {failed_count} 个模组更新失败", Color.YELLOW)
    
    print_color("\n对于更新失败的模组，您可以手动编辑它们的manifest.json文件", Color.YELLOW)
    print_color("脚本执行完毕", Color.DARK_GRAY)

if __name__ == "__main__":
    # 在Windows上启用ANSI颜色支持
    if sys.platform == "win32":
        os.system("")  # 启用VT100模式
    main() 