using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Flow.Launcher.Plugin.QuickSSH
{
    /// <summary>
    /// Persisted plugin data: SSH profiles, custom shells, and selected shell.
    /// </summary>
    public class UserData
    {
        public string PluginVersion { get; set; } = "1.0";

        [JsonProperty]
        private Dictionary<string, string> EntriesLists { get; set; } = new();

        [JsonIgnore]
        public AutoSaveDictionary<string, string> Entries { get; private set; }

        [JsonProperty]
        private Dictionary<string, string> CustomShellLists { get; set; } = new();

        [JsonIgnore]
        public AutoSaveDictionary<string, string> CustomShell { get; private set; }

        public string? SelectedCustomShell { get; set; }

        /// <summary>
        /// Binds auto-save callbacks after construction or deserialization.
        /// </summary>
        public void Attach(Action onChanged)
        {
            EntriesLists ??= new Dictionary<string, string>();
            CustomShellLists ??= new Dictionary<string, string>();
            Entries = new AutoSaveDictionary<string, string>(EntriesLists, onChanged);
            CustomShell = new AutoSaveDictionary<string, string>(CustomShellLists, onChanged);
        }
    }

    /// <summary>
    /// Dictionary wrapper that invokes a callback on every mutation.
    /// </summary>
    public sealed class AutoSaveDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private static readonly Action Noop = () => { };
        private readonly IDictionary<TKey, TValue> _inner;
        private Action _onChanged;

        public AutoSaveDictionary(IDictionary<TKey, TValue> inner, Action onChanged)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _onChanged = onChanged ?? Noop;
        }

        public void SetCallback(Action onChanged) => _onChanged = onChanged ?? Noop;

        public TValue this[TKey key]
        {
            get => _inner[key];
            set { _inner[key] = value; _onChanged(); }
        }

        public ICollection<TKey> Keys => _inner.Keys;
        public ICollection<TValue> Values => _inner.Values;
        public int Count => _inner.Count;
        public bool IsReadOnly => _inner.IsReadOnly;

        public void Add(TKey key, TValue value) { _inner.Add(key, value); _onChanged(); }
        public void Add(KeyValuePair<TKey, TValue> item) { _inner.Add(item); _onChanged(); }
        public bool Remove(TKey key) { var r = _inner.Remove(key); if (r) _onChanged(); return r; }
        public bool Remove(KeyValuePair<TKey, TValue> item) { var r = _inner.Remove(item); if (r) _onChanged(); return r; }
        public void Clear() { _inner.Clear(); _onChanged(); }
        public bool ContainsKey(TKey key) => _inner.ContainsKey(key);
        public bool Contains(KeyValuePair<TKey, TValue> item) => _inner.Contains(item);
        public bool TryGetValue(TKey key, out TValue value) => _inner.TryGetValue(key, out value);
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _inner.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_inner).GetEnumerator();
    }

    /// <summary>
    /// Manages loading and saving of user data to a JSON file.
    /// </summary>
    public class ProfileManager
    {
        private readonly string _path;
        public UserData UserData { get; private set; }

        public ProfileManager(string path)
        {
            _path = path;

            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(_path))
            {
                UserData = new UserData();
                UserData.Attach(SaveConfiguration);
                SaveConfiguration();
            }
            else
            {
                LoadConfiguration();
            }
        }

        public void SaveConfiguration()
        {
            var json = JsonConvert.SerializeObject(UserData, Formatting.Indented);
            var tmp = _path + ".tmp";
            try
            {
                File.WriteAllText(tmp, json);
                File.Move(tmp, _path, overwrite: true);
            }
            finally
            {
                if (File.Exists(tmp))
                    try { File.Delete(tmp); } catch { /* best effort cleanup */ }
            }
        }

        public void LoadConfiguration()
        {
            var json = File.ReadAllText(_path);
            UserData = JsonConvert.DeserializeObject<UserData>(json) ?? new UserData();
            UserData.Attach(SaveConfiguration);
        }
    }
}