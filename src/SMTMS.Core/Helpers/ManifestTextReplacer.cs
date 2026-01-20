using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace SMTMS.Core.Helpers;

/// <summary>
/// 纯函数工具类 - 负责 manifest.json 文本的正则替换逻辑
/// 这是一个无状态的静态类,所有方法都是纯函数,便于单元测试
/// </summary>
public static partial class ManifestTextReplacer
{
    // 🔥 正则表达式缓存优化 - 使用 GeneratedRegex (C# 11+)
    [GeneratedRegex(@"[\u4e00-\u9fff]")]
    private static partial Regex ChinesePatternRegex();

    [GeneratedRegex(@"[\u4e00-\u9fa5]")]
    private static partial Regex ChineseSimplifiedRegex();

    [GeneratedRegex("""
                    "Name"\s*:\s*"[^"]*"
                    """)]
    private static partial Regex NameFieldRegex();

    [GeneratedRegex("""("Name"\s*:\s*")[^"]*(")""")]
    private static partial Regex NameReplaceRegex();

    [GeneratedRegex("""
                    "Description"\s*:\s*"[^"]*"
                    """)]
    private static partial Regex DescriptionFieldRegex();

    [GeneratedRegex("""("Description"\s*:\s*")[^"]*(")""")]
    private static partial Regex DescriptionReplaceRegex();

    /// <summary>
    /// 检测文本是否包含中文字符
    /// </summary>
    public static bool ContainsChinese(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        return ChinesePatternRegex().IsMatch(text);
    }

    /// <summary>
    /// 检测文本是否包含简体中文字符
    /// </summary>
    public static bool ContainsSimplifiedChinese(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        return ChineseSimplifiedRegex().IsMatch(text);
    }

    /// <summary>
    /// 替换 manifest.json 内容中的 Name 字段
    /// 使用正则表达式进行非破坏性替换,保留 JSON 注释
    /// </summary>
    /// <param name="jsonContent">原始 JSON 内容</param>
    /// <param name="newName">新的 Name 值</param>
    /// <returns>替换后的 JSON 内容,如果未找到 Name 字段则返回原内容</returns>
    public static string ReplaceName(string jsonContent, string newName)
    {
        if (string.IsNullOrEmpty(jsonContent))
            return jsonContent;

        if (string.IsNullOrEmpty(newName))
            return jsonContent;

        // 转义特殊字符(使用 Newtonsoft.Json 的转义逻辑)
        var escapedName = JsonConvert.ToString(newName).Trim('"');

        // 检查是否存在 Name 字段
        if (!NameFieldRegex().IsMatch(jsonContent))
            return jsonContent;

        // 执行替换
        var newContent = NameReplaceRegex().Replace(jsonContent, $"${{1}}{escapedName}${{2}}");
        return newContent;
    }

    /// <summary>
    /// 替换 manifest.json 内容中的 Description 字段
    /// 使用正则表达式进行非破坏性替换,保留 JSON 注释
    /// </summary>
    /// <param name="jsonContent">原始 JSON 内容</param>
    /// <param name="newDescription">新的 Description 值</param>
    /// <returns>替换后的 JSON 内容,如果未找到 Description 字段则返回原内容</returns>
    public static string ReplaceDescription(string jsonContent, string newDescription)
    {
        if (string.IsNullOrEmpty(jsonContent))
            return jsonContent;

        if (string.IsNullOrEmpty(newDescription))
            return jsonContent;

        // 转义特殊字符
        var escapedDesc = JsonConvert.ToString(newDescription).Trim('"');

        // 检查是否存在 Description 字段
        if (!DescriptionFieldRegex().IsMatch(jsonContent))
            return jsonContent;

        // 执行替换
        var newContent = DescriptionReplaceRegex().Replace(jsonContent, $"${{1}}{escapedDesc}${{2}}");
        return newContent;
    }

    /// <summary>
    /// 同时替换 Name 和 Description 字段
    /// </summary>
    public static string ReplaceNameAndDescription(string jsonContent, string? newName, string? newDescription)
    {
        var result = jsonContent;

        if (!string.IsNullOrEmpty(newName))
        {
            result = ReplaceName(result, newName);
        }

        if (!string.IsNullOrEmpty(newDescription))
        {
            result = ReplaceDescription(result, newDescription);
        }

        return result;
    }

