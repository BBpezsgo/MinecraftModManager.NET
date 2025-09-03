#!/bin/sh
dotnet publish ./mmm.csproj -c Release -o ./publish/linux-x64 -r linux-x64 /p:UseAppHost=true
dotnet publish ./mmm.csproj -c Release -o ./publish/win-x64 -r win-x64 /p:UseAppHost=true /p:DefineConstants=WIN

mv ./publish/linux-x64/mmm ./publish/mmm-linux-x64
rm -r ./publish/linux-x64

mv ./publish/win-x64/mmm.exe ./publish/mmm-win-x64.exe
rm -r ./publish/win-x64
