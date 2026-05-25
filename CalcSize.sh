#!/bin/bash
BuildNum="2"
cd "$(dirname "$0")"
if [ "$#" -eq 1 ]
then
	if [[ "$1" == "-h" || "$1" == "--help" ]]
	then
		echo "CalcSize.sh (build #${BuildNum})"
		echo "	Calculates the total amount of unqiue storage taken up by backups."
		echo "	Note that this script expects to be stored the directory containing the timeshift backup folder, I reccomend placing a symlink there and running that."
		echo "	This script should be run as root, needs direct access to the timeshift snapshots folder and can't handle multiple backup disks or btrfs snapshots."
		exit 0
	fi
fi
set -euo pipefail
renice -n 15 -p $$ >/dev/null

#Ensure we have access to the snapshots folder before we do anything
ls ./timeshift/snapshots > /dev/null

cd ./timeshift/snapshots/
echo "#Started at $(date)"
for snapshot in $(ls -X)
do
	echo "$snapshot: $(sudo find "$snapshot" -type f -links 1 -printf "%k\n" | awk '{s=s+$1} END {print s}' | numfmt --from-unit=1024 --to=iec --suffix=B)"
done
echo "#Ended at $(date)"
