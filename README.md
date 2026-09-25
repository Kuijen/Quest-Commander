# Quest-Commander
A simple TTY tool to manage Quest developer commands to change dev settings on the fly.
<br>
<h1></h1>
Some options include:<ul>
<li>Changing resolutions</li>
<li>Changing refresh rates</li>
<li>Toggling the proximity sensor</li>
<li>Toggling the boundary</li>
<li>Changing the recording settings</li></ul>
<br>
<img width="293" height="253" alt="image" src="https://github.com/user-attachments/assets/9809d218-cdbe-41ab-8b45-713baf4e00f8" />

<h1>Requirements:</h1><br>
<ul>
  <li><a href="https://dotnet.microsoft.com/en-us/download/dotnet/8.0">.NET 8 Runtime (SDK to compile)</a></li>
  <li><a href="https://developer.android.com/tools/releases/platform-tools">adb.exe & AdbWinApi.dll<a/> (Windows)</li>
</ul>
<br>
To compile:
<ul>Windows: <code>dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true</code>
  <br>Linux: <code>dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true</code></ul>
