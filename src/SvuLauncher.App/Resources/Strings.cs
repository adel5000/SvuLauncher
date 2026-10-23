using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace SvuLauncher.App.Resources;

/// <summary>
/// Typed access to Strings.resx / Strings.ar.resx. Each property name is its resource key;
/// a test checks that every property exists in both files. XAML uses {x:Static res:Strings.Key}.
/// </summary>
public static class Strings
{
    public static ResourceManager ResourceManager { get; } =
        new("SvuLauncher.App.Resources.Strings", typeof(Strings).Assembly);

    public static string AppTitle => Get();
    public static string Login_Subtitle => Get();
    public static string Login_UserName => Get();
    public static string Login_Password => Get();
    public static string Login_RememberMe => Get();
    public static string Login_SignIn => Get();
    public static string Login_ErrorRejected => Get();
    public static string Login_ErrorUnreachable => Get();
    public static string Sessions_Title => Get();
    public static string Sessions_SignOut => Get();
    public static string Sessions_Settings => Get();
    public static string Sessions_Refresh => Get();
    public static string Sessions_Empty => Get();
    public static string Sessions_EmptyHint => Get();
    public static string Sessions_ChooseProgram => Get();
    public static string Sessions_ChooseProgramHint => Get();
    public static string Sessions_LoadAll => Get();
    public static string Filter_Scope => Get();
    public static string Scope_MyClasses => Get();
    public static string Scope_AllMyCourseClasses => Get();
    public static string Scope_ByProgramAndCourse => Get();
    public static string Filter_Program => Get();
    public static string Filter_Course => Get();
    public static string AllPrograms => Get();
    public static string AllCourses => Get();
    public static string Session_Join => Get();
    public static string Session_Details => Get();
    public static string Role_Student => Get();
    public static string Role_Tutor => Get();
    public static string Greeting => Get();
    public static string Status_SessionCount => Get();
    public static string StatusFilter_Label => Get();
    public static string StatusFilter_All => Get();
    public static string SessionState_InProgress => Get();
    public static string SessionState_Planned => Get();
    public static string SessionState_Ended => Get();
    public static string Session_StartedAt => Get();
    public static string Session_OutsideMyClasses => Get();
    public static string Session_OutsideMyClassesHint => Get();
    public static string Status_SessionCountFiltered => Get();
    public static string Sessions_SortHint => Get();
    public static string Status_ProgramsFailed => Get();
    public static string Status_CoursesFailed => Get();
    public static string Status_NonNumericProgram => Get();
    public static string Status_SessionsFailed => Get();
    public static string Status_NotStarted => Get();
    public static string Status_Running => Get();
    public static string Status_LaunchFailed => Get();
    public static string Status_Ended => Get();
    public static string Status_EndedWithCode => Get();
    public static string Status_ClientFailed => Get();
    public static string Settings_Title => Get();
    public static string Settings_Microphone => Get();
    public static string Settings_MicrophoneOn => Get();
    public static string Settings_MicrophoneLevel => Get();
    public static string Settings_NoMicrophone => Get();
    public static string Settings_MicrophoneError => Get();
    public static string Settings_MuteOnLaunch => Get();
    public static string Settings_MuteOnLaunchHint => Get();
    public static string Settings_Appearance => Get();
    public static string Settings_Language => Get();
    public static string Settings_Theme => Get();
    public static string Settings_RestartRequired => Get();
    public static string Language_System => Get();
    public static string Language_English => Get();
    public static string Language_Arabic => Get();
    public static string Theme_System => Get();
    public static string Theme_Light => Get();
    public static string Theme_Dark => Get();
    public static string Mic_Mute => Get();
    public static string Mic_Unmute => Get();
    public static string Close_Confirm => Get();
    public static string Dialog_Yes => Get();
    public static string Dialog_No => Get();
    public static string Startup_SettingsError => Get();

    public static string Format(string template, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, template, args);

    private static string Get([CallerMemberName] string key = "") =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
