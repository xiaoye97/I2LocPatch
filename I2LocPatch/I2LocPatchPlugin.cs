using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using HarmonyLib;
using I2.Loc;
using UnityEngine;
using System.IO;
using BepInEx.Configuration;

namespace I2LocPatch
{
    [BepInPlugin("xiaoye97.I2LocPatch", "I2LocPatch", "1.0.0")]
    public class I2LocPatchPlugin : BaseUnityPlugin
    {
        public static I2LocPatchPlugin Instance;
        public ConfigEntry<string> TargetLanguage;
        public ConfigEntry<bool> DevMode, DontLoadCsvOnDevMode;
        public ConfigEntry<bool> CommaSepWhenLoad;

        void Awake()
        {
            Instance = this;
            DevMode = Config.Bind<bool>("Dev", "DevMode", false, "开发模式时，按下Ctrl+Keypad1进行Dump文本");
            DontLoadCsvOnDevMode = Config.Bind<bool>("Dev", "DontLoadCsvOnDevMode", true, "开发模式时，不自动加载翻译文本，而是使用Ctrl+Keypad2手动加载");
            TargetLanguage = Config.Bind<string>("config", "TargetLanguage", "Chinese", "目标语言");
            CommaSepWhenLoad = Config.Bind<bool>("config", "CommaSepWhenLoad", true, "在加载csv时使用逗号分隔而不是制表符");
        }

        void Start()
        {
            if (DevMode.Value && DontLoadCsvOnDevMode.Value)
            {
                return;
            }
            Invoke("LoadCsv", 1f);
        }

        void Update()
        {
            if (DevMode.Value)
            {
                if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Keypad1))
                {
                    DumpAllLocRes();
                }
                if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Keypad2))
                {
                    LoadCsv();
                }
            }
        }

        public void LoadCsv()
        {
            var i2Files = LoadAllI2CsvSingleMode();
            if (i2Files != null && i2Files.Count > 0)
            {
                var mResourcesCache = Traverse.Create(ResourceManager.pInstance).Field("mResourcesCache").GetValue<Dictionary<string, UnityEngine.Object>>();
                if (mResourcesCache != null)
                {
                    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                    sw.Start();
                    foreach (var kv in mResourcesCache)
                    {
                        // 语言资源
                        if (kv.Value != null && kv.Value is LanguageSourceAsset)
                        {
                            var asset = kv.Value as LanguageSourceAsset;
                            if (asset.SourceData.GetLanguages().Contains(TargetLanguage.Value))
                            {
                                int langCount = asset.SourceData.GetLanguages().Count;
                                int index = asset.SourceData.GetLanguageIndex(TargetLanguage.Value);
                                foreach (var i2File in i2Files)
                                {
                                    foreach (var line in i2File.Lines)
                                    {
                                        if (asset.SourceData.ContainsTerm(line.Name))
                                        {
                                            var term = asset.SourceData.GetTermData(line.Name);
                                            term.SetTranslation(index, line.Texts[0]);
                                        }
                                        else
                                        {
                                            TermData term = new TermData();
                                            term.TermType = eTermType.Text;
                                            List<string> languages = new List<string>();
                                            for (int i = 0; i < langCount; i++)
                                            {
                                                languages.Add(line.Texts[i]);
                                            }
                                            term.Languages = languages.ToArray();
                                        }
                                    }
                                }
                            }
                        }
                    }
                    LocalizationManager.CurrentLanguage = "Chinese";
                    sw.Stop();
                    LogInfo($"LoadCsv加载完毕 耗时{sw.ElapsedMilliseconds}ms");
                }
            }
        }

        public static void LogInfo(string log)
        {
            Instance.Logger.LogInfo(log);
        }

        public static void LogError(string log)
        {
            Instance.Logger.LogError(log);
        }

        public void DumpAllLocRes()
        {
            var mResourcesCache = Traverse.Create(ResourceManager.pInstance).Field("mResourcesCache").GetValue<Dictionary<string, UnityEngine.Object>>();
            if (mResourcesCache != null)
            {
                foreach (var kv in mResourcesCache)
                {
                    // 语言资源
                    if (kv.Value != null && kv.Value is LanguageSourceAsset)
                    {
                        DumpLocRes(kv.Value as LanguageSourceAsset);
                    }
                }
            }
            LogInfo($"全部Dump完毕");
        }

        public void DumpLocRes(LanguageSourceAsset asset)
        {
            I2File i2File = new I2File();
            i2File.Name = asset.name;
            i2File.Languages = asset.SourceData.GetLanguages();
            //int langLen = i2File.Languages.Count;
            int termLen = asset.SourceData.mTerms.Count;
            for (int i = 0; i < termLen; i++)
            {
                var term = asset.SourceData.mTerms[i];
                if (term.TermType == eTermType.Text)
                {
                    if (string.IsNullOrWhiteSpace(term.Term))
                    {
                        continue;
                    }
                    TermLine line = new TermLine();
                    line.Name = term.Term;
                    line.Texts = term.Languages;
                    i2File.Lines.Add(line);
                }
            }
            //i2File.WriteTest($"{Paths.GameRootPath}/I2/Test.txt");
            i2File.WriteCSVTable($"{Paths.GameRootPath}/I2/{i2File.Name}.csv");
            LogInfo($"Dump {i2File.Name}完毕");
        }

        /// <summary>
        /// 加载所有的翻译后的CSV，搜索plugins/I2LocPatch文件夹
        /// </summary>
        public List<I2File> LoadAllI2CsvSingleMode()
        {
            try
            {
                DirectoryInfo dir = new DirectoryInfo($"{Paths.PluginPath}/I2LocPatch");
                if (!dir.Exists)
                {
                    dir.Create();
                }
                var files = dir.GetFiles("*.csv");
                List<I2File> i2Files = new List<I2File>();
                for (int i = 0; i < files.Length; i++)
                {
                    I2File file = I2File.LoadFromCSVSingleMode(files[i].FullName);
                    if (file != null)
                    {
                        i2Files.Add(file);
                    }
                }
                return i2Files;
            }
            catch (Exception ex)
            {
                LogError(ex.ToString());
            }
            return null;
        }
    }
}
