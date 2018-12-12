@ECHO OFF

SET publishDirectoryPath=".\bin\Publish"

IF EXIST %publishDirectoryPath% CALL rmdir /S /Q %publishDirectoryPath%
CALL mkdir %publishDirectoryPath%

SET mode=[%1]
IF %mode%==[] SET mode=Release
dotnet publish -c %mode% -r win10-x64 -o %publishDirectoryPath%

CALL robocopy bin\%mode%\netcoreapp2.1\win10-x64 %publishDirectoryPath% chromedriver.exe /NFL /NDL /NJH /NJS /nc /ns /np
CALL robocopy bin\%mode%\netcoreapp2.1\win10-x64\gui %publishDirectoryPath%\gui /E /NFL /NDL /NJH /NJS /nc /ns /np

PAUSE
