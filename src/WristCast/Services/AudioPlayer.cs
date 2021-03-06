﻿using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Autofac;
using Tizen.Multimedia;
using WristCast.Core.IoC;
using WristCast.Core.Services;
using WristCast.Core.Shared;
using WristCast.ViewModels;
using Timer = System.Timers.Timer;

namespace WristCast.Services
{
    public enum AudioPlayerSourceType
    {
        File = 1,
        Stream
    }

    public sealed class AudioPlayer : BindableBase
    {
        private static AudioPlayer _current;

        private static readonly Player _mediaPlayer = new Player();
        private static readonly AudioVolume _audioVolume = AudioManager.VolumeController;
        private static readonly int _maxVolume = AudioManager.VolumeController.MaxLevel[AudioVolumeType.Media];
        private MediaSource _source;
        private readonly Timer _timer;
        

        private AudioPlayer()
        {
            _log = IocContainer.Instance.Resolve<ILog>();
            _audioVolume.Changed += OnVolumeChanged;
            _timer = new Timer(1000) { AutoReset = true, Enabled = true };
            _timer.Elapsed += OnTimerElapsed;
            _mediaPlayer.PlaybackInterrupted += OnPlaybackInterrupted;
            _mediaPlayer.PlaybackCompleted += OnPlaybackCompleted;
            _mediaPlayer.ErrorOccurred += OnError;
            _mediaPlayer.BufferingProgressChanged += OnBufferingProgressChanged;
            SourceChanged += OnSourceChanged;
        }

        private void OnVolumeChanged(object sender, VolumeChangedEventArgs e)
        {
            VolumeChanged?.Invoke(this, Volume);
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

        public event EventHandler SourceChanged;

        public event EventHandler Stopped;

        public event EventHandler<int> VolumeChanged;
        #endregion

        #region Properties

        public static AudioPlayer Current => _current ?? (_current = new AudioPlayer());

        private TimeSpan _duration;

        public TimeSpan Duration
        {
            get => _duration;
            set => SetProperty(ref _duration, value);
        }

        public bool IsLooping
        {
            get => _mediaPlayer.IsLooping;
            set
            {
                _mediaPlayer.IsLooping = value;
                OnPropertyChanged();
            }
        }

        public int MaxVolume => _maxVolume;

        public bool Muted
        {
            get => _mediaPlayer.Muted;
            set
            {
                _mediaPlayer.Muted = value; 
                OnPropertyChanged();
            }
        }

        private TimeSpan _position;

        private ILog _log;

        public TimeSpan Position
        {
            get => _position;
            set => SetProperty(ref _position,value);
        }

        public AudioPlayerSourceType SourceType { get; private set; } = 0;

        public int Volume
        {
            get => _audioVolume.Level[AudioVolumeType.Media];
            set
            {
                _audioVolume.Level[AudioVolumeType.Media] = value; 
                OnPropertyChanged();
            }
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
            SetProperty(ref _source,source,
                nameof(Position),nameof(Duration),nameof(SourceType),nameof(this.State));
        }

        public void Pause()
        {
            _mediaPlayer.Pause();
            _timer.Stop();
            Paused?.Invoke(this, EventArgs.Empty);
        }

        public void Play()
        {
            _mediaPlayer.Start();
            _timer.Start();
            PlayStarted?.Invoke(this, EventArgs.Empty);
        }

        public async Task SeekTo(TimeSpan seconds)
        {
            int targetPosition = (int)seconds.TotalMilliseconds;
            if (targetPosition < 0) targetPosition = 0;
            if (targetPosition > Duration.TotalMilliseconds) targetPosition = (int)Duration.TotalMilliseconds;
            await SetPlayPosition(TimeSpan.FromMilliseconds(targetPosition));
        }

        public Task SeekToStart()
        {
            return SetPlayPosition(TimeSpan.Zero);
        }

        public async Task SetPlayPosition(TimeSpan position)
        {
            if (_mediaPlayer.State == PlayerState.Idle || _mediaPlayer.State == PlayerState.Preparing) return;
            _timer.Stop();
            await _mediaPlayer.SetPlayPositionAsync((int)position.TotalMilliseconds, true);
            _timer.Start();
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
            Console.WriteLine(e.Error);
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
            MetadataReady?.Invoke(this, EventArgs.Empty);
        }

        private void Unprepare()
        {
            _mediaPlayer.Unprepare();
            _timer.Stop();
        }
    }
}