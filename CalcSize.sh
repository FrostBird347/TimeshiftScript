#!/bin/bash
BuildNum="3"
cd "$(dirname "$0")"
if [ "$#" -eq 1 ]
then
	if [[ "$1" == "-h" || "$1" == "--help" ]]
	then
		echo "CalcSize.sh (build #${BuildNum})"
		echo "	Calculates the total amount of unqiue storage taken up by backups."
		echo "	Note that this script expects to be stored the directory containing the timeshift backup folder, I reccomend placing a symlink there and running that."
		echo "	This script should be run as root, needs direct access to the timeshift snapshots folder and can't handle multiple backup disks or btrfs snapshots."
		echo "	Also note that this script's output should only be treated as the minimum amount of space any single deletion can recover, with the exception of the most recent snapshot (which will be skipped if no incomplete snapshots are present) whatever value it returns should never ever decrease once other snapshots are made or removed. Also note that as of timeshift version 25.12.5, local hardlinks are supported within snapshots and thus will cause this script to under-report size estimates even more. Fixing the latter issue would probably require me to rewrite this script as a dedicated program, which isn't really a worthwhile endeavor in my opinion as you will be working under the assumption of minimum size savings when deleting multiple snapshots anyway."
		exit 0
	fi
fi
set -euo pipefail
renice -n 15 -p $$ >/dev/null

#Ensure we have access to the snapshots folder before we do anything
ls ./timeshift/snapshots > /dev/null

cd ./timeshift/snapshots/
echo "#Started at $(date)"
#Do all this nextSnapshot nonsense to skip the latest snapshot
snapshot=""
for nextSnapshot in $(ls -X)
do
	if [[ "$snapshot" != "" ]]
	then
		echo "$snapshot: $(sudo find "$snapshot" -type f -links 1 -printf "%k\n" | awk '{s=s+$1} END {print s}' | numfmt --from-unit=1024 --to=iec --suffix=B)"
	fi
	snapshot="$nextSnapshot"
done
echo "#Ended at $(date)"
