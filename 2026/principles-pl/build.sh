#!/bin/bash
.paket/paket.exe restore
exit_code=$?
if [ $exit_code -ne 0 ]; then
  exit $exit_code
fi

dotnet fsi slides.fsx
