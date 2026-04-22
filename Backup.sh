#!/bin/bash
BuildNum="7"
cd "$(dirname "$0")"
if [ "$#" -eq 1 ]
then
	if [[ "$1" == "-h" || "$1" == "--help" ]]
	then
		echo "Backup.sh (build #${BuildNum})"
		echo "	Schedules a timeshift backup and marks outdated backups for deletion."
		echo "	Note that this script expects to be stored the directory containing the timeshift backup folder, I reccomend placing a symlink there and running that."
		echo "	It also expects ParseTimeshiftSnapshots to be stored in the same directory, once again I reccomend using symlinks."
		echo "	This script must also be run as root, needs direct access to the timeshift snapshots folder and can't handle multiple backup disks or btrfs snapshots."
		echo "	Please run ParseTimeshiftSnapshots with the same arg for details regarding when a backup is outdated."
		exit 0
	fi
fi
renice -n 15 -p $$
set -euo pipefail

#Ensure we have access to the snapshots folder before we do anything
ls ./timeshift/snapshots > /dev/null

if [[ ! -v 1 || "$1" != "--skip-check" ]]
then
	echo "Running empty check to clear backups marked for deletion along with any incomplete backups..."
	timeshift --check --scripted
fi

echo "Getting snapshot list..."
RawSnapshots="$(timeshift --list-snapshots --scripted)"

echo "Processing list..."
SnapshotsToDelete="$(echo "$RawSnapshots" | ./ParseTimeshiftSnapshots)"
echo ""

if [[ ! -z $SnapshotsToDelete ]]
then
	echo "$SnapshotsToDelete" | while IFS= read -r SnapshotId
	do
		echo "Marking ${SnapshotId} for deletion after backup..."
		touch "./timeshift/snapshots/${SnapshotId}/delete"
	done
fi

echo "Creating new backup..."
timeshift --create --scripted

echo "Backup complete!"
if [[ ! -v 1 || "$1" != "--auto-exit" ]]
then
	read -sp "Press enter to exit:"
fi
