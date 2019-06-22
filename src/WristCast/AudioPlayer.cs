﻿using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;
using Tizen.Multimedia;

namespace WristCast
{
    public enum AudioPlayerSourceType
    {
        File = 1,
        Stream
    }

    public sealed class AudioPlayer : INotifyPropertyChanged
    {
        private static AudioPlayer _current;

        private readonly Player _mediaPlayer;
        private MediaSource _source;
        private readonly Timer _timer;

        private AudioPlayer()
        {
            _mediaPlayer = new Player();
            _timer = new Timer(1000) { AutoReset = true, Enabled = true };
            _mediaPlayer.PlaybackInterrupted += OnPlaybackInterrupted;
            _mediaPlayer.PlaybackCompleted += OnPlaybackCompleted;
            _mediaPlayer.ErrorOccurred += OnError;
            _mediaPlayer.BufferingProgressChanged += OnBufferingProgressChanged;
            SourceChanged += OnSourceChanged;
        }

        #region Events
        public event EventHandler<BufferingProgressChangedEventArgs> BufferingProgressChanged;

        public event EventHandler ErrorOcurred;

        public event EventHandler MetadataReady;

        public event EventHandler Paused;

        public event EventHandler PlaybackCompleted;

        public event EventHandler PlaybackInterrupted;

        public event EventHandler<TimeSpan> PlayPositionChanged;

        public event EventHandler PlayStarted;

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler SourceChanged;

        public event EventHandler Stopped;
        #endregion

        #region Properties

        public static AudioPlayer Current => _current ?? (_current = new AudioPlayer());

        public TimeSpan Duration { get; set; }

        public bool IsLooping
        {
            get => _mediaPlayer.IsLooping;
            set => _mediaPlayer.IsLooping = value;
        }

        public bool Muted
        {
            get => _mediaPlayer.Muted;
            set => _mediaPlayer.Muted = value;
        }

        public TimeSpan Position { get; set; }

        public AudioPlayerSourceType SourceType { get; private set; } = 0;

        public int Volume
        {
            get => (int)(_mediaPlayer.Volume * 10);
            set => _mediaPlayer.Volume = value / 10f;
        }

        public PlayerState State => _mediaPlayer.State;

        #endregion

        #region Public API

        public async Task ChangeSource(MediaSource source)
        {
            if (_mediaPlayer.State == PlayerState.Preparing) return;
            if (_mediaPlayer.State != PlayerState.Idle)
                _mediaPlayer.Unprepare();
            _mediaPlayer.SetSource(source);
            await _mediaPlayer.PrepareAsync();
            _source = source;
            SourceType = GetSourceType(source);
            SourceChanged?.Invoke(this, EventArgs.Empty);
            GetCurentPlayPosition();
            GetDuration();
        }

        public void Pause()
        {
            _mediaPlayer.Pause();
            _timer.Stop();
            Paused?.Invoke(this, EventArgs.Empty);
        }

        public void Play()
        {
            _timer.Elapsed += OnTimerElapsed;
            _mediaPlayer.Start();
            _timer.Start();
            PlayStarted?.Invoke(this, EventArgs.Empty);
        }

        public Task SeekTo(int seconds)
        {
            var actualPosition = _mediaPlayer.GetPlayPosition();
            var targetPosition = actualPosition + seconds * 1000;
            if (targetPosition < 0) targetPosition = 0;
            if (targetPosition > Duration.TotalMilliseconds) targetPosition = (int)Duration.TotalMilliseconds;
            return _mediaPlayer.SetPlayPositionAsync(targetPosition, true);
        }

        public Task SeekToStart()
        {
            return _mediaPlayer.SetPlayPositionAsync(0, false);
        }

        public Task SetPlayPosition(TimeSpan position)
        {
            return _mediaPlayer.SetPlayPositionAsync(position.Milliseconds, true);
        }

        public void Stop()
        {
            _mediaPlayer.Stop();
            _timer.Stop();
            Stopped?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            GetCurentPlayPosition();
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void GetCurentPlayPosition()
        {
            Position = TimeSpan.FromMilliseconds(_mediaPlayer.GetPlayPosition());
            PlayPositionChanged?.Invoke(this, Position);
        }

        private void GetDuration()
        {
            Duration = TimeSpan.FromMilliseconds(_mediaPlayer.StreamInfo.GetDuration());
        }

        private AudioPlayerSourceType GetSourceType(MediaSource source)
        {
            var mus = source as MediaUriSource ?? throw new ArgumentException();
            return mus.Uri.StartsWith("/", StringComparison.InvariantCultureIgnoreCase) ?
                AudioPlayerSourceType.File :
                AudioPlayerSourceType.Stream;
        }

        private void OnBufferingProgressChanged(object sender, BufferingProgressChangedEventArgs e)
        {
            BufferingProgressChanged?.Invoke(this, e);
        }

        private void OnError(object sender, PlayerErrorOccurredEventArgs e)
        {
            ErrorOcurred?.Invoke(this, EventArgs.Empty);
        }

        private void OnPlaybackCompleted(object sender, EventArgs e)
        {
            PlaybackCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void OnPlaybackInterrupted(object sender, PlaybackInterruptedEventArgs e)
        {
            PlaybackInterrupted?.Invoke(this, EventArgs.Empty);
        }

        private void OnSourceChanged(object sender, EventArgs e)
        {
            GetCurentPlayPosition();
            GetDuration();
            MetadataReady?.Invoke(this, EventArgs.Empty);
        }

        private void Unprepare()
        {
            _mediaPlayer.Unprepare();
            _timer.Stop();
        }
    }
}