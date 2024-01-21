using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace SwissbitSecureSDUtils
{
    // The following commands have only been tested with the USB version of PU-50n, not with the PU-45n SD card

    // There is a big Problem with CardManagerLite.exe and CardManagerCLI.exe
    // For the USB PU-50n, CardManagement.dll cannot access "G:\". It can only access the name "Swissbit Secure USB PU-50n DP 0".
    // You can verify this by query "G:\" in CardManager.exe .
    // The big problem is that CardManagerLite.exe and CardManagerCli.exe only accept "G:\", not names.
    // Therefore, you cannot use the CLI tool for the USB stick! Not good!
    // To fix this issue, patch CardManagerCLI.exe by disabling the syntax check for the "--mountpoint" argument:
    //     Search:  75 1F 0F BE 02 50 FF 15 D8 D2 40 00 8B 7D FC 83 C4 04 85 C0 74 0E 8B 47 0C 80 78 01 3A 74
    //     Replace: 90 90 0F BE 02 50 FF 15 D8 D2 40 00 8B 7D FC 83 C4 04 85 C0 90 90 8B 47 0C 80 78 01 3A EB
    //              ^^ ^^                                                       ^^ ^^                      ^^
    // Some commands like fetching Partition Table don't seem to work though. I didn't investigate that yet.

    // This C# library can help you using the USD/uSD card by calling the DLL instead of the non-working CLI EXE.

    internal class SecureSDUtils
    {
        // TODO: Implement more methods of CardManagement.dll

        // Error Messages collected:
        // Return 0000 : OK
        // Return 9001 : Change Protection Profile (Partition): Sum of Partition sizes is larger than total size of drive .... sometimes something else??? generic error???
        // Return 6B00 : Happens at getPartitionTable if Firmware of DP Card is too old
        // Return 6F02 : Wrong password entered
        // Return 6F05 : No password entered, or password too short
        // Return 6FFC : Security Settings changed; need powercycle to reload stuff

        #region verify (Unlock Data Protection)
        [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "verify", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        private static extern int _verify(
            [MarshalAs(UnmanagedType.LPStr)] string CardName,
            [MarshalAs(UnmanagedType.LPStr)] string password,
            [MarshalAs(UnmanagedType.U4)] int passwordLength);
        [return: MarshalAs(UnmanagedType.U4)]
        public static bool SecureSd_Unlock_Card(string CardName, string Passwort)
        {
            Console.WriteLine("Unlock Card: " + CardName);
            int res = _verify(CardName, Passwort, Passwort.Length);
            if (res != 0)
            {
                Console.WriteLine("ERROR: verify() returned " + res.ToString("X4"));
            }
            return res == 0;
        }
        #endregion

        #region lockCard (Lock Data Protection)
        [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "lockCard", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        private static extern int _lockCard(
            [MarshalAs(UnmanagedType.LPStr)] string CardName);
        public static bool SecureSd_Lock_Card(string CardName)
        {
            Console.WriteLine("Lock Card: " + CardName);
            int res = _lockCard(CardName);
            if (res != 0)
            {
                Console.WriteLine("ERROR: lockCard() returned " + res.ToString("X4"));
            }
            return res == 0;
        }
        #endregion

        #region Various device info methods

        #region getStatus
        [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getStatus", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        private static extern int _getStatus(
            [MarshalAs(UnmanagedType.LPStr)] string CardName,
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
        private static extern int _getStatusException(
            [MarshalAs(UnmanagedType.LPStr)] string CardName,
            [MarshalAs(UnmanagedType.U4)] out int ExceptionUnknown1,
            [MarshalAs(UnmanagedType.U4)] out int ExceptionUnknown2,
            [MarshalAs(UnmanagedType.U4)] out int ExceptionUnknown3,
            [MarshalAs(UnmanagedType.U4)] out int partition1Offset,
            [MarshalAs(UnmanagedType.U4)] out int ExceptionUnknown4);
        #endregion

        #region getApplicationVersion
        [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getApplicationVersion", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        private static extern int _getApplicationVersion(
            [MarshalAs(UnmanagedType.LPStr)] string CardName,
            [MarshalAs(UnmanagedType.U4)] out int ApplicationVersion);
        #endregion

        #region getOverallSize
        [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getOverallSize", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        private static extern int _getOverallSize(
            [MarshalAs(UnmanagedType.LPStr)] string CardName,
            [MarshalAs(UnmanagedType.U4)] out uint OverallSize);
        #endregion

        #region getStatusNvram
        [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getStatusNvram", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        private static extern int _getStatusNvram(
            [MarshalAs(UnmanagedType.LPStr)] string CardName,
            [MarshalAs(UnmanagedType.U4)] out int AccessRights,
            [MarshalAs(UnmanagedType.U4)] out int TotalNvRamSize,
            [MarshalAs(UnmanagedType.U4)] out int RandomAccessSectors,
            [MarshalAs(UnmanagedType.U4)] out int CyclicAccessSectors,
            [MarshalAs(UnmanagedType.U4)] out int NextCyclicWrite);
        #endregion

        #region getCardId
        [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getCardId", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        private static extern int _getCardId(
            [MarshalAs(UnmanagedType.LPStr)] string CardName,
            [MarshalAs(UnmanagedType.SysInt)] IntPtr cardid12bytes);
        #endregion

        #region getControllerId
        [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getControllerId", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        private static extern int _getControllerId(
            [MarshalAs(UnmanagedType.LPStr)] string CardName,
            [MarshalAs(UnmanagedType.SysInt)] IntPtr conrollerId,
            [MarshalAs(UnmanagedType.U4)] ref int conrollerIdSize);
        #endregion

        #region getBaseFWVersion
        [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getBaseFWVersion", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        private static extern int _getBaseFWVersion(
            [MarshalAs(UnmanagedType.LPStr)] string CardName,
            [MarshalAs(UnmanagedType.SysInt)] IntPtr firmware8bytes,
            [MarshalAs(UnmanagedType.U4)] ref int part2);
        #endregion

        #region getProtectionProfiles
        [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getProtectionProfiles", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        private static extern int _getProtectionProfiles(
            [MarshalAs(UnmanagedType.LPStr)] string CardName,
            [MarshalAs(UnmanagedType.U4)] out int ProtectionProfileUnknown1,
            [MarshalAs(UnmanagedType.U4)] out int ProtectionProfileUnknown2,
            [MarshalAs(UnmanagedType.U4)] out int ProtectionProfileUnknown3);
        #endregion

        #region getVersion
        [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getVersion", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        private static extern int _getVersion();
        #endregion

        #region getBuildDateAndTime (only for 2019 USB/uSD DLL, not for 2022 uSD DLL)
        [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getBuildDateAndTime", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.SysInt)]
        private static extern IntPtr _getBuildDateAndTime();
        #endregion

        public static bool SecureSd_DeviceInfo(string CardName)
        {
            #region DLL Version
            int dllVersion = _getVersion();
            string dllVersionString = "CardManagement.dll version " + ((int)Math.Floor((decimal)dllVersion / 0x100)).ToString("X") + "." + (dllVersion % 0x100).ToString("X");
            try
            {
                string s = Marshal.PtrToStringAnsi(_getBuildDateAndTime());
                dllVersionString += " built on " + s;
            }
            catch (Exception) { }
            Console.WriteLine(dllVersionString);
            Console.WriteLine("");
            #endregion

            #region getStatus()
            int LicenseMode, SystemState, RetryCounter, SoRetryCounter, ResetCounter, CdRomAddress, ExtSecurityFlags;
            int res = _getStatus(CardName, out LicenseMode, out SystemState, out RetryCounter, out SoRetryCounter, out ResetCounter, out CdRomAddress, out ExtSecurityFlags);
            Console.WriteLine("***** CardManagement.dll getStatus() returns: 0x" + res.ToString("X4"));
            Console.WriteLine("License Mode            : 0x" + LicenseMode.ToString("X2")); // Raspberry Pi Edition = 0x40. What else is possible?
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
            Console.WriteLine("");
            #endregion

            #region getStatusException()
            int ExceptionUnknown1, ExceptionUnknown2, ExceptionUnknown3, partition1Offset, ExceptionUnknown4;
            res = _getStatusException(CardName, out ExceptionUnknown1, out ExceptionUnknown2, out ExceptionUnknown3, out partition1Offset, out ExceptionUnknown4);
            Console.WriteLine("***** CardManagement.dll getStatusException() returns: 0x" + res.ToString("X4"));
            // TODO: What are these values??? Somewhere should be partition1Offset
            // NetPolicyServer User Manual version 2.6-2.9 shows 4 values
            // - defaultCDRomAddress         (this is not part of the current EXE or DLL)
            // - readExceptionAddress        (this is not part of the current EXE or DLL) 
            // - partition1Offset (in hex)
            // - partition1Offset (in dec)
            // Assuming that partition1Offset is only 1 value returned by the DLL, then we would only have 3 values. What are the other 2?
            // RevEng CLI shows that DLL argument 5 is the partition1Offset. Arguments 2, 3, 4, 6 are unknown yet.
            Console.WriteLine("ExceptionUnknown1       : 0x" + ExceptionUnknown1.ToString("X"));
            Console.WriteLine("ExceptionUnknown2       : 0x" + ExceptionUnknown2.ToString("X"));
            Console.WriteLine("ExceptionUnknown3       : 0x" + ExceptionUnknown3.ToString("X"));
            Console.WriteLine("partition1Offset        : 0x" + partition1Offset.ToString("X") + " = " + partition1Offset + " blocks");
            Console.WriteLine("ExceptionUnknown4       : 0x" + ExceptionUnknown4.ToString("X"));
            Console.WriteLine("");
            #endregion

            #region getApplicationVersion
            int ApplicationVersion;
            res = _getApplicationVersion(CardName, out ApplicationVersion);
            Console.WriteLine("***** CardManagement.dll getApplicationVersion() returns: 0x" + res.ToString("X4"));
            Console.WriteLine("Application Version     : " + ApplicationVersion.ToString("x")); // RevEng: This seems to be called "CFE version". Also funny typos: "Yor CFE version is" and "You must be loged in to read the partition table".
            Console.WriteLine("");
            #endregion

            #region getBaseFWVersion
            int baseFWPart2 = 0;
            int baseFWVersionSize = 8;
            byte[] baseFWVersion = new byte[baseFWVersionSize];
            IntPtr unmanagedPointer = Marshal.AllocHGlobal(baseFWVersionSize);
            //Marshal.Copy(baseFWVersion, 0, unmanagedPointer, baseFWVersionSize);
            res = _getBaseFWVersion(CardName, unmanagedPointer, ref baseFWPart2);
            Marshal.Copy(unmanagedPointer, baseFWVersion, 0, baseFWVersionSize);
            Marshal.FreeHGlobal(unmanagedPointer);
            Console.WriteLine("***** CardManagement.dll getBaseFWVersion() returns: 0x" + res.ToString("X4"));
            Console.Write("Base Firmware Version: ");
            for (int i = 0; i < baseFWVersionSize; i++)
            {
                Console.Write(Convert.ToChar(baseFWVersion[i]));
            }
            // Note: The screenshots in the manual shows the examples "211028s9 X100" and "170614s8  110"
            //       My PU-50n USB has "180912u9  106"
            Console.WriteLine(" " + Encoding.ASCII.GetString(BitConverter.GetBytes(baseFWPart2).ToArray()));
            Console.WriteLine("");
            #endregion

            #region getCardId
            int cardIdSize = 16;
            var cardId = new byte[cardIdSize];
            unmanagedPointer = Marshal.AllocHGlobal(cardIdSize);
            //Marshal.Copy(cardId, 0, unmanagedPointer, cardIdSize);
            res = _getCardId(CardName, unmanagedPointer);
            Marshal.Copy(unmanagedPointer, cardId, 0, cardIdSize);
            Marshal.FreeHGlobal(unmanagedPointer);
            Console.WriteLine("***** CardManagement.dll getCardId() returns: 0x" + res.ToString("X4"));
            Console.Write("Card ID: ");
            for (int i = 0; i < cardIdSize; i++)
            {
                Console.Write(" " + cardId[i].ToString("X2"));
            }
            Console.WriteLine("");
            Console.WriteLine("");
            #endregion

            #region getControllerId (Unique ID)
            int controlerIdSize = 99;
            byte[] controlerId = new byte[controlerIdSize];
            unmanagedPointer = Marshal.AllocHGlobal(controlerIdSize);
            //Marshal.Copy(controlerId, 0, unmanagedPointer, controlerIdSize);
            res = _getControllerId(CardName, unmanagedPointer, ref controlerIdSize);
            Marshal.Copy(unmanagedPointer, controlerId, 0, controlerIdSize);
            Marshal.FreeHGlobal(unmanagedPointer);
            Console.WriteLine("***** CardManagement.dll getControllerId() returns: 0x" + res.ToString("X4"));
            // "getControllerId()" shows the "UniqueID" for USB PU-50n, while getCardId() shows something weird. Confusing!
            // The "NetPolicyServer User Manual" writes:
            //    Please note the last value in the output (“Controller ID”, Figure 12). This alphanumeric sequence
            //    without any blank spaces is the Unique ID of the DataProtection device, which is needed for the Net
            //    Policy database entry in the Net Policy server.
            //    Note: It is not the value shown as the Unique Card ID!
            Console.Write("Unique ID: ");
            for (int i = 0; i < controlerIdSize; i++)
            {
                Console.Write(" " + controlerId[i].ToString("X2"));
            }
            Console.WriteLine("");
            Console.WriteLine("");
            #endregion

            // TODO: getPartitionTable(?, ?)

            #region getProtectionProfiles (Work in progress)
            // TODO: getProtectionProfiles(?, ?, ?, ?).
            //       Swissbit CLI Help says:  <ProfileStr> is a , separated list in the form StartOfRange,Type,StartOfRange,Type StartOfRange is a hexadecimal value, Type: 0=PRIVATE_RW, 1=PUBLIC_RW, 2=PRIVATE_CDROM 3=PUBLIC_CDROM
            //       In CardManagerCli.exe of the uSDCard (not USB) there are 3 more entries: 4=PRIVATE_RO, 5=PUBLIC_RO, 6=FLEXIBLE_RO
            //       On my stick it says in CardManagerCli.exe:
            //       0x00000000 PUBLIC_CDROM
            //       0x00018000 PRIVATE_RW      <-- Why does this say "0x18000" ? My CD ROM partition is 46 MB, so shouldn't the PrivateRW start earlier? 
            int ProtectionProfileUnknown1, ProtectionProfileUnknown2, ProtectionProfileUnknown3;
            res = _getProtectionProfiles(CardName, out ProtectionProfileUnknown1, out ProtectionProfileUnknown2, out ProtectionProfileUnknown3);
            Console.WriteLine("***** CardManagement.dll getProtectionProfiles() returns: 0x" + res.ToString("X4"));
            Console.WriteLine("ProtectionProfileUnknown1:         0x" + ProtectionProfileUnknown1.ToString("X8")); // For me, outputs 0x2
            Console.WriteLine("ProtectionProfileUnknown2:         0x" + ProtectionProfileUnknown2.ToString("X8")); // For me, outputs 0x3
            Console.WriteLine("ProtectionProfileUnknown3:         0x" + ProtectionProfileUnknown3.ToString("X8")); // For me, leaves value unchanged (therefore it seems to be an input argument)
            Console.WriteLine("");
            #endregion

            #region getStatusNvram()
            int NvramAccessRights, NvramTotalNvramSize, NvramRandomAccessSectors, NvramCyclicAccessSectors, NextCyclicWrite;
            res = _getStatusNvram(CardName, out NvramAccessRights, out NvramTotalNvramSize, out NvramRandomAccessSectors, out NvramCyclicAccessSectors, out NextCyclicWrite);
            Console.WriteLine("***** CardManagement.dll getStatusNvram() returns: 0x" + res.ToString("X4"));
            int RandomRights = NvramAccessRights & 0xFF;
            int CyclicRights = (NvramAccessRights >> 8) & 0xFF;
            Console.WriteLine("Access Rights:         0x" + NvramAccessRights.ToString("X8"));
            Console.WriteLine("   Cyclic NVRAM:       0x" + CyclicRights.ToString("X2"));
            Console.WriteLine("                       [" + ((CyclicRights & 0x1) != 0 ? "x" : " ") + "] All Read (0x1)");
            Console.WriteLine("                       [" + ((CyclicRights & 0x2) != 0 ? "x" : " ") + "] All Write (0x2)");
            Console.WriteLine("                       [" + ((CyclicRights & 0x4) != 0 ? "x" : " ") + "] User Read (0x4)");
            Console.WriteLine("                       [" + ((CyclicRights & 0x8) != 0 ? "x" : " ") + "] User Write (0x8)");
            Console.WriteLine("                       [" + ((CyclicRights & 0x10) != 0 ? "x" : " ") + "] Wrap around (0x10)");
            Console.WriteLine("                       [" + ((CyclicRights & 0x20) != 0 ? "x" : " ") + "] Security Officer Read (0x20)");
            Console.WriteLine("                       [" + ((CyclicRights & 0x40) != 0 ? "x" : " ") + "] Security Officer Write (0x40)");
            Console.WriteLine("                       [" + ((CyclicRights & 0x80) != 0 ? "x" : " ") + "] Fused Persistently (0x80)"); // not 100% sure
            Console.WriteLine("   Random NVRAM:       0x" + RandomRights.ToString("X2"));
            Console.WriteLine("                       [" + ((RandomRights & 0x1) != 0 ? "x" : " ") + "] All Read (0x1)");
            Console.WriteLine("                       [" + ((RandomRights & 0x2) != 0 ? "x" : " ") + "] All Write (0x2)");
            Console.WriteLine("                       [" + ((RandomRights & 0x4) != 0 ? "x" : " ") + "] User Read (0x4)");
            Console.WriteLine("                       [" + ((RandomRights & 0x8) != 0 ? "x" : " ") + "] User Write (0x8)");
            //Console.WriteLine("                       [" + ((RandomRights & 0x10) != 0 ? "x" : " ") + "] Not defined (0x10)");
            Console.WriteLine("                       [" + ((RandomRights & 0x20) != 0 ? "x" : " ") + "] Security Officer Read (0x20)");
            Console.WriteLine("                       [" + ((RandomRights & 0x40) != 0 ? "x" : " ") + "] Security Officer Write (0x40)");
            Console.WriteLine("                       [" + ((RandomRights & 0x80) != 0 ? "x" : " ") + "] Fused Persistently (0x80)"); // not 100% sure
            Console.WriteLine("Total NVRAM Size:      0x" + NvramTotalNvramSize.ToString("X"));
            Console.WriteLine("Random Access Sectors: 0x" + NvramRandomAccessSectors.ToString("X"));
            Console.WriteLine("Cyclic Access Sectors: 0x" + NvramCyclicAccessSectors.ToString("X"));
            Console.WriteLine("Next Cyclic Write:     0x" + NextCyclicWrite.ToString("X"));
            Console.WriteLine("");
            #endregion

            #region getOverallSize
            uint OverallSize;
            res = _getOverallSize(CardName, out OverallSize);
            Console.WriteLine("***** CardManagement.dll getOverallSize() returns: 0x" + res.ToString("X4"));
            Console.WriteLine("Overal size             : 0x" + OverallSize.ToString("X") + " = " + OverallSize + " blocks = " + ((Int64)OverallSize * 512 / 1024 / 1024) + " MiB");
            Console.WriteLine("");
            #endregion

            return res == 0;
        }
        #endregion

    }
    internal class VendorCommandsInterface
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

    internal class Program
    {

        static void Main(string[] args)
        {
            Console.WriteLine("Swissbit Secure USB Stick PU-50n");
            Console.WriteLine("Access using FileTunnelInterface.dll (from TSE Maintenance Tool) and CardManagement.dll (from SDK)");
            Console.WriteLine("");

            // TODO: Auto Recognize or select
            const string DEVICE_NAME = "Swissbit Secure USB PU-50n DP 0";
            const string DECIDE_DRIVE_LETTER = "h";

            SecureSDUtils.SecureSd_DeviceInfo(DEVICE_NAME);

            // Test
            //SecureSDUtils.SecureSd_Unlock_Card(DEVICE_NAME, "test123");
            //SecureSDUtils.SecureSd_Lock_Card(DEVICE_NAME);

            if (DECIDE_DRIVE_LETTER != "")
            {
                // The FileTunnelInterface.dll is part of the TSE Maintenance Tool.
                // If FileTunnelInterface.dll works on a TSE, then the TSE is considered "in an undefined state".
                // But with a Secure SD, the FileTunnelInterface works.
                // So, I guess both SecureSD and TSE are made out of the same "raw material",
                // and the firmware and/or configuration was uploaded via the FileTunnelInterface (Vendor Command Interface).
                // If FileTunnelInterface.dll works, then either the firmware was not applied in the factory,
                // or maybe the TSE can fall back into the raw state if the firmware failed to boot (TSE Panic).
                // But that's just a theory. Let's just enjoy that FileTunnelInterface.dll works on a SecureSD,
                // because now we can also fetch the LTM data! The LTM data has the same structure as described
                // in the TSE Firmware Specification.
                // Unfortunately, FileTunnelInterface.dll does only work if there is a drive letter visible,
                // and it does not work with CD-ROM drive letters.

                VendorCommandsInterface vci = new VendorCommandsInterface(DECIDE_DRIVE_LETTER);

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
}
