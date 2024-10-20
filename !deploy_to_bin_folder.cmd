rem @echo off

taskkill /f /im GenericWebAppWpfWrapper.exe

ECHO,
echo DONT FORGET TO BUILD THE RELEASE FOLDER!!!!
ECHO,
PAUSE
ECHO,
ECHO,

rmdir /q /s "%bin%\GenericWebAppWpfWrapper\bin"
md "%bin%\GenericWebAppWpfWrapper\bin\runtimes\win-x64\native"

copy .\GenericWebAppWpfWrapper\bin\Release\net6.0-windows\*.* "%bin%\GenericWebAppWpfWrapper\bin\"
copy .\GenericWebAppWpfWrapper\bin\Release\net6.0-windows\runtimes\win-x64\native\*.* "%bin%\GenericWebAppWpfWrapper\bin\runtimes\win-x64\native\"

pause