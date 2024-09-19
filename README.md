# UpdatePackageReferences

CLI to update package references

## Install

```batch
dotnet tool install KsWare.UpdatePackageReferences --global
```

## Usage

```text
UpdatePackageReferences [<switches>] <file(s)>

  -help -?                   Shows this help
  -ReferenceSwitcher         special mode for ReferenceSwitcher
  -Major                     update up to the highest major (default)
  -Minor                     update up to the highest minor (major remains the same)
  -Patch                     update up to the highest patch (major/minor remains the same)
  -PreRelease                allow pre releases
  -ro -readonly              does not make any changes
  -unicode                   use Unicode encoding
  -color -noColor            turns colorization on/off (default: auto)
  <file(s)>                  path(s) to sln/proj file(s)

-ReferenceSwitcher:
  If this is specified, only PackageReferences with matching ProjectReference will be updated

-color -noColor  
  By default redirected console output (VS Console Window) does not use colors,
  because ANSI sequences are not supported but System Console Windows does.
  With -color -noColor you can force a mode. 
```
⚠️ **Use `UpdatePackageReferences --help` to get the latest instructions.**

## License
Copyright © 2024 by KsWare. All rights reserved.  
Licensed under [KsWare Open Commercial License](LICENSE.txt).