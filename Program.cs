using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace SwissbitSecureSDUtils
{
    // Seltsam: CardManagerLite.exe und CardManagerCLI.exe gehen nicht mit USB.  Aber CardManager.exe geht.
    //          Sind diese zwei EXE Dateien vielleicht nur für die SD-Kartenlösung???

    internal class SecureSDUtils
    {

        // TODO: Implement more methods of CardManagement.dll

        #region verify (Karte entsperren)
        [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "verify", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        private static extern int _verify(
            [MarshalAs(UnmanagedType.LPStr)] string CardName,
            [MarshalAs(UnmanagedType.LPStr)] string password,
            [MarshalAs(UnmanagedType.U4)] int passwordLength);
        [return: MarshalAs(UnmanagedType.U4)]
        public static bool SecureSd_Entsperren(string CardName, string Passwort)
        {
            Console.WriteLine("Unlock Card: " + CardName);
            int res = _verify(CardName, Passwort, Passwort.Length);
            // Return 0000 = OK
            // Return 6F02 = Passwort falsch
            // Return 6F05 = Kein Passwort angegeben oder Passwort zu kurz
            Console.WriteLine("Result : " + res.ToString("X4"));
            return res == 0;
        }
        #endregion

        #region lockCard (Karte sperren)
        [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "lockCard", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        private static extern int _lockCard(
            [MarshalAs(UnmanagedType.LPStr)] string CardName);
        public static bool SecureSd_Sperren(string CardName)
        {
            int res = _lockCard(CardName);
            return res == 0;
        }
        #endregion

        #region Various device info methods
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
        [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getStatusException", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        private static extern int _getStatusException(
            [MarshalAs(UnmanagedType.LPStr)] string CardName,
            [MarshalAs(UnmanagedType.U4)] out int ExceptionUnknown1,
            [MarshalAs(UnmanagedType.U4)] out int ExceptionUnknown2,
            [MarshalAs(UnmanagedType.U4)] out int ExceptionUnknown3,
            [MarshalAs(UnmanagedType.U4)] out int ExceptionUnknown4,
            [MarshalAs(UnmanagedType.U4)] out int ExceptionUnknown5);
        [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getStatusNvram", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        private static extern int _getStatusNvram(
            [MarshalAs(UnmanagedType.LPStr)] string CardName,
            [MarshalAs(UnmanagedType.U4)] out int AccessRights,
            [MarshalAs(UnmanagedType.U4)] out int TotalNvRamSize,
            [MarshalAs(UnmanagedType.U4)] out int RandomAccessSectors,
            [MarshalAs(UnmanagedType.U4)] out int CyclicAccessSectors,
            [MarshalAs(UnmanagedType.U4)] out int NextCyclicWrite);
        [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getCardId", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        private static extern int _getCardId(
            [MarshalAs(UnmanagedType.LPStr)] string CardName,
            [MarshalAs(UnmanagedType.SysInt)] IntPtr cardid12bytes);
        [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getControllerId", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        private static extern int _getControllerId(
            [MarshalAs(UnmanagedType.LPStr)] string CardName,
            [MarshalAs(UnmanagedType.SysInt)] IntPtr conrollerId,
            [MarshalAs(UnmanagedType.U4)] ref int conrollerIdSize);
        [DllImport("CardManagement.dll", CharSet = CharSet.Ansi, EntryPoint = "getBaseFWVersion", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        private static extern int _getBaseFWVersion(
            [MarshalAs(UnmanagedType.LPStr)] string CardName,
            [MarshalAs(UnmanagedType.SysInt)] IntPtr firmware8bytes,
            [MarshalAs(UnmanagedType.U4)] ref int part2);
        public static bool SecureSd_DeviceInfo(string CardName)
        {
            #region getStatus()
            int LicenseMode, SystemState, RetryCounter, SoRetryCounter, ResetCounter, CdRomAddress, ExtSecurityFlags;
            int res = _getStatus(CardName, out LicenseMode, out SystemState, out RetryCounter, out SoRetryCounter, out ResetCounter, out CdRomAddress, out ExtSecurityFlags);
            Console.WriteLine("***** CardManagement.dll getStatus() returns: 0x" + res.ToString("X4"));
            Console.WriteLine("License Mode            : 0x" + LicenseMode.ToString("X"));
            Console.Write    ("System State            : 0x" + SystemState.ToString("X") + " = ");
            switch (SystemState)
            {
                case 0: Console.Write("Transparent Mode"); break;
                case 1: Console.Write("Data Protection Unlocked"); break;
                case 2: Console.Write("Data Protection Locked"); break;
                default: Console.Write("Unknown"); break;
            }
            Console.WriteLine("");
            Console.WriteLine("Retry Counter           : " + RetryCounter);
            Console.WriteLine("SO Retry Counter        : " + SoRetryCounter);
            Console.WriteLine("Number of Resets        : " + ResetCounter);
            Console.WriteLine("CD ROM Address          : 0x" + CdRomAddress.ToString("X")); // This is only in cardManagerCLI.exe, but not cardManager.exe
            Console.WriteLine("Extended Security Flags : 0x" + ExtSecurityFlags.ToString("X"));
            /* TODO: Extended Security Flags
            ??	    Support Fast Wipe
            0x1?	Reset Requires SO PIN
            0x2?	Multiple Partition Protection
            0x10	Secure PIN Entry
            0x20	Login Status Survives Soft Reset
            */
            Console.WriteLine("");
            #endregion

            #region getStatusException()
            int ExceptionUnknown1, ExceptionUnknown2, ExceptionUnknown3, ExceptionUnknown4, ExceptionUnknown5;
            res = _getStatusException(CardName, out ExceptionUnknown1, out ExceptionUnknown2, out ExceptionUnknown3, out ExceptionUnknown4, out ExceptionUnknown5);
            Console.WriteLine("***** CardManagement.dll getStatusException() returns: 0x" + res.ToString("X4"));
            // TODO: What are these values???
            Console.WriteLine("ExceptionUnknown1       : 0x" + ExceptionUnknown1.ToString("X"));
            Console.WriteLine("ExceptionUnknown2       : 0x" + ExceptionUnknown2.ToString("X"));
            Console.WriteLine("ExceptionUnknown3       : 0x" + ExceptionUnknown3.ToString("X"));
            Console.WriteLine("ExceptionUnknown4       : 0x" + ExceptionUnknown4.ToString("X"));
            Console.WriteLine("ExceptionUnknown5       : 0x" + ExceptionUnknown5.ToString("X"));
            Console.WriteLine("");
            #endregion

            // TODO: getApplicationVersion(?, ?)
            // - Application version cb

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
            Console.Write("Unique ID: "); // "getControllerId()" shows the "UniqueID" for USB PU-50n, while getCardId() shows something weird. Confusing!
            for (int i = 0; i < controlerIdSize; i++)
            {
                Console.Write(" " + controlerId[i].ToString("X2"));
            }
            Console.WriteLine("");
            Console.WriteLine("");
            #endregion

            // TODO: getVersion() => hartkodiert 0x400 in CardManagement.dll
            // TODO: getBuildDateAndTime() => hartkodiert "2019/04/11 11:33:04" in CardManagement.dll
            // TODO: getOverallSize(?, ?)
            // TODO: getPartitionTable(?, ?)
            // TODO: getProtectionProfiles(?, ?, ?, ?)

            #region getStatusNvram()
            int NvramAccessRights, NvramTotalNvramSize, NvramRandomAccessSectors, NvramCyclicAccessSectors, NextCyclicWrite;
            res = _getStatusNvram(CardName, out NvramAccessRights, out NvramTotalNvramSize, out NvramRandomAccessSectors, out NvramCyclicAccessSectors, out NextCyclicWrite);
            Console.WriteLine("***** CardManagement.dll getStatusNvram() returns: 0x" + res.ToString("X4"));
            // TODO: "Fuse persistently" might also change parts of "Access Rights". But I don't want to try it with my card!
            // 0000ccrr    c=cyclic   r=random
            // 1 = all read
            // 2 = all write
            // 4 = user read
            // 8 = user write
            // 10 = wrap (cyclic)
            bool Wrap = (NvramAccessRights & 0x0000F) == 1;
            int RandomRights = NvramAccessRights & 0xFF;
            int CyclicRights = (NvramAccessRights >> 8) & 0xFF;
            Console.WriteLine("Access Rights:         0x" + NvramAccessRights.ToString("X8"));
            Console.WriteLine("   Cyclic NVRAM:       0x" + CyclicRights.ToString("X2"));
            Console.WriteLine("                       [" + ((CyclicRights & 0x1) != 0 ? "x" : " ") + "] All Read (0x1)");
            Console.WriteLine("                       [" + ((CyclicRights & 0x2) != 0 ? "x" : " ") + "] All Write (0x2)");
            Console.WriteLine("                       [" + ((CyclicRights & 0x4) != 0 ? "x" : " ") + "] User Read (0x4)");
            Console.WriteLine("                       [" + ((CyclicRights & 0x8) != 0 ? "x" : " ") + "] User Write (0x8)");
            Console.WriteLine("                       [" + ((CyclicRights & 0x10) != 0 ? "x" : " ") + "] Wrap around (0x10)");
            Console.WriteLine("   Random NVRAM:       0x" + RandomRights.ToString("X2"));
            Console.WriteLine("                       [" + ((RandomRights & 0x1) != 0 ? "x" : " ") + "] All Read (0x1)");
            Console.WriteLine("                       [" + ((RandomRights & 0x2) != 0 ? "x" : " ") + "] All Write (0x2)");
            Console.WriteLine("                       [" + ((RandomRights & 0x4) != 0 ? "x" : " ") + "] User Read (0x4)");
            Console.WriteLine("                       [" + ((RandomRights & 0x8) != 0 ? "x" : " ") + "] User Write (0x8)");
            Console.WriteLine("Total NVRAM Size:      0x" + NvramTotalNvramSize.ToString("X"));
            Console.WriteLine("Random Access Sectors: 0x" + NvramRandomAccessSectors.ToString("X"));
            Console.WriteLine("Cyclic Access Sectors: 0x" + NvramCyclicAccessSectors.ToString("X"));
            Console.WriteLine("Next Cyclic Write:     0x" + NextCyclicWrite.ToString("X"));
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
            Console.WriteLine("Swissbit Secure USB Stick PU50-n");
            Console.WriteLine("Access using FileTunnelInterface.dll (from TSE Maintenance Tool) and CardManagement.dll (from SDK)");
            Console.WriteLine("");

            // TODO: Auto Recognize or select
            const string DEVICE_NAME = "Swissbit Secure USB PU-50n DP 0";
            const string DECIDE_DRIVE_LETTER = "G";

            SecureSDUtils.SecureSd_DeviceInfo(DEVICE_NAME);

            // Test
            //SecureSDUtils.SecureSd_Entsperren(DEVICE_NAME, "damdamdamdam");
            //SecureSDUtils.SecureSd_Sperren(DEVICE_NAME);



            // The FileTunnelInterface.dll is part of the TSE Maintenance Tool.
            // If FileTunnelInterface.dll works on a TSE, then the TSE is considered "in an undefined state".
            // But with a Secure SD, the FileTunnelInterface works.
            // So, I guess both SecureSD and TSE are made out of the same "raw material",
            // and the firmware and/or configuration was uploaded via the FileTunnelInterface (Vendor Command Interface).
            // If a FileTunnelInterface.dll works, then either the firmware was not applied in the factory,
            // or maybe the TSE can fall back into the raw state if the firmware failed to boot (TSE Panic).
            // But that's just a theory. Let's just enjoy that FileTunnelInterface.dll works on a SecureSD,
            // because now we can also fetch the LTM data! The LTM data has the same structure as described
            // in the TSE Firmware Specification.

            if (DECIDE_DRIVE_LETTER != "")
            {
                VendorCommandsInterface vci = new VendorCommandsInterface(DECIDE_DRIVE_LETTER);

                #region vci_read_chip_id
                Console.WriteLine("***** FileTunnelInterface.dll vci_read_chip_id():");
                Console.WriteLine(vci.readChipID());
                Console.WriteLine("");
                #endregion

                #region vci_read_extended_card_lifetime_information and its interpretation
                Console.WriteLine("***** FileTunnelInterface.dll vci_read_extended_card_lifetime_information():");
                byte[] array = vci.readLTMData();
                MemoryStream stream = new MemoryStream();
                stream.Write(array, 0, array.Length);
                stream.Seek(0, SeekOrigin.Begin);
                using (BinaryReaderBigEndian r = new BinaryReaderBigEndian(stream))
                {
                    // Selber Aufbau wie in TSE Firmwarebeschreibung angegeben
                    Console.WriteLine("Proprietary 1 Manufacturer proprietary format: " + BitConverter.ToString(r.ReadBytes(26)).Replace("-", " ")); // Firmware-Dokumentation ist falsch. Länge ist 26, nicht 25.
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
                    Console.WriteLine("Proprietary 2 Manufacturer proprietary format: " + BitConverter.ToString(r.ReadBytes(100)).Replace("-", " "));
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
