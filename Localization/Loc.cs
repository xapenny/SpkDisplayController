using System.Globalization;

namespace SpkDisplayController.Localization;

/// <summary>
/// 轻量 i18n：内置 5 种语言表，按系统区域自动选择，回退到英语。
/// </summary>
internal static class Loc
{
    public static string LanguageCode { get; private set; } = "en";

    private static readonly Dictionary<string, Dictionary<string, string>> Tables =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new()
            {
                ["AppName"] = "SpkDisplayController",
                ["TrayTooltip"] = "SpkDisplayController",
                ["StartWithWindows"] = "Start with Windows",
                ["RefreshMonitors"] = "Refresh monitor list",
                ["About"] = "About",
                ["Exit"] = "Exit",
                ["Hdr"] = "HDR",
                ["Brightness"] = "Brightness",
                ["SoftOff"] = "Soft off",
                ["Unavailable"] = "(Unavailable)",
                ["AlreadySoftOff"] = "(Yes)",
                ["NoMonitors"] = "(No monitors detected)",
                ["AboutTitle"] = "About SpkDisplayController",
                ["Version"] = "Version",
                ["Runtime"] = "Runtime",
                ["MonitorCount"] = "Monitors",
                ["SoftOffOk"] = "Soft off sent to {0}.",
                ["SoftOffFail"] = "Failed to soft off {0}.",
                ["HdrFail"] = "Failed to change HDR on {0}.",
                ["BrightnessFail"] = "Failed to set brightness on {0}.",
                ["AboutBody"] = "Per-monitor HDR, brightness and soft-off control from the system tray.\n\nSoft off keeps the monitor connected in Windows: no resolution change, no layout reshuffle.",
            },

            ["zh-Hans"] = new()
            {
                ["AppName"] = "SpkDisplayController",
                ["TrayTooltip"] = "SpkDisplayController",
                ["StartWithWindows"] = "随 Windows 启动",
                ["RefreshMonitors"] = "刷新显示器列表",
                ["About"] = "关于",
                ["Exit"] = "退出",
                ["Hdr"] = "HDR",
                ["Brightness"] = "亮度",
                ["SoftOff"] = "软关闭",
                ["Unavailable"] = "(不可用)",
                ["AlreadySoftOff"] = "(是)",
                ["NoMonitors"] = "(未检测到显示器)",
                ["AboutTitle"] = "关于 SpkDisplayController",
                ["Version"] = "版本",
                ["Runtime"] = "运行时",
                ["MonitorCount"] = "显示器",
                ["SoftOffOk"] = "已向 {0} 发送软关闭。",
                ["SoftOffFail"] = "无法对 {0} 执行软关闭。",
                ["HdrFail"] = "无法切换 {0} 的 HDR。",
                ["BrightnessFail"] = "无法设置 {0} 的亮度。",
                ["AboutBody"] = "从系统托盘对每台显示器单独控制 HDR、亮度与软关闭。\n\n软关闭不会让 Windows 判定显示器断开：分辨率、排列与窗口位置均保持不变。",
            },

            ["ja"] = new()
            {
                ["AppName"] = "SpkDisplayController",
                ["TrayTooltip"] = "SpkDisplayController",
                ["StartWithWindows"] = "Windows と共に起動",
                ["RefreshMonitors"] = "モニターリストを更新",
                ["About"] = "バージョン情報",
                ["Exit"] = "終了",
                ["Hdr"] = "HDR",
                ["Brightness"] = "明るさ",
                ["SoftOff"] = "ソフトオフ",
                ["Unavailable"] = "(使用不可)",
                ["AlreadySoftOff"] = "(はい)",
                ["NoMonitors"] = "(モニターが検出されません)",
                ["AboutTitle"] = "SpkDisplayController について",
                ["Version"] = "バージョン",
                ["Runtime"] = "ランタイム",
                ["MonitorCount"] = "モニター",
                ["SoftOffOk"] = "{0} にソフトオフを送信しました。",
                ["SoftOffFail"] = "{0} のソフトオフに失敗しました。",
                ["HdrFail"] = "{0} の HDR を切り替えられませんでした。",
                ["BrightnessFail"] = "{0} の明るさを設定できませんでした。",
                ["AboutBody"] = "システムトレイからモニターごとに HDR・明るさ・ソフトオフを制御します。\n\nソフトオフしても Windows はモニターを接続中と認識するため、解像度や配置は変わりません。",
            },

