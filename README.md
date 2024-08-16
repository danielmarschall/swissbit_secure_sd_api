
# Inofficial Swissbit Secure SD Card Utils

## Contents

This repository contains:

1. Partial reverse enginerring, documentation and header files for Swissbit PS-45u DP and PU-50n DP Secure USB Stick, since the "SDK" is just a bunch of useless tools and docs, but nothing to develop software with (and this is the purpose of a Software DEVELOPMENT Kit).

2. Swissbit Secure SD Utils (for Windows, language C#): A command line utility written in C that reads all the information of the medium. It also contains header files, so it can be used to implement programmatically lock and unlock a secure SD card or secure SD stick by calling CardManagement.dll. Note that the PU-50n DP Raspberry Pi Edition can be used as 8 GB Secure USB Stick! It is not just a dongle, but can also store data!

3. Unlock Card (For Linux, language C): Can be used to unlock PS-45u if you simply want to use it as "Secure SD Card" and mount and unmount it using Linux (without booting from it). Implements low-level file access without any library.

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
	verify(device, password);
}
```

If the extended security flag "Secure PIN Entry" is enabled, then the login is done like this:

```
int LicMode, SysState, RetryCount, SoRetryCount, ResetCount, CdRomAddress, ExtSecurityFlags;
getStatus(deviceName, &LicMode, &SysState, &RetryCount, &SoRetryCount, &ResetCount, &CdRomAddress, &ExtSecurityFlags);
if (ExtSecurityFlags&0x10 != 0) {
	char[32] challenge;
	getHashChallenge(device, &challenge); // Alias of getLoginChallenge(). The login challenge (also called hash challenge) gets changed after each successful login.
	code = sha256(sha256(password) + challenge);
	verify(device, code);
}
```

### Locking the card

Locking the card is done via `lockCard()` method in CardManagement.dll.

### Error codes

Some Error Messages collected:

```
Return 0000 : OK
Return 0008 : Generic read error, e.g. "Failed SCardTransmit"  (happens for some reason when you save 259+ bytes to NVRAM and then try to read it)
Return 3790 : Happens when you save 257 bytes to NVRAM and then try to read it
Return 3131 : Happens when you save 258 bytes to NVRAM and then try to read it
Return 9001 : Change Protection Profile (Partition): Sum of Partition sizes is larger than total size of drive .... sometimes something else??? generic error???
Return 6B00 : Happens at getPartitionTable if Firmware of Card is too old
Return 6F02 : Wrong password entered or access denied
Return 6F05 : No password entered, or password too short
Return 6FFC : Security Settings changed; need powercycle to reload stuff
```

### CardManagement.dll methods

See the source code of SwissbitSecureSDUtils to see how to use the methods the library CardManagement.dll exports.

Note that not all methods have yet been researched.

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

Commands are written to X:\__communicationFile (write-through) and then the result is read from that file (read without buffer).

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
       30FF verify(dev,pwd) = unlock card   Password with 8 bit length prefix    01 00 00 0A FF 30 00 00 05 04 11 22 33 44
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
	03      00 00    11   40   02       0E    0F      00 00 00 01     00 00 FF FF FF FF   2B       90 00 
	state?  ?????    len  lic sysstate retry soRetry  resetCounter    ????? cdRomAddr    secflag   response 0x9000=ok
```
