using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;

namespace I2LocPatch
{
    public class I2File
    {
        // טּ
        public static char sep = '\t';
        public string Name;
        public List<string> Languages = new List<string>();
        public List<TermLine> Lines = new List<TermLine>();

        /// <summary>
        /// 将表格写入文件
        /// </summary>
        /// <param name="path"></param>
        public void WriteCSVTable(string path)
        {
            Lines.Sort();
            StringBuilder sb = new StringBuilder();

            // 写入表头
            sb.Append($"Key{sep}");
            int langLen = Languages.Count;
            for (int i = 0; i < langLen; i++)
            {
                sb.Append(Languages[i]);
                if (i < langLen-1)
                {
                    sb.Append(sep);
                }
                else
                {
                    sb.Append('\n');
                }
            }
            // 写入数据
            foreach (var line in Lines)
            {
                string key = line.Name.StrToI2Str();
                sb.Append($"{key}{sep}");
                for (int i = 0; i < langLen; i++)
                {
                    string text = line.Texts[i].StrToI2Str();
                    sb.Append(text);
                    if (i < langLen - 1)
                    {
                        sb.Append(sep);
                    }
                    else
                    {
                        sb.Append("\n");
                    }
                }
            }
            try
            {
                FileInfo fileInfo = new FileInfo(path);
                if (!fileInfo.Directory.Exists)
                {
                    fileInfo.Directory.Create();
                }
                File.WriteAllText(path, sb.ToString());
            }
            catch (Exception ex)
            {
                I2LocPatchPlugin.LogError("写入文件出现异常");
                I2LocPatchPlugin.LogError(ex.ToString());
            }
        }

        /// <summary>
        /// 从CSV文件加载语言(单语言模式)
        /// </summary>
        /// <param name="path"></param>
        public static I2File LoadFromCSVSingleMode(string path)
        {
            try
            {
                char loadSep = I2LocPatchPlugin.Instance.CommaSepWhenLoad.Value ? ',' : sep;
                FileInfo fileInfo = new FileInfo(path);
                if (fileInfo.Exists)
                {
                    var lines = File.ReadAllLines(path);
                    I2File i2File = new I2File();
                    i2File.Name = fileInfo.Name;
                    i2File.Languages.Add(i2File.Name);
                    I2LocPatchPlugin.LogInfo($"开始读取语言文件：{i2File.Name}");
                    if (lines.Length < 2)
                    {
                        I2LocPatchPlugin.LogError($"文件行数小于2行");
                        return null;
                    }
                    for (int i = 1; i < lines.Length; i++)
                    {
                        if (string.IsNullOrWhiteSpace(lines[i])) continue;
                        var kv = lines[i].Split(new char[] { loadSep }, 2);
                        if (kv.Length != 2) continue;
                        if (I2LocPatchPlugin.Instance.DevMode.Value)
                        {
                            I2LocPatchPlugin.LogInfo($"index:{i} {kv[0]} {kv[1]}");
                        }
                        if (string.IsNullOrWhiteSpace(kv[1]))
                        {
                            // 没有内容，跳过
                            continue;
                        }
                        TermLine line = new TermLine();
                        line.Name = kv[0];
                        line.Texts = new string[1];
                        line.Texts[0] = kv[1].I2StrToStr();
                        i2File.Lines.Add(line);
                    }
                    I2LocPatchPlugin.LogInfo($"{path} 读取完毕，共{i2File.Lines.Count}条翻译");
                    return i2File;
                }
                else
                {
                    I2LocPatchPlugin.LogError($"要读取的文件不存在 path:{path}");
                }
            }
            catch (Exception ex)
            {
                I2LocPatchPlugin.LogError($"读取文件出现异常 path:{path}");
                I2LocPatchPlugin.LogError(ex.ToString());
            }
            return null;
        }
    }

    public class TermLine : IComparable
    {
        public string Name;
        public string[] Texts;

        public int CompareTo(object obj)
        {
            return Name.CompareTo(((TermLine)obj).Name);
        }
    }
}
