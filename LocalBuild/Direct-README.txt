FluentWeather (Local) with saved locations and tray icons
=========================================================

FluentWeather is a weather app for Windows by Gabboxl (github.com/Gabboxl/FluentWeather).
This version adds saved locations, tray icons with a weather picture and a details panel,
more display settings and a language choice. duedilly made these changes. The source code
of this version is at github.com/Duedilly108/FluentWeather. FEATURES.txt there lists every
added feature.

This folder holds three files.

  FluentWeather-Setup.exe       installs the app and the tray icons
  FluentWeather-Uninstall.exe   removes everything that the setup added
  README.txt                    this file

The setup holds a ready-built copy of the app. It downloads nothing. It does not need
Visual Studio, Git or a Microsoft account.


What you need
-------------
Windows 10 version 1809 or later, or Windows 11.
A 64-bit PC. Most PCs from the last ten years are 64-bit.
The password of an administrator account for one approval during setup.
About 150 MB of free disk space.
An internet connection while you use the app, for the weather data.


What the setup changes on your PC
---------------------------------
It adds the app's signing certificate to "Trusted People" of this PC. Windows installs
an app from outside the Microsoft Store only when its signing certificate is trusted.
This step needs administrator approval. The certificate was made only for this setup.
Its private key was deleted right after the app was signed, so nobody can sign anything
else with it. Windows can therefore accept only this one app package through it.
A newer setup replaces it with its own certificate.

It installs the app "FluentWeather (Local)" for your Windows account. It also installs
the Microsoft packages the app needs, if they are missing.

It copies the tray icons program, this file and the uninstaller to
%LOCALAPPDATA%\Programs\FluentWeather.

It writes two small files for the tray icons to %LOCALAPPDATA%\FluentWeatherLocal.

It adds a Startup item, so the tray icons start when you sign in.

It adds the folder "FluentWeather" to the Start menu. It holds "FluentWeather", which
opens the app, "FluentWeather tray icons", "Read me" and "Uninstall FluentWeather".

It adds "FluentWeather (Local) with tray icons" to Settings > Apps.

It changes nothing else. It adds no scheduled tasks, no services and no firewall rules.


Install
-------
1. Download the zip file and extract it. Right-click the zip file, select
   "Extract All", then select "Extract".

2. Double-click FluentWeather-Setup.exe in the extracted folder. Do not use
   "Run as administrator". Windows installs the app for the account that runs the setup.

3. Windows may show "Windows protected your PC". The setup is not signed by a known
   publisher, so Windows shows this for a file from the internet. Select "More info",
   then select "Run anyway".

4. The setup describes what it does and asks to install. Select "Yes".

5. A window shows the progress. Windows asks for administrator approval once, for the
   certificate. Select "Yes", or enter the administrator password.

6. When the setup is done, the app opens and the tray icons appear next to the clock.
   If you do not see the tray icons, click the ^ arrow next to the clock. Windows puts
   new icons there first. Drag them onto the taskbar to keep them in view.

If the setup stops, its window shows the reason. Nothing else is changed in that case.


First steps in the app
----------------------
Type a city in the search box at the top and select it in the list.

Click the star next to the location name to save the location. Saved locations appear
as buttons below the name. Click a button to switch to that location. Ctrl+1 to Ctrl+9
also switch.

Click the gear button for the settings. They include the text and picture size, the
background contrast, the length of the hourly forecast, the refresh interval and the
language.


The tray icons
--------------
The left icon shows the temperature. The right icon shows a picture of the current
weather. At night it shows the moon in its current phase.

Keep the pointer on the icons for 1.2 seconds to see a panel with the location and the
weather details. If the pointer leaves the icons sooner, the panel does not open. The panel closes 0.9 seconds
after the pointer leaves the icons and the panel.
Drag that panel with the left mouse button to move it. It opens at that place after that.

Right-click the icons for the menu. It sets which number the left icon shows, which
details the panel shows and which saved location the tray shows. It also closes the app
window or the tray icons.

Left-click the icons to open the app window. Left-click again to close the window.
The tray icons stay.

While the app window is closed, the tray icons ask the app for new weather data every
5 minutes. The tray icons program itself never connects to the internet.

After "Exit tray icons", start them again with Start menu > FluentWeather >
"FluentWeather tray icons".


Firewall
--------
Only the app connects to the internet. It connects to api.weather.com on port 443.
If you use a firewall that blocks programs by default, allow the app
"FluentWeather (Local)" as an app package. A Store-type app gets a new folder with each
version, so a rule for one program path stops working after an update. In TinyWall, use
"Add application", then "Pick a UWP app...", then select FluentWeather (Local).
The tray icons, the setup and the uninstaller need no firewall exception.


Updates
-------
This version does not update itself. When a new version is ready, run the new
FluentWeather-Setup.exe. It installs over the old version. Your saved locations and
settings stay.


Uninstall
---------
There are two ways. Both remove everything the setup added.

  Settings > Apps > Installed apps > "FluentWeather (Local) with tray icons" > Uninstall
  Start menu > FluentWeather > "Uninstall FluentWeather"

You can also run FluentWeather-Uninstall.exe from the extracted folder.

The uninstaller ends the tray icons. It removes the app with its saved locations and
settings, the Startup item, the Start menu folder, the Settings > Apps entry and the tray
files. It asks for administrator approval once to remove the certificate from
"Trusted People". It deletes the folder %LOCALAPPDATA%\Programs\FluentWeather after it
closes.

Settings > Apps also lists the app itself as "FluentWeather (Local)". Uninstalling that
entry removes only the app. The tray icons and the certificate stay. Use
"FluentWeather (Local) with tray icons" to remove everything.


Privacy
-------
The app sends the locations you search for and view to weather.com to get the weather.
It sends no other data. The app and the tray icons collect no usage data. Saved
locations and settings stay on your PC.


Problems
--------
If the setup reports "The app package is not signed with the certificate in the setup"
or "The file may be damaged", download the zip file again and extract it again.

If the setup reports "This PC has the FluentWeather local build from Setup.cmd", this PC
builds the app itself and already has everything. Use that version.

If the tray icons show "--", the app has no weather data yet, or the data is older than
3 hours. Open the app once. If it stays at "--", check the firewall. Also check that the
app may run in the background in Settings > Apps > Installed apps > FluentWeather (Local)
> Advanced options.

If the app reports "Weather data update failed", check the internet connection and the
firewall.


License
-------
FluentWeather is free software under the GNU General Public License version 3.
The source code of this version is at github.com/Duedilly108/FluentWeather.
The original app and its source code are at github.com/Gabboxl/FluentWeather.
