Newsroom Clocks

===============

This is a little utility app that lets you put clocks into your Windows notification area (systray)

![](NewsroomClocks\\Assets\\InSysTray.png)


# Installation

[Install from the Microsoft Store](https://apps.microsoft.com/detail/9MXPWK2WG2SX?hl=en-us&gl=DE&ocid=pdpshare)

# Usage

Run the app and it shows all the info, but the basics:
* Shows one or more clocks in the systray. (In the overflow popup by default but you can drag it to the main tray to keep it always visible.)
* Add new clocks by looking up a city name
* Launches automatically on system start by default
* Displays as yellow like the sun in the daytime for that location (6am-6pm), black at nighttime

# Interesting things about time zones

There are two interesting time zone databases, and they're not the same: Windows and IANA (Internet Assigned Numbers Authority).

We need a Windows time zone, and to make it easy to figure that out, you can look up a time zone by city name in the app.

Antartica has no time zones, yet you continue to age while you're there

Time zones are dyanmic across time and space, for example:
* Daylight Savings Time rules change frequently
* In 1949, China combined 5 time zones into one (aka Beijing time)
* Starke County Indiana moved from Central to Eastern time in 1991 and back to Central in 2006

# Implementation notes

Process in this app to map a city name to a time zone

* There's [a repo](github.com/kevinroberts/city-timezones.git) that maps city names to Iana time zones. It's [submodule'd here](NewsroomClocks/Submodules/city-timezones/), and no code is used from there, just data (cityMap.json).

* But we need Windows time zones. So there's [another repo](https://github.com/unicode-org/cldr.git), call the CLDR and [submodule'd here](NewsroomClocks/Submodules/cldr/), that has data to map Iana time zones to Windows time zones. Again data only.

* These data files are big, so this app has some indices for fast lookup

Perf note ... If you launch the app there's a window with instructions and a button to let you pick the time zones you're going to see. By default (if you've picked a city) the app will automatically relaunch on system startup and not show a window. In that case the UI (WinUI) isn't started up, not until you click on a clock in the systray to open the window.
