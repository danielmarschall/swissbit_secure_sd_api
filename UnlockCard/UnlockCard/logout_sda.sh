#!/bin/bash

umount /mnt/sdcard_data/
./UnlockCard LOCK /mnt/sdcard_comm/
umount /mnt/sdcard_comm/
