#include <iostream>
#include <cstring>

#ifdef _WIN32
#include <windows.h>
#elif __linux__
#include <unistd.h> // for sleep
#include <fcntl.h> // for open
#include <unistd.h> // for write, close
#endif

bool secure_sd_comm(char* commFileName, char* data) {

#ifdef _WIN32

	HANDLE hComm = CreateFileA(commFileName, GENERIC_READ | GENERIC_WRITE, 0, NULL,
		CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_NO_BUFFERING, NULL);
	if (hComm == INVALID_HANDLE_VALUE) {
		fprintf(stderr, "Cannot open file %s\n", commFileName);
		return false;
	}

	// Write command
	DWORD dwWritten;
	if (!WriteFile(hComm, data, 512, &dwWritten, NULL)) {
		fprintf(stderr, "Cannot write to file %s\n", commFileName);
		return false;
	}

	for (int i = 0; i < 10; i++) {
		memset(data, 0, sizeof(data));

		Sleep(200);

		// Read reply
		DWORD dwRead;
		if ((SetFilePointer(hComm, 0, NULL, FILE_BEGIN) == INVALID_SET_FILE_POINTER) || !ReadFile(hComm, data, 512, &dwRead, NULL)) {
			fprintf(stderr, "Cannot read from file %s\n", commFileName);
			return false;
		}

		if (data[0] == 0x03) break; // NOT SURE: I could imagine that the first byte is the status, and 0x03 means "response data is available"
	}

	CloseHandle(hComm);


#elif __linux__

	#define BLOCKSIZE 512
        void *buffer;
        posix_memalign(&buffer, BLOCKSIZE, BLOCKSIZE);
        memcpy(buffer, data, BLOCKSIZE);
	// TODO: O_DIRECT does only work with super user! isn't there any possibility for normal users?
        int f = open(commFileName, O_CREAT|O_TRUNC|O_RDWR|O_DIRECT|O_SYNC, S_IRUSR|S_IWUSR);
        write(f, buffer, BLOCKSIZE);

	for (int i = 0; i < 10; i++) {
		memset(data, 0, sizeof(data));

		usleep(200*1000); // 200ms

		lseek(f, 0, SEEK_SET);
		read(f, buffer, BLOCKSIZE);
		memcpy(data, buffer, BLOCKSIZE);

		if (data[0] == 0x03) break; // NOT SURE: I could imagine that the first byte is the status, and 0x03 means "response data is available"
	}

        close(f);

        free(buffer);

#else

	#error "OS not supported!"

#endif

	remove(commFileName);

	return data[0] == 0x03;
}

