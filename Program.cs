using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CS_ConsoleTest
{
	internal class SecureSDUtils
	{
		public static string ByteArrayToString(byte[] ba)
		{
			StringBuilder hex = new StringBuilder(ba.Length * 2);
			foreach (byte b in ba)
				hex.AppendFormat("{0:x2}", b);
			return hex.ToString();
		}       
		
		// Folgende Funktionen durch RevEng (IDA Debugger) herausgefunden

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

		#region getStatus (TODO)
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
		public static bool SecureSd_DeviceInfo(string CardName)
		{
			int LicenseMode, SystemState, RetryCounter, SoRetryCounter, ResetCounter, CdRomAddress, ExtSecurityFlags;

			int res = _getStatus(CardName, out LicenseMode, out SystemState, out RetryCounter, out SoRetryCounter, out ResetCounter, out CdRomAddress, out ExtSecurityFlags);
			Console.WriteLine("getstatus() returns: 0x" + res.ToString("X4");
			Console.WriteLine("License Mode : 0x" + LicenseMode.ToString("X"));
			Console.WriteLine("System State : 0x" + SystemState.ToString("X"));
			Console.WriteLine("Retry Counter : " + RetryCounter);
			Console.WriteLine("SO Retry Counter : " + SoRetryCounter);
			Console.WriteLine("Number of Resets : " + ResetCounter);
			Console.WriteLine("CD ROM Address : 0x" + CdRomAddress.ToString("X")); // This is only in cardManagerCLI.exe, but not cardManager.exe
			Console.WriteLine("Extended Security Flags : 0x" + ExtSecurityFlags.ToString("X"));

			// Seltsam: CardManagerLite.exe und CardManagerCLI.exe gehen nicht mit USB.  Aber CardManager.exe geht.

			// TODO: getStatusException()
			// - defaultCDRomAddress 0x0
			// - readExceptionAddress 0x0
			// - partition1Offset 0x0
			// - partition1Offset 0 blocks
			// - Application version cb

			// TODO: getBaseFWVersion()
			// - Base Firmware Version

			// TODO: cardId()
			// - Unique Card ID:

			// TODO: getControllerId()
			// - Controller ID:

			return res == 0;
		}
		#endregion

	}
	internal class VendorCommandsInterface
	{
		private IntPtr ci;

		// Diese Methoden sind durch das TSE Maintenance Tool eingebunden:

		[DllImport("FileTunnelInterface.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int vci_open_volume(out IntPtr ci, uint flags, char volume);

		[DllImport("FileTunnelInterface.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int vci_read_extended_card_lifetime_information(IntPtr ci, byte[] response);

		[DllImport("FileTunnelInterface.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int vci_read_chip_id(IntPtr ci, byte[] chipID, int chipIDLen);

		[DllImport("FileTunnelInterface.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int vci_close(IntPtr ci);

		// Diese weiteren Funktionen gibt es:
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
		public static string ByteArrayToString(byte[] ba)
		{
			StringBuilder hex = new StringBuilder(ba.Length * 2);
			foreach (byte b in ba)
				hex.AppendFormat("{0:x2}", b);
			return hex.ToString();
		}

		static void Main(string[] args)
		{

			SecureSDUtils.SecureSd_DeviceInfo("Swissbit Secure USB PU-50n DP 0");

			// Test
			//SecureSDUtils.SecureSd_Entsperren("Swissbit Secure USB PU-50n DP 0", "damdamdamdam");
			//SecureSDUtils.SecureSd_Sperren("Swissbit Secure USB PU-50n DP 0");

			return;


			Console.WriteLine("Swissbit Secure USB Stick");
			Console.WriteLine("Read using FileTunnelInterface.dll from TSE Maintenance Tool");
			Console.WriteLine("");

			VendorCommandsInterface vci = new VendorCommandsInterface("f");
			Console.WriteLine("=== Unique Chip ID ===");
			Console.WriteLine(vci.readChipID());
			Console.WriteLine("");

			Console.WriteLine("=== LTM Data ===");
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

			vci = null;



			//Console.WriteLine(ByteArrayToString(array));

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
