using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PS2Manager.App.Converters;

/// <summary>
/// <c>conv:AsyncImage.Path="{Binding CoverPath}" conv:AsyncImage.DecodeWidth="320"</c> on an Image:
/// decodes off the UI thread and caches results, so scrolling large libraries stays smooth.
/// Replaces a converter that decoded synchronously on the UI thread.
/// </summary>
public static class AsyncImage
{
    public static readonly DependencyProperty PathProperty = DependencyProperty.RegisterAttached(
        "Path", typeof(string), typeof(AsyncImage), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty DecodeWidthProperty = DependencyProperty.RegisterAttached(
        "DecodeWidth", typeof(int), typeof(AsyncImage), new PropertyMetadata(300, OnChanged));

    // Identifies the latest request per Image; stale completions (recycled rows) are dropped.
    private static readonly DependencyProperty RequestProperty = DependencyProperty.RegisterAttached(
        "Request", typeof(object), typeof(AsyncImage), new PropertyMetadata(null));

    public static string? GetPath(DependencyObject d) => (string?)d.GetValue(PathProperty);
    public static void SetPath(DependencyObject d, string? v) => d.SetValue(PathProperty, v);
    public static int GetDecodeWidth(DependencyObject d) => (int)d.GetValue(DecodeWidthProperty);
    public static void SetDecodeWidth(DependencyObject d, int v) => d.SetValue(DecodeWidthProperty, v);

    private static readonly ImageCache Cache = new(128L * 1024 * 1024);
    private static readonly SemaphoreSlim Decoders = new(Math.Clamp(Environment.ProcessorCount / 2, 2, 4));

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image img) return;
        string? path = GetPath(img);
        int width = GetDecodeWidth(img);
        var request = new object();
        img.SetValue(RequestProperty, request);

        if (string.IsNullOrEmpty(path)) { img.Source = null; return; }

        string key = $"{width}|{path}";
        var hit = Cache.TryGet(key);
        img.Source = hit?.Bitmap;   // show cached image now (or clear a recycled one)

        _ = LoadAsync(img, request, path, width, key, hit?.Ticks);
    }

    private static async Task LoadAsync(Image img, object request, string path, int width, string key, long? cachedTicks)
    {
        BitmapSource? bmp;
        try
        {
            bmp = await Task.Run(async () =>
            {
                var fi = new FileInfo(path);
                if (!fi.Exists) return null;
                long ticks = fi.LastWriteTimeUtc.Ticks;
                if (cachedTicks == ticks) return null;   // cached copy is current; nothing to do

                await Decoders.WaitAsync();
                try
                {
                    var decoded = Decode(path, width);
                    if (decoded is not null) Cache.Add(key, decoded, ticks);
                    return decoded;
                }
                finally { Decoders.Release(); }
            });
        }
        catch { return; }

        if (bmp is null) return;
        await img.Dispatcher.InvokeAsync(() =>
        {
            if (ReferenceEquals(img.GetValue(RequestProperty), request)) img.Source = bmp;
        }, DispatcherPriority.Background);
    }

    private static BitmapSource? Decode(string path, int width)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            if (width > 0) bmp.DecodePixelWidth = width;
            bmp.StreamSource = fs;
            bmp.EndInit();
            bmp.Freeze();   // usable from the UI thread
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Thread-safe LRU cache bounded by decoded pixel bytes.</summary>
    private sealed class ImageCache(long budgetBytes)
    {
        public sealed record Entry(string Key, BitmapSource Bitmap, long Ticks, long Bytes);

        private readonly Dictionary<string, LinkedListNode<Entry>> _map = [];
        private readonly LinkedList<Entry> _lru = new();
        private readonly object _lock = new();
        private long _bytes;

        public Entry? TryGet(string key)
        {
            lock (_lock)
            {
                if (!_map.TryGetValue(key, out var node)) return null;
                _lru.Remove(node);
                _lru.AddFirst(node);
                return node.Value;
            }
        }

        public void Add(string key, BitmapSource bmp, long ticks)
        {
            long bytes = (long)bmp.PixelWidth * bmp.PixelHeight * 4;
            lock (_lock)
            {
                if (_map.Remove(key, out var old)) { _lru.Remove(old); _bytes -= old.Value.Bytes; }
                var node = _lru.AddFirst(new Entry(key, bmp, ticks, bytes));
                _map[key] = node;
                _bytes += bytes;
                while (_bytes > budgetBytes && _lru.Last is { } last && last != node)
                {
                    _lru.RemoveLast();
                    _map.Remove(last.Value.Key);
                    _bytes -= last.Value.Bytes;
                }
            }
        }
    }
}
