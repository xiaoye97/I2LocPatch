using TMPro;
using I2.Loc;
using BepInEx;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;


namespace I2LocPatch
{
    /// <summary>
    /// 这里是针对游戏中没有翻译组件的地方进行翻译的处理
    /// </summary>
    public class ModLocalization
    {
        public static ModLocalization Instance;
        public List<ModLocData> LocList = new List<ModLocData>();
        public List<TextLocData> NormalTextList = new List<TextLocData>();
        public List<ModLocDataRuntime> LocRuntimeList = new List<ModLocDataRuntime>();

        /// <summary>
        /// 深度0名字集合 所有翻译内容的绑定物体的第0层的名字
        /// </summary>
        public List<string> Depth0NameList = new List<string>();

        public ModLocalization()
        {
            Instance = this;
        }

        /// <summary>
        /// 加载Mod翻译
        /// </summary>
        public void LoadModLoc()
        {
            string path = $"{Paths.PluginPath}/I2LocPatch/TMPTextLoc.json";
            FileInfo fileInfo = new FileInfo(path);
            // 如果文件不存在，则创建一个默认的文件
            if (!fileInfo.Exists)
            {
                if (!fileInfo.Directory.Exists)
                {
                    fileInfo.Directory.Create();
                }
                CreateDefault(path);
            }
            else
            {
                string json = File.ReadAllText(path);
                LocList = JsonConvert.DeserializeObject<List<ModLocData>>(json);
                if (LocList == null)
                {
                    CreateDefault(path);
                }
            }
            // 根据LocList生成LocRuntimeList
            foreach (var loc in LocList)
            {
                ModLocDataRuntime runtime = new ModLocDataRuntime();
                runtime.Text = loc.Text;
                if (loc.Bind.Contains("/"))
                {
                    var binds = loc.Bind.Split('/');
                    for (int i = binds.Length - 1; i >= 0; i--)
                    {
                        runtime.Bind.Add(binds[i]);
                    }
                }
                else
                {
                    runtime.Bind.Add(loc.Bind);
                }
                LocRuntimeList.Add(runtime);
            }
            // 生成索引
            foreach (var loc in LocRuntimeList)
            {
                string depth0 = loc.Bind[0];
                if (!Depth0NameList.Contains(depth0))
                {
                    Depth0NameList.Add(depth0);
                }
            }
        }

        /// <summary>
        /// 加载文本对照翻译
        /// </summary>
        public void LoadTextLoc()
        {
            NormalTextList = TextLocData.LoadFromTxtFile($"{Paths.PluginPath}/I2LocPatch/NormalTextLoc.txt");
        }

        private void CreateDefault(string path)
        {
            LocList = new List<ModLocData>();
            ModLocData data = new ModLocData();
            data.Text = "Text";
            data.Bind = "BindPath";
            LocList.Add(data);
            string json = JsonConvert.SerializeObject(LocList, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// 检查并处理文本
        /// </summary>
        /// <param name="tmp"></param>
        public void FixTMPText(TextMeshProUGUI tmp)
        {
            // 如果名字不在列表则不处理，节约性能
            if (!Depth0NameList.Contains(tmp.name)) return;
            // 如果此文本已经有翻译组件，则跳过
            if (tmp.GetComponent<Localize>() != null) return;

            bool hasText = false;
            string text = "";
            // 开始匹配翻译
            foreach (var loc in LocRuntimeList)
            {
                // 第0层匹配的话则继续，否则直接跳过
                if (loc.Bind[0] == tmp.name)
                {
                    // 如果只有一个绑定，则说明已经找到目标
                    if (loc.Bind.Count == 1)
                    {
                        hasText = true;
                        text = loc.Text.I2StrToStr();
                        break;
                    }
                    Transform p = tmp.transform.parent;
                    for (int i = 1; i < loc.Bind.Count; i++)
                    {
                        if (p != null && p.name == loc.Bind[i])
                        {
                            p = p.parent;
                            if (i == loc.Bind.Count - 1)
                            {
                                hasText = true;
                                text = loc.Text.I2StrToStr();
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            if (hasText)
            {
                tmp.text = text;
            }
            // 如果既没有翻译组件，也没有匹配的路径，则查找是否有文本对照翻译
            else
            {
                if (!string.IsNullOrWhiteSpace(tmp.text))
                {
                    string ori = tmp.text.StrToI2Str();
                    foreach (var normal in NormalTextList)
                    {
                        if (ori == normal.Ori)
                        {
                            tmp.text = normal.Loc.I2StrToStr();
                            break;
                        }
                    }
                }
            }
        }
    }

    [System.Serializable]
    public class ModLocData
    {
        public string Text;
        public string Bind;
    }

    [System.Serializable]
    public class ModLocDataRuntime
    {
        public string Text;
        public List<string> Bind = new List<string>();
    }

    public static class TextEx
    {
        public static string newLineChar = "þ";
        public static string StrToI2Str(this string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return str;
            return str.Replace("\n", newLineChar).Replace("\r", newLineChar);
        }

        public static string I2StrToStr(this string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return str;
            return str.Replace(newLineChar, "\n");
        }
    }
}
