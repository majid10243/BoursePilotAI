[Setup]
AppName=BoursePilotAI
AppVersion=1.0.0
DefaultDirName={autopf}\BoursePilotAI
OutputDir=..\Release
OutputBaseFilename=BoursePilotAI_Setup
Compression=lzma
SolidCompression=yes

[Files]
Source: "..\Source_Code\*"; DestDir: "{app}"; Flags: recursesubdirs

[Icons]
Name: "{autoprograms}\BoursePilotAI"; Filename: "{app}\BoursePilotAI.exe"
