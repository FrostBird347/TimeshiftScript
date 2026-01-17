#!/bin/bash
cd "$(dirname "$0")"
#Currently does nothing due to https://github.com/linuxmint/timeshift/issues/500
renice -n 15 -p $$
set -euo pipefail

#Ensure we have access to the snapshots folder before we do anything
ls ./timeshift/snapshots > /dev/null

echo "Running empty check to clear backups marked for deletion..."
timeshift --check --scripted

echo "Getting snapshot list..."
RawSnapshots="$(timeshift --list-snapshots --scripted)"

echo "Processing list..."
SnapshotsToDelete="$(echo "$RawSnapshots" | ./ParseTimeshiftSnapshots)"
echo ""

if [[ ! -z $SnapshotsToDelete ]]
then
	echo "$SnapshotsToDelete" | while IFS= read -r SnapshotId
	do
		echo "Marking ${SnapshotId} for deletion on next backup..."
		touch "./timeshift/snapshots/${SnapshotId}/delete"
	done
fi

echo "Creating new backup..."
timeshift --create --scripted

echo "Backup complete!"
read -p "Press enter to exit:"