            ["ko"] = new()
            {
                ["AppName"] = "SpkDisplayController",
                ["TrayTooltip"] = "SpkDisplayController",
                ["StartWithWindows"] = "Windows 시작 시 실행",
                ["RefreshMonitors"] = "모니터 목록 새로 고침",
                ["About"] = "정보",
                ["Exit"] = "종료",
                ["Hdr"] = "HDR",
                ["Brightness"] = "밝기",
                ["SoftOff"] = "소프트 오프",
                ["Unavailable"] = "(사용할 수 없음)",
                ["AlreadySoftOff"] = "(예)",
                ["NoMonitors"] = "(모니터를 찾을 수 없음)",
                ["AboutTitle"] = "SpkDisplayController 정보",
                ["Version"] = "버전",
                ["Runtime"] = "런타임",
                ["MonitorCount"] = "모니터",
                ["SoftOffOk"] = "{0}에 소프트 오프를 보냈습니다.",
                ["SoftOffFail"] = "{0} 소프트 오프에 실패했습니다.",
                ["HdrFail"] = "{0}의 HDR을 전환할 수 없습니다.",
                ["BrightnessFail"] = "{0}의 밝기를 설정할 수 없습니다.",
                ["AboutBody"] = "시스템 트레이에서 모니터별로 HDR, 밝기, 소프트 오프를 제어합니다.\n\n소프트 오프 후에도 Windows는 모니터가 연결된 것으로 인식하므로 해상도와 배치가 바뀌지 않습니다.",
            },

            ["ru"] = new()
            {
                ["AppName"] = "SpkDisplayController",
                ["TrayTooltip"] = "SpkDisplayController",
                ["StartWithWindows"] = "Запускать вместе с Windows",
                ["RefreshMonitors"] = "Обновить список мониторов",
                ["About"] = "О программе",
                ["Exit"] = "Выход",
                ["Hdr"] = "HDR",
                ["Brightness"] = "Яркость",
                ["SoftOff"] = "Мягкое выключение",
                ["Unavailable"] = "(Недоступно)",
                ["AlreadySoftOff"] = "(Да)",
                ["NoMonitors"] = "(Мониторы не обнаружены)",
                ["AboutTitle"] = "О программе SpkDisplayController",
                ["Version"] = "Версия",
                ["Runtime"] = "Среда выполнения",
                ["MonitorCount"] = "Мониторы",
                ["SoftOffOk"] = "Мягкое выключение отправлено на {0}.",
                ["SoftOffFail"] = "Не удалось выполнить мягкое выключение {0}.",
                ["HdrFail"] = "Не удалось переключить HDR на {0}.",
                ["BrightnessFail"] = "Не удалось установить яркость на {0}.",
                ["AboutBody"] = "Управление HDR, яркостью и мягким выключением для каждого монитора из системного трея.\n\nПосле мягкого выключения Windows продолжает считать монитор подключённым: разрешение и расположение не меняются.",
            },
        };

    public static void Initialize()
    {
        var culture = CultureInfo.CurrentUICulture;

        var code = culture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "zh" => "zh-Hans",
            "ja" => "ja",
            "ko" => "ko",
            "ru" => "ru",
            _ => "en"
        };

        LanguageCode = Tables.ContainsKey(code) ? code : "en";
    }

    public static string Get(string key)
    {
        if (Tables.TryGetValue(LanguageCode, out var table) && table.TryGetValue(key, out var value))
            return value;

        return Tables["en"].TryGetValue(key, out var fallback) ? fallback : key;
    }

    public static string Get(string key, params object[] args)
    {
        var format = Get(key);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }
}
