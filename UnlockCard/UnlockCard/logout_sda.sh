#!/bin/bash

DIR=$( dirname "$0" )
cd "$DIR"

if [ "$EUID" -ne 0 ]
  then echo "Please run as root"
  exit
fi

make
chmod +x UnlockCard

umount /mnt/sdcard_data/
./UnlockCard LOCK /mnt/sdcard_comm/
umount /mnt/sdcard_comm/
