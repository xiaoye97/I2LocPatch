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
using TMPro;

namespace I2LocPatch
{
    [BepInPlugin("xiaoye97.I2LocPatch", "I2LocPatch", "1.4.0")]
    public class I2LocPatchPlugin : BaseUnityPlugin
    {
        public static I2LocPatchPlugin Instance;
        public ModLocalization ModLoc;
        public ConfigEntry<string> TargetLanguage;
        public ConfigEntry<string> TargetCsv;
        public ConfigEntry<bool> DevMode, DontLoadCsvOnDevMode;
        public ConfigEntry<bool> CommaSepWhenLoad;
        public ConfigEntry<bool> ShowLocCall;

        public static IJson Json
        {
            get
            {
                if (_json == null)
                {
                    _json = new LitJsonHelper();
                }
                return _json;
            }
        }

        private static IJson _json;

        private void Awake()
        {
            Instance = this;
            DevMode = Config.Bind<bool>("Dev", "DevMode", false, "开发模式时，按下Ctrl+Keypad1进行Dump文本");
            DontLoadCsvOnDevMode = Config.Bind<bool>("Dev", "DontLoadCsvOnDevMode", true, "开发模式时，不自动加载翻译文本，而是使用Ctrl+Keypad2手动加载");
            TargetLanguage = Config.Bind<string>("config", "TargetLanguage", "Chinese", "目标语言");
            TargetCsv = Config.Bind<string>("config", "TargetCsv", "", "目标csv文件，默认为空，如果不为空的话，则只会加载指定的csv文件");
            CommaSepWhenLoad = Config.Bind<bool>("config", "CommaSepWhenLoad", true, "在加载csv时使用逗号分隔而不是制表符");
            ShowLocCall = Config.Bind<bool>("config", "ShowLocCall", false, "在控制台显示翻译的调用");
            ModLoc = new ModLocalization();
            ModLoc.LoadModLoc();
            ModLoc.LoadTextLoc();
            Harmony.CreateAndPatchAll(typeof(I2LocPatchPlugin));
        }

        private void Start()
        {
            if (DevMode.Value && DontLoadCsvOnDevMode.Value)
            {
                return;
            }
            Invoke("LoadCsv", 1f);
        }

        private void Update()
        {
            if (DevMode.Value)
            {
                // Ctrl + 小键盘1 Dump I2翻译表格
                if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Keypad1))
                {
                    DumpAllLocRes();
                }
                // Ctrl + 小键盘2 加载 I2翻译表格
                if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Keypad2))
                {
                    LoadCsv();
                }
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(TextMeshProUGUI), "OnEnable")]
        public static void TextMeshProUGUIOnEnablePatch(TextMeshProUGUI __instance)
        {
            ModLocalization.Instance.FixTMPText(__instance);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(LocalizationManager), "GetTranslation")]
        public static void LocalizationManager_GetTranslation_Patch(string Term, string __result)
        {
            if (Instance.ShowLocCall.Value)
            {
                LogInfo($"调用翻译:Key: {Term} \t结果: {__result}");
            }
        }

        public void LoadCsv()
        {
            var i2Files = LoadAllI2CsvSingleMode(TargetCsv.Value);
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
                                            SetTranslation(asset, line, index);
                                        }
                                        else
                                        {
                                            SetTranslation2(asset, line, langCount);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    LocalizationManager.CurrentLanguage = TargetLanguage.Value;
                    sw.Stop();
                    LogInfo($"LoadCsv加载完毕 耗时{sw.ElapsedMilliseconds}ms");
                }
            }
        }

        private void SetTranslation(LanguageSourceAsset asset, TermLine line, int index)
        {
            var term = asset.SourceData.GetTermData(line.Name);
            term.SetTranslation(index, line.Texts[0]);
        }

        private void SetTranslation2(LanguageSourceAsset asset, TermLine line, int langCount)
        {
            TermData term = asset.SourceData.AddTerm(line.Name);
            term.TermType = eTermType.Text;
            List<string> languages = new List<string>();
            for (int i = 0; i < langCount; i++)
            {
                languages.Add(line.Texts[0]);
            }
            term.Languages = languages.ToArray();
        }

        public static void LogInfo(string log)
        {
            Instance.Logger.LogInfo(log);
        }

        public static void LogError(string log)
        {
            Instance.Logger.LogError(log);
        }

        public void DumpAllLocRes(List<string> ignoreTermList = null)
        {
            var mResourcesCache = Traverse.Create(ResourceManager.pInstance).Field("mResourcesCache").GetValue<Dictionary<string, UnityEngine.Object>>();
            if (mResourcesCache != null)
            {
                foreach (var kv in mResourcesCache)
                {
                    // 语言资源
                    if (kv.Value != null && kv.Value is LanguageSourceAsset)
                    {
                        DumpLocRes(kv.Value as LanguageSourceAsset, ignoreTermList);
                    }
                }
            }
            LogInfo($"全部Dump完毕");
        }

        public void DumpLocRes(LanguageSourceAsset asset, List<string> ignoreTermList = null)
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
                    if (ignoreTermList != null && ignoreTermList.Contains(term.Term))
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
            LogInfo($"Dump {i2File.Name}完毕 共{i2File.Lines.Count}条");
        }

        /// <summary>
        /// 加载所有的翻译后的CSV，搜索plugins/I2LocPatch文件夹
        /// </summary>
        public List<I2File> LoadAllI2CsvSingleMode(string targetCsv = "")
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
                    if (string.IsNullOrWhiteSpace(targetCsv) || files[i].Name == targetCsv)
                    {
                        I2File file = I2File.LoadFromCSVSingleMode(files[i].FullName);
                        if (file != null)
                        {
                            i2Files.Add(file);
                        }
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