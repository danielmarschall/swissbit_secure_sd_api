#include <iostream>
#include <cstring>

#pragma clang diagnostic ignored "-Wdeprecated-declarations"

#ifdef _WIN32
#include <windows.h>
#elif __linux__
#include <unistd.h> // for sleep
#include <fcntl.h> // for open
#include <unistd.h> // for write, close
#elif __APPLE__
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <fcntl.h>
#include <sys/stat.h>
#endif

#include "sha256.c"

#define BLOCKSIZE 512

unsigned char magicSequence[] = {
    0x10, 0x6A, 0xF8, 0x1A, 0xD6, 0xF8, 0xC8, 0x70,
    0xAC, 0x7E, 0x85, 0xF0, 0xE9, 0x9E, 0xF3, 0x9D,
    0x1E, 0x11, 0xA1, 0xBA, 0x87, 0x4A, 0xC6, 0xDB,
    0x42, 0x81, 0x15, 0x8E, 0xFE, 0x6D, 0x3C, 0x81
};

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
		memset(data, 0, BLOCKSIZE);

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

	void* buffer;
	posix_memalign(&buffer, BLOCKSIZE, BLOCKSIZE);
	memcpy(buffer, data, BLOCKSIZE);
	// TODO: O_DIRECT does only work with super user! isn't there any possibility for normal users?
	int f = open(commFileName, O_CREAT | O_TRUNC | O_RDWR | O_DIRECT | O_SYNC, S_IRUSR | S_IWUSR);
	write(f, buffer, BLOCKSIZE);

	for (int i = 0; i < 10; i++) {
		memset(data, 0, BLOCKSIZE);

		usleep(200 * 1000); // 200ms

		lseek(f, 0, SEEK_SET);
		read(f, buffer, BLOCKSIZE);
		memcpy(data, buffer, BLOCKSIZE);

		if (data[0] == 0x03) break; // NOT SURE: I could imagine that the first byte is the status, and 0x03 means "response data is available"
	}

	close(f);

	free(buffer);

#elif __APPLE__

	// Allocate aligned memory buffer (using posix_memalign for alignment).
	void* buffer;
	if (posix_memalign(&buffer, BLOCKSIZE, BLOCKSIZE) != 0) {
		perror("posix_memalign failed");
		return false;
	}

	// Copy data into buffer.
	memcpy(buffer, data, BLOCKSIZE);

	int f = open(commFileName, O_CREAT | O_TRUNC | O_RDWR | O_SYNC, S_IRUSR | S_IWUSR);
	if (f < 0) {
		perror("Failed to open file");
		free(buffer);
		return false;
	}
	
	// Disable caching (because macOS does not have O_DIRECT, we need to use F_NOCACHE)
	// TODO: check if this solves the "require root" problem with Linux
	if (fcntl(f, F_NOCACHE, 1) == -1) {
		perror("Failed to disable cache");
		close(f);
		return 1;
	}

	// Write buffer to file.
	if (write(f, buffer, BLOCKSIZE) != BLOCKSIZE) {
		perror("Failed to write to file");
		close(f);
		free(buffer);
		return false;
	}

	// Loop for reading and processing data.
	for (int i = 0; i < 10; i++) {
		memset(data, 0, BLOCKSIZE);

		usleep(200 * 1000); // 200ms delay.

		// Rewind file position to start.
		if (lseek(f, 0, SEEK_SET) < 0) {
			perror("lseek failed");
			break;
		}

		// Read from file into buffer.
		if (read(f, buffer, BLOCKSIZE) != BLOCKSIZE) {
			perror("Failed to read from file");
			break;
		}

		// Copy buffer content back into data.
		memcpy(data, buffer, BLOCKSIZE);

		// Check for the status byte (assumed logic).
		if (data[0] == 0x03) break;
	}

	// Clean up.
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
		fprintf(stderr, "Locks/Unlocks a Swissbit PS-45u DP card using Windows, Linux, or macOS!\n");
#ifdef _WIN32
		fprintf(stderr, "Syntax: %s LOCK <X:\\>\n", argv[0]);
		fprintf(stderr, "Syntax: %s UNLOCK <X:\\> <Password>\n", argv[0]);
		fprintf(stderr, "        DO NOT FORGET THE TRAILING PATH DELIMITER!\n");
