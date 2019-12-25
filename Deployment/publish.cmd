@ECHO OFF

SET entryDirectoryPath="..\Gui"
SET publishDirectoryPath="%~dp0Helix 2.1\bin"

IF EXIST %publishDirectoryPath% CALL rmdir /S /Q %publishDirectoryPath%
CALL mkdir %publishDirectoryPath%

SET mode=[%1]
IF %mode%==[] SET mode=Release
CALL dotnet publish %entryDirectoryPath% -c %mode% -r win10-x64 -o %publishDirectoryPath%

CALL robocopy %entryDirectoryPath%\bin\%mode%\netcoreapp3.1\win10-x64\ui %publishDirectoryPath%\ui /E /NFL /NDL /NJH /NJS /nc /ns /np
CALL robocopy %entryDirectoryPath%\bin\%mode%\netcoreapp3.1\win10-x64 %publishDirectoryPath% chromedriver.exe /NFL /NDL /NJH /NJS /nc /ns /np
CALL robocopy %entryDirectoryPath%\bin\%mode%\netcoreapp3.1\win10-x64\chromium %publishDirectoryPath%\chromium /E /NFL /NDL /NJH /NJS /nc /ns /np
CALL robocopy %entryDirectoryPath%\bin\%mode%\netcoreapp3.1\win10-x64\sqlite-browser %publishDirectoryPath%\sqlite-browser /E /NFL /NDL /NJH /NJS /nc /ns /np
CALL rcedit-x64.exe %publishDirectoryPath%\helix.exe --set-icon icon.ico

PAUSE
