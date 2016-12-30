using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace System.Diagnostics.Activity
{
    public class Activity : IDisposable
    {
        public string OperationName { get; }
        public string Id { get; }
        public DateTime StartTimeUtc { get; private set; }
        public TimeSpan Duration { get; private set; }
        public Activity Parent { get; }
        public IEnumerable<KeyValuePair<string, string>> Tags => _tags;
        public IEnumerable<KeyValuePair<string, string>> Baggage => _baggage;

        public Activity(string operationName)
        {
            OperationName = operationName;
            Parent = Current;
            Id = GenerateId();

            _tags = new LinkedList<KeyValuePair<string, string>>();
            _baggage = Parent?._baggage ?? new LinkedList<KeyValuePair<string, string>>();

            StartTimeUtc = DateTime.UtcNow;
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
            StartTimeUtc = startTime;
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

        public static Activity Start(string operationName, DateTime startTimeUtc)
        {
            var activity = new Activity(operationName);
            Start(activity, startTimeUtc);
            return activity;
        }

        public static void Start(Activity activity, DateTime startTimeUtc)
        {
            activity.Start(startTimeUtc);
        }

        public static void Stop(Activity activity, TimeSpan duration)
        {
            activity.Stop(duration);
        }

        public void Dispose()
        {
            Stop(DateTime.UtcNow - StartTimeUtc);
        }

        public static Activity Current => _current.Value;

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

        public static bool CurrentEnabled { get; set; } = true;

        public static void SetCurrent(Activity newActivity)
        {
            if (CurrentEnabled)
            {
                _current.Value = newActivity;
            }
        }

        #region private 
        private string GenerateId()
        {
            string ret;
            if (Parent != null)
#if DEBUG 
                ret = Parent.Id + "/" + OperationName + "_" + Interlocked.Increment(ref Parent._currentChildId);
#else           // To keep things short, we drop the operation name 
                ret = Parent.Id + "/" + Interlocked.Increment(ref Parent._currentChildId);
#endif
            else
            {
                if (_uniqPrefix == null)
                {
                    // Here we make an ID to represent the Process/AppDomain.   Ideally we use process ID but 
                    // it is unclear if we have that ID handy.   Currently we use low bits of high freq tick 
                    // as a unique random number (which is not bad, but loses randomness for startup scenarios).  
                    int uniqNum = (int)Stopwatch.GetTimestamp();
                    string uniqPrefix = $"//{Environment.MachineName}_{uniqNum:x}/";
                    Interlocked.CompareExchange(ref _uniqPrefix, uniqPrefix, null);
                }
#if DEBUG
                ret = _uniqPrefix + OperationName + "_" + Interlocked.Increment(ref _currentRootId);
#else           // To keep things short, we drop the operation name 
                ret = _uniqPrefix + Interlocked.Increment(ref _currentRootId);
#endif 
            }
            // Useful place to place a conditional breakpoint.  
            return ret;
        }

        // Used to generate an ID 
        int _currentChildId;            // A unique number for all children of this activity.  
        static int _currentRootId;      // A unique number inside the appdomain.
        static string _uniqPrefix;      // A unique prefix that represents the machine/process/appdomain

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

        #endregion // private
    }
}
