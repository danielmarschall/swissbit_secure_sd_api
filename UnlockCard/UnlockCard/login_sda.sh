#!/bin/bash

DIR=$( dirname "$0" )
cd "$DIR"

if [ "$EUID" -ne 0 ]
  then echo "Please run as root"
  exit
fi

make
chmod +x UnlockCard

if [ ! -d /mnt/sdcard_comm ]; then
	mkdir /mnt/sdcard_comm
fi

if [ ! -d /mnt/sdcard_data ]; then
	mkdir /mnt/sdcard_data
fi

echo "Please enter your password:"
read -s password

mount /dev/sda1 /mnt/sdcard_comm/
./UnlockCard UNLOCK /mnt/sdcard_comm/ "$password"
mount /dev/sda2 /mnt/sdcard_data/
ls -la /mnt/sdcard_data/