int main(int argc, char** argv) {

	if (
			(argc < 2) ||
			((strcmp(argv[1], "LOCK") != 0) && (strcmp(argv[1], "UNLOCK") != 0)) ||
			((strcmp(argv[1], "LOCK") == 0) && (argc != 3)) ||
			((strcmp(argv[1], "UNLOCK") == 0) && (argc != 4))
		) {
		fprintf(stderr, "Unlocks a Swissbit PS-45u DP card using Windows or Linux!\n");
#ifdef _WIN32
		fprintf(stderr, "Syntax: %s LOCK <X:\\>\n", argv[0]);
		fprintf(stderr, "Syntax: %s UNLOCK <X:\\> <Password>\n", argv[0]);
#else
		fprintf(stderr, "Syntax: %s LOCK </mnt/sdcard/>\n", argv[0]);
		fprintf(stderr, "Syntax: %s UNLOCK </mnt/sdcard/> <Password>\n", argv[0]);
		fprintf(stderr, "\n");
		fprintf(stderr, "ATTENTION:\n");
		fprintf(stderr, "On Linux, only works if you have 2 partitions with the security profiles Public CDRom at offset 4096KB + Private RW for the rest\n");
		fprintf(stderr, "otherwise, data will be lost since changes are always reverted from partition #1\n");
		fprintf(stderr, "In the Extended Security Flags, the multiple partition option must be checked.\n");
		fprintf(stderr, "(Windows is OK with having only 1 partition for everything)\n");
		fprintf(stderr, "Please note, this program requires being run as root user.\n");
		fprintf(stderr, "\n");
		fprintf(stderr, "USAGE EXAMPLE:\n");
		fprintf(stderr, "mount /dev/sda1 /mnt/sdcard_comm/\n");
		fprintf(stderr, "%s UNLOCK /mnt/sdcard_comm/ testpassword\n", argv[0]);
		fprintf(stderr, "mount /dev/sda2 /mnt/sdcard_data/\n");
		fprintf(stderr, "...\n");
		fprintf(stderr, "umount /mnt/sdcard_data/\n");
		fprintf(stderr, "%s LOCK /mnt/sdcard_comm/\n", argv[0]);
		fprintf(stderr, "umount /mnt/sdcard_comm/\n");
		fprintf(stderr, "\n");
#endif
		fprintf(stderr, "Note: Does not work with PU-50n DP (USB).\n");
		// TODO: Remove this note as soon as "Secure PIN Entry" is implemented.
		fprintf(stderr, "Also, \"Secure PIN Entry\" must be disabled! Does not work with PU-50n DP (USB).\n");
		return 2;
	}

	bool doLock = strcmp(argv[1], "LOCK") == 0;

	char commFileName[1024];
	sprintf(commFileName, "%s__communicationFile", argv[2]);


	char data[512] = { };

	memset(data, 0, sizeof(data));
	sprintf(data, "\x10\x6A\xF8\x1A\xD6\xF8\xC8\x70\xAC\x7E\x85\xF0\xE9\x9E\xF3\x9D\x1E\x11\xA1\xBA\x87\x4A\xC6\xDB\x42\x81\x15\x8E\xFE\x6D\x3C\x81"); // magic sequence
	data[0x20] = 1; // protocol version?
	data[0x21] = 0x00;
	data[0x22] = 0x00;
	data[0x23] = 5;
	data[0x24] = 0xFF;
	data[0x25] = 0x70; // 0x70FF = getStatus
	data[0x26] = 0x00;
	data[0x27] = 0x00;
	data[0x28] = 0x00; // length of parameters (none)
	secure_sd_comm(commFileName, &data[0]);

	if ((data[0x00] != 0x03) || (data[0x01] != 0x00) || (data[0x02] != 0x00) || (data[0x03] != 0x11)) {
		FILE* fDebug;
		fDebug = fopen("response_debug.dat", "wb");
		fwrite(data, sizeof(char), sizeof(data), fDebug);
		fclose(fDebug);

		fprintf(stderr, "Invalid response from device! (Path correct? Running as root?)\n");
		return 1;
	}

	int retry = data[0x06] & 0xFF;
	fprintf(stdout, "Retry Counter (before unlock attempt) is %d!\n", retry);

	if ((data[0x12] & 0x10) != 0) {
		// TODO: Implement "Secure PIN Entry" like this:  verify(sha256(sha256(password) + challenge)) where challenge is command 0x570FF.
		fprintf(stderr, "You must disable \"Secure PIN Entry\" in the security settings of the device!\n");
		return 1;
	}

	if (doLock) {
		memset(data, 0, sizeof(data));
		sprintf(data, "\x10\x6A\xF8\x1A\xD6\xF8\xC8\x70\xAC\x7E\x85\xF0\xE9\x9E\xF3\x9D\x1E\x11\xA1\xBA\x87\x4A\xC6\xDB\x42\x81\x15\x8E\xFE\x6D\x3C\x81"); // magic sequence
		data[0x20] = 1; // protocol version?
		data[0x21] = 0x00;
		data[0x22] = 0x00;
		data[0x23] = 5;
		data[0x24] = 0xFF;
		data[0x25] = 0x31; // 0x31FF = lock card
		data[0x26] = 0x00;
		data[0x27] = 0x00;
		data[0x28] = 0; // length of parameters (none)
	}
	else
	{
		memset(data, 0, sizeof(data));
		sprintf(data, "\x10\x6A\xF8\x1A\xD6\xF8\xC8\x70\xAC\x7E\x85\xF0\xE9\x9E\xF3\x9D\x1E\x11\xA1\xBA\x87\x4A\xC6\xDB\x42\x81\x15\x8E\xFE\x6D\x3C\x81"); // magic sequence
		data[0x20] = 1; // protocol version?
		data[0x21] = 0x00;
		data[0x22] = 0x00;
		data[0x23] = 6 + strlen(argv[3]); // length of message
		data[0x24] = 0xFF;
		data[0x25] = 0x30; // 0x30FF = unlock card
		data[0x26] = 0x00;
		data[0x27] = 0x00;
		data[0x28] = 1 + strlen(argv[3]); // length of parameters
		data[0x29] = strlen(argv[3]); // password length
		sprintf(data + 0x2A, "%s", argv[3]); // password
	}
	secure_sd_comm(commFileName, &data[0]);

	if ((data[0x00] != 0x03) || (data[0x01] != 0x00) || (data[0x02] != 0x00) || (data[0x03] != 0x02)) {
		fprintf(stderr, "Invalid response from device!\n");
		return 1;
	}

	int response = (data[0x04] & 0xFF) << 8 + (data[0x05] & (0xFF));

	if (response == 0x9000) {
		char tmpFileName[1024];
		if (doLock) {
			sprintf(tmpFileName, "%sCARD EXPOSED", argv[2]);
			remove(tmpFileName);
			sprintf(tmpFileName, "%sCARD SECURED", argv[2]);
			fclose(fopen(tmpFileName, "w+"));
			fprintf(stdout, "Card locked!\n");
			return 0;
		}
		else {
			sprintf(tmpFileName, "%sCARD SECURED", argv[2]);
			remove(tmpFileName);
			sprintf(tmpFileName, "%sCARD EXPOSED", argv[2]);
			fclose(fopen(tmpFileName, "w+"));
			fprintf(stdout, "Card unlocked!\n");
			return 0;
		}
	}
	else if (response == 0x6F02) {
		fprintf(stderr, "Wrong password!\n");
		return 1;
	}
	else {
		fprintf(stderr, "Error 0x%x!\n", response);
		return 1;
	}
}
