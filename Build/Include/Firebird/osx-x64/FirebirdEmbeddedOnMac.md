# Firebird Embedded on Mac

To make it work properly in embedded mode on macOS, without relying on separate installation of Firebird, some Firebird binaries are patched:

```bash
install_name_tool -change /Library/Frameworks/Firebird.framework/Versions/A/Firebird @executable_path/libfbclient.dylib plugins/libEngine12.dylib
```
