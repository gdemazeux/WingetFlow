WingetFlow
==================
[![MIT License](https://img.shields.io/badge/License-MIT-green.svg)](https://choosealicense.com/licenses/mit/)

A winget plugin for the [Flow launcher](https://github.com/Flow-Launcher/Flow.Launcher).
With this plugin, you can search, install, uninstall or upgrade packages using the Windows Package Manager (winget) directly from Flow Launcher.

### Install

    pm install WingetFlow



### Usage

    wget <your search>

The search can be performed using the package name or ID.\
With Enter, WingetFlow suggests the most natural action based on the package's status :
- Package not installed -> install
- Package installed -> uninstall
- Updated package available -> update

You can change the default action using the context menu with right-click or right-arrow on a search result

![preview](demo.gif)