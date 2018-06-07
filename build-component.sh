#!/bin/bash
# publish to bin directory
dotnet publish $1/$1.csproj -o ../bin --framework netcoreapp2.0
# create an executable script
echo "dotnet $PWD/bin/$1.dll \"\$@\"" > bin/$1
chmod 777 bin/$1
