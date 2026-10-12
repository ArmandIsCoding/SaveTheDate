using System.Globalization;
using System.Text;
using SaveTheDate.Domain;

namespace SaveTheDate.Services;

public static class CalendarFile
{
    public static byte[] Create(Celebration item)
    {
        static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\r", "").Replace("\n", "\\n").Replace(";", "\\;").Replace(",", "\\,");
        static string Utc(DateTime value) => value.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var lines = new[] { "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//SaveTheDate//Invitaciones//ES", "CALSCALE:GREGORIAN", "BEGIN:VEVENT", $"UID:{item.Id}@savethedate", $"DTSTAMP:{Utc(DateTime.UtcNow)}", $"DTSTART:{Utc(item.StartsAtUtc)}", $"SUMMARY:{Escape(item.Title)}", $"LOCATION:{Escape(item.Venue)}", "END:VEVENT", "END:VCALENDAR" };
        var result = new StringBuilder();
        foreach (var line in lines)
        {
            var count = 0;
            foreach (var rune in line.EnumerateRunes())
            {
                if (count + rune.Utf8SequenceLength > 73) { result.Append("\r\n "); count = 1; }
                result.Append(rune.ToString()); count += rune.Utf8SequenceLength;
            }
            result.Append("\r\n");
        }
        return Encoding.UTF8.GetBytes(result.ToString());
    }
}
