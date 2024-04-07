#!/usr/bin/env bash
output=SOHFerryTransfer
outputFull=SOHFerryTransfer

# Build the application and all of its dependencies
#dotnet publish -c Release -o ${outputFull}/win -r win-x64 --self-contained
#
#dotnet publish -c Release -o ${outputFull}/linux -r linux-x64 --self-contained
#
#dotnet publish -c Release -o ${outputFull}/mac -r osx-x64 --self-contained


dotnet publish -c Release -r osx-x64 /p:DebugSymbols=false /p:DebugType=None \
  -p:PublishSingleFile=true --self-contained true -o ${output}/${output}_MACOSX && \
  cp -r resources/ ./${output}/${output}_MACOSX/resources && \
  cp config.json ./${output}/${output}_MACOSX/

  
dotnet publish -c Release -r win-x64 /p:DebugSymbols=false /p:DebugType=None \
  -p:PublishSingleFile=true --self-contained true -o ${output}/${output}_WINDOWS && \
  cp -r resources/ ./${output}/${output}_WINDOWS/resources && \
  cp config.json ./${output}/${output}_WINDOWS/
  
dotnet publish -c Release -r linux-x64 /p:DebugSymbols=false /p:DebugType=None \
  -p:PublishSingleFile=true --self-contained true -o ${output}/${output}_LINUX && \
  cp -r resources/ ./${output}/${output}_LINUX/resources && \
  cp config.json ./${output}/${output}_LINUX/

