using System;
using System.Threading;
using System.Threading.Tasks;

namespace DailyQuest.Services;

internal static class SaveThrottle
{
    private static readonly object _lock = new();

    private static bool _dirty;
    private static DateTime _nextSaveUtc = DateTime.MinValue;

    private static TimeSpan _interval = TimeSpan.FromSeconds(10);
    private static Action _saveAction;

    private static bool _initialized;
    
    private static CancellationTokenSource _cts;

    public static void Init(Action saveAction, TimeSpan? interval = null)
    {
        if (saveAction == null) throw new ArgumentNullException(nameof(saveAction));

        lock (_lock)
        {
            if (_initialized) return;
            
            _saveAction = saveAction;
            if (interval.HasValue) _interval = interval.Value;
            _initialized = true;

            _cts = new CancellationTokenSource();
        }

        var token = _cts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, token);
                    Tick();
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    Core.Log.LogError($"[SaveThrottle] Driver Error: {e.Message}");
                }
            }
        }, token);
    }

    public static void MarkDirty()
    {
        lock (_lock)
        {
            if (!_initialized || _saveAction == null) return;

            _dirty = true;

            if (_nextSaveUtc == DateTime.MinValue)
                _nextSaveUtc = DateTime.UtcNow.Add(_interval);
        }
    }

    private static void Tick()
    {
        Action save = null;

        lock (_lock)
        {
            if (!_initialized || _saveAction == null) return;
            if (!_dirty || _nextSaveUtc == DateTime.MinValue) return;
            if (DateTime.UtcNow < _nextSaveUtc) return;

            _dirty = false;
            _nextSaveUtc = DateTime.MinValue;
            save = _saveAction;
        }

        try
        {
            save();
        }
        catch (Exception e)
        {
            Core.Log.LogError($"[SaveThrottle] Save failed: {e.Message}");
            
            lock (_lock)
            {
                _dirty = true;
                if (_nextSaveUtc == DateTime.MinValue)
                    _nextSaveUtc = DateTime.UtcNow.AddSeconds(2);
            }
        }
    }

    public static void ForceSave()
    {
        Action save;

        lock (_lock)
        {
            if (!_initialized || _saveAction == null) return;

            _dirty = false;
            _nextSaveUtc = DateTime.MinValue;
            save = _saveAction;
        }

        save();
    }

    public static void Stop()
    {
        lock (_lock)
        {
            if (!_initialized) return;
            
            ForceSave(); 
            
            _cts?.Cancel(); 
            _cts?.Dispose(); 
            
            _initialized = false;
        }
    }
}