
# Inofficial Swissbit Secure SD Card Utils

## Contents

This repository contains:

1. Partial reverse enginerring, documentation and header files for Swissbit PS-45u DP and PU-50n DP Secure USB Stick, since the "SDK" is just a bunch of useless tools and docs, but nothing to develop software with (and this is the purpose of a Software DEVELOPMENT Kit).

2. "Unlock Card" command line tool (For Linux and Windows, language C): Can be used to unlock PS-45u (not PU-50n) if you simply want to use it as "Secure SD Card" and mount and unmount it using Linux (without booting from it). Implements low-level file access without any library.

```
Linux Syntax:
UnlockCard LOCK /mnt/sdcard/
UnlockCard UNLOCK /mnt/sdcard/ PasswordHere

Windows Syntax:
UnlockCard.exe LOCK G:\
UnlockCard.exe UNLOCK G:\ PasswordHere
```

3. "Swissbit Secure SD Utils" (for Windows, language C#): A command line utility written in C that reads all the information of the medium. It also contains header files, so it can be used to implement programmatically lock and unlock a secure SD card or secure SD stick by calling CardManagement.dll. Note that the PU-50n DP Raspberry Pi Edition can be used as 8 GB Secure USB Stick! It is not just a dongle, but can also store data!

The C# library can help you using the USD/uSD device by calling the DLL instead of the non-working CLI EXE.
I have also found a lot of undocumented things, e.g. how to interprete the extended security flags and how to read the Life Time Management (LTM) data.
All these things shall be part of the documentation, but they are not. The documentation is insufficient,
error codes are not documented, various options in the GUI are not described, and the worst of all:
The Software DEVELOPMENT Kit does not allow that you develop using a programming language... It is just a collection of EXE, DLL, and PDF files...

## Patch for CardManagerLite.exe and CardManagerCLI.exe to support PU-50n DP (USB Stick)

There is a severe bug in CardManagerLite.exe and CardManagerCLI.exe in the USB version of the SDK.

For the USB Stick, CardManagement.dll can only access the stick using the Smartcard-API, hence it requires a device name (e.g. "Swissbit Secure USB PU-50n DP 0") rather than a drive letter.

The big problem is that CardManagerLite.exe and CardManagerCli.exe only accept drive letters  (e.g. "G:"), not SmartCard names.

Therefore, you CANNOT use the CLI tool for the USB Secure USB Stick! This is a showstopper, so you must use the GUI tool and cannot
configure the secure USB stick via command line or programmatically. Nobody noticed that before?!

To fix this issue for the CLI tool, patch CardManagerCLI.exe by disabling the syntax check for the "--mountpoint" argument:

```
Search:  75 1F 0F BE 02 50 FF 15 D8 D2 40 00 8B 7D FC 83 C4 04 85 C0 74 0E 8B 47 0C 80 78 01 3A 74
Replace: 90 90 0F BE 02 50 FF 15 D8 D2 40 00 8B 7D FC 83 C4 04 85 C0 90 90 8B 47 0C 80 78 01 3A EB
         ^^ ^^                                                       ^^ ^^                      ^^
```

Therefore, the string with be delivered unfiltered to CardManagement.dll.
Some commands like fetching Partition Table don't seem to work though. I didn't investigate that yet.

Currently no patch for CardManagerLite.exe. Use CardManager.exe instead.

## Technical notes

### Unlocking the card

Unlocking the card is done via the `verify()` method in CardManagement.dll.

```
int LicMode, SysState, RetryCount, SoRetryCount, ResetCount, CdRomAddress, ExtSecurityFlags;
getStatus(deviceName, &LicMode, &SysState, &RetryCount, &SoRetryCount, &ResetCount, &CdRomAddress, &ExtSecurityFlags);
if (ExtSecurityFlags&0x10 == 0) {
  verify(deviceName, password);
}
```

If the extended security flag "Secure PIN Entry" is enabled, then the login is done like this:

```
int LicMode, SysState, RetryCount, SoRetryCount, ResetCount, CdRomAddress, ExtSecurityFlags;
getStatus(deviceName, &LicMode, &SysState, &RetryCount, &SoRetryCount, &ResetCount, &CdRomAddress, &ExtSecurityFlags);
if (ExtSecurityFlags&0x10 != 0) {
  char[32] challenge;
  getHashChallenge(deviceName, &challenge); // Alias of getLoginChallenge(). The login challenge (also called hash challenge) gets changed after each successful or failed login, or powercycle.
  code = sha256(sha256(password) + challenge);
  verify(deviceName, code);
}
```

### Locking the card

Locking the card is done via `lockCard()` method in CardManagement.dll.

### Error codes

Some Error Messages collected:

```
Return 0000 : OK (the DLL returns this, but the actual response code is 0x9000)
Return 0008 : Generic read error, e.g. "Failed SCardTransmit"  (happens for some reason when you save 259+ bytes to NVRAM and then try to read it)
Return 3790 : Happens when you save 257 bytes to NVRAM and then try to read it
Return 3131 : Happens when you save 258 bytes to NVRAM and then try to read it
Return 6700 : Message invalid (e.g. wrong length)
Return 6B00 : Happens at getPartitionTable if Firmware of Card is too old
Return 6F02 : Wrong password entered or access denied
Return 6F05 : No password entered, or password too short
Return 6FFC : Security Settings changed; need powercycle to reload stuff
Return 9000 : Success
Return 9001 : Change Protection Profile (Partition): Sum of Partition sizes is larger than total size of drive .... sometimes something else??? generic error???
```

### CardManagement.dll API

This is the current state of the research. The known methods are implemented in SwissbitSecureSDUtils (in C#). The code below is pseudo-code. Adapt it to the language you are using.

Note that `szDeviceName` is either the name of the USB Stick ("Smartcard Name") or the drive letter of the SD card.

SD solution (PS-45u) and USB solution (PU-50n) require different DLL files!

```
__cdecl int activate(char* szDeviceName, char* password, int passwordLength, char* soPassword, int soPasswordLength, int retryCount)
   Purpose:       Enabled Data Protection
   Command:       10FF
   Raw data in:   
   Raw data out:  

__cdecl int activateSecure(dev,?)
   Purpose:       Unknown
   Command:       20010FF
   Raw data in:   
   Raw data out:  

__cdecl int challengeFirmware(dev,?,?,?)
   Purpose:       Unknown
   Command:       F1FF
   Raw data in:   
   Raw data out:  

__cdecl int changePassword(dev,?,?,?,?)
   Purpose:       Changes the password (which?)
   Command:       40FF
   Raw data in:   
   Raw data out:  

__cdecl int checkAuthenticity(dev,?,?)
   Purpose:       Unknown
   Command:       F2FF
   Raw data in:   
   Raw data out:  

__cdecl int clearProtectionProfiles(dev)
   Purpose:       Unknown
   Command:       353FF
   Raw data in:   
   Raw data out:  

__cdecl int configureNvram(char* szDeviceName, int ramRights, int camRights, int ramSize, int camSize, int camNext???)
   Purpose:       Configures the NVRAM of the device
	                Note: I am not sure about the last parameter. It is always 0 if I use the Device Manager. It is probably CAM next cycle.
									Also, if I check "Fuse Peristently", the ram/camRights are still the same. Shouldn't flag 0x80 be set? I don't want to try it, so I stopped the debugger at that point. Seems to be a bug in CardManager.exe
   Command:       380FF
   Raw data in:   
   Raw data out:  

__cdecl int deactivate(char* szDeviceName, char* soPassword, int soPasswordLength)
   Purpose:       Disable Data Protection
   Command:       20FF
   Raw data in:   
   Raw data out:  

__cdecl int getApplicationVersion(char* szDeviceName, int* ApplicationVersion)
   Purpose:       In the binary it can be seen that it also seems to be called "CFE version". Also funny typos: "Yor CFE version is" and "You must be loged in to read the partition table".
                  My SD card (PS-45u DP) has F2.
   Command:       270FF
   Raw data in:   01     0000 05  FF700200 00
                  state? ???? len(cmd      len())
   Raw data out:  03     0000 06  000000F2 9000
                  state? ???? len(appver   response)

cdecl int getBaseFWVersion(char *szDeviceName, char* firmware8bytes, long* part2)
   Purpose:       Get firmware version of the device
                  Note: Firmware8bytes is read left to right, while part2 is appended reading right to left.
                  The screenshots in the manual shows the examples "211028s9 X100" and "170614s8  110"
                  My USB device (PU-50n DP) has "180912u9  106" but showns in the Swissbit Device Manager as "180912 1.06"
                  My SD card (PS-45u DP) has "170614s8  105"
                  The firmwareid in the raw data is "170614s8 105" (only 1 whitespace),
                  while the DLL returns part1="170614s8 " and part2=' 501'. Note the two whitespaces...
   Command:       10270FF
   Raw data in:   01     0000 05  FF700201 00
                  state? ???? len(cmd      len())
   Raw data out:  03     0000 0E  313730363134733820313035 9000
                  state? ???? len(firmwareid               response)

__cdecl int char* getBuildDateAndTime()
   Purpose:       Shows the version of the DLL. Does not contact the device. Only for 2019 USB/uSD DLL, not for 2022 uSD DLL.
   Command:       n/a
   Raw data in:   n/a
   Raw data out:  n/a

__cdecl int getCardId(char *szDeviceName, char* cardid16bytes)
   Purpose:       Unknown. This is NOT the unique ID of the card!
   Command:       170FF
   Raw data in:   01     0000 05  FF700100 00
                  state? ???? len(cmd      len())
   Raw data out:  03     0000 12  000102030405060708090A0B0C0D0E0F 9000
                  state? ???? len(cardId16bytes                    response)

__cdecl int getControllerId(char *szDeviceName, char *conrollerId, int *conrollerIdSize)
   Purpose:       THIS is the unique ID of the card! Don't be tricked by the name...
                  The "NetPolicyServer User Manual" writes:
                              Please note the last value in the output (“Controller ID”, Figure 12). This alphanumeric sequence
                              without any blank spaces is the Unique ID of the DataProtection device, which is needed for the Net
                              Policy database entry in the Net Policy server.
                              Note: It is not the value shown as the Unique Card ID!
                  CardManager.exe of USB and CardManager of uSD both use getControllerId() to display the "Unique ID"
                  field in the Device Status dialog. Therefore we can be sure that this is surely the Unique ID,
                  and also identical to the Chip ID from the FileTunnelInterface.
   Command:       670FF
   Raw data in:   01     0000 05  FF700600 00
                  state? ???? len(cmd      len())
   Raw data out:  03     0000 0E  000102030405060708090A0B 9000
                  state? ???? len(id12bytes                response)

__cdecl int getHashChallenge(char *szDeviceName, char* challenge)
__cdecl int getLoginChallenge(char *szDeviceName, char* challenge) // alias
   Purpose:       Returns a Nonce for the SHA256 Challenge–response authentication. Gets reset after each successful or failed login, or powercycle.
   Command:       570FF
   Raw data in:   01     0000 05  FF700500 00
                  state? ???? len(cmd      len())
   Raw data out:  03     0000 22  000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F 9000
                  state? ???? len(challenge32bytes                                                 response)

__cdecl int getOverallSize(char *szDeviceName, int* OverallSize)
   Purpose:       Get memory size in units of 512 byte blocks
   Command:       970FF? 870FF?
   Raw data in:   
   Raw data out:  

__cdecl int getPartitionTable(char *szDeviceName, int* PartitionTableUnknown1)
   Purpose:       Unknown
   Command:       870FF
   Raw data in:   
   Raw data out:  

__cdecl int getProtectionProfiles(char *szDeviceName, int* ProtectionProfileUnknown1, int* ProtectionProfileUnknown2, int* ProtectionProfileUnknown3)
   Purpose:       Unknown
   Command:       770FF
   Raw data in:   01     0000 05  FF700700 00
                  state? ???? len(cmd      len())
   Raw data out:  

__cdecl int getStatus(char *szDeviceName, int* LicenseMode, int* SystemState, int* RetryCounter, int* SoRetryCounter, int* ResetCounter, int* CdRomAddress, int* ExtSecurityFlags)
   Purpose:       Get information about the current state of the device
                  License Mode   = PU-50n DP and PS-45u Raspberry Pi Edition = 0x40. What else is possible?
                                   CardManager.exe : If License Mode is equal to 0x20, then "Extended Security Flags" are not shown in the "Device Status" dialog, also getStatusException() is not called.
                                   However, the "Security Settings" dialog can still be opened?!
                  System State   = 0 (Transparent Mode)
                                   1 (Data Protection Unlocked)
                                   2 (Data Protection Locked)
                  RetryCounter   = Current retry counter for the normal user
                  SoRetryCounter = Current retry counter for the Security Officer
                  ResetCounter   = Device Reset counter
                  CD_ROM_Address = This field is described in NetPolicyServer User Manual version 2.6-2.9 for CardManagerCLI.exe, but not shown in the current version of any EXE or DLL. It might be obsolete
                  Extended Security Flags:
                  - Support Fast Wipe (~0x1)
                  - Reset Requires SO PIN (0x2)
                  - Unknown (0x4)
                  - Multiple Partition Protection (~0x8)
                  - Secure PIN Entry (0x10)
                  - Login Status Survives Soft Reset (0x20)
                  - Unknown (0x40)
                  - Unknown (0x80)
   Command:       70FF
   Raw data in:   01     0000 05  FF700000 00
                  state? ???? len(cmd      len())
   Raw data out:  03     0000 11  40  02       0E    0F      00000001     0000 FFFFFFFF  2B      9000
                  state? ???? len(lic sysstate retry soRetry resetCounter ???? cdRomAddr secflag response)

__cdecl int getStatusException(char *szDeviceName, int* ExceptionUnknown1, int* ExceptionUnknown2, int* ExceptionUnknown3, int* partition1Offset, int* ExceptionUnknown4)
   Purpose:       Unknown
   Command:       470FF
   Raw data in:   01     0000 05  FF700400 00
                  state? ???? len(cmd      len())
   Raw data out:  

__cdecl int getStatusNvram(char *szDeviceName, int* AccessRights, int* TotalNvRamSize, int* RandomAccessSectors, int* CyclicAccessSectors, int* NextCyclicWrite)
   Purpose:       Get NVRAM configuration
                  Returned AccessRights = CyclicRights || RandomRights
                  CyclicRights and RandomRights are a flag byte containing:
                  - All Read (0x1)
                  - All Write (0x2)
                  - User Read (0x4)
                  - User Write (0x8)
                  - Wrap around (0x10), only defined for CyclicRights, not RandomRights
                  - Security Officer Read (0x20)
                  - Security Officer Write (0x40)
                  - Fused Persistently (0x80), not 100% sure
   Command:       370FF
   Raw data in:   01     0000 05  FF700300 00
                  state? ???? len(cmd      len())
   Raw data out:  03     0000 15  00000007  10        1F        00000003 00000002 00000001 00 9000
                  state? ???? len(totalsize ramRights camRights ramSize  camSize  camNext  ?? response)

__cdecl int getVersion()
   Purpose:       DLL only version, no device contact.
   Command:       n/a
   Raw data in:   n/a
   Raw data out:  n/a

__cdecl int lockCard(char *szDeviceName)
   Purpose:       Locks data protection
   Command:       31FF
   Raw data in:   01     0000 05  FF310000  00
                  state? ???? len(cmd       len())
   Raw data out:  

__cdecl int readNvram(char *szDeviceName, char* value, int valueLength, byte isCyclic, int sectorNumber)
   Purpose:       Reads NVRAM data
   Command:        D0FF if isCyclic=0
                  1D0FF if isCyclic=1
   Raw data in:   01     0000 09  FFD0 01 00 04  00000007
                  state? ???? len(cmd  cy .. len(sector  ))
   Raw data out:  03     0000 08  666F6F626172 9000
                  state? ???? len(contents     response)

__cdecl int reset(dev,unk,?)
   Purpose:       Resets the device
   Command:        60FF if unk=1
	                160FF if unk=0
   Raw data in:   
   Raw data out:  

__cdecl int resetAndFormat(dev,?,?)
   Purpose:       Reset and format the device
   Command:       ???
   Raw data in:   
   Raw data out:  

__cdecl int setAuthenticityCheckSecret(dev,?,unk)
   Purpose:       Unknown
   Command:        680FF if unk=0 (store cleartext?)
                  1680FF if unk=1 (store hashed?)
   Raw data in:   
   Raw data out:  

__cdecl int setCdromAreaAndReadException(dev,?,?)
   Purpose:       Unknown
   Command:       253FF
   Raw data in:   
   Raw data out:  

__cdecl int setCdromAreaBackToDefault(dev)
   Purpose:       Unknown
   Command:       53FF
   Raw data in:   
   Raw data out:  

__cdecl int setExtendedSecurityFlags(char *szDeviceName, int newFlags)
   Purpose:       Sets extended security flags
   Command:       580FF
   Raw data in:   
   Raw data out:  

__cdecl int setProtectionProfiles(dev,?,?)
   Purpose:       Unknown
   Command:       10353FF
   Raw data in:   
   Raw data out:  

__cdecl int setSecureActivationKey(dev,?)
   Purpose:       Unknown
   Command:       780FF
   Raw data in:   
   Raw data out:  

__cdecl int unblockPassword(dev,?,?,?,?)
   Purpose:       Unknown
   Command:       50FF
   Raw data in:   
   Raw data out:  

__cdecl int verify(char *szDeviceName, char* code, int codeLength)
   Purpose:       Unlocks data protection
                  If Extended Security Flag 0x10 (Secure PIN Entry) is set, then code=sha256(sha256(password)+challenge)
                  where challenge comes from getHashChallenge(), which gets changed after each successful or failed login, or powercycle.
                  If Secure PIN Entry is disabled, then code=password.
   Command:       30FF
   Raw data in:   01     0000 0A  FF300000 05  1122334455
                  state? ???? len(cmd      len(code      ))
   Raw data out:  03     0000 02  9000
                  state? ???? len(response)

__cdecl int writeNvram(char *szDeviceName, char* data, int dataLength, byte isCycle, byte isAppend, int sector)
   Purpose:       Writes NVRAM. Note that isCycle and isAppend only read the lower 8 bits (00h or 01h). CardManager.exe sends an U4 with garbage in the upper bits.
   Command:          D1FF if isCycle=0 and isAppend=0
                    1D1FF if isCycle=1 and isAppend=0
                  100D1FF if isCycle=0 and isAppend=1
                  101D1FF if isCycle=1 and isAppend=1
   Raw data in:   For RAM:
                  01     0000 0A  FF30 00 00 09  00000001 1122334455
                  state? ???? len(cmd  cy ap len(sector   data      ))
                  For CAM:
                  01     0000 0A  FF30 01 00 05  1122334455
                  state? ???? len(cmd  cy ap len(data      ))
   Raw data out:  03     0000 02  9000
                  state? ???? len(response)
```

### Possible bug with Cyclic NVRAM

I'm not sure if I did something wrong, or if CardManager.exe has a bug!

In Cyclic Access Memory: I write something (e.g. "foo") in sector 0 and commit it,
then I click "Select New" (sector 1 gets selected), then I write something else (e.g. "bar") to the new sector, and also commit this.
But when I re-open the Cyclic Access Dialog, every input is combined in sector 0 (i.e. "foobar") and all other sectors are empty.
`getStatusNvram()` shows Next Cyclic Write 0x0.  And `readNvram()` shows everything in Cyclic sector 0.
By using `writeNvram()` you can create a state where Next Cyclic Write > 0,
so this seems to be a bug in CardManger.exe and not a bug in the firmware.

### Possible bug with Fused NVRAM

The option `Fuse Persistently` at Card Manager does not seem to do anything.
In the disassembly, 13 checkboxes are evaluated, not 14.
The arguments to `configurateNvram()` in the stack trace are unchanged if this checkbox is checked.

### How does the uSD version of CardManagement.dll communicate with the hardware?

```
      // - The communication will be done through the file X:\__communicationFile
      //   Actually, the file can have any name. It is just important that the magic sequence is written somewhere (and the reply is read from that sector),
      //   this is how cryptovision TSE works. However, if another file name is chosen, there seems to be some problem with files on wrong sectors. So be careful and don't do it!
      // - In the initial state, the file has the contents:
      //   50 4C 45 41 53 45 20 44 4F 20 4E 4F 54 20 44 45 4C 45 54 45 20 54 48 49 53 20 46 49 4C 45 21 00 ("PLEASE DO NOT DELETE THIS FILE!\n")
      //   followed by 480 NUL bytes.
      // - When commands are sent, the first 32 bytes are overwritten by the hardcoded magic sequence
      //   10 6A F8 1A D6 F8 C8 70 AC 7E 85 F0 E9 9E F3 9D 1E 11 A1 BA 87 4A C6 DB 42 81 15 8E FE 6D 3C 81
      //   After that comes command data.
      //   A few useful dumps:     lockCard          =  01 00 00 05     FF 31 00 00  00
      //                                                <??????> <len>( < command >  <len>( no parameters ))
      //                           verify("test123") =  01 00 00 0D     FF 30 00 00  08     07     74 65 73 74 31 32 33
      //                                                <??????> <len>( < command >  <len>( <len> ( t  e  s  t  1  2  3 )))
      // - Analysis of the commands is in the table below.
      // - How to find which data is written? Patch CardManagement.dll and replace
      //   55 8B EC FF 75 0C E8 D5 19 00 00 FF 75 08 FF 15 C4 80 00 10 83 C4 08 33 C0 5D  with
      //   C3 8B EC FF 75 0C E8 D5 19 00 00 FF 75 08 FF 15 C4 80 00 10 83 C4 08 33 C0 5D
      //   This method calls Import "remove" from api-ms-win-crt-filesystem-l1-1-0.dll.
      //   To see what is written, execute one command (only one, because the second does not work since the file is not closed)
      //   on a drive that is NOT a secure card, for example a harddrive or normal USB stick.
      //   To see what is returned, execute one command on the real hardware.
```

### How does the USB version of CardManagement.dll communicate with the hardware?

```
      // - The SmartCard API (WinSCard.dll) is called to transmit data.
      //   The command codes seem to be the same as for the uSD card,
      //   because the command codes in the disassembly of the USB DLL match
      //   the data that the uSD DLL wrote to the communcation file.
      // - The file "<DeviceName>\__communicationFile" will be deleted. It is NOT used for transmitting data.
      //   Note that this is non-sense because for the PU-50n, the <DeviceName> has to be a name rather than a drive letter,
      //   so there will a file access to the local file "Swissbit Secure USB PU-50n DP 0\__communicationFile"
```

### How does the FileTunnelInterface.dll (from the TSE Maintenance Tool) communicate to the PU-50n (requires writeable drive letter though)?

```
      // The FileTunnelInterface.dll is part of the TSE Maintenance Tool.
      // If FileTunnelInterface.dll works on a TSE, then the TSE is considered "in an undefined state".
      // But with a Secure SD, the FileTunnelInterface works.
      // So, I guess both SecureSD and TSE are made out of the same "raw material",
      // and the own differences are crypto core yes/no (DP=no, SE/PE/TSE=yes), the firmware, and possible configuration/license.
      // The firmware and/or configuration is probably uploaded via the FileTunnelInterface (Vendor Command Interface).
      // If FileTunnelInterface.dll works, then either the firmware was not applied in the factory,
      // or maybe the TSE can fall back into the raw state if the firmware failed to boot (TSE Panic).
      // But that's just a theory. Let's just enjoy that FileTunnelInterface.dll works on a SecureSD,
      // because now we can also fetch the LTM data! The LTM data has the same structure as described
      // in the TSE Firmware Specification.
      // Unfortunately, FileTunnelInterface.dll does only work if there is a drive letter visible,
      // and it does not work with CD-ROM drive letters.
      //
      // But how does it communicate?
      // - It is neither SmartCard API, nor a file called __communicationFile!
      // - ProcessMonitor shows: There are ONLY calls of CreateFile("H:"), ReadFile("H:"), WriteFile("H:") and CloseFile("H:")
      //   Isn't this risky if FileTunnelInterface.dll would try to write to a regular drive?
      //   I noticed: There are a LOT of Read-Accesses to offsets 0 (sector 0), 1024 (sector 2), 1536 (sector 3) before any WriteFile is called at all.
      //   Theory: The read accesses are interpreted by a Swissbit device like a "morse code" and if the device reacts accordingly, then FileTunnelInterface.dll knows that it can now start WriteFile(),
      //   and the device will most likely interprete these WriteFile() accesses as commands (like the TSE-IO.bin for the TSE)
      // - FileTunnelInterface.dll from the Swissbit TSE does not work with PS-45u
```

### How does WormApi.dll communicate with the TSE?

- Via the file TSE-IO.bin (to a fixed sector). It is documented in the firmware specification.

### How does Swissbit Device Manager and libsbltm.dll communicate with PU-50n DP?

```
      // - First via the File Tunnel protocol to \Device\Harddrisk3\DR4\
      // - Then by writing to a non-existant file "G:\sb.vc"
      // - No Data Protection may be enabled. A PU-50n TSE cannot be detected either.
      // How does it communicate with PS-45u?
      // - First via the File Tunnel protocol to \Device\Harddrisk3\DR4\
      // - Direct ReadFile access to specific sectors of drive G:\
      //   Sector 598016   (offset 0x12400000)
      //   Sector 1048576  (offset 0x20000000)
      //   Sector 2097152  (offset 0x40000000)
      //   Sector 3145728  (offset 0x60000000)
      //   Sector 4194304  (offset 0x80000000)
      //   For some reason, if you look at them with Hex Editor (without prior File Tunnel Interface?), you only see the string
      //   *PROTECTED DATA*
      // - Data Protection may be enabled
```

### What I don't know yet (TODO):

- Where does the Device Manager get data like Temperature, Modell-ID, etc.? Does not seem to be in the unknown LTM data?

### Raw Commands

Commands are written to `X:\__communicationFile` (write-through) and then the result is read from that file (read without buffer).

Looking at the disassembly, the SmartCard API seems to use the same commands, but I don't have knowledge with that API.

#### Request

(TODO: Implement and analyze more commands. Also find out the return data.)

```
   ---------------------------------------------------------------------------------------------------------------------
    Command DLL name                        Parameter description                Example data
   ---------------------------------------------------------------------------------------------------------------------
       10FF activate(dev,?,?,?,?,?)
    20010FF activateSecure(dev,?)
       20FF deactivate(dev,?,?)
       30FF verify(dev,code) = unlock card  Code with 8 bit length prefix        01 00 00 0A FF 30 00 00 05 04 11 22 33 44
       31FF lockCard(dev)                   None (len=0)                         01 00 00 05 FF 31 00 00 00
       40FF changePassword(dev,?,?,?,?)
       50FF unblockPassword(dev,?,?,?,?)
       53FF setCdromAreaBackToDefault(dev)
      253FF setCdromAreaAndReadException(dev,?,?)
      353FF clearProtectionProfiles(dev)
    10353FF setProtectionProfiles(dev,?,?)
       ???? resetAndFormat(dev,?,?)         not yet implemented / analyzed
       60FF reset(dev,1,?)                  not yet implemented / analyzed
      160FF reset(dev,0,?)                  not yet implemented / analyzed
       70FF getStatus(dev,...)              None (len=0)                         01 00 00 05 FF 70 00 00 00
      170FF getCardId(dev,...)              None (len=0)                         01 00 00 05 FF 70 01 00 00
      270FF getApplicationVersion(dev,...)  None (len=0)                         01 00 00 05 FF 70 02 00 00
    10270FF getBaseFWVersion(dev,...)       None (len=0)                         01 00 00 05 FF 70 02 01 00
      370FF getStatusNvram(dev,...)         None (len=0)                         01 00 00 05 FF 70 03 00 00
      470FF getStatusException(dev,...)     None (len=0)                         01 00 00 05 FF 70 04 00 00
      570FF getLoginChallenge(dev,?)        not yet implemented / analyzed
      670FF getControllerId(dev,...)        None (len=0)                         01 00 00 05 FF 70 06 00 00
      770FF getProtectionProfiles(dev,...)  None (len=0)                         01 00 00 05 FF 70 07 00 00
      870FF getPartitionTable(dev,...)
      970FF getOverallSize(dev,...)
      380FF configureNvram(dev,?,?,?,?,?)   not yet implemented / analyzed 
      580FF setExtendedSecurityFlags(dev,?) not yet implemented / analyzed
      680FF setAuthenticityCheckSecret(dev,?,0), stored clear-text?
     1680FF setAuthenticityCheckSecret(dev,?,1), store hashed?
      780FF setSecureActivationKey(dev,?)   not yet implemented / analyzed
       D0FF readNvram(0,c), i.e. RAM        32 byte sector count <c>             01 00 00 09 FF D0 00 00 04 00 00 00 07
      1D0FF readNvram(1,c), i.e. cyclic     32 byte sector count <c>             01 00 00 09 FF D0 01 00 04 00 00 00 07
       D1FF writeNvram(dev,?,0,?,0,?)       not yet implemented / analyzed
      1D1FF writeNvram(dev,?,1,?,0,?)       not yet implemented / analyzed
    100D1FF writeNvram(dev,?,0,?,1,?)       not yet implemented / analyzed
    101D1FF writeNvram(dev,?,1,?,1,?)       not yet implemented / analyzed
       F1FF challengeFirmware(dev,?,?,?)    not yet implemented / analyzed 
       F2FF checkAuthenticity(dev,?,?)      not yet implemented / analyzed 
      (DLL) getVersion
      (DLL) getBuildDateAndTime
   ---------------------------------------------------------------------------------------------------------------------
```

#### Responses

```
   The first byte might be the status (0x03=finished, something else for "not ready")?
   2nd and 3rd byte are 00. Unknown usage.
   The 4th byte is the length of the response. Most fields are little endian.
   The last 2 bytes of the response are the status code in big endian.
   
   Example 1: Reply for failed verify(), command 0x30FF:
   03      00 00  02   6F 02
   state?  ?????  len  response 0x6F02=wrongPassword
   
   Example 2: Reply for getStatus(), command 0x70FF:
   03      00 00    11   40   02       0E    0F       00 00 00 01   00 00  FF FF FF FF  2B       90 00 
   state?  ?????    len  lic  sysstate retry soRetry  resetCounter  ?????  cdRomAddr    secflag  response 0x9000=ok
```

## Disclaimer

I am not responsible for any damages that may cause
by using this API or documentation. Note that
programmatically logging in with wrong password
(or wrong implementation) can destroy the device.

Use this API for private use only.
If you need this API commercially, please
search legal advice if this API may be used
(It's okay for me, but I am not sure about Swissbit).
I am not responsible for any legal issues or consequences
that may arise from the use of this information.
