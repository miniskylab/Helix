@ECHO OFF

SET publishDirectoryPath=".\bin\Publish"

IF EXIST %publishDirectoryPath% CALL rmdir /S /Q %publishDirectoryPath%
CALL mkdir %publishDirectoryPath%

SET mode="%1"
IF %mode%=="" SET mode="Debug"
dotnet publish -c %mode% -r win10-x64 -o %publishDirectoryPath%

PAUSE