    /// <summary>
    /// 检查 Name 字段是否存在
    /// </summary>
    public static bool HasNameField(string jsonContent)
    {
        if (string.IsNullOrEmpty(jsonContent))
            return false;

        return NameFieldRegex().IsMatch(jsonContent);
    }

    /// <summary>
    /// 检查 Description 字段是否存在
    /// </summary>
    public static bool HasDescriptionField(string jsonContent)
    {
        if (string.IsNullOrEmpty(jsonContent))
            return false;

        return DescriptionFieldRegex().IsMatch(jsonContent);
    }

    /// <summary>
    /// 在 UpdateKeys 数组中添加或更新 Nexus ID
    /// 如果不存在 UpdateKeys，则创建；如果存在，则添加或替换 Nexus 条目
    /// </summary>
    /// <param name="jsonContent">原始 JSON 内容</param>
    /// <param name="nexusId">Nexus 模组 ID（纯数字）</param>
    /// <returns>更新后的 JSON 内容</returns>
    public static string AddOrUpdateNexusId(string jsonContent, string? nexusId)
    {
        if (string.IsNullOrEmpty(jsonContent))
            return jsonContent;

        if (string.IsNullOrWhiteSpace(nexusId))
            return jsonContent;

        // 匹配现有的 UpdateKeys 数组
        var updateKeysRegex = new Regex(@"""UpdateKeys""\s*:\s*\[([^\]]*)\]", RegexOptions.Singleline);
        var nexusKeyRegex = new Regex(@"""Nexus:\s*\d+""", RegexOptions.IgnoreCase);
        
        var match = updateKeysRegex.Match(jsonContent);
        
        if (match.Success)
        {
            // UpdateKeys 已存在
            var existingContent = match.Groups[1].Value;
            var newNexusKey = $"\"Nexus:{nexusId}\"";
            
            if (nexusKeyRegex.IsMatch(existingContent))
            {
                // 替换现有的 Nexus 条目
                var newContent = nexusKeyRegex.Replace(existingContent, newNexusKey);
                return jsonContent.Substring(0, match.Groups[1].Index) + newContent + 
                       jsonContent.Substring(match.Groups[1].Index + match.Groups[1].Length);
            }
            else
            {
                // 添加到现有数组
                var trimmed = existingContent.Trim();
                string newArrayContent;
                if (string.IsNullOrEmpty(trimmed))
                {
                    newArrayContent = " " + newNexusKey + " ";
                }
                else
                {
                    newArrayContent = existingContent.TrimEnd() + ", " + newNexusKey + " ";
                }
                return jsonContent.Substring(0, match.Groups[1].Index) + newArrayContent + 
                       jsonContent.Substring(match.Groups[1].Index + match.Groups[1].Length);
            }
        }
        else
        {
            // UpdateKeys 不存在，在 UniqueID 后面添加
            // 优化：检查 UniqueID 后是否已经有逗号，避免 JSON 格式错误
            var uniqueIdRegex = new Regex(@"(""UniqueID""\s*:\s*""[^""]*"")(\s*,?)");
            var uniqueIdMatch = uniqueIdRegex.Match(jsonContent);
            
            if (uniqueIdMatch.Success)
            {
                var hasTrailingComma = uniqueIdMatch.Groups[2].Value.Contains(',');
                var insertPos = uniqueIdMatch.Index + uniqueIdMatch.Length;
                
                string newUpdateKeys;
                if (hasTrailingComma)
                {
                    // Case: "UniqueID": "...", "Name": "..."
                    // 已经有逗号了，我们在逗号后面插入，并且需要在新插入的行末尾加逗号
                    // 结果: "UniqueID": "...",
                    //       "UpdateKeys": [ "Nexus:xxx" ], 
                    //       "Name": "..."
                    newUpdateKeys = $"\n  \"UpdateKeys\": [ \"Nexus:{nexusId}\" ],";
                }
                else
                {
                    // Case: "UniqueID": "..." (它是最后一个元素)
                    // 没有逗号，我们需要在前面加逗号
                    // 结果: "UniqueID": "...",
                    //       "UpdateKeys": [ "Nexus:xxx" ]
                    newUpdateKeys = $",\n  \"UpdateKeys\": [ \"Nexus:{nexusId}\" ]";
                }
                
                return jsonContent.Insert(insertPos, newUpdateKeys);
            }
        }
        
        return jsonContent;
    }
}
