#!/bin/bash
cd "$(dirname "$0")"
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
read -sp "Press enter to exit:"