#else
#ifdef __linux__
		fprintf(stderr, "Syntax: sudo %s LOCK </mnt/sdcard/>\n", argv[0]);
		fprintf(stderr, "Syntax: sudo %s UNLOCK </mnt/sdcard/> <Password>\n", argv[0]);
		fprintf(stderr, "        DO NOT FORGET THE TRAILING PATH DELIMITER!\n");
#elif __APPLE__
		fprintf(stderr, "Syntax: %s LOCK </Volumes/sdcard/>\n", argv[0]);
		fprintf(stderr, "Syntax: %s UNLOCK </Volumes/sdcard/> <Password>\n", argv[0]);
		fprintf(stderr, "        DO NOT FORGET THE TRAILING PATH DELIMITER!\n");
#endif
		fprintf(stderr, "\n");
		fprintf(stderr, "ATTENTION:\n");
		fprintf(stderr, "On macOS and Linux, only works if you have 2 partitions with the security profiles Public CDRom at offset 4096KB + Private RW for the rest\n");
		fprintf(stderr, "otherwise, data will be lost since changes are always reverted from partition #1\n");
		fprintf(stderr, "In the Extended Security Flags, the multiple partition option must be checked.\n");
		fprintf(stderr, "(Windows is OK with having only 1 partition for everything)\n");
		fprintf(stderr, "\n");
#ifdef WIN32
		fprintf(stderr, "USAGE EXAMPLE (Windows):\n");
		fprintf(stderr, "%s UNLOCK E:\\ testpassword\n", argv[0]);
		fprintf(stderr, "...\n");
		fprintf(stderr, "%s LOCK E:\\\n", argv[0]);
		fprintf(stderr, "\n");
#elif __linux__
		fprintf(stderr, "Please note, this program requires being run as root user.\n"); // TODO: any way to avoid that?
		fprintf(stderr, "\n");
		fprintf(stderr, "USAGE EXAMPLE (Linux):\n");
		fprintf(stderr, "sudo mount /dev/sda1 /mnt/sdcard_comm/\n");
		fprintf(stderr, "%s UNLOCK /mnt/sdcard_comm/ testpassword\n", argv[0]);
		fprintf(stderr, "sudo mount /dev/sda2 /mnt/sdcard_data/\n");
		fprintf(stderr, "...\n");
		fprintf(stderr, "sudo umount /mnt/sdcard_data/\n");
		fprintf(stderr, "%s LOCK /mnt/sdcard_comm/\n", argv[0]);
		fprintf(stderr, "sudo umount /mnt/sdcard_comm/\n");
		fprintf(stderr, "\n");
#elif __APPLE__
		fprintf(stderr, "USAGE EXAMPLE (macOS):\n");
		fprintf(stderr, "diskutil list\n");
		fprintf(stderr, "diskutil mount /dev/disk2s1  (Should usually happen automatically)\n");
		fprintf(stderr, "%s UNLOCK /Volumes/COMM/ testpassword\n", argv[0]);
		fprintf(stderr, "diskutil mount /dev/disk2s2\n");
		fprintf(stderr, "...\n");
		fprintf(stderr, "diskutil umount /dev/disk2s2\n");
		fprintf(stderr, "%s LOCK /mnt/COMM/\n", argv[0]);
		fprintf(stderr, "diskutil mount /dev/disk2s1\n");
		fprintf(stderr, "\n");
