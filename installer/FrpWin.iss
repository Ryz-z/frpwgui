; ============================================================================
;  FrpWin 内网穿透套装 —— Inno Setup 安装脚本
;  编译：ISCC.exe /DStageDir=... /DOutputDir=... FrpWin.iss
; ============================================================================

#ifndef StageDir
  #define StageDir "..\build\staging"
#endif
#ifndef OutputDir
  #define OutputDir "..\build\output"
#endif

#define AppName        "FrpWin 内网穿透套装"
#define AppShortName   "FrpWin"
#define AppVersion     "1.0.0"
#define AppPublisher   "FrpWin"
#define AppURL         "https://github.com/fatedier/frp"
#define AppExeName     "FrpWin.exe"

[Setup]
AppId={{8E31F0A2-4C6B-4B7E-9F3A-2D5C7B1E4A90}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
VersionInfoVersion=1.0.0.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} 安装程序
VersionInfoProductName={#AppShortName}
VersionInfoProductVersion={#AppVersion}
DefaultDirName={autopf}\{#AppShortName}
DefaultGroupName={#AppShortName}
DisableProgramGroupPage=yes
DisableDirPage=no
AllowNoIcons=yes
LicenseFile=
OutputDir={#OutputDir}
OutputBaseFilename=FrpWin-Setup-{#AppVersion}
SetupIconFile={#StageDir}\app.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
CloseApplications=no
RestartIfNeededByRun=no

; 简体中文语言文件不是 Inno Setup 自带的，需要单独下载后放进
;   <Inno Setup 安装目录>\Languages\ChineseSimplified.isl
; 下载地址：https://github.com/kira-96/Inno-Setup-Chinese-Simplified-Translation
; 如果本机没有装这个文件，就自动只编译英文版安装程序，不会构建失败。
#define ChineseISL AddBackslash(CompilerPath) + "Languages\ChineseSimplified.isl"
#define HasChinese FileExists(ChineseISL)

[Languages]
#if HasChinese
Name: "chinese"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
#endif
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
#if HasChinese
chinese.CreateDesktopIcon=创建桌面快捷方式
chinese.OpenFirewall=在 Windows 防火墙中放行服务端端口 7000（TCP + UDP）
chinese.AdditionalTasks=附加任务：
chinese.LaunchApp=立即运行 FrpWin 管理器
chinese.FirewallStatus=正在配置 Windows 防火墙...
chinese.StoppingServices=正在停止已运行的 frp 服务...
chinese.DataFiles=配置与日志（保留）
chinese.Readme=使用说明
chinese.Uninstall=卸载 FrpWin
#endif

english.CreateDesktopIcon=Create a desktop shortcut
english.OpenFirewall=Open server port 7000 (TCP + UDP) in Windows Firewall
english.AdditionalTasks=Additional tasks:
english.LaunchApp=Launch FrpWin Manager now
english.FirewallStatus=Configuring Windows Firewall...
english.StoppingServices=Stopping running frp services...
english.DataFiles=Configuration and logs (kept)
english.Readme=Read me
english.Uninstall=Uninstall FrpWin

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalTasks}"; Flags: checkedonce
Name: "firewall"; Description: "{cm:OpenFirewall}"; GroupDescription: "{cm:AdditionalTasks}"; Flags: unchecked

[Dirs]
; 数据目录对所有用户可写，这样普通用户也能在图形界面里保存配置
Name: "{commonappdata}\{#AppShortName}"; Permissions: users-modify
Name: "{commonappdata}\{#AppShortName}\logs"; Permissions: users-modify

[Files]
Source: "{#StageDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\frps.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\frpc.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\使用说明.txt"; DestDir: "{app}"; Flags: ignoreversion isreadme
; 示例配置只在不存在时放入，卸载时不删除，避免覆盖用户自己的配置
Source: "{#StageDir}\conf\frps.toml"; DestDir: "{commonappdata}\{#AppShortName}"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#StageDir}\conf\frpc.toml"; DestDir: "{commonappdata}\{#AppShortName}"; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"; Comment: "frp 内网穿透图形管理器"; Tasks: desktopicon
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\{cm:DataFiles}"; Filename: "{sys}\explorer.exe"; Parameters: """{commonappdata}\{#AppShortName}"""
Name: "{group}\{cm:Readme}"; Filename: "{app}\使用说明.txt"
Name: "{group}\{cm:Uninstall}"; Filename: "{uninstallexe}"

[Run]
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""FrpWin frps 7000 TCP"" dir=in action=allow protocol=TCP localport=7000"; Flags: runhidden; Tasks: firewall; StatusMsg: "{cm:FirewallStatus}"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""FrpWin frps 7000 UDP"" dir=in action=allow protocol=UDP localport=7000"; Flags: runhidden; Tasks: firewall; StatusMsg: "{cm:FirewallStatus}"
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[Code]
{ ---------------------------------------------------------------------------
  安装前后清理：如果 frp 正在以服务或普通进程运行，文件会被占用，
  必须先停止服务并结束进程，否则覆盖安装会失败。
  --------------------------------------------------------------------------- }
procedure StopFrpServices();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop FrpWinServer', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop FrpWinClient', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(700);
end;

procedure KillFrpProcesses();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM FrpWin.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM frps.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM frpc.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(300);
end;

procedure RemoveFirewallRules();
var
  ResultCode: Integer;
  Q: String;
begin
  Q := '"';
  Exec(ExpandConstant('{sys}\netsh.exe'),
       'advfirewall firewall delete rule name=' + Q + 'FrpWin frps 7000 TCP' + Q,
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\netsh.exe'),
       'advfirewall firewall delete rule name=' + Q + 'FrpWin frps 7000 UDP' + Q,
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  WizardForm.StatusLabel.Caption := ExpandConstant('{cm:StoppingServices}');
  StopFrpServices();
  KillFrpProcesses();
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    { 安装结束后确保数据目录的可写权限（某些系统上 ProgramData 继承权限不一致） }
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    StopFrpServices();
    KillFrpProcesses();
    Exec(ExpandConstant('{sys}\sc.exe'), 'delete FrpWinServer', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec(ExpandConstant('{sys}\sc.exe'), 'delete FrpWinClient', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    RemoveFirewallRules();
  end;
end;
