using System;
using System.Windows.Forms;

namespace KahoiiMusic.Services
{
    public enum SleepTimerOption
    {
        Off = 0,
        Minutes5 = 5,
        Minutes10 = 10,
        Minutes15 = 15,
        Minutes20 = 20,
        Minutes30 = 30,
        Minutes45 = 45,
        Minutes60 = 60,
        EndOfTrack = -1
    }

    public class SleepTimerService
    {
        private static readonly Lazy<SleepTimerService> _instance = new Lazy<SleepTimerService>(() => new SleepTimerService());
        public static SleepTimerService Instance => _instance.Value;

        private readonly Timer _timer;
        private SleepTimerOption _currentOption = SleepTimerOption.Off;
        private DateTime _targetEndTime = DateTime.MinValue;

        public event EventHandler StateChanged;
        public event EventHandler TimerExpired;

        private SleepTimerService()
        {
            _timer = new Timer();
            _timer.Interval = 1000;
            _timer.Tick += Timer_Tick;
        }

        public SleepTimerOption CurrentOption => _currentOption;
        public bool IsActive => _currentOption != SleepTimerOption.Off;
        public bool IsEndOfTrack => _currentOption == SleepTimerOption.EndOfTrack;

        public TimeSpan RemainingTime
        {
            get
            {
                if (!IsActive || IsEndOfTrack) return TimeSpan.Zero;
                TimeSpan diff = _targetEndTime - DateTime.UtcNow;
                return diff > TimeSpan.Zero ? diff : TimeSpan.Zero;
            }
        }

        public void SetTimer(SleepTimerOption option)
        {
            _currentOption = option;
            if (option == SleepTimerOption.Off)
            {
                _timer.Stop();
                _targetEndTime = DateTime.MinValue;
            }
            else if (option == SleepTimerOption.EndOfTrack)
            {
                _timer.Stop();
                _targetEndTime = DateTime.MinValue;
            }
            else
            {
                int minutes = (int)option;
                _targetEndTime = DateTime.UtcNow.AddMinutes(minutes);
                if (!_timer.Enabled)
                {
                    _timer.Start();
                }
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Stop()
        {
            SetTimer(SleepTimerOption.Off);
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!IsActive || IsEndOfTrack)
            {
                _timer.Stop();
                return;
            }

            if (DateTime.UtcNow >= _targetEndTime)
            {
                _timer.Stop();
                _currentOption = SleepTimerOption.Off;
                _targetEndTime = DateTime.MinValue;
                StateChanged?.Invoke(this, EventArgs.Empty);
                TimerExpired?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public string GetStatusString()
        {
            if (!IsActive) return "Hẹn giờ: Tắt";
            if (IsEndOfTrack) return "Hẹn giờ: Dừng khi hết bài";
            TimeSpan rem = RemainingTime;
            if (rem.TotalHours >= 1)
            {
                return string.Format("Hẹn giờ: Còn {0}:{1:D2}:{2:D2}", (int)rem.TotalHours, rem.Minutes, rem.Seconds);
            }
            return string.Format("Hẹn giờ: Còn {0}:{1:D2}", (int)rem.TotalMinutes, rem.Seconds);
        }
    }
}

