using System;
using System.Collections.Generic;
using System.Linq;

namespace WindowsRemoteTools
{
    public class Notification
    {
        public string Level { get; set; } = "info";
        public string Message { get; set; } = "";
        public long Timestamp { get; set; }
        public DateTime DateTime => DateTimeOffset.FromUnixTimeMilliseconds(Timestamp).DateTime;

        public Notification(string level, string message, long timestamp)
        {
            Level = level;
            Message = message;
            Timestamp = timestamp;
        }
    }

    public static class NotificationStorage
    {
        private static readonly List<Notification> _notifications = new List<Notification>();
        private static readonly object _lock = new object();
        private const int MaxNotifications = 100;

        public static void AddNotification(string level, string message, long timestamp)
        {
            lock (_lock)
            {
                var notification = new Notification(level, message, timestamp);
                _notifications.Insert(0, notification); // Insert at beginning (newest first)

                // Keep only the last 100 notifications
                if (_notifications.Count > MaxNotifications)
                {
                    _notifications.RemoveRange(MaxNotifications, _notifications.Count - MaxNotifications);
                }
            }
        }

        public static List<Notification> GetNotifications()
        {
            lock (_lock)
            {
                return new List<Notification>(_notifications);
            }
        }

        public static List<Notification> GetNotificationsByLevel(string level)
        {
            lock (_lock)
            {
                return _notifications.Where(n => n.Level.Equals(level, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        public static int GetCount()
        {
            lock (_lock)
            {
                return _notifications.Count;
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _notifications.Clear();
            }
        }
    }
}
