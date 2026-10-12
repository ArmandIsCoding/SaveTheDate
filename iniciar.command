#!/bin/zsh
set -e
cd "$(dirname "$0")"
print "SaveTheDate · iniciando tu espacio de celebraciones"
print "Abrí http://localhost:5105 cuando termine de iniciar."
dotnet run --project "01 - Code/SaveTheDate" --launch-profile http
