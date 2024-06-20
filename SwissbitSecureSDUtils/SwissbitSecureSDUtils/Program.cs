using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace SwissbitSecureSDUtils
{
  // ******************************************************************
  // There is a big Problem with CardManagerLite.exe and CardManagerCLI.exe
  // For the USB Stick 'PU-50n DP', CardManagement.dll cannot access the device via drive letter (e.g. "G:\").
  // It can only access the SmartCard name "Swissbit Secure USB PU-50n DP 0".
  // You can verify this by query "G:\" in CardManager.exe .
  // The big problem is that CardManagerLite.exe and CardManagerCli.exe only accept drive letters  (e.g. "G:"), not SmartCard names.
  // Therefore, you CANNOT use the CLI tool for the USB Secure USB Stick! This is a showstopper, so you must use the GUI tool and cannot
  // configure the secure USB stick via command line or programmatically. (Isn't this the purpose of a Software DEVELOPMENT Kit?)
  // 
  // To fix this issue, patch CardManagerCLI.exe by disabling the syntax check for the "--mountpoint" argument:
  //     Search:  75 1F 0F BE 02 50 FF 15 D8 D2 40 00 8B 7D FC 83 C4 04 85 C0 74 0E 8B 47 0C 80 78 01 3A 74
  //     Replace: 90 90 0F BE 02 50 FF 15 D8 D2 40 00 8B 7D FC 83 C4 04 85 C0 90 90 8B 47 0C 80 78 01 3A EB
  //              ^^ ^^                                                       ^^ ^^                      ^^
  // Some commands like fetching Partition Table don't seem to work though. I didn't investigate that yet.
  // ******************************************************************

  // ******************************************************************
  // In addition, this C# library can help you using the USD/uSD device by calling the DLL instead of the non-working CLI EXE.
  // I have also found a lot of undocumented things, e.g. how to interprete the extended security flags and how to read the Life Time Management (LTM) data.
  // All these things shall be part of the documentation, but they are not. The documentation is insufficient,
  // error codes are not documented, various options in the GUI are not described, and the worst of all:
  // The Software DEVELOPMENT Kit does not allow that you develop using a programming language... It is just a collection of EXE, DLL, and PDF files...
  // Note: This C# library has only been tested with the USB Stick (PU-50n DP), not with the uSD Card (PS-45u DP).
  // ******************************************************************

  /**
   * <summary>Calls methods of CardManagement.dll (part of the Swissbit Raspberry Pi Secure Boot "SDK")</summary>
   */
  class CardManagement
  {
    // Some Error Messages collected:
    // Return 0000 : OK
    // Return 0008 : Generic read error, e.g. "Failed SCardTransmit"  (happens for some reason when you save 259+ bytes to NVRAM and then try to read it)
    // Return 3790 : Happens when you save 257 bytes to NVRAM and then try to read it
    // Return 3131 : Happens when you save 258 bytes to NVRAM and then try to read it
    // Return 9001 : Change Protection Profile (Partition): Sum of Partition sizes is larger than total size of drive .... sometimes something else??? generic error???
    // Return 6B00 : Happens at getPartitionTable if Firmware of Card is too old
    // Return 6F02 : Wrong password entered or access denied
    // Return 6F05 : No password entered, or password too short
    // Return 6FFC : Security Settings changed; need powercycle to reload stuff

    // TODO: Implement activate(...)

    // TODO: Implement activateSecure(...)

    // TODO: Implement challengeFirmware(...)

    // TODO: Implement changePassword(...)

    // TODO: Implement checkAuthenticity(...)

    // TODO: Implement clearProtectionProfiles(...)

    // TODO: Implement configureNvram(...)

    // TODO: Implement deactivate(...)

    #region getApplicationVersion
    [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getApplicationVersion", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U4)]
    public static extern int getApplicationVersion(
        [MarshalAs(UnmanagedType.LPStr)] string deviceName,
        [MarshalAs(UnmanagedType.U4)] out int ApplicationVersion);
    #endregion

    #region getBaseFWVersion
    [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getBaseFWVersion", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U4)]
    public static extern int _getBaseFWVersion(
        [MarshalAs(UnmanagedType.LPStr)] string deviceName,
        [MarshalAs(UnmanagedType.SysInt)] IntPtr firmware8bytes,
        [MarshalAs(UnmanagedType.U4)] ref int part2);
    public static int getBaseFWVersion(string deviceName, out string baseFwVersion)
    {
      int baseFWPart2 = 0;
      int baseFWVersionSize = 8;
      byte[] baseFWVersion = new byte[baseFWVersionSize];
      IntPtr unmanagedPointer = Marshal.AllocHGlobal(baseFWVersionSize);
      //Marshal.Copy(baseFWVersion, 0, unmanagedPointer, baseFWVersionSize);
      int res = _getBaseFWVersion(deviceName, unmanagedPointer, ref baseFWPart2);
      Marshal.Copy(unmanagedPointer, baseFWVersion, 0, baseFWVersionSize);
      Marshal.FreeHGlobal(unmanagedPointer);
      baseFwVersion = "";
      for (int i = 0; i < baseFWVersionSize; i++)
      {
        baseFwVersion += Convert.ToChar(baseFWVersion[i]);
      }
      // Note: The screenshots in the manual shows the examples "211028s9 X100" and "170614s8  110"
      //       My USB device (PU-50n DP) has "180912u9  106" but showns in the Swissbit Device Manager as "180912 1.06"
      baseFwVersion += " " + Encoding.ASCII.GetString(BitConverter.GetBytes(baseFWPart2).ToArray());
      return res;
    }
    #endregion

    #region getBuildDateAndTime (only for 2019 USB/uSD DLL, not for 2022 uSD DLL)
    [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getBuildDateAndTime", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.SysInt)]
    private static extern IntPtr _getBuildDateAndTime();
    public static string getBuildDateAndTime()
    {
      return Marshal.PtrToStringAnsi(_getBuildDateAndTime());
    }
    #endregion

    #region getCardId
    [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getCardId", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U4)]
    private static extern int _getCardId(
        [MarshalAs(UnmanagedType.LPStr)] string deviceName,
        [MarshalAs(UnmanagedType.SysInt)] IntPtr cardid12bytes);
    public static int getCardId(string deviceName, out string cardId)
    {
      int cardIdSize = 16;
      var cardIdBytes = new byte[cardIdSize];
      IntPtr unmanagedPointer = Marshal.AllocHGlobal(cardIdSize);
      //Marshal.Copy(cardIdBytes, 0, unmanagedPointer, cardIdSize);
      int res = _getCardId(deviceName, unmanagedPointer);
      Marshal.Copy(unmanagedPointer, cardIdBytes, 0, cardIdSize);
      Marshal.FreeHGlobal(unmanagedPointer);
      cardId = "";
      for (int i = 0; i < cardIdSize; i++)
      {
        cardId += " " + cardIdBytes[i].ToString("X2");
      }
      cardId = cardId.Trim();
      return res;
    }
    #endregion

    #region getControllerId (Unique ID)
    [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getControllerId", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U4)]
    private static extern int _getControllerId(
        [MarshalAs(UnmanagedType.LPStr)] string deviceName,
        [MarshalAs(UnmanagedType.SysInt)] IntPtr conrollerId,
        [MarshalAs(UnmanagedType.U4)] ref int conrollerIdSize);
    public static int getControllerId(string deviceName, out string controllerId)
    {
      int controllerIdSize = 99;
      byte[] controllerIdBytes = new byte[controllerIdSize];
      IntPtr unmanagedPointer = Marshal.AllocHGlobal(controllerIdSize);
      //Marshal.Copy(controllerIdBytes, 0, unmanagedPointer, controllerIdSize);
      int res = _getControllerId(deviceName, unmanagedPointer, ref controllerIdSize);
      Marshal.Copy(unmanagedPointer, controllerIdBytes, 0, controllerIdSize);
      Marshal.FreeHGlobal(unmanagedPointer);
      controllerId = "";
      for (int i = 0; i < controllerIdSize; i++)
      {
        controllerId += " " + controllerIdBytes[i].ToString("X2");
      }
      controllerId = controllerId.Trim();
      return res;
    }
    #endregion

    // TODO: Implement getHashChallenge(...)

    // TODO: Implement getLoginChallenge(...)

    #region getOverallSize
    [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getOverallSize", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U4)]
    public static extern int getOverallSize(
        [MarshalAs(UnmanagedType.LPStr)] string deviceName,
        [MarshalAs(UnmanagedType.U4)] out uint OverallSize);
    #endregion

    #region getPartitionTable
    [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getPartitionTable", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U4)]
    public static extern int getPartitionTable(
        [MarshalAs(UnmanagedType.LPStr)] string deviceName,
        [MarshalAs(UnmanagedType.U4)] out int PartitionTableUnknown1);
    #endregion

    #region getProtectionProfiles
    [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getProtectionProfiles", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U4)]
    public static extern int getProtectionProfiles(
        [MarshalAs(UnmanagedType.LPStr)] string deviceName,
        [MarshalAs(UnmanagedType.U4)] out int ProtectionProfileUnknown1,
        [MarshalAs(UnmanagedType.U4)] out int ProtectionProfileUnknown2,
        [MarshalAs(UnmanagedType.U4)] out int ProtectionProfileUnknown3);
    #endregion

    #region getStatus
    [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getStatus", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U4)]
    public static extern int getStatus(
        [MarshalAs(UnmanagedType.LPStr)] string deviceName,
        [MarshalAs(UnmanagedType.U4)] out int LicenseMode,
        [MarshalAs(UnmanagedType.U4)] out int SystemState,
        [MarshalAs(UnmanagedType.U4)] out int RetryCounter,
        [MarshalAs(UnmanagedType.U4)] out int SoRetryCounter,
        [MarshalAs(UnmanagedType.U4)] out int ResetCounter,
        [MarshalAs(UnmanagedType.U4)] out int CdRomAddress,
        [MarshalAs(UnmanagedType.U4)] out int ExtSecurityFlags);
    #endregion

    #region getStatusException
    [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getStatusException", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U4)]
    public static extern int getStatusException(
        [MarshalAs(UnmanagedType.LPStr)] string deviceName,
        [MarshalAs(UnmanagedType.U4)] out int ExceptionUnknown1,
        [MarshalAs(UnmanagedType.U4)] out int ExceptionUnknown2,
        [MarshalAs(UnmanagedType.U4)] out int ExceptionUnknown3,
        [MarshalAs(UnmanagedType.U4)] out int partition1Offset,
        [MarshalAs(UnmanagedType.U4)] out int ExceptionUnknown4);
    #endregion

    #region getStatusNvram
    [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getStatusNvram", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U4)]
    public static extern int getStatusNvram(
        [MarshalAs(UnmanagedType.LPStr)] string deviceName,
        [MarshalAs(UnmanagedType.U4)] out int AccessRights,
        [MarshalAs(UnmanagedType.U4)] out int TotalNvRamSize,
        [MarshalAs(UnmanagedType.U4)] out int RandomAccessSectors,
        [MarshalAs(UnmanagedType.U4)] out int CyclicAccessSectors,
        [MarshalAs(UnmanagedType.U4)] out int NextCyclicWrite);
    #endregion

    #region getVersion
    [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getVersion", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U4)]
    public static extern int getVersion();
    #endregion

    #region lockCard (Lock Data Protection)
    [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "lockCard", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U4)]
    public static extern int lockCard(
        [MarshalAs(UnmanagedType.LPStr)] string deviceName);
    #endregion

    #region readNvram
    [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "readNvram", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U4)]
    private static extern int _readNvram(
        [MarshalAs(UnmanagedType.LPStr)] string deviceName,
        [MarshalAs(UnmanagedType.SysInt)] IntPtr value,
        [MarshalAs(UnmanagedType.U4)] ref int valueLength,
        [MarshalAs(UnmanagedType.Bool)] bool cyclic,
        [MarshalAs(UnmanagedType.U4)] int sectorNumber
        );
    public static int readNvram(string deviceName, bool cyclic, int sectorNumber, out byte[] output)
    {
      int outputSize = 2000;
      byte[] outputBytes = new byte[outputSize];
      IntPtr unmanagedPointer = Marshal.AllocHGlobal(outputSize);
      //Marshal.Copy(controllerIdBytes, 0, unmanagedPointer, outputSize);
      int res = _readNvram(deviceName, unmanagedPointer, ref outputSize, cyclic, sectorNumber);
      Marshal.Copy(unmanagedPointer, outputBytes, 0, outputSize);
      Marshal.FreeHGlobal(unmanagedPointer);
      if (res != 0)
      {
        output = null;
      }
      else
      {
        output = new byte[outputSize];
        for (int i = 0; i < outputSize; i++)
        {
          output[i] = outputBytes[i];
        }
      }
      return res;
    }
    #endregion

    // TODO: Implement reset(...)

    // TODO: Implement resetAndFormat(...)

    // TODO: Implement setAuthenticityCheckSecret(...)

    // TODO: Implement setCdromAreaAndReadException(...)

    // TODO: Implement setCdromAreaBackToDefault(...)

    // TODO: Implement setExtendedSecurityFlags(...)

    // TODO: Implement setProtectionProfiles(...)

    // TODO: Implement setSecureActivationKey(...)

    // TODO: Implement unblockPassword(...)

    #region verify (Unlock Data Protection)
    [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "verify", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U4)]
    private static extern int _verify(
        [MarshalAs(UnmanagedType.LPStr)] string deviceName,
        [MarshalAs(UnmanagedType.LPStr)] string password,
        [MarshalAs(UnmanagedType.U4)] int passwordLength);
    public static int verify(string deviceName, string password)
    {
      return _verify(deviceName, password, password.Length);
    }
    #endregion

    // TODO: Implement writeNvram(...)
  }

  /**
   * <summary>Calls methods of FileTunnelInterface.dll (part of the Swissbit PU-50n TSE Maintenance Tool), which is also compatible with the PU-50n DP stick.</summary>
   */
  class VendorCommandsInterface
  {
    private IntPtr ci;

    // Diese Methoden sind durch das TSE Maintenance Tool eingebunden:

    [DllImport("FileTunnelInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int vci_open_volume(out IntPtr ci, uint flags, char volume);

    [DllImport("FileTunnelInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int vci_read_extended_card_lifetime_information(IntPtr ci, byte[] response);

    [DllImport("FileTunnelInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int vci_read_chip_id(IntPtr ci, byte[] chipID, int chipIDLen);

    [DllImport("FileTunnelInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int vci_close(IntPtr ci);

    // TODO: Diese weiteren Funktionen gibt es:
    // - ci_open(???) in IDA ist es rot
    // - ci_transmit(x, x, x, x, x, x)
    // - ci_close(x)

    // ---

    public VendorCommandsInterface(string volumeName)
    {
      ci = default(IntPtr);
      if (vci_open_volume(out ci, 1u, volumeName[0]) != 0)
      {
        throw new Exception("Cannot open the volume " + volumeName);
      }
    }

    ~VendorCommandsInterface()
    {
      vci_close(ci);
    }

    public byte[] readLTMData()
    {
      byte[] array = new byte[512];
      int num = vci_read_extended_card_lifetime_information(ci, array);
      if (num != 0)
      {
        throw new Exception("Error reading the card lifetime information, error: " + num);
      }
      return array;
    }

    public string readChipID()
    {
      byte[] array = new byte[25];
      int num = vci_read_chip_id(ci, array, array.Length);
      if (num != 0)
      {
        throw new Exception("Error reading the target info, error: " + num);
      }
      return Encoding.ASCII.GetString(array).TrimEnd('\0');
    }
  }

  // TODO: Implement libsbltm.dll (from Device Manager LTM SDK)

  /**
   * <summary>Test program to receive various data about an attached device. It is mainly the same as the things that CardManagerCLI.exe outputs. Just working. And with LTM data.</summary>
   */
  internal class Program
  {

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool SetDllDirectory(string path);

    static void Main(string[] args)
    {
      Console.WriteLine("Swissbit Secure USB Stick 'PU-50n DP' and Secure SD Card 'PS-45u DP'");
      Console.WriteLine("(probably also compatible with SE and PE security products)");
      Console.WriteLine("Access using FileTunnelInterface.dll (from TSE Maintenance Tool) and CardManagement.dll (from SDK)");
      Console.WriteLine("");

      if ((args.Length < 1) || (args.Length > 1) || (args.Length == 1 && (args[0] == "/?" || args[0] == "--help")))
      {
        Console.WriteLine("Syntax: SwissbitSecureSDUtils.exe 'Device Name'");
        Console.WriteLine("For PS-45u (uSD): Use drive letter, e.g. 'X:'");
        Console.WriteLine("For PU-50n (USB): Use SmartCard device name, e.g. 'Swissbit Secure USB PU-50n DP 0'");
        Console.WriteLine("If Device Name is omitted, then 'Swissbit Secure USB PU-50n DP 0' will be chosen.");
        Console.WriteLine("");
        if (args.Length != 0) return;
      }

      string deviceName;
      if (args.Length == 1)
      {
        deviceName = args[0];
      }
      else
      {
        deviceName = "Swissbit Secure USB PU-50n DP 0";
      }
      bool isUSB = !deviceName.Contains(':');

      var path = Directory.GetCurrentDirectory(); // Path.GetDirectoryName(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location));
      path = Path.Combine(path, "dll_" + (isUSB ? "usb" : "sd") + "_" + (IntPtr.Size == 8 ? "64" : "32"));
      bool ok = SetDllDirectory(path);
      if (!ok) throw new System.ComponentModel.Win32Exception();
      Console.WriteLine("Choose DLL " + path + "\\CardManagement.dll");
      Console.WriteLine("for device '" + deviceName + "'");

      // Test
      //SecureSd_Unlock_Card(deviceName, "test123");
      //SecureSd_Lock_Card(deviceName);
      //return;


      SecureSd_DeviceInfo(deviceName);

      #region Try to find a writeable driveletter in order to call FileTunnelInterface (identify using Unique Chip ID)
      {
        Console.WriteLine("Try to find a writeable driveletter in order to call FileTunnelInterface...");
        bool foundSomething = false;
        string sUniqueId;
        if (CardManagement.getControllerId(deviceName, out sUniqueId) == 0)
        {
          sUniqueId = sUniqueId.Replace(" ", "").ToLower().Trim();
          sUniqueId = sUniqueId.TrimStart('0'); // vci.readChipID does this automatically, so we need to do it too

          for (char driveLetter = 'd'; driveLetter <= 'z'; driveLetter++)
          {
            try
            {
              VendorCommandsInterface vci = new VendorCommandsInterface(driveLetter.ToString());
              string sChipId = vci.readChipID();
              vci = null;
              if (sChipId.ToLower().TrimStart('0').Equals(sUniqueId))
              {
                Console.WriteLine("Found drive letter " + driveLetter.ToString().ToUpper() + ": to call FileTunnelInterface!");
                Console.WriteLine("");
                VendorCommandsInterfaceDeviceStatus(driveLetter.ToString());
                foundSomething = true;
                break;
              }
            }
            catch (Exception)
            {
            }
          }
        }
        if (!foundSomething)
        {
          Console.WriteLine("Nothing found. You need to unlock the drive and/or a writeable partition must be visible");
        }
      }
      #endregion
    }

    private static bool SecureSd_Unlock_Card(string deviceName, string password)
    {
      Console.WriteLine("Unlock Card: " + deviceName);
      int res = CardManagement.verify(deviceName, password);
      if (res != 0)
      {
        Console.WriteLine("ERROR: verify() returned " + res.ToString("X4"));
      }
      return res == 0;
    }

    private static bool SecureSd_Lock_Card(string deviceName)
    {
      Console.WriteLine("Lock Card: " + deviceName);
      int res = CardManagement.lockCard(deviceName);
      if (res != 0)
      {
        Console.WriteLine("ERROR: lockCard() returned " + res.ToString("X4"));
      }
      return res == 0;
    }

    private static void SecureSd_DeviceInfo(string deviceName)
    {
      #region DLL Version
      int dllVersion = CardManagement.getVersion();
      string dllVersionString = "CardManagement.dll version " + ((int)Math.Floor((decimal)dllVersion / 0x100)).ToString("X") + "." + (dllVersion % 0x100).ToString("X");
      try
      {
        string s = CardManagement.getBuildDateAndTime();
        dllVersionString += " built on " + s;
      }
      catch (Exception) { }
      Console.WriteLine(dllVersionString);
      Console.WriteLine("");
      #endregion

      #region getStatus()
      int LicenseMode, SystemState, RetryCounter, SoRetryCounter, ResetCounter, CdRomAddress, ExtSecurityFlags;
      int res = CardManagement.getStatus(deviceName, out LicenseMode, out SystemState, out RetryCounter, out SoRetryCounter, out ResetCounter, out CdRomAddress, out ExtSecurityFlags);
      Console.WriteLine("***** CardManagement.dll getStatus() returns: 0x" + res.ToString("X4"));
      if (res == 0)
      {
        // PU-50n DP Raspberry Pi Edition = 0x40. What else is possible?
        // CardManager.exe : If License Mode is equal to 0x20, then "Extended Security Flags" are not shown in the "Device Status" dialog, also getStatusException() is not called.
        //                   However, the "Security Settings" dialog can still be opened?!
        Console.WriteLine("License Mode            : 0x" + LicenseMode.ToString("X2"));
        if (LicenseMode == 0x20) Console.WriteLine("                          [Extended Security Flags are not available]");
        Console.Write("System State            : 0x" + SystemState.ToString("X2") + " = ");
        switch (SystemState)
        {
          case 0: Console.WriteLine("Transparent Mode"); break;
          case 1: Console.WriteLine("Data Protection Unlocked"); break;
          case 2: Console.WriteLine("Data Protection Locked"); break;
          default: Console.WriteLine("Unknown"); break;
        }
        Console.WriteLine("Retry Counter           : " + RetryCounter);
        Console.WriteLine("SO Retry Counter        : " + SoRetryCounter);
        Console.WriteLine("Number of Resets        : " + ResetCounter);
        Console.WriteLine("CD_ROM address          : 0x" + CdRomAddress.ToString("X")); // This field is described in NetPolicyServer User Manual version 2.6-2.9 for CardManagerCLI.exe, but not shown in the current version of any EXE or DLL. It might be obsolete
        Console.WriteLine("Extended Security Flags : 0x" + ExtSecurityFlags.ToString("X2"));
        Console.WriteLine("                          [" + ((ExtSecurityFlags & 0x1) == 0 ? "x" : " ") + "] Support Fast Wipe (~0x1)");
        Console.WriteLine("                          [" + ((ExtSecurityFlags & 0x2) != 0 ? "x" : " ") + "] Reset Requires SO PIN (0x2)");
        Console.WriteLine("                          [" + ((ExtSecurityFlags & 0x4) != 0 ? "x" : " ") + "] Unknown (0x4)");
        Console.WriteLine("                          [" + ((ExtSecurityFlags & 0x8) == 0 ? "x" : " ") + "] Multiple Partition Protection (~0x8)");
        Console.WriteLine("                          [" + ((ExtSecurityFlags & 0x10) != 0 ? "x" : " ") + "] Secure PIN Entry (0x10)");
        Console.WriteLine("                          [" + ((ExtSecurityFlags & 0x20) != 0 ? "x" : " ") + "] Login Status Survives Soft Reset (0x20)");
        Console.WriteLine("                          [" + ((ExtSecurityFlags & 0x40) != 0 ? "x" : " ") + "] Unknown (0x40)");
        Console.WriteLine("                          [" + ((ExtSecurityFlags & 0x80) != 0 ? "x" : " ") + "] Unknown (0x80)");
      }
      Console.WriteLine("");
      #endregion

      #region getStatusException()
      int ExceptionUnknown1, ExceptionUnknown2, ExceptionUnknown3, partition1Offset, ExceptionUnknown4;
      res = CardManagement.getStatusException(deviceName, out ExceptionUnknown1, out ExceptionUnknown2, out ExceptionUnknown3, out partition1Offset, out ExceptionUnknown4);
      Console.WriteLine("***** CardManagement.dll getStatusException() returns: 0x" + res.ToString("X4"));
      if (res == 0)
      {
        // TODO: What are these values??? Somewhere should be partition1Offset
        // NetPolicyServer User Manual version 2.6-2.9 shows 4 values
        // - defaultCDRomAddress         (this is not part of the current EXE or DLL)
        // - readExceptionAddress        (this is not part of the current EXE or DLL) 
        // - partition1Offset (in hex)
        // - partition1Offset (in dec)
        // Assuming that partition1Offset is only 1 value returned by the DLL, then we would only have 3 values. What are the other 2?
        // In CLI, argument 5 is shown as partition1Offset. Arguments 2, 3, 4, 6 are unknown yet.
        Console.WriteLine("ExceptionUnknown1       : 0x" + ExceptionUnknown1.ToString("X"));
        Console.WriteLine("ExceptionUnknown2       : 0x" + ExceptionUnknown2.ToString("X"));
        Console.WriteLine("ExceptionUnknown3       : 0x" + ExceptionUnknown3.ToString("X"));
        Console.WriteLine("partition1Offset        : 0x" + partition1Offset.ToString("X") + " = " + partition1Offset + " blocks");
        Console.WriteLine("ExceptionUnknown4       : 0x" + ExceptionUnknown4.ToString("X"));
      }
      Console.WriteLine("");
      #endregion

      #region getApplicationVersion
      int ApplicationVersion;
      res = CardManagement.getApplicationVersion(deviceName, out ApplicationVersion);
      Console.WriteLine("***** CardManagement.dll getApplicationVersion() returns: 0x" + res.ToString("X4"));
      if (res == 0)
      {
        Console.WriteLine("Application Version     : " + ApplicationVersion.ToString("x")); // In the binary it can be seen that it also seems to be called "CFE version". Also funny typos: "Yor CFE version is" and "You must be loged in to read the partition table".
      }
      Console.WriteLine("");
      #endregion

      #region getBaseFWVersion
      string baseFwVersion = "";
      res = CardManagement.getBaseFWVersion(deviceName, out baseFwVersion);
      Console.WriteLine("***** CardManagement.dll getBaseFWVersion() returns: 0x" + res.ToString("X4"));
      if (res == 0)
      {
        Console.WriteLine("Base Firmware Version   : " + baseFwVersion);
      }
      Console.WriteLine("");
      #endregion

      #region getCardId
      string cardId = "";
      res = CardManagement.getCardId(deviceName, out cardId);
      Console.WriteLine("***** CardManagement.dll getCardId() returns: 0x" + res.ToString("X4"));
      if (res == 0)
      {
        Console.WriteLine("Card ID                 : " + cardId);
      }
      Console.WriteLine("");
      #endregion

      #region getControllerId (Unique ID)
      string controllerId = "";
      res = CardManagement.getControllerId(deviceName, out controllerId);
      Console.WriteLine("***** CardManagement.dll getControllerId() returns: 0x" + res.ToString("X4"));
      if (res == 0)
      {
        // "getControllerId()" shows the "UniqueID" for USB PU-50n, while getCardId() shows something weird. Confusing!
        // The "NetPolicyServer User Manual" writes:
        //    Please note the last value in the output (“Controller ID”, Figure 12). This alphanumeric sequence
        //    without any blank spaces is the Unique ID of the DataProtection device, which is needed for the Net
        //    Policy database entry in the Net Policy server.
        //    Note: It is not the value shown as the Unique Card ID!
        // CardManager.exe of USB and CardManager of uSD both use getControllerId() to display the "Unique ID"
        // field in the Device Status dialog. Therefore we can be sure that this is surely the Unique ID,
        // and also identical to the Chip ID from the FTI.
        // On the other hand, a screenshot in the Swissbit Raspberry Pi Secure Boot documentation shows
        // that Controller ID can be all zeros. Weird?? Maybe just a development system?
        Console.WriteLine("Unique ID               : " + controllerId);
      }
      Console.WriteLine("");
      #endregion

      // TODO: getPartitionTable(?, ?)

      #region getProtectionProfiles (Work in progress)
      // TODO: getProtectionProfiles(?, ?, ?, ?).
      //       CardManagerCLI.exe help says:  <ProfileStr> is a , separated list in the form StartOfRange,Type,StartOfRange,Type StartOfRange is a hexadecimal value, Type: 0=PRIVATE_RW, 1=PUBLIC_RW, 2=PRIVATE_CDROM 3=PUBLIC_CDROM
      //       In CardManagerCli.exe of the uSDCard (not USB) there are 3 more entries: 4=PRIVATE_RO, 5=PUBLIC_RO, 6=FLEXIBLE_RO
      //       On my stick it says in CardManagerCli.exe:
      //       0x00000000 PUBLIC_CDROM
      //       0x00018000 PRIVATE_RW      <-- Why does this say "0x18000" ? My CD ROM partition is 46 MB, so shouldn't the PrivateRW start earlier? 
      // Note: CardManager.exe only calls getProtectionProfiles, not getPartitionTable... therefore, every information needs to be in getProtectionProfiles
      int ProtectionProfileUnknown1, ProtectionProfileUnknown2, ProtectionProfileUnknown3;
      res = CardManagement.getProtectionProfiles(deviceName, out ProtectionProfileUnknown1, out ProtectionProfileUnknown2, out ProtectionProfileUnknown3);
      Console.WriteLine("***** CardManagement.dll getProtectionProfiles() returns: 0x" + res.ToString("X4"));
      if (res == 0)
      {
        Console.WriteLine("Unknown1                : 0x" + ProtectionProfileUnknown1.ToString("X8")); // For me, outputs 0x2
        Console.WriteLine("Unknown2                : 0x" + ProtectionProfileUnknown2.ToString("X8")); // For me, outputs 0x3
        Console.WriteLine("Unknown3                : 0x" + ProtectionProfileUnknown3.ToString("X8")); // For me, leaves value unchanged (therefore it seems to be an input argument)
      }
      Console.WriteLine("");
      #endregion

      /*
      #region getPartitionTable (Work in progress)
      int PartitionTableUnknown1;
      // TODO: How does getPartitionTable(?, ?) work?
      res = CardManagement.getPartitionTable(deviceName, out PartitionTableUnknown1); // Outputs 0x6FFD
      Console.WriteLine("***** CardManagement.dll getPartitionTable() returns: 0x" + res.ToString("X4"));
      if (res == 0)
      {
          Console.WriteLine("Unknown1                : 0x" + PartitionTableUnknown1.ToString("X8"));
      }
      Console.WriteLine("");
      #endregion
      */

      #region getStatusNvram()
      int NvramAccessRights, NvramTotalNvramSize, NvramRandomAccessSectors, NvramCyclicAccessSectors, NextCyclicWrite;
      res = CardManagement.getStatusNvram(deviceName, out NvramAccessRights, out NvramTotalNvramSize, out NvramRandomAccessSectors, out NvramCyclicAccessSectors, out NextCyclicWrite);
      Console.WriteLine("***** CardManagement.dll getStatusNvram() returns: 0x" + res.ToString("X4"));
      if (res == 0)
      {
        int RandomRights = NvramAccessRights & 0xFF;
        int CyclicRights = (NvramAccessRights >> 8) & 0xFF;
        Console.WriteLine("Access Rights           : 0x" + NvramAccessRights.ToString("X8"));
        Console.WriteLine("     Cyclic NVRAM       : 0x" + CyclicRights.ToString("X2"));
        Console.WriteLine("                          [" + ((CyclicRights & 0x1) != 0 ? "x" : " ") + "] All Read (0x1)");
        Console.WriteLine("                          [" + ((CyclicRights & 0x2) != 0 ? "x" : " ") + "] All Write (0x2)");
        Console.WriteLine("                          [" + ((CyclicRights & 0x4) != 0 ? "x" : " ") + "] User Read (0x4)");
        Console.WriteLine("                          [" + ((CyclicRights & 0x8) != 0 ? "x" : " ") + "] User Write (0x8)");
        Console.WriteLine("                          [" + ((CyclicRights & 0x10) != 0 ? "x" : " ") + "] Wrap around (0x10)");
        Console.WriteLine("                          [" + ((CyclicRights & 0x20) != 0 ? "x" : " ") + "] Security Officer Read (0x20)");
        Console.WriteLine("                          [" + ((CyclicRights & 0x40) != 0 ? "x" : " ") + "] Security Officer Write (0x40)");
        Console.WriteLine("                          [" + ((CyclicRights & 0x80) != 0 ? "x" : " ") + "] Fused Persistently (0x80)"); // not 100% sure
        Console.WriteLine("     Random NVRAM       : 0x" + RandomRights.ToString("X2"));
        Console.WriteLine("                          [" + ((RandomRights & 0x1) != 0 ? "x" : " ") + "] All Read (0x1)");
        Console.WriteLine("                          [" + ((RandomRights & 0x2) != 0 ? "x" : " ") + "] All Write (0x2)");
        Console.WriteLine("                          [" + ((RandomRights & 0x4) != 0 ? "x" : " ") + "] User Read (0x4)");
        Console.WriteLine("                          [" + ((RandomRights & 0x8) != 0 ? "x" : " ") + "] User Write (0x8)");
        Console.WriteLine("                          [" + ((RandomRights & 0x10) != 0 ? "x" : " ") + "] Not defined (0x10)");
        Console.WriteLine("                          [" + ((RandomRights & 0x20) != 0 ? "x" : " ") + "] Security Officer Read (0x20)");
        Console.WriteLine("                          [" + ((RandomRights & 0x40) != 0 ? "x" : " ") + "] Security Officer Write (0x40)");
        Console.WriteLine("                          [" + ((RandomRights & 0x80) != 0 ? "x" : " ") + "] Fused Persistently (0x80)"); // not 100% sure
        Console.WriteLine("Total NVRAM Size        : 0x" + NvramTotalNvramSize.ToString("X"));
        Console.WriteLine("Random Access Sectors   : 0x" + NvramRandomAccessSectors.ToString("X"));
        Console.WriteLine("Cyclic Access Sectors   : 0x" + NvramCyclicAccessSectors.ToString("X"));
        Console.WriteLine("Next Cyclic Write       : 0x" + NextCyclicWrite.ToString("X"));
      }
      Console.WriteLine("");
      #endregion

      if (res == 0)
      {
        // TODO: I'm not sure if I did something wrong, or if CardManager.exe has a bug.
        //       In Cyclic Access Memory: I write something in sector 0, then I click "Select New" (sector 1 gets selected), then I write something to the new sector.
        //       But after I commit and re-open the Cyclic Access Dialog, every input is combined in sector 0 and all other sectors are empty.
        //       getStatusNvram shows Next Cyclic Write 0x0.  And readNvram() shows everything in Cyclic sector 0.
        #region readNvram()
        // NVRAM has 7 sectors. Order is first RAM, then CAM.
        Console.WriteLine("*****CardManagement.dll readNvram()");
        for (int overallSector = 0; overallSector < (NvramRandomAccessSectors + NvramCyclicAccessSectors); overallSector++)
        {
          bool cyclic = overallSector >= NvramRandomAccessSectors;
          int sector = cyclic ? overallSector - NvramRandomAccessSectors : overallSector;
          Console.WriteLine("    NVRAM Sector " + overallSector + " (" + (cyclic ? "Cyclic" : "Random") + " Access Memory " + sector + "):");
          byte[] nvRamOut = null;
          res = CardManagement.readNvram(deviceName, cyclic, sector, out nvRamOut);
          if (res != 0) Console.WriteLine("        readNvram() returns: 0x" + res.ToString("X4"));
          if (res == 0 && nvRamOut != null)
          {
            if (nvRamOut.Length == 0)
            {
              Console.WriteLine("        (Empty)");
            }
            else
            {
              string binaryString = "";
              for (int i = 0; i < nvRamOut.Length; i++)
              {
                binaryString += " " + nvRamOut[i].ToString("X2");
              }
              Console.WriteLine("        Size:   " + nvRamOut.Length + " Bytes");
              Console.WriteLine("        HEX:    " + binaryString.Trim());
              Console.WriteLine("        ASCII:  " + Encoding.ASCII.GetString(nvRamOut));
            }
          }
          Console.WriteLine("");
        }
        #endregion
      }

      #region getOverallSize()
      uint OverallSize;
      res = CardManagement.getOverallSize(deviceName, out OverallSize);
      Console.WriteLine("***** CardManagement.dll getOverallSize() returns: 0x" + res.ToString("X4"));
      if (res == 0)
      {
        Console.WriteLine("Overall size            : 0x" + OverallSize.ToString("X") + " = " + OverallSize + " blocks = " + ((Int64)OverallSize * 512 / 1024 / 1024) + " MiB");
      }
      Console.WriteLine("");
      #endregion

      return;
    }

    private static void VendorCommandsInterfaceDeviceStatus(string driveLetter)
    {
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

      VendorCommandsInterface vci = new VendorCommandsInterface(driveLetter);

      #region vci_read_chip_id
      Console.WriteLine("***** FileTunnelInterface.dll vci_read_chip_id():");
      try
      {
        Console.WriteLine(vci.readChipID());
      }
      catch (Exception ex)
      {
        Console.WriteLine("ERROR: " + ex.Message);
      }
      Console.WriteLine("");
      #endregion

      #region vci_read_extended_card_lifetime_information and its interpretation
      Console.WriteLine("***** FileTunnelInterface.dll vci_read_extended_card_lifetime_information():");
      try
      {
        byte[] array = vci.readLTMData();
        MemoryStream stream = new MemoryStream();
        stream.Write(array, 0, array.Length);
        stream.Seek(0, SeekOrigin.Begin);
        using (BinaryReaderBigEndian r = new BinaryReaderBigEndian(stream))
        {
          #region Interpretation of Swissbit LTM Data (Version 2024-01-20)
          // The first 26 bytes are described in the TSE Firmware Specification as manufacturer proprietary format,
          // however, they are described as "SSR register" in various Swissbit Product Data Sheets (Google "SSR Register")
          {
            uint tmp = r.ReadUInt16(); // Data bus width (2) || Secured mode (1) || Reserved for security (7) || Reserved (6)
            {
              string stmp2;
              uint tmp2 = (tmp >> 14) & 0b11;
              if (tmp2 == 0x2) stmp2 = "4 bit width";
              else stmp2 = "Unknown";
              Console.WriteLine("Data bus width: " + stmp2 + " (" + tmp2 + ")");
            }
            {
              string stmp2;
              uint tmp2 = (tmp >> 13) & 0b1;
              if (tmp2 == 0x1) stmp2 = "Secured";
              else stmp2 = "Not secured";
              Console.WriteLine("Secured mode: " + stmp2 + " (" + tmp2 + ")");
            }
            {
              uint tmp2 = (tmp >> 6) & 0b1111111;
              Console.WriteLine("Reserved for security (7 bits): 0x" + tmp2.ToString("X2"));
            }
            {
              uint tmp2 = tmp & 0b111111;
              Console.WriteLine("Reserved (6 bits): 0x" + tmp2.ToString("X2"));
            }
          }
          {
            string stmp2;
            uint tmp2 = r.ReadUInt16();
            if (tmp2 == 0) stmp2 = "Regular SD";
            else if (tmp2 == 257) stmp2 = "USB"; // not documented
            else stmp2 = "Unknown";
            Console.WriteLine("SD Card Type: " + stmp2 + " (" + tmp2 + ")");
          }
          {
            Console.WriteLine("Size protected area: " + (r.ReadUInt32() / 1024 / 1024) + " MB");
          }
          {
            string stmp2;
            uint tmp2 = r.ReadUInt8();
            if (tmp2 == 3) stmp2 = "Class 6";
            else if (tmp2 == 4) stmp2 = "Class 10";
            else stmp2 = "Unknown";
            Console.WriteLine("Speed class: " + stmp2 + " (" + tmp2 + ")");
          }
          {
            string stmp2;
            uint tmp2 = r.ReadUInt8();
            if (tmp2 == 0) stmp2 = "SequentialWrite";
            else stmp2 = tmp2 + "MB/s";
            Console.WriteLine("Move performance: " + stmp2 + " (" + tmp2 + ")");
          }
          {
            uint tmp = r.ReadUInt8(); // Allocation unit size (4) || Reserved (4)
            {
              string stmp2;
              uint tmp2 = ((tmp >> 4) & 0b1111);
              if (tmp2 == 0) stmp2 = "8 KiB?"; // not documented (not 100% sure)
              else if (tmp2 == 1) stmp2 = "16 KiB"; // not documented
              else if (tmp2 == 2) stmp2 = "32 KiB"; // not documented
              else if (tmp2 == 3) stmp2 = "64 KiB"; // not documented
              else if (tmp2 == 4) stmp2 = "128 KiB"; // not documented
              else if (tmp2 == 5) stmp2 = "256 KiB"; // not documented
              else if (tmp2 == 6) stmp2 = "512 KiB"; // not documented
              else if (tmp2 == 7) stmp2 = "1 MiB";
              else if (tmp2 == 8) stmp2 = "2 MiB";
              else if (tmp2 == 9) stmp2 = "4 MiB";
              else if (tmp2 == 10) stmp2 = "8 MiB"; // not documented
              else if (tmp2 == 11) stmp2 = "16 MiB"; // not documented
              else if (tmp2 == 12) stmp2 = "32 MiB"; // not documented
              else if (tmp2 == 13) stmp2 = "64 MiB"; // not documented
              else if (tmp2 == 14) stmp2 = "128 MiB"; // not documented
              else if (tmp2 == 15) stmp2 = "256 MiB"; // not documented
              else stmp2 = "Unknown";
              Console.WriteLine("Allocation unit size: " + stmp2 + " (" + tmp2 + ")");
            }
            {
              uint tmp2 = tmp & 0b1111;
              Console.WriteLine("Reserved (4 bits): 0x" + tmp2.ToString("X1"));
            }
          }
          {
            Console.WriteLine("Erase unit size: " + r.ReadUInt16() + " AU");
          }
          {
            uint tmp = r.ReadUInt8(); // Erase unit timeout (6) || Erase unit offset (2)
            {
              Console.WriteLine("Erase unit timeout: " + ((tmp >> 2) & 0b111111) + " seconds");
            }
            {
              Console.WriteLine("Erase unit offset: " + (tmp & 0b11) + " seconds");
            }
          }
          {
            uint tmp = r.ReadUInt8(); // UHS mode Speed Grade (4) || Allocation unit size in UHS mode (4)
            {
              string stmp2;
              uint tmp2 = (tmp >> 4) & 0b1111;
              if (tmp2 == 0) stmp2 = "NoUHS";
              else if (tmp2 == 1) stmp2 = "10MB/s and above";
              else if (tmp2 == 3) stmp2 = "UHS Grade 3";
              else stmp2 = "Unknown";
              Console.WriteLine("UHS mode speed grade: " + stmp2 + " (" + tmp2 + ")");
            }
            {
              string stmp2;
              uint tmp2 = tmp & 0b1111;
              if (tmp2 == 0) stmp2 = "NoUHS";
              else if (tmp2 == 1) stmp2 = "16 KiB"; // not documented
              else if (tmp2 == 2) stmp2 = "32 KiB"; // not documented
              else if (tmp2 == 3) stmp2 = "64 KiB"; // not documented
              else if (tmp2 == 4) stmp2 = "128 KiB"; // not documented
              else if (tmp2 == 5) stmp2 = "256 KiB"; // not documented
              else if (tmp2 == 6) stmp2 = "512 KiB"; // not documented
              else if (tmp2 == 7) stmp2 = "1 MiB";
              else if (tmp2 == 8) stmp2 = "2 MiB";
              else if (tmp2 == 9) stmp2 = "4 MiB";
              else if (tmp2 == 10) stmp2 = "8 MiB"; // not documented
              else if (tmp2 == 11) stmp2 = "16 MiB"; // not documented
              else if (tmp2 == 12) stmp2 = "32 MiB"; // not documented
              else if (tmp2 == 13) stmp2 = "64 MiB"; // not documented
              else if (tmp2 == 14) stmp2 = "128 MiB"; // not documented
              else if (tmp2 == 15) stmp2 = "256 MiB"; // not documented
              else stmp2 = "Unknown";
              Console.WriteLine("Allocation unit size in UHS mode: " + stmp2 + " (" + tmp2 + ")");
            }
          }
          {
            Console.WriteLine("Video Speed Class: " + r.ReadUInt8()); // 30=VideoSpeedClass30
          }
          {
            uint tmp = r.ReadUInt16(); // Reserved (6) || AU size for Video Speed Class (10)
            {
              uint tmp2 = (tmp >> 10) & 0b111111;
              Console.WriteLine("Reserved (6 bits): 0x" + tmp2.ToString("X2"));
            }
            {
              // PS-66DP_data_sheet.pdf describes 0x8=8MiB, which is a bit weird, because AU size in UHS mode used 0xA=8MiB
              Console.WriteLine("Allocation unit size for Video Speed Class: " + (tmp & 0b1111111111) + " MiB");
            }
          }
          {
            uint tmp = r.ReadUInt32(); // Suspension Address (22) || Reserved (6) || Application Performance Class (4)
            {
              Console.WriteLine("Suspension Address: " + ((tmp >> 10) & 0b1111111111111111111111));
            }
            {
              uint tmp2 = (tmp >> 4) & 0b111111;
              Console.WriteLine("Reserved (6 bits): 0x" + tmp2.ToString("X2"));
            }
            {
              string stmp2;
              uint tmp2 = tmp & 0b1111;
              if (tmp2 == 1) stmp2 = "Class A1";
              else stmp2 = "Unknown";
              Console.WriteLine("Application Performance Class: " + stmp2 + " (" + tmp2 + ")");
            }
          }
          {
            Console.WriteLine("Performance Enhancement: " + r.ReadUInt8());
          }
          {
            uint tmp = r.ReadUInt16(); // Reserved (14) || Discard Support (1) || Full User Area Logical Erase Support (1)
            {
              uint tmp2 = (tmp >> 2) & 0b11111111111111;
              Console.WriteLine("Reserved (14 bit): 0x" + tmp2.ToString("X4"));
            }
            {
              string stmp2;
              uint tmp2 = (tmp >> 1) & 0b1;
              if (tmp2 == 0x1) stmp2 = "Supported";
              else stmp2 = "Not Supported";
              Console.WriteLine("Discard Support: " + stmp2 + " (" + tmp2 + ")");
            }
            {
              string stmp2;
              uint tmp2 = tmp & 0b1;
              if (tmp2 == 0x1) stmp2 = "Supported";
              else stmp2 = "Not Supported";
              Console.WriteLine("Fill User Area Logical Erase Support: " + stmp2 + " (" + tmp2 + ")");
            }
          }
          {
            Console.WriteLine("Data structure version identifier: " + r.ReadUInt8());
          }

          // These fields are taken from the TSE Firmware Specification:
          Console.WriteLine("Number of manufacturer marked defect blocks: " + r.ReadUInt16());
          Console.WriteLine("Number of initial spare blocks (worst interleave unit): " + r.ReadUInt16());
          Console.WriteLine("Number of initial spare blocks (sum over all interleave units): " + r.ReadUInt16());
          Console.WriteLine("Percentage of remaining spare blocks (worst interleave unit): " + r.ReadByte() + "%");
          Console.WriteLine("Percentage of remaining spare blocks (all interleave units): " + r.ReadByte() + "%");
          Console.WriteLine("Number of uncorrectable ECC errors (not including startup ECC errors): " + r.ReadUInt16());
          Console.WriteLine("Number of correctable ECC errors (not including startup ECC errors): " + r.ReadUInt32());
          Console.WriteLine("Lowest wear level class: " + r.ReadUInt16());
          Console.WriteLine("Highest wear level class: " + r.ReadUInt16());
          Console.WriteLine("Wear level threshold: " + r.ReadUInt16());
          Console.WriteLine("Total number of block erases: " + r.ReadUInt48());
          Console.WriteLine("Number of flash blocks, in units of 256 blocks: " + r.ReadUInt16());
          Console.WriteLine("Maximum flash block erase count target, in wear level class units: " + r.ReadUInt16());
          Console.WriteLine("Power on count: " + r.ReadUInt32());

          // This following part is also described as manufacturer proprietary format in the TSE Firmware Specification.
          // The first 4 bytes of them are described in the product data sheets as firmware version 0xYYMMDDXX and last element of the SSR register.
          {
            uint tmp = r.ReadUInt32();
            uint fwYear = (tmp >> 24) & 0xFF;
            uint fwMonth = (tmp >> 16) & 0xFF;
            uint fwDay = (tmp >> 8) & 0xFF;
            uint fwUnknown = tmp & 0xFF;
            Console.WriteLine("Firmware Version: 0x" + tmp.ToString("X8") + " (20" + fwYear.ToString("X2") + "-" + fwMonth.ToString("X2") + "-" + fwDay.ToString("X2") + ", 0x" + fwUnknown.ToString("X2") + ")");
          }
          // The next 96 bytes are not described anywhere else (except as manufacturer proprietary format in the TSE specification)
          {
            Console.WriteLine("Manufacturer proprietary format:");
            Console.WriteLine(BitConverter.ToString(r.ReadBytes(32)).Replace("-", " "));
            Console.WriteLine(BitConverter.ToString(r.ReadBytes(32)).Replace("-", " "));
            Console.WriteLine(BitConverter.ToString(r.ReadBytes(32)).Replace("-", " "));
          }
          #endregion
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine("ERROR: " + ex.Message);
      }
      Console.WriteLine("");
      #endregion

      vci = null;
    }

    class BinaryReaderBigEndian : BinaryReader
    {
      // https://stackoverflow.com/questions/8620885/c-sharp-binary-reader-in-big-endian

      public BinaryReaderBigEndian(System.IO.Stream stream) : base(stream) { }

      public override Int16 ReadInt16()
      {
        var data = base.ReadBytes(2);
        Array.Reverse(data);
        return BitConverter.ToInt16(data, 0);
      }

      public override int ReadInt32()
      {
        var data = base.ReadBytes(4);
        Array.Reverse(data);
        return BitConverter.ToInt32(data, 0);
      }

      public Int64 ReadInt48()
      {
        var data = base.ReadBytes(6);

        var list = new List<byte>(data);
        list.Insert(0, 0);
        list.Insert(0, 0);
        data = list.ToArray();

        Array.Reverse(data);
        return BitConverter.ToInt64(data, 0);
      }

      public override Int64 ReadInt64()
      {
        var data = base.ReadBytes(8);
        Array.Reverse(data);
        return BitConverter.ToInt64(data, 0);
      }

      public ushort ReadUInt8()
      {
        var data = base.ReadBytes(1);
        return data[0];
      }

      public override UInt16 ReadUInt16()
      {
        var data = base.ReadBytes(2);
        Array.Reverse(data);
        return BitConverter.ToUInt16(data, 0);
      }

      public override UInt32 ReadUInt32()
      {
        var data = base.ReadBytes(4);
        Array.Reverse(data);
        return BitConverter.ToUInt32(data, 0);
      }

      public UInt64 ReadUInt48()
      {
        var data = base.ReadBytes(6);

        var list = new List<byte>(data);
        list.Insert(0, 0);
        list.Insert(0, 0);
        data = list.ToArray();

        Array.Reverse(data);
        return BitConverter.ToUInt64(data, 0);
      }

      public override UInt64 ReadUInt64()
      {
        var data = base.ReadBytes(8);
        Array.Reverse(data);
        return BitConverter.ToUInt64(data, 0);
      }

    }

  }

  // Technical foot note / interesting things:
  //
  // How does the uSD version of CardManagement.dll communicate with the hardware?
  // - The communication will be done through the file X:\__communicationFile
  // - In the initial state, the file has the contents:
  //   50 4C 45 41 53 45 20 44 4F 20 4E 4F 54 20 44 45 4C 45 54 45 20 54 48 49 53 20 46 49 4C 45 21 00 ("PLEASE DO NOT DELETE THIS FILE!\n")
  //   followed by 480 NUL bytes.
  // - When commands are sent, the first 32 bytes are overwritten by the hardcoded magic sequence
  //   10 6A F8 1A D6 F8 C8 70 AC 7E 85 F0 E9 9E F3 9D 1E 11 A1 BA 87 4A C6 DB 42 81 15 8E FE 6D 3C 81
  //   After that comes command data.
  //   A few useful dumps:     lockCard          =  01 00 00 05     FF 31 00 00  00
  //                           verify("test123") =  01 00 00 0D     FF 30 00 00  08     07     74 65 73 74 31 32 33
  //                                                <??????> <len>( < command >  <len>( <len> ( t  e  s  t  1  2  3 )))
  //   ... some commands found in disassembly of the USB DLL (because the disassembly of the uSD DLL does not work correctly with IDA)
  /*
   10FF activate
20010FF activateSecure
   20FF deactivate
   30FF verify (unlock card)
   31FF lockCard
   40FF changePassword
   50FF unblockPassword
   53FF setCdromAreaBackToDefault
  253FF setCdromAreaAndReadException
  353FF clearProtectionProfiles
10353FF setProtectionProfiles
   60FF reset
   ???? resetAndFormat
  170FF getCardId
  270FF getApplicationVersion
10270FF getBaseFWVersion
  570FF getLoginChallenge
  670FF getControllerId
  770FF getProtectionProfiles
  870FF getPartitionTable
  970FF getOverallSize
   80FF?configureNvram
   80FF?setAuthenticityCheckSecret
  580FF setExtendedSecurityFlags
  780FF setSecureActivationKey
   D0FF readNvram
   D1FF writeNvram
   F1FF challengeFirmware
   F2FF checkAuthenticity
   ???? getStatus
   ???? getStatusException
   ???? getStatusNvram
  (DLL) getVersion
  (DLL) getBuildDateAndTime
*/
  // - TODO: Further analysis...
  // - How to find which data is written? Execute with IDA, let the DLL write to a harddrive (or any other non-supported drive)
  //   and cancel the process before the file is deleted.
  //
  // How does the USB version of CardManagement.dll communicate with the hardware?
  // - The SmartCard API (WinSCard.dll) is called to transmit data.
  //   The command codes seem to be the same as for the uSD card, because the command codes in the disassembly of the USB DLL match
  //   the data that the uSD DLL wrote to the communcation file.
  // - The file "<DeviceName>\__communicationFile" will be deleted. It is NOT used for transmitting data.
  //   Note that this is non-sense because for the PU-50n, the <DeviceName> has to be a name rather than a drive letter,
  //   so there will a file access to the local file "Swissbit Secure USB PU-50n DP 0\__communicationFile"
  //
  // How does the FileTunnelInterface.dll (from the TSE Maintenance Tool) communicate to the PU-50n (requires writeable drive letter though)?
  // - It is neither SmartCard API, nor a file called __communicationFile!
  // - ProcessMonitor shows: There are ONLY calls of CreateFile("H:"), ReadFile("H:"), WriteFile("H:") and CloseFile("H:")
  //   Isn't this risky if FileTunnelInterface.dll would try to write to a regular drive?
  //   I noticed: There are a LOT of Read-Accesses to offsets 0 (sector 0), 1024 (sector 2), 1536 (sector 3) before any WriteFile is called at all.
  //   Theory: The read accesses are interpreted by a Swissbit device like a "morse code" and if the device reacts accordingly, then FileTunnelInterface.dll knows that it can now start WriteFile(),
  //   and the device will most likely interprete these WriteFile() accesses as commands (like the TSE-IO.bin for the TSE)
  // - Wow... there are a lot of different "interfaces" to communicate with devices over the file system...
  // - Although FTI is a super tiny file, I could not manage to analyze it.
  //
  // How does WormApi.dll communicate with the TSE?
  // - Via the file TSE-IO.bin (to a fixed sector). It is documented in the firmware specification.
  //
  // How does Swissbit Device Manager and libsbltm.dll communicate with PU-50n DP?
  // - First via the File Tunnel protocol to \Device\Harddrisk3\DR4\
  // - Then by writing to a non-existant file "G:\sb.vc"
  // - No Data Protection may be enabled. A PU-50n TSE cannot be detected either.
  //
  // What I don't know yet (TODO):
  // - How is the communication with the PS-45u card working? Also via Smartcard API? Or via __communicationFile?
  // - Where does the Device Manager get data like Temperature, Modell-ID, etc.? Does not seem to be in the unknown LTM data?

}
