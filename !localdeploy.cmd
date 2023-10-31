taskkill /f /im GenericWebAppWpfWrapper.exe

ECHO,
echo DONT FORGET TO BUILD THE RELEASE FOLDER!!!!
ECHO,
PAUSE
ECHO,
ECHO,

del /f /q C:\save\GenericWebAppWpfWrapper\bin\*.*
rmdir /q /s C:\save\GenericWebAppWpfWrapper\bin\runtimes

copy C:\save\repos\GenericWebAppWpfWrapper\GenericWebAppWpfWrapper\bin\Release\net6.0-windows\*.* C:\save\GenericWebAppWpfWrapper\bin

md C:\save\GenericWebAppWpfWrapper\bin\runtimes
xcopy /s C:\save\repos\GenericWebAppWpfWrapper\GenericWebAppWpfWrapper\bin\Release\net6.0-windows\runtimes C:\save\GenericWebAppWpfWrapper\bin\runtimes

pause