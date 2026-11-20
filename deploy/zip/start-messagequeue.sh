#!/bin/sh
cd "$(dirname "$0")/api/app" || exit 1
exec dotnet MessageQueueNET.dll --urls http://localhost:9091
