@ECHO OFF

CALL .\build Release
CALL dotnet electronize build /target win /dotnet-configuration release /electron-params "--executable-name=Helix"
