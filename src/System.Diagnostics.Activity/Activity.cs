using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace System.Diagnostics.Activity
{
    public class Activity : IDisposable
    {
        public string OperationName { get; }
        public string Id { get;}
        public DateTime StartTime { get; private set; }
        public TimeSpan Duration { get; private set; }
        public Activity Parent { get; }
        public IEnumerable<KeyValuePair<string, string>> Tags => _tags;
        public IEnumerable<KeyValuePair<string, string>> Baggage => _baggage;

        public Activity(string operationName)
        {
            OperationName = operationName;
            Id = GenerateId();
            Parent = Current;

            _tags = new LinkedList<KeyValuePair<string, string>>();
            _baggage =  Parent?._baggage ?? new LinkedList<KeyValuePair<string, string>>();

            StartTime = DateTime.UtcNow;
            Duration = TimeSpan.Zero;
        }

        public Activity WithTag(string key, string value)
        {
            _tags.AddFirst(new KeyValuePair<string, string>(key, value));
            return this;
        }

        public Activity WithBaggage(string key, string value)
        {
            _baggage.AddFirst(new KeyValuePair<string, string>(key, value));
            return this;
        }

        public string GetBaggageItem(string key)
        {
            foreach (var keyValue in _baggage)
                if (key == keyValue.Key)
                    return keyValue.Value;
            return null;
        }

        public void Start(DateTime startTime)
        {
            StartTime = startTime;
            SetCurrent(this);
            ActivityStarting?.Invoke();
        }

        public void Stop(TimeSpan duration)
        {
            if (!isFinished)
            {
                Duration = duration;
                isFinished = true;
                ActivityStopping?.Invoke();
                SetCurrent(Parent);
            }
        }

        public static Activity Start(string operationName, DateTime startTime)
        {
            var activity = new Activity(operationName);
            Start(activity, startTime);
            return activity;
        }

        public static void Start(Activity activity, DateTime startTime)
        {
            activity.Start(startTime);
        }

        public static void Stop(Activity activity, TimeSpan duration)
        {
            activity.Stop(duration);
        }

        public void Dispose()
        {
            Stop(DateTime.UtcNow - StartTime);
        }

        public static Activity Current => _current.Value;

        /// <summary>
        /// Generates unique id for request
        /// </summary>
        /// <returns></returns>
        private string GenerateId()
        {
            return Guid.NewGuid().ToString();
            //if id is incremental:
            // - multiple instances of the same service will produce non-unique ids
            // - after restart, service will start generating ids from 0, so they will repeat
        }

        public static bool CurrentEnabled { get; set; } = true;

        public static void SetCurrent(Activity newActivity)
        {
            if (CurrentEnabled)
            {
                _current.Value = newActivity;
            }
        }

        //We expect users to be interested only about activity start and stop events: such events may be logged
        //Current activity could be changed without being started or stopped
        //Current changing event would require subscriber to guess what happened and how it should be logged
        //It would also mean that user will be notified about new activity only when new one will set, which may never happen
        /// <summary>
        /// Notifies subscribers about activity start
        /// Activity.Current is set to activity being started
        /// </summary>
        public static event Action ActivityStarting;

        /// <summary>
        /// Notifies subscribers about activity stop
        /// Activity.Current is set to activity being stopped
        /// </summary>
        public static event Action ActivityStopping;

        public override string ToString()
        {
            return $"operation: {OperationName}, Id={Id}, context: {{{dictionaryToString(Baggage)}}}, tags: {{{dictionaryToString(Tags)}}}";
        }

        private readonly LinkedList<KeyValuePair<string, string>> _tags;
        private readonly LinkedList<KeyValuePair<string, string>> _baggage;
        private static readonly AsyncLocal<Activity> _current = new AsyncLocal<Activity>();
        private bool isFinished;

        private string dictionaryToString(IEnumerable<KeyValuePair<string, string>> dictionary)
        {
            var sb = new StringBuilder();
            foreach (var kv in dictionary)
            {
                if (kv.Value != null)
                    sb.Append($"{kv.Key}={kv.Value},");
            }
            if (sb.Length > 0)
                sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }
    }
}
