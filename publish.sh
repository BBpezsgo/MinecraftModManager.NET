#!/bin/sh
dotnet publish ./mmm.csproj -c Release -o ./publish/linux-x64 -r linux-x64 /p:UseAppHost=true
dotnet publish ./mmm.csproj -c Release -o ./publish/win-x64 -r win-x64 /p:UseAppHost=true
mv ./publish/linux-x64/mmm ./publish/linux-x64/mmm-linux-x64
mv ./publish/win-x64/mmm.exe ./publish/win-x64/mmm-win-x64.exe
