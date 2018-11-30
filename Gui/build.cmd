@ECHO OFF

SET publishDirectoryPath=".\obj\host\bin"

IF EXIST ..\Abstractions\bin CALL rmdir /S /Q ..\Abstractions\bin
IF EXIST ..\Abstractions\obj CALL rmdir /S /Q ..\Abstractions\obj
IF EXIST ..\Crawler\bin CALL rmdir /S /Q ..\Crawler\bin
IF EXIST ..\Crawler\obj CALL rmdir /S /Q ..\Crawler\obj
IF EXIST .\bin CALL rmdir /S /Q .\bin
IF EXIST .\obj CALL rmdir /S /Q .\obj
CALL mkdir %publishDirectoryPath%

SET mode="%1"
IF %mode%=="" SET mode="Debug"
CALL dotnet restore
CALL dotnet publish -c %mode% -r win10-x64 -o %publishDirectoryPath%
CALL copy bin\%mode%\netcoreapp2.1\win10-x64\chromedriver.exe %publishDirectoryPath%
