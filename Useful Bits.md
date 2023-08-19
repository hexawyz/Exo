# Useful Bits

Let's put here the commands / regex that can be useful for writing or refactoring code here.

## PowerShell stuff

## Generate a new GUID in `new Guid()` format

```powershell
$tmp = [System.Guid]::NewGuid().ToByteArray(); "new Guid(0x$([System.BitConverter]::ToUInt32($tmp, 0).ToString("X8")), 0x$([System.BitConverter]::ToUInt16($tmp, 4).ToString("X4")), 0x$([System.BitConverter]::ToUInt16($tmp, 6).ToString("X4")), 0x$($tmp[8].ToString("X2")), 0x$($tmp[9].ToString("X2")), 0x$($tmp[10].ToString("X2")), 0x$($tmp[11].ToString("X2")), 0x$($tmp[12].ToString("X2")), 0x$($tmp[13].ToString("X2")), 0x$($tmp[14].ToString("X2")), 0x$($tmp[15].ToString("X2")))"
```