#endif
#endif
		fprintf(stderr, "Note: Does NOT work with PU-50n DP (USB).\n");
		return 2;
	}

	bool doLock = strcmp(argv[1], "LOCK") == 0;

	char commFileName[1024];
	sprintf(commFileName, "%s__communicationFile", argv[2]); // TODO: if the user forgets the trailing path delimiter, then add it

	char data[BLOCKSIZE] = { };

	memset(data, 0, BLOCKSIZE);
	memcpy(data, magicSequence, sizeof(magicSequence));
	data[0x20] = 1; // state?
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
		fwrite(data, sizeof(char), BLOCKSIZE, fDebug);
		fclose(fDebug);

		fprintf(stderr, "Invalid response from device at getStatus()! (Path correct? Running as root?)\n");
		return 1;
	}
	int response = ((data[0x13] & 0xFF) << 8) + (data[0x14] & (0xFF));
	if (response != 0x9000) {
		fprintf(stderr, "Error 0x%x at getStatus()!\n", response);
		return 1;
	}


	if (doLock) {
		fprintf(stdout, "Try locking card...\n");
		memset(data, 0, BLOCKSIZE);
		memcpy(data, magicSequence, sizeof(magicSequence));
		data[0x20] = 1; // state?
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
		int retry = data[0x06] & 0xFF;
		fprintf(stdout, "Retry Counter (before unlock attempt) is %d!\n", retry);

		if ((data[0x12] & 0x10) != 0) {
			fprintf(stdout, "Try unlocking card with Secure PIN Entry...\n");

			// Get the hash challenge (changed after each successful or failed login, or powercycle)

			memset(data, 0, BLOCKSIZE);
			memcpy(data, magicSequence, sizeof(magicSequence));
			data[0x20] = 1; // state?
			data[0x21] = 0x00;
			data[0x22] = 0x00;
			data[0x23] = 5; // length of message
			data[0x24] = 0xFF;
			data[0x25] = 0x70; // 0x570FF = get login challenge
			data[0x26] = 0x05;
			data[0x27] = 0x00;
			data[0x28] = 0; // length of parameters (no data)
			secure_sd_comm(commFileName, &data[0]);
			if ((data[0x00] != 0x03) || (data[0x01] != 0x00) || (data[0x02] != 0x00) || (data[0x03] != 0x22)) {
				fprintf(stderr, "Invalid response from device! (getLoginChallenge)\n");
				return 1;
			}
			int response = ((data[0x24] & 0xFF) << 8) + (data[0x25] & (0xFF));
			if (response != 0x9000) {
				fprintf(stderr, "Error 0x%x at getLoginChallenge()!\n", response);
				return 1;
			}

			// Calculate the code which will be sent to verify():
			// code = sha256(sha256(password) + challenge)
			
			void* tmp;
#ifdef WIN32
			tmp = malloc(BLOCKSIZE);
#else
			if (posix_memalign(&tmp, BLOCKSIZE, BLOCKSIZE) != 0) {
				perror("posix_memalign failed");
				return 1;
			}
#endif
			
			memcpy(tmp, argv[3], strlen(argv[3]));

			SHA256_CTX h;

			sha256_init(&h);
			sha256_update(&h, (const BYTE*)tmp, strlen(argv[3]));
			sha256_final(&h, (BYTE*)tmp); // add password hash

			memcpy((char*)tmp + 0x20, data + 0x04, 32); // add challenge

			sha256_init(&h);
			sha256_update(&h, (const BYTE*)tmp, 64);
			sha256_final(&h, (BYTE*)tmp);

			// Now send the code to verify()

			memset(data, 0, BLOCKSIZE);
			memcpy(data, magicSequence, sizeof(magicSequence));
			data[0x20] = 1; // state?
			data[0x21] = 0x00;
			data[0x22] = 0x00;
			data[0x23] = 6 + 32; // length of message
			data[0x24] = 0xFF;
			data[0x25] = 0x30; // 0x30FF = unlock card
			data[0x26] = 0x00;
			data[0x27] = 0x00;
			data[0x28] = 1 + 32; // length of parameters
			data[0x29] = 32; // code length
			memcpy(data + 0x2A, tmp, 32); // code
		}
		else {
			fprintf(stdout, "Try unlocking card with Standard PIN Entry...\n");
			memset(data, 0, BLOCKSIZE);
			memcpy(data, magicSequence, sizeof(magicSequence));
			data[0x20] = 1; // state?
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
	}

	secure_sd_comm(commFileName, &data[0]);

	if ((data[0x00] != 0x03) || (data[0x01] != 0x00) || (data[0x02] != 0x00) || (data[0x03] != 0x02)) {
		fprintf(stderr, "Invalid response from device!\n");
		return 1;
	}

	response = ((data[0x04] & 0xFF) << 8) + (data[0x05] & (0xFF));

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
