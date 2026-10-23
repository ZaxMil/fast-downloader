@echo off
title FastDownloader Pro - Release Packager
powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0scripts\package-release.ps1"
pause
