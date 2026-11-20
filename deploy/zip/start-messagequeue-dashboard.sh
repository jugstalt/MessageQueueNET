#!/bin/sh
cd "$(dirname "$0")/dashboard/app" || exit 1
exec dotnet MessageQueueNET.Blazor.dll --urls http://localhost:9090
