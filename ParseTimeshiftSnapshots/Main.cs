using System.Globalization;
using System.Text.RegularExpressions;

namespace ParseTimeshiftSnapshots {
	class MainClass {

		public enum SnapshotStatus {
			COMMENT = -1,
			KEEP = 0,
			REMOVE = 1
		}

		public static void Main(string[] rawArgs) {
			//Use the same time throughout the script to lower the chances of things breaking
			DateTime now = DateTime.Now;

			//Ensure utf-8 output/input
			Console.OutputEncoding = System.Text.Encoding.UTF8;
			Console.InputEncoding = System.Text.Encoding.UTF8;

			string[] rawInput;
			using (StreamReader streamInput = new(Console.OpenStandardInput()))
				rawInput = streamInput.ReadToEnd().Split('\n');

			//-1: Has comment, prioritise it
			//0: Don't remove
			//1: Remove
			Dictionary<string, SnapshotStatus> snapshotActions = [];
			//0: >2 years
			//1: >3 months
			//2: >2 weeks
			//3: >2 days
			//4: <=2 days
			SortedDictionary<DateTime, string>[] snapshotDates = new SortedDictionary<DateTime, string>[5];
			for (int i = 0; i < 5; i++) {
				snapshotDates[i] = [];
			}
			Dictionary<string, string> snapshotComments = [];
			bool atMetadata = true;
			Regex metadataEndRegex = new Regex(@"^\s*Num\s+Name\s+Tags\s+Description\s*$");
			Regex metadataEndRegexPart2 = new Regex(@"^\-+$");
			Regex rawSnapshotRegex = new Regex(@"^([0-9]+\s+>\s+)(20[0-9][0-9]-[0-9][0-9]-[0-9][0-9]_[0-9][0-9]-[0-9][0-9]-[0-9][0-9])(\s+\S+\s+)(.*)$");
			KeyValuePair<string, DateTime> latestDate = new("NULL", DateTime.MinValue);
			for (int i = 0; i < rawInput.Length; i++) {
				if (atMetadata) {
					if (!metadataEndRegex.IsMatch(rawInput[i])) {
						Console.Error.WriteLine(rawInput[i]);
					} else if (metadataEndRegexPart2.IsMatch(rawInput[i + 1])) {
						atMetadata = false;
						//Skip over the next line since we just checked it
						i++;
					}
				} else if (rawInput[i].Trim() != "") {
					//0: Full string
					//1: ID + '>'
					//2: Timestamp/Name
					//3: Tags
					//4: Comment (untrimmed)
					GroupCollection snapshotInfo = rawSnapshotRegex.Match(rawInput[i]).Groups;

					//Record the snapshot's comment if it has one
					snapshotActions[snapshotInfo[2].Value] = (snapshotInfo[4].Value == "") ? SnapshotStatus.KEEP : SnapshotStatus.COMMENT;
					if (snapshotActions[snapshotInfo[2].Value] == SnapshotStatus.COMMENT)
						snapshotComments[snapshotInfo[2].Value] = snapshotInfo[4].Value.Trim();

					//Don't need to verify the formatting of the date since the regex just did that for us
					string[] splitDate = snapshotInfo[2].Value.Replace('_', '-').Split("-");
					DateTime parsedDate = new DateTime(int.Parse(splitDate[0]), int.Parse(splitDate[1]), int.Parse(splitDate[2]), int.Parse(splitDate[3]), int.Parse(splitDate[4]), int.Parse(splitDate[5]), DateTimeKind.Local);
					if (latestDate.Value < parsedDate)
						latestDate = new(snapshotInfo[2].Value, parsedDate);

					snapshotDates[
						//0: >2 years
						(parsedDate.Year < now.Year - 2) ? 0 :
						//1: >3 months, note the comparison adds an extra 12 months so don't just blindly copy paste this elsewhere
						parsedDate.Month + (parsedDate.Year * 12) < (now.Month + (now.Year * 12) - 3) ? 1 :
						//2: >2 weeks
						ISOWeek.GetWeekOfYear(parsedDate) < ISOWeek.GetWeekOfYear(now) + (parsedDate.Year < now.Year ? ISOWeek.GetWeeksInYear(now.Year - 1) : 0) - 2 ? 2 :
						//3: >2 days, note the comparison adds an extra 365 days so don't just blindly copy paste this elsewhere
						now.DayOfYear - 1 + (now.Year * 365) - (parsedDate.DayOfYear - 1 + (parsedDate.Year * 365)) > 2 ? 3 :
						//4: <=2 days
						4

					][parsedDate] = snapshotInfo[2].Value;
				}
			}

			//Now we finally figure out which to remove
			for (int i = 0; i < 5; i++) {
				if (snapshotDates[i].Count > 1) {
					KeyValuePair<DateTime, string> lastSnapshot = snapshotDates[i].First();
					foreach (KeyValuePair<DateTime, string> snapshot in snapshotDates[i]) {
						if (lastSnapshot.Value != snapshot.Value) {

							switch (i) {
								//4: <=2 days, don't do anything
								case 4:
									break;
								//3: >2 days, allow 1 a day
								case 3:
									if (lastSnapshot.Key.Day == snapshot.Key.Day) {
										if (snapshotActions[lastSnapshot.Value] == SnapshotStatus.COMMENT && snapshotActions[snapshot.Value] != SnapshotStatus.COMMENT) {
											if (snapshotActions[snapshot.Value] == SnapshotStatus.COMMENT)
												Console.Error.WriteLine($"Snapshot with comment \"{snapshotComments[snapshot.Value]}\" has just been marked for deletion!");
											snapshotActions[snapshot.Value] = SnapshotStatus.REMOVE;
										} else {
											if (snapshotActions[lastSnapshot.Value] == SnapshotStatus.COMMENT)
												Console.Error.WriteLine($"Snapshot with comment \"{snapshotComments[lastSnapshot.Value]}\" has just been marked for deletion!");
											snapshotActions[lastSnapshot.Value] = SnapshotStatus.REMOVE;
										}
									} else {
										lastSnapshot = snapshot;
									}
									break;
								//2: >2 weeks, allow 1 a week
								case 2:
									int currentWeek = ISOWeek.GetWeekOfYear(snapshot.Key) + (lastSnapshot.Key.Year < snapshot.Key.Year ? ISOWeek.GetWeeksInYear(snapshot.Key.Year - 1) : 0);
									int lastWeek = ISOWeek.GetWeekOfYear(lastSnapshot.Key);
									if (currentWeek == lastWeek) {
										if (snapshotActions[lastSnapshot.Value] == SnapshotStatus.COMMENT && snapshotActions[snapshot.Value] != SnapshotStatus.COMMENT) {
											if (snapshotActions[snapshot.Value] == SnapshotStatus.COMMENT)
												Console.Error.WriteLine($"Snapshot with comment \"{snapshotComments[snapshot.Value]}\" has just been marked for deletion!");
											snapshotActions[snapshot.Value] = SnapshotStatus.REMOVE;
										} else {
											if (snapshotActions[lastSnapshot.Value] == SnapshotStatus.COMMENT)
												Console.Error.WriteLine($"Snapshot with comment \"{snapshotComments[lastSnapshot.Value]}\" has just been marked for deletion!");
											snapshotActions[lastSnapshot.Value] = SnapshotStatus.REMOVE;
										}
									} else {
										lastSnapshot = snapshot;
									}
									break;
								//1: >3 months
								case 1:
									int currentMonth = snapshot.Key.Month + ((snapshot.Key.Year - 1) * 12);
									int lastMonth = lastSnapshot.Key.Month + ((lastSnapshot.Key.Year - 1) * 12);
									if (currentMonth == lastMonth) {
										if (snapshotActions[lastSnapshot.Value] == SnapshotStatus.COMMENT && snapshotActions[snapshot.Value] != SnapshotStatus.COMMENT) {
											if (snapshotActions[snapshot.Value] == SnapshotStatus.COMMENT)
												Console.Error.WriteLine($"Snapshot with comment \"{snapshotComments[snapshot.Value]}\" has just been marked for deletion!");
											snapshotActions[snapshot.Value] = SnapshotStatus.REMOVE;
										} else {
											if (snapshotActions[lastSnapshot.Value] == SnapshotStatus.COMMENT)
												Console.Error.WriteLine($"Snapshot with comment \"{snapshotComments[lastSnapshot.Value]}\" has just been marked for deletion!");
											snapshotActions[lastSnapshot.Value] = SnapshotStatus.REMOVE;
										}
									} else {
										lastSnapshot = snapshot;
									}
									break;
								//0: >2 years
								case 0:
									bool currentHalf = snapshot.Key.Month <= 6;
									bool lastHalf = lastSnapshot.Key.Month <= 6;
									if (currentHalf == lastHalf) {
										if (snapshotActions[lastSnapshot.Value] == SnapshotStatus.COMMENT && snapshotActions[snapshot.Value] != SnapshotStatus.COMMENT) {
											if (snapshotActions[snapshot.Value] == SnapshotStatus.COMMENT)
												Console.Error.WriteLine($"Snapshot with comment \"{snapshotComments[snapshot.Value]}\" has just been marked for deletion!");
											snapshotActions[snapshot.Value] = SnapshotStatus.REMOVE;
										} else {
											if (snapshotActions[lastSnapshot.Value] == SnapshotStatus.COMMENT)
												Console.Error.WriteLine($"Snapshot with comment \"{snapshotComments[lastSnapshot.Value]}\" has just been marked for deletion!");
											snapshotActions[lastSnapshot.Value] = SnapshotStatus.REMOVE;
										}
									} else {
										lastSnapshot = snapshot;
									}
									break;
							}

							if (snapshotActions[lastSnapshot.Value] != SnapshotStatus.COMMENT)
								lastSnapshot = snapshot;
						}
					}
				}
			}

			//And print the output
			int commentCount = 0;
			int keepCount = 0;
			int removeCount = 0;
			foreach (KeyValuePair<string, SnapshotStatus> snapshot in snapshotActions) {
				if (snapshot.Value == SnapshotStatus.REMOVE && snapshot.Key != latestDate.Key) {
					Console.WriteLine(snapshot.Key);
					removeCount++;
				} else {
					if (snapshot.Value == SnapshotStatus.COMMENT)
						commentCount++;
					keepCount++;
				}
			}
			Console.Error.WriteLine($"Processed {snapshotActions.Count} snapshots\n{removeCount} marked for deletion\n{keepCount} saved (of those {commentCount} {((commentCount != 1) ? "have" : "has")} a comment)");

			System.Environment.Exit(0);
		}
	}
}
