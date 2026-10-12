@echo off
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize /out:"%~dp0SecCevir.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF\System.Speech.dll" "%~dp0SecCevir.cs"
