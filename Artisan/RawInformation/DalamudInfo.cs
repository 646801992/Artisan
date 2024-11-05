using ECommons.DalamudServices;
using ECommons.Reflection;
using Newtonsoft.Json;
using System;
using System.IO;

namespace Artisan.RawInformation
{
    internal static class DalamudInfo
    {
        public static bool StagingChecked = false;
        public static bool IsStaging = false;
        public static bool IsOnStaging()
        {
#if DEBUG
            return false;
#endif
            if (StagingChecked)
            {
                return IsStaging;
            }
            if (DalamudReflector.TryGetDalamudStartInfo(out var startinfo, Svc.PluginInterface))
            {
                if (File.Exists(startinfo.ConfigurationPath))
                {
                    try
                    {
                        var file = File.ReadAllText(startinfo.ConfigurationPath);
                        var ob = JsonConvert.DeserializeObject<dynamic>(file);
                        string type = ob.DalamudBetaKind;
                        if (type is not null && !string.IsNullOrEmpty(type) && type != "release")
                        {
                            StagingChecked = true;
                            IsStaging = true;
                            return true;
                        }
                        else
                        {
                            StagingChecked = true;
                            IsStaging = false;
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Svc.Chat.PrintError($"无法确定 Dalamud 版本，因配置文件无法读取。");
                        StagingChecked = true;
                        IsStaging = false;
                        return false;
                    }
                }
                else
                {
                    StagingChecked = true;
                    IsStaging = false;
                    return false;
                }
            }
            return false;
        }
    }
}
