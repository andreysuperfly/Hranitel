; Inno Setup script for Hranitel

#define MyAppName "Хранитель"
#define MyAppVersion "1.0"
#define MyAppPublisher "Хранитель"
#define MyAppExeName "WindowsGift.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=.\bin
OutputBaseFilename=Hranitel_Setup
SetupIconFile=Assets\icon.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\icon.ico

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Run Собрать_установщик.bat — он публикует в bin\publish
Source: "bin\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Assets\icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "WindowsGift"; Flags: uninsdeletevalue

[Run]
; --show — показать окно сразу (после установки). Без аргумента — только трей (автозапуск при перезагрузке).
Filename: "{app}\{#MyAppExeName}"; Parameters: "--show"; Description: "Запустить {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeUninstall(): Boolean;
begin
  Result := MsgBox(
    'Может, не стоит удалять?' + #13#10 + #13#10 +
    '«Человек, занятый переделыванием мира, забыл переделать себя.»' + #13#10 +
    '— Андрей Платонов' + #13#10 + #13#10 +
    'Хранитель напоминает о ценности времени. Продолжить удаление?',
    mbConfirmation, MB_YESNO) = IDYES;
end;
