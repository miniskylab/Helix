@ECHO OFF

SET publishDirectoryPath=".\obj\host\bin"

IF EXIST .\bin CALL rmdir /S /Q .\bin
IF EXIST .\obj CALL rmdir /S /Q .\obj
CALL mkdir %publishDirectoryPath%

SET mode="%1"
IF %mode%=="" SET mode="Debug"
CALL dotnet publish -c %mode% -r win10-x64 -o %publishDirectoryPath%
CALL copy bin\%mode%\netcoreapp2.1\win10-x64\chromedriver.exe %publishDirectoryPath%
CALL dotnet restore
CALL dotnet electronize start
