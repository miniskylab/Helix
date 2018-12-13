@ECHO OFF

CALL pushd ..\
SET publishDirectoryPath="deployment\helix"

IF EXIST %publishDirectoryPath% CALL rmdir /S /Q %publishDirectoryPath%
CALL mkdir %publishDirectoryPath%

SET mode=[%1]
IF %mode%==[] SET mode=Release
CALL dotnet publish -c %mode% -r win10-x64 -o %publishDirectoryPath%

CALL robocopy bin\%mode%\netcoreapp2.1\win10-x64 %publishDirectoryPath% chromedriver.exe /NFL /NDL /NJH /NJS /nc /ns /np
CALL robocopy bin\%mode%\netcoreapp2.1\win10-x64\ui %publishDirectoryPath%\ui /E /NFL /NDL /NJH /NJS /nc /ns /np
CALL deployment\rcedit-x64.exe %publishDirectoryPath%\helix.exe --set-icon deployment\icon.ico

CALL popd ..\
PAUSE